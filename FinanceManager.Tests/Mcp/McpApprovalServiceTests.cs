using FinanceManager.Application.Exceptions;
using FinanceManager.Application.Mcp;
using FinanceManager.Application.Notifications;
using FinanceManager.Domain.Mcp;
using FinanceManager.Domain.Notifications;
using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Mcp;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FinanceManager.Tests.Mcp;

/// <summary>
/// Unit tests of <see cref="McpApprovalService"/> (F-0902): request creation with
/// idempotency deduplication and pending limit, pre-approved enqueue, owner-scoped
/// decisions (approve/reject/cancel) with coupled <c>ApprovalEvent</c> audit records,
/// owner isolation of the read paths and the system withdrawal of open requests.
/// </summary>
public sealed class McpApprovalServiceTests
{
    private static (McpApprovalService svc, AppDbContext db, Mock<INotificationWriter> notifications) Create(
        int maxPending = 25, int expiryHours = 72, bool realAudit = true)
    {
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        var sp = services.BuildServiceProvider();
        var db = sp.GetRequiredService<AppDbContext>();
        IMcpAuditWriter audit = realAudit
            ? new McpAuditWriter(db, NullLogger<McpAuditWriter>.Instance)
            : new NullMcpAuditWriter(NullLogger<NullMcpAuditWriter>.Instance);
        var notifications = new Mock<INotificationWriter>();
        var options = new TestOptionsMonitor(new McpOptions
        {
            Approval = new McpApprovalOptions { MaxPendingPerAccess = maxPending, DefaultExpiryHours = expiryHours }
        });
        var svc = new McpApprovalService(db, audit, notifications.Object, options,
            NullLogger<McpApprovalService>.Instance);
        return (svc, db, notifications);
    }

    private static McpAccess NewAccess(Guid owner, McpApprovalMode mode = McpApprovalMode.PerAction)
        => new(owner, "acc-1", "HINT01", "pbkdf2|1|AA|AA", McpAccessLevel.Buchhalter, mode);

    private static Task<AiActionRequest> EnqueueAsync(McpApprovalService svc, McpAccess access, string? key = "k1")
        => svc.EnqueueAsync(access, "fm_contacts_manage", "delete", "katalog-v1",
            "{\"contactId\":\"x\"}", null, Guid.NewGuid(), key, CancellationToken.None);

