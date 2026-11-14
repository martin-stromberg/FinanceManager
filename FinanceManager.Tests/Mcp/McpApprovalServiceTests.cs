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
        int maxPending = 25, int expiryHours = 72, bool realAudit = true,
        Mock<IMcpToolRegistry>? registry = null, Mock<IMcpScopeEvaluator>? scopes = null)
    {
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        var sp = services.BuildServiceProvider();
        var db = sp.GetRequiredService<AppDbContext>();
        IMcpAuditWriter audit = realAudit
            ? new McpAuditWriter(db, NullLogger<McpAuditWriter>.Instance)
            : new NullMcpAuditWriter(NullLogger<NullMcpAuditWriter>.Instance);
        var notifications = new Mock<INotificationWriter>();
        registry ??= new Mock<IMcpToolRegistry>();
        scopes ??= new Mock<IMcpScopeEvaluator>();
        // Safe default: an empty effective profile — the coverage check never dereferences null.
        scopes.Setup(s => s.GetEffectiveScopesAsync(It.IsAny<McpAccess>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<McpAccessScope>());
        var options = new TestOptionsMonitor(new McpOptions
        {
            Approval = new McpApprovalOptions { MaxPendingPerAccess = maxPending, DefaultExpiryHours = expiryHours }
        });
        var svc = new McpApprovalService(db, audit, notifications.Object, registry.Object,
            scopes.Object, options, NullLogger<McpApprovalService>.Instance);
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
        var entry = await db.McpAuditEntries.SingleAsync(cancellationToken: TestContext.Current.CancellationToken);
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
        Assert.Equal(1, await db.AiActionRequests.CountAsync(cancellationToken: TestContext.Current.CancellationToken));
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
        Assert.Equal(2, await db.AiActionRequests.CountAsync(cancellationToken: TestContext.Current.CancellationToken));
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

    /// <summary>GetAccessMonitoring aggregates queue depth and 24h outcomes of the access (F-1103).</summary>
    [Fact]
    public async Task GetAccessMonitoring_ShouldAggregateOwnAccess()
    {
        var (svc, db, _) = Create();
        var owner = Guid.NewGuid();
        var access = NewAccess(owner);
        db.McpAccesses.Add(access);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var pending = await svc.EnqueueAsync(access, "fm_contacts_manage", "delete", "katalog-v1",
            "{}", null, Guid.NewGuid(), "m1", CancellationToken.None);
        var approved = await svc.EnqueueAsync(access, "fm_contacts_manage", "merge", "katalog-v1",
            "{}", null, Guid.NewGuid(), "m2", CancellationToken.None);
        await svc.ApproveAsync(owner, approved.Id, null, CancellationToken.None);
        var failed = await svc.EnqueueAsync(access, "fm_contacts_manage", "archive", "katalog-v1",
            "{}", null, Guid.NewGuid(), "m3", CancellationToken.None);
        var failedRow = await db.AiActionRequests.FindAsync(new object?[] { failed.Id }, TestContext.Current.CancellationToken);
        failedRow!.Approve(owner, null);
        failedRow.MarkClaimed(Guid.NewGuid(), DateTime.UtcNow);
        failedRow.MarkFailed("Err_TargetGone");
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var dto = await svc.GetAccessMonitoringAsync(owner, access.Id, CancellationToken.None);

        Assert.NotNull(dto);
        Assert.Equal(1, dto!.PendingCount);
        Assert.Equal(1, dto.ApprovedCount);
        Assert.NotNull(dto.OldestPendingUtc);
        Assert.Equal(pending.RequestedUtc, dto.OldestPendingUtc!.Value, TimeSpan.FromSeconds(5));
        Assert.Equal(3, dto.RequestsLast24h);
        Assert.Equal(0, dto.ExecutedLast24h);
        Assert.Equal(1, dto.FailedLast24h);
        Assert.Equal(1d, dto.FailureRateLast24h);
        Assert.NotNull(dto.LastRequestUtc);

        Assert.Null(await svc.GetAccessMonitoringAsync(Guid.NewGuid(), access.Id, CancellationToken.None));
        Assert.Null(await svc.GetAccessMonitoringAsync(owner, Guid.NewGuid(), CancellationToken.None));
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
        var reloaded = await db.AiActionRequests.FindAsync(new object?[] { request.Id }, TestContext.Current.CancellationToken);
        Assert.Equal(AiActionRequestStatus.Approved, reloaded!.Status);
        Assert.Equal("lgtm", reloaded.DecisionNote);
        Assert.Equal(2, await db.McpAuditEntries.CountAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal("ApprovalDecided", (await db.McpAuditEntries.OrderByDescending(e => e.SequenceNo).FirstAsync(cancellationToken: TestContext.Current.CancellationToken)).EventType);
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
            (await db.AiActionRequests.FindAsync(new object?[] { request.Id }, TestContext.Current.CancellationToken))!.Status);
    }

    /// <summary>Cancel ShouldWithdrawPendingRequest.</summary>
    [Fact]
    public async Task Cancel_ShouldWithdrawPendingRequest()
    {
        var (svc, db, _) = Create();
        var owner = Guid.NewGuid();
        var request = await EnqueueAsync(svc, NewAccess(owner));

        Assert.True(await svc.CancelAsync(owner, request.Id, CancellationToken.None));
        var reloaded = await db.AiActionRequests.FindAsync(new object?[] { request.Id }, TestContext.Current.CancellationToken);
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

        var all = await svc.ListAsync(owner, null, null, null, 0, 50, CancellationToken.None);
        Assert.Equal(2, all.Count);
        var pending = await svc.ListAsync(owner, AiActionRequestStatus.Pending, null, null, 0, 50, CancellationToken.None);
        Assert.Equal(2, pending.Count);
        var byAccess = await svc.ListAsync(owner, null, access.Id, null, 0, 50, CancellationToken.None);
        Assert.Equal(2, byAccess.Count);
        var empty = await svc.ListAsync(owner, AiActionRequestStatus.Executed, null, null, 0, 50, CancellationToken.None);
        Assert.Empty(empty);
    }

    /// <summary>The tool filter narrows the list server-side (ui-konzept.md §6.2 —
    /// „Zugang/Tool/Zustand"): requests of other tools stay out of the page.</summary>
    [Fact]
    public async Task ListAsync_ToolNameFilter_ShouldReturnOnlyMatchingTool()
    {
        var (svc, _, _) = Create();
        var owner = Guid.NewGuid();
        var access = NewAccess(owner);
        await svc.EnqueueAsync(access, "fm_contacts_manage", "delete", "katalog-v1",
            "{}", null, Guid.NewGuid(), "a", CancellationToken.None);
        await svc.EnqueueAsync(access, "fm_posting_reverse", "fm_posting_reverse", "katalog-v1",
            "{}", null, Guid.NewGuid(), "b", CancellationToken.None);

        var filtered = await svc.ListAsync(owner, null, null, "fm_posting_reverse", 0, 50, CancellationToken.None);
        var other = await svc.ListAsync(owner, null, null, "fm_unknown_tool", 0, 50, CancellationToken.None);

        var item = Assert.Single(filtered);
        Assert.Equal("fm_posting_reverse", item.ToolName);
        Assert.Empty(other);
    }

    /// <summary>The list item carries the server-side effect preview of the request
    /// (inbox-row effect statement of the approval center).</summary>
    [Fact]
    public async Task ListAsync_ShouldIncludeEffectPreview()
    {
        var (svc, _, _) = Create();
        var owner = Guid.NewGuid();
        await svc.EnqueueAsync(NewAccess(owner), "fm_contacts_manage", "delete", "katalog-v1",
            "{\"contactId\":\"x\"}", null, Guid.NewGuid(), "p", CancellationToken.None);

        var item = Assert.Single(await svc.ListAsync(owner, null, null, null, 0, 50, CancellationToken.None));
        Assert.NotNull(item.EffectPreview);
        Assert.Equal("delete", item.EffectPreview!.Kind);
        Assert.True(item.EffectPreview.IsDestructive);
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
        var all = await db.AiActionRequests.ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(AiActionRequestStatus.Rejected, all.Single(x => x.Id == pending.Id).Status);
        Assert.Equal(AiActionRequestStatus.Rejected, all.Single(x => x.Id == approved.Id).Status);
        Assert.Equal(AiActionRequestStatus.Approved, all.Single(x => x.Id == claimed.Id).Status);
        Assert.Equal(AiActionRequestStatus.Rejected, all.Single(x => x.Id == rejected.Id).Status);
        Assert.True(await db.McpAuditEntries.Where(e => e.EventType == "ApprovalWithdrawn").CountAsync(cancellationToken: TestContext.Current.CancellationToken) == 2);
    }

    /// <summary>A data-tool descriptor stub declaring the given required scope tuples.</summary>
    private static IMcpToolDescriptor DataToolDescriptor(string name, params McpToolScope[] requiredScopes)
    {
        var d = new Mock<IMcpToolDescriptor>();
        d.SetupGet(x => x.Name).Returns(name);
        d.SetupGet(x => x.RequiredScopes).Returns(requiredScopes);
        d.SetupGet(x => x.MetaScope).Returns((string?)null);
        return d.Object;
    }

    /// <summary>
    /// F-0904 (approval-konzept.md §3.3/§6.1 „Recht entfallen"): the granular withdrawal
    /// rejects only the open requests whose tool descriptor declares a scope tuple the new
    /// effective profile no longer grants — still-covered requests and requests under an
    /// active claim stay untouched; every withdrawal couples its ApprovalWithdrawn record.
    /// </summary>
    [Fact]
    public async Task WithdrawScopeUncovered_ShouldRejectOnlyUncoveredRequests()
    {
        var registry = new Mock<IMcpToolRegistry>();
        var scopes = new Mock<IMcpScopeEvaluator>();
        var (svc, db, _) = Create(registry: registry, scopes: scopes);
        var owner = Guid.NewGuid();
        var access = NewAccess(owner);
        db.McpAccesses.Add(access);

        registry.Setup(r => r.Find("fm_delete_tool")).Returns(DataToolDescriptor("fm_delete_tool",
            new McpToolScope(McpDataType.Contacts, McpRightKind.Delete)));
        registry.Setup(r => r.Find("fm_read_tool")).Returns(DataToolDescriptor("fm_read_tool",
            new McpToolScope(McpDataType.Contacts, McpRightKind.Read)));
        // The profile after the scope change grants Read only — the delete tool lost its tuple.
        scopes.Setup(s => s.GetEffectiveScopesAsync(It.IsAny<McpAccess>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new McpAccessScope(access.Id, owner, McpDataType.Contacts, McpRightKind.Read) });

        var pendingDelete = new AiActionRequest(owner, access.Id, "acc-1", "fm_delete_tool", "delete",
            "katalog-v1", "{}", null, DateTime.UtcNow.AddHours(72), Guid.NewGuid(), "k-pd");
        var approvedDelete = new AiActionRequest(owner, access.Id, "acc-1", "fm_delete_tool", "delete",
            "katalog-v1", "{}", null, DateTime.UtcNow.AddHours(72), Guid.NewGuid(), "k-ad");
        approvedDelete.Approve(owner, null);
        var claimedDelete = new AiActionRequest(owner, access.Id, "acc-1", "fm_delete_tool", "delete",
            "katalog-v1", "{}", null, DateTime.UtcNow.AddHours(72), Guid.NewGuid(), "k-cd");
        claimedDelete.Approve(owner, null);
        claimedDelete.MarkClaimed(Guid.NewGuid(), DateTime.UtcNow); // active execution — untouched
        var pendingRead = new AiActionRequest(owner, access.Id, "acc-1", "fm_read_tool", "list",
            "katalog-v1", "{}", null, DateTime.UtcNow.AddHours(72), Guid.NewGuid(), "k-pr");
        db.AiActionRequests.AddRange(pendingDelete, approvedDelete, claimedDelete, pendingRead);
        await db.SaveChangesAsync(CancellationToken.None);

        var count = await svc.WithdrawScopeUncoveredForAccessAsync(access.Id, "Recht entfallen", CancellationToken.None);

        Assert.Equal(2, count);
        var all = await db.AiActionRequests.AsNoTracking().ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
        foreach (var id in new[] { pendingDelete.Id, approvedDelete.Id })
        {
            var row = all.Single(r => r.Id == id);
            Assert.Equal(AiActionRequestStatus.Rejected, row.Status);
            Assert.Equal("Recht entfallen", row.DecisionNote);
            Assert.Null(row.DecidedByUserId); // system transition
        }
        Assert.Equal(AiActionRequestStatus.Approved, all.Single(r => r.Id == claimedDelete.Id).Status);
        Assert.Equal(AiActionRequestStatus.Pending, all.Single(r => r.Id == pendingRead.Id).Status);
        Assert.Equal(2, await db.McpAuditEntries.AsNoTracking()
            .CountAsync(e => e.EventType == "ApprovalWithdrawn", cancellationToken: TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Meta-scope tools carry an inherent scope (never profile-lost) and unknown tools keep
    /// their request — they fail classified at execution, not at the profile check.
    /// </summary>
    [Fact]
    public async Task WithdrawScopeUncovered_MetaAndUnknownTools_StayOpen()
    {
        var registry = new Mock<IMcpToolRegistry>();
        var scopes = new Mock<IMcpScopeEvaluator>(); // default: empty effective profile
        var (svc, db, _) = Create(registry: registry, scopes: scopes);
        var owner = Guid.NewGuid();
        var access = NewAccess(owner);
        db.McpAccesses.Add(access);

        var metaDescriptor = new Mock<IMcpToolDescriptor>();
        metaDescriptor.SetupGet(d => d.Name).Returns("fm_meta_tool");
        metaDescriptor.SetupGet(d => d.MetaScope).Returns("Ausführungsstatus");
        metaDescriptor.SetupGet(d => d.RequiredScopes).Returns(Array.Empty<McpToolScope>());
        registry.Setup(r => r.Find("fm_meta_tool")).Returns(metaDescriptor.Object);
        // "fm_unknown" deliberately not registered — Find returns null.

        var meta = new AiActionRequest(owner, access.Id, "acc-1", "fm_meta_tool", "status",
            "katalog-v1", "{}", null, DateTime.UtcNow.AddHours(72), Guid.NewGuid(), "k-meta");
        var unknown = new AiActionRequest(owner, access.Id, "acc-1", "fm_unknown", "call",
            "katalog-v1", "{}", null, DateTime.UtcNow.AddHours(72), Guid.NewGuid(), "k-unk");
        db.AiActionRequests.AddRange(meta, unknown);
        await db.SaveChangesAsync(CancellationToken.None);

        var count = await svc.WithdrawScopeUncoveredForAccessAsync(access.Id, "Recht entfallen", CancellationToken.None);

        Assert.Equal(0, count);
        var all = await db.AiActionRequests.AsNoTracking().ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.All(all, r => Assert.Equal(AiActionRequestStatus.Pending, r.Status));
        Assert.Equal(0, await db.McpAuditEntries.AsNoTracking()
            .CountAsync(e => e.EventType == "ApprovalWithdrawn", cancellationToken: TestContext.Current.CancellationToken));
    }

    /// <summary>A scope check on a gone access is a no-op — the executor re-check covers it.</summary>
    [Fact]
    public async Task WithdrawScopeUncovered_MissingAccess_ReturnsZero()
    {
        var (svc, _, _) = Create();

        Assert.Equal(0, await svc.WithdrawScopeUncoveredForAccessAsync(
            Guid.NewGuid(), "Recht entfallen", CancellationToken.None));
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