    /// <summary>Enqueue ShouldCreatePending WithAuditAndNotification.</summary>
    [Fact]
    public async Task Enqueue_ShouldCreatePending_WithAuditAndNotification()
    {
        var (svc, db, notifications) = Create();
        var owner = Guid.NewGuid();
        var access = NewAccess(owner);

        var request = await EnqueueAsync(svc, access);

        Assert.Equal(AiActionRequestStatus.Pending, request.Status);
        Assert.Equal(owner, request.OwnerUserId);
        Assert.Equal(access.Id, request.McpAccessId);
        Assert.True(request.ExpiresUtc > DateTime.UtcNow);
        var entry = await db.McpAuditEntries.SingleAsync();
        Assert.Equal(McpAuditCategory.ApprovalEvent, entry.Category);
        Assert.Equal("ApprovalRequested", entry.EventType);
        Assert.Equal(request.Id, entry.RelatedRequestId);
        notifications.Verify(n => n.CreateForUserAsync(owner, It.IsAny<string>(), It.IsAny<string>(),
            NotificationType.EventDriven, NotificationTarget.Dashboard, It.IsAny<DateTime>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Enqueue SameIdempotencyKey ShouldReturnExisting.</summary>
    [Fact]
    public async Task Enqueue_SameIdempotencyKey_ShouldReturnExisting()
    {
        var (svc, db, _) = Create();
        var access = NewAccess(Guid.NewGuid());

        var first = await EnqueueAsync(svc, access, "dup-key");
        var second = await EnqueueAsync(svc, access, "dup-key");

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(1, await db.AiActionRequests.CountAsync());
    }

    /// <summary>Enqueue DifferentKeys ShouldCreateSeparateRequests.</summary>
    [Fact]
    public async Task Enqueue_DifferentKeys_ShouldCreateSeparateRequests()
    {
        var (svc, db, _) = Create();
        var access = NewAccess(Guid.NewGuid());

        var first = await EnqueueAsync(svc, access, "k1");
        var second = await EnqueueAsync(svc, access, "k2");

        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(2, await db.AiActionRequests.CountAsync());
    }

    /// <summary>Enqueue PendingLimitReached ShouldThrowErrMcpPendingLimit.</summary>
    [Fact]
    public async Task Enqueue_PendingLimitReached_ShouldThrowErrMcpPendingLimit()
    {
        var (svc, _, _) = Create(maxPending: 2);
        var access = NewAccess(Guid.NewGuid());
        await EnqueueAsync(svc, access, "a");
        await EnqueueAsync(svc, access, "b");

        var ex = await Assert.ThrowsAsync<DomainValidationException>(() => EnqueueAsync(svc, access, "c"));
        Assert.Equal("Err_Mcp_PendingLimit", ex.Code);
    }

    /// <summary>Enqueue PreApprovedMode ShouldCreateApprovedImmediately.</summary>
    [Fact]
    public async Task Enqueue_PreApprovedMode_ShouldCreateApprovedImmediately()
    {
        var (svc, db, notifications) = Create();
        var owner = Guid.NewGuid();
        var access = NewAccess(owner, McpApprovalMode.PreApproved);

        var request = await EnqueueAsync(svc, access);

        Assert.Equal(AiActionRequestStatus.Approved, request.Status);
        Assert.Equal(owner, request.DecidedByUserId);
        Assert.Equal(McpApprovalService.PreApprovedDecisionNote, request.DecisionNote);
        // no "new request" notification — pre-approved runs transparently to the queue
        notifications.Verify(n => n.CreateForUserAsync(It.IsAny<Guid>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<NotificationType>(), It.IsAny<NotificationTarget>(),
            It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Approve ShouldTransitionAndAudit.</summary>
    [Fact]
    public async Task Approve_ShouldTransitionAndAudit()
    {
        var (svc, db, _) = Create();
        var owner = Guid.NewGuid();
        var request = await EnqueueAsync(svc, NewAccess(owner));

        var ok = await svc.ApproveAsync(owner, request.Id, "lgtm", CancellationToken.None);

        Assert.True(ok);
        var reloaded = await db.AiActionRequests.FindAsync(request.Id);
        Assert.Equal(AiActionRequestStatus.Approved, reloaded!.Status);
        Assert.Equal("lgtm", reloaded.DecisionNote);
        Assert.Equal(2, await db.McpAuditEntries.CountAsync());
        Assert.Equal("ApprovalDecided", (await db.McpAuditEntries.OrderByDescending(e => e.SequenceNo).FirstAsync()).EventType);
    }

    /// <summary>Approve ForeignOwner ShouldReturnFalse.</summary>
    [Fact]
    public async Task Approve_ForeignOwner_ShouldReturnFalse()
    {
        var (svc, _, _) = Create();
        var request = await EnqueueAsync(svc, NewAccess(Guid.NewGuid()));

        Assert.False(await svc.ApproveAsync(Guid.NewGuid(), request.Id, null, CancellationToken.None));
    }

    /// <summary>Approve OnRejected ShouldThrowErrMcpInvalidState.</summary>
    [Fact]
    public async Task Approve_OnRejected_ShouldThrowErrMcpInvalidState()
    {
        var (svc, _, _) = Create();
        var owner = Guid.NewGuid();
        var request = await EnqueueAsync(svc, NewAccess(owner));
        await svc.RejectAsync(owner, request.Id, "no", CancellationToken.None);

        var ex = await Assert.ThrowsAsync<DomainValidationException>(
            () => svc.ApproveAsync(owner, request.Id, null, CancellationToken.None));
        Assert.Equal("Err_Mcp_InvalidState", ex.Code);
    }

    /// <summary>Reject ShouldTransitionAndAudit.</summary>
    [Fact]
    public async Task Reject_ShouldTransitionAndAudit()
    {
        var (svc, db, _) = Create();
        var owner = Guid.NewGuid();
        var request = await EnqueueAsync(svc, NewAccess(owner));

        Assert.True(await svc.RejectAsync(owner, request.Id, "reason", CancellationToken.None));
        Assert.Equal(AiActionRequestStatus.Rejected,
            (await db.AiActionRequests.FindAsync(request.Id))!.Status);
    }

    /// <summary>Cancel ShouldWithdrawPendingRequest.</summary>
    [Fact]
    public async Task Cancel_ShouldWithdrawPendingRequest()
    {
        var (svc, db, _) = Create();
        var owner = Guid.NewGuid();
        var request = await EnqueueAsync(svc, NewAccess(owner));

        Assert.True(await svc.CancelAsync(owner, request.Id, CancellationToken.None));
        var reloaded = await db.AiActionRequests.FindAsync(request.Id);
        Assert.Equal(AiActionRequestStatus.Rejected, reloaded!.Status);
        Assert.Equal("Withdrawn by owner", reloaded.DecisionNote);
    }

    /// <summary>GetAsync And ListAsync ShouldRespectOwnerIsolation.</summary>
    [Fact]
    public async Task GetAsync_And_ListAsync_ShouldRespectOwnerIsolation()
    {
        var (svc, _, _) = Create();
        var owner = Guid.NewGuid();
        var foreign = Guid.NewGuid();
        var access = NewAccess(owner);
        var r1 = await EnqueueAsync(svc, access, "a");
        await EnqueueAsync(svc, access, "b");
        await EnqueueAsync(svc, NewAccess(foreign), "c");

        Assert.NotNull(await svc.GetAsync(owner, r1.Id, CancellationToken.None));
        Assert.Null(await svc.GetAsync(foreign, r1.Id, CancellationToken.None));

        var all = await svc.ListAsync(owner, null, null, 0, 50, CancellationToken.None);
        Assert.Equal(2, all.Count);
        var pending = await svc.ListAsync(owner, AiActionRequestStatus.Pending, null, 0, 50, CancellationToken.None);
        Assert.Equal(2, pending.Count);
        var byAccess = await svc.ListAsync(owner, null, access.Id, 0, 50, CancellationToken.None);
        Assert.Equal(2, byAccess.Count);
        var empty = await svc.ListAsync(owner, AiActionRequestStatus.Executed, null, 0, 50, CancellationToken.None);
        Assert.Empty(empty);
    }

    /// <summary>CountPendingAsync ShouldCountPerAccess.</summary>
    [Fact]
    public async Task CountPendingAsync_ShouldCountPerAccess()
    {
        var (svc, _, _) = Create();
        var accessA = NewAccess(Guid.NewGuid());
        var accessB = NewAccess(Guid.NewGuid());
        await EnqueueAsync(svc, accessA, "a");
        await EnqueueAsync(svc, accessA, "b");
        await EnqueueAsync(svc, accessB, "c");

        Assert.Equal(2, await svc.CountPendingAsync(accessA.Id, CancellationToken.None));
        Assert.Equal(1, await svc.CountPendingAsync(accessB.Id, CancellationToken.None));
    }

    /// <summary>WithdrawOpenForAccess ShouldRejectOpenButNotClaimedOrTerminal.</summary>
    [Fact]
    public async Task WithdrawOpenForAccess_ShouldRejectOpenButNotClaimedOrTerminal()
    {
        var (svc, db, _) = Create();
        var access = NewAccess(Guid.NewGuid());
        var pending = await EnqueueAsync(svc, access, "p");
        var approved = await EnqueueAsync(svc, access, "a");
        approved.Approve(approved.OwnerUserId, null);
        var claimed = await EnqueueAsync(svc, access, "c");
        claimed.Approve(claimed.OwnerUserId, null);
        claimed.MarkClaimed(Guid.NewGuid(), DateTime.UtcNow);
        var rejected = await EnqueueAsync(svc, access, "r");
        rejected.Reject(rejected.OwnerUserId, null);
        await db.SaveChangesAsync(CancellationToken.None);

        var count = await svc.WithdrawOpenForAccessAsync(access.Id, "Zugang entzogen", CancellationToken.None);

        Assert.Equal(2, count);
        var all = await db.AiActionRequests.ToListAsync();
        Assert.Equal(AiActionRequestStatus.Rejected, all.Single(x => x.Id == pending.Id).Status);
        Assert.Equal(AiActionRequestStatus.Rejected, all.Single(x => x.Id == approved.Id).Status);
        Assert.Equal(AiActionRequestStatus.Approved, all.Single(x => x.Id == claimed.Id).Status);
        Assert.Equal(AiActionRequestStatus.Rejected, all.Single(x => x.Id == rejected.Id).Status);
        Assert.True(await db.McpAuditEntries.Where(e => e.EventType == "ApprovalWithdrawn").CountAsync() == 2);
    }

    /// <summary>Minimal options monitor stub.</summary>
    private sealed class TestOptionsMonitor : IOptionsMonitor<McpOptions>
    {
        public TestOptionsMonitor(McpOptions value) => CurrentValue = value;
        public McpOptions CurrentValue { get; }
        public McpOptions Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<McpOptions, string?> listener) => null;
    }
}
