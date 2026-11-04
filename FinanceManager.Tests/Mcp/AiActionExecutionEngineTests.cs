using FinanceManager.Application.Mcp;
using FinanceManager.Application.Notifications;
using FinanceManager.Domain.Mcp;
using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Mcp;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using System.Text.Json;
using Xunit;

namespace FinanceManager.Tests.Mcp;

/// <summary>
/// Unit tests of <see cref="AiActionExecutionEngine"/> (F-0903/F-0904): the atomic queue
/// claim, execution-time guard re-evaluation (expiry, access state, write-stop lid, scope
/// profile), execution via the original <see cref="IMcpToolHandler"/> in
/// <see cref="McpToolExecutionMode.ApprovedExecution"/> mode, terminal states with coupled
/// audit records and the expiry sweep.
/// </summary>
public sealed class AiActionExecutionEngineTests
{
    private sealed class Fixture : IDisposable
    {
        private readonly SqliteConnection _connection;

        public AppDbContext Db { get; }
        public AiActionExecutionEngine Engine { get; }
        public Mock<IMcpScopeEvaluator> Scopes { get; } = new();
        public Mock<IMcpToolRegistry> Registry { get; } = new();
        public Mock<IMcpToolDescriptor> Descriptor { get; } = new();
        public Mock<INotificationWriter> Notifications { get; } = new();
        public List<(string EventType, string Result)> Writes { get; } = new();
        public McpToolExecutionMode? SeenMode { get; set; }
        public IMcpAccessContext? SeenAccess { get; set; }
        public object HandlerResult { get; set; } = new { ok = true };
        public Func<JsonElement, Task<object>>? Invoke { get; set; }

        public Fixture(bool writeEnabled = true, int claimStaleMinutes = 10, int timeoutSeconds = 60)
        {
            // SQLite in-memory: the atomic conditional claim (ExecuteUpdate) needs a real
            // relational provider — the EF InMemory provider does not translate it.
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            var services = new ServiceCollection();
            services.AddDbContext<AppDbContext>(o => o.UseSqlite(_connection));
            services.AddScoped<IMcpToolHandler>(_ => new FakeHandler(this));
            var sp = services.BuildServiceProvider();
            Db = sp.GetRequiredService<AppDbContext>();
            Db.Database.EnsureCreated();

            Descriptor.SetupGet(d => d.Name).Returns("fm_test_tool");
            Descriptor.SetupGet(d => d.SecurityClass).Returns(McpToolSecurityClass.Genehmigungspflichtig);
            Descriptor.SetupGet(d => d.RequiredScopes).Returns(new[] { new McpToolScope(McpDataType.Contacts, McpRightKind.Delete) });
            Descriptor.SetupGet(d => d.MetaScope).Returns((string?)null);
            Registry.Setup(r => r.Find("fm_test_tool")).Returns(Descriptor.Object);
            Registry.SetupGet(r => r.CatalogVersion).Returns("katalog-v1");
            Scopes.Setup(s => s.IsAllowedAsync(It.IsAny<McpAccess>(), It.IsAny<McpDataType>(), It.IsAny<McpRightKind>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var audit = new Mock<IMcpAuditWriter>();
            audit.Setup(a => a.StageAsync(It.IsAny<McpAuditWriteRequest>(), It.IsAny<CancellationToken>()))
                .Callback<McpAuditWriteRequest, CancellationToken>((r, _) => Writes.Add((r.EventType, r.ResultStatus)))
                .ReturnsAsync((McpAuditEntry?)null);
            audit.Setup(a => a.WriteAsync(It.IsAny<McpAuditWriteRequest>(), It.IsAny<CancellationToken>()))
                .Callback<McpAuditWriteRequest, CancellationToken>((r, _) => Writes.Add((r.EventType, r.ResultStatus)))
                .Returns(Task.CompletedTask);

            var options = new TestOptionsMonitor(new McpOptions
            {
                WriteAccess = new McpWriteAccessOptions { Enabled = writeEnabled },
                Approval = new McpApprovalOptions
                {
                    ClaimStaleMinutes = claimStaleMinutes,
                    ExecutionTimeoutSeconds = timeoutSeconds,
                    MaxExecutionBatchSize = 20
                }
            });
            Engine = new AiActionExecutionEngine(Db, Scopes.Object, audit.Object, Registry.Object,
                Notifications.Object, sp, options, NullLogger<AiActionExecutionEngine>.Instance);
        }

        /// <summary>Releases the database context and the in-memory SQLite connection.</summary>
        public void Dispose()
        {
            Db.Dispose();
            _connection.Dispose();
        }

        private sealed class FakeHandler : IMcpToolHandler
        {
            private readonly Fixture _f;
            public FakeHandler(Fixture f) => _f = f;
            public string ToolName => "fm_test_tool";
            public Task<object> InvokeAsync(JsonElement arguments, McpToolCallContext context, CancellationToken ct)
            {
                _f.SeenMode = context.Mode;
                _f.SeenAccess = context.Access;
                return _f.Invoke is not null ? _f.Invoke(arguments) : Task.FromResult(_f.HandlerResult);
            }
        }
    }

    private sealed class TestOptionsMonitor : IOptionsMonitor<McpOptions>
    {
        public TestOptionsMonitor(McpOptions value) => CurrentValue = value;
        public McpOptions CurrentValue { get; }
        public McpOptions Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<McpOptions, string?> listener) => null;
    }

    private static McpAccess NewAccess(Guid owner, McpApprovalMode mode = McpApprovalMode.PerAction)
        => new(owner, "acc", "HINT01", "pbkdf2|1|AA|AA", McpAccessLevel.Buchhalter, mode);

    private static AiActionRequest ApprovedRequest(McpAccess access, DateTime? expiresUtc = null)
    {
        var r = new AiActionRequest(access.OwnerUserId, access.Id, access.Name, "fm_test_tool",
            "delete", "katalog-v1", "{\"id\":\"1\"}", null,
            expiresUtc ?? DateTime.UtcNow.AddHours(1), Guid.NewGuid(), $"k-{Guid.NewGuid():N}");
        r.Approve(access.OwnerUserId, null);
        return r;
    }

    private static AiActionRequest PendingRequest(McpAccess access, DateTime expiresUtc)
        => new(access.OwnerUserId, access.Id, access.Name, "fm_test_tool", "delete",
            "katalog-v1", "{}", null, expiresUtc, Guid.NewGuid(), $"k-{Guid.NewGuid():N}");

    /// <summary>ExecuteDue ShouldExecuteApprovedRequest ViaHandlerInApprovedMode.</summary>
    [Fact]
    public async Task ExecuteDue_ShouldExecuteApprovedRequest_ViaHandlerInApprovedMode()
    {
        var f = new Fixture();
        var access = NewAccess(Guid.NewGuid());
        f.Db.McpAccesses.Add(access);
        var request = ApprovedRequest(access);
        f.Db.AiActionRequests.Add(request);
        await f.Db.SaveChangesAsync(CancellationToken.None);

        var done = await f.Engine.ExecuteDueRequestsAsync(10, CancellationToken.None);

        Assert.Equal(1, done);
        var reloaded = await f.Db.AiActionRequests.AsNoTracking().FirstAsync();
        Assert.Equal(AiActionRequestStatus.Executed, reloaded.Status);
        Assert.NotNull(reloaded.ExecutionResult);
        Assert.Null(reloaded.ClaimToken);
        Assert.Equal(1, reloaded.AttemptCount);
        Assert.Equal(McpToolExecutionMode.ApprovedExecution, f.SeenMode);
        Assert.Equal(access.OwnerUserId, f.SeenAccess!.OwnerUserId);
        Assert.Contains(f.Writes, w => w.EventType == "ApprovalExecutionStarted");
        Assert.Contains(f.Writes, w => w.EventType == "ApprovalExecuted");
    }

    /// <summary>ExecuteDue ShouldNotPickPendingOrClaimedOrTerminal.</summary>
    [Fact]
    public async Task ExecuteDue_ShouldNotPickPendingOrClaimedOrTerminal()
    {
        var f = new Fixture();
        var access = NewAccess(Guid.NewGuid());
        f.Db.McpAccesses.Add(access);
        var pending = PendingRequest(access, DateTime.UtcNow.AddHours(1));
        var claimed = ApprovedRequest(access);
        claimed.MarkClaimed(Guid.NewGuid(), DateTime.UtcNow);
        var executed = ApprovedRequest(access);
        executed.MarkClaimed(Guid.NewGuid(), DateTime.UtcNow);
        executed.MarkExecuted("{}");
        f.Db.AiActionRequests.AddRange(pending, claimed, executed);
        await f.Db.SaveChangesAsync(CancellationToken.None);

        var done = await f.Engine.ExecuteDueRequestsAsync(10, CancellationToken.None);

        Assert.Equal(0, done);
        Assert.Null(f.SeenMode);
    }

    /// <summary>ExecuteDue StaleClaim ShouldBeReclaimed.</summary>
    [Fact]
    public async Task ExecuteDue_StaleClaim_ShouldBeReclaimed()
    {
        var f = new Fixture(claimStaleMinutes: 10);
        var access = NewAccess(Guid.NewGuid());
        f.Db.McpAccesses.Add(access);
        var request = ApprovedRequest(access);
        request.MarkClaimed(Guid.NewGuid(), DateTime.UtcNow.AddMinutes(-30));
        f.Db.AiActionRequests.Add(request);
        await f.Db.SaveChangesAsync(CancellationToken.None);

        var done = await f.Engine.ExecuteDueRequestsAsync(10, CancellationToken.None);

        Assert.Equal(1, done);
        Assert.Equal(AiActionRequestStatus.Executed, (await f.Db.AiActionRequests.AsNoTracking().FirstAsync()).Status);
        Assert.Equal(2, (await f.Db.AiActionRequests.AsNoTracking().FirstAsync()).AttemptCount);
    }

    /// <summary>ExecuteDue HandlerFailure ShouldMarkFailedWithClassifiedReason.</summary>
    [Fact]
    public async Task ExecuteDue_HandlerFailure_ShouldMarkFailedWithClassifiedReason()
    {
        var f = new Fixture();
        f.Invoke = _ => throw new McpToolException("Err_AlreadyBooked", "booked");
        var access = NewAccess(Guid.NewGuid());
        f.Db.McpAccesses.Add(access);
        f.Db.AiActionRequests.Add(ApprovedRequest(access));
        await f.Db.SaveChangesAsync(CancellationToken.None);

        var done = await f.Engine.ExecuteDueRequestsAsync(10, CancellationToken.None);

        Assert.Equal(1, done);
        var reloaded = await f.Db.AiActionRequests.AsNoTracking().FirstAsync();
        Assert.Equal(AiActionRequestStatus.Failed, reloaded.Status);
        Assert.Equal("Err_AlreadyBooked", reloaded.FailureReason);
        Assert.Contains(f.Writes, w => w.EventType == "ApprovalFailed" && w.Result == "Failed");
    }

    /// <summary>ExecuteDue UnknownException ShouldMarkFailedInternal.</summary>
    [Fact]
    public async Task ExecuteDue_UnknownException_ShouldMarkFailedInternal()
    {
        var f = new Fixture();
        f.Invoke = _ => throw new InvalidOperationException("boom");
        var access = NewAccess(Guid.NewGuid());
        f.Db.McpAccesses.Add(access);
        f.Db.AiActionRequests.Add(ApprovedRequest(access));
        await f.Db.SaveChangesAsync(CancellationToken.None);

        await f.Engine.ExecuteDueRequestsAsync(10, CancellationToken.None);

        Assert.Equal("Err_Internal", (await f.Db.AiActionRequests.AsNoTracking().FirstAsync()).FailureReason);
    }

    /// <summary>ExecuteDue RevokedAccess ShouldWithdrawRequest.</summary>
    [Fact]
    public async Task ExecuteDue_RevokedAccess_ShouldWithdrawRequest()
    {
        var f = new Fixture();
        var access = NewAccess(Guid.NewGuid());
        access.Revoke("gone");
        f.Db.McpAccesses.Add(access);
        f.Db.AiActionRequests.Add(ApprovedRequest(access));
        await f.Db.SaveChangesAsync(CancellationToken.None);

        var done = await f.Engine.ExecuteDueRequestsAsync(10, CancellationToken.None);

        Assert.Equal(1, done);
        var reloaded = await f.Db.AiActionRequests.AsNoTracking().FirstAsync();
        Assert.Equal(AiActionRequestStatus.Rejected, reloaded.Status);
        Assert.Contains(f.Writes, w => w.EventType == "ApprovalWithdrawn");
        Assert.Null(f.SeenMode);
    }

    /// <summary>ExecuteDue LostScope ShouldFailRightsRevoked.</summary>
    [Fact]
    public async Task ExecuteDue_LostScope_ShouldFailRightsRevoked()
    {
        var f = new Fixture();
        f.Scopes.Setup(s => s.IsAllowedAsync(It.IsAny<McpAccess>(), It.IsAny<McpDataType>(), It.IsAny<McpRightKind>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var access = NewAccess(Guid.NewGuid());
        f.Db.McpAccesses.Add(access);
        f.Db.AiActionRequests.Add(ApprovedRequest(access));
        await f.Db.SaveChangesAsync(CancellationToken.None);

        await f.Engine.ExecuteDueRequestsAsync(10, CancellationToken.None);

        Assert.Equal("Err_RightsRevoked", (await f.Db.AiActionRequests.AsNoTracking().FirstAsync()).FailureReason);
    }

    /// <summary>ExecuteDue WriteStop ShouldReleaseClaimAndKeepApproved.</summary>
    [Fact]
    public async Task ExecuteDue_WriteStop_ShouldReleaseClaimAndKeepApproved()
    {
        var f = new Fixture(writeEnabled: false);
        var access = NewAccess(Guid.NewGuid());
        f.Db.McpAccesses.Add(access);
        f.Db.AiActionRequests.Add(ApprovedRequest(access));
        await f.Db.SaveChangesAsync(CancellationToken.None);

        var done = await f.Engine.ExecuteDueRequestsAsync(10, CancellationToken.None);

        Assert.Equal(0, done);
        var reloaded = await f.Db.AiActionRequests.AsNoTracking().FirstAsync();
        Assert.Equal(AiActionRequestStatus.Approved, reloaded.Status);
        Assert.Null(reloaded.ClaimToken);
        Assert.Null(f.SeenMode);
    }

    /// <summary>ExecuteDue ExpiredApproved ShouldExpireNotExecute.</summary>
    [Fact]
    public async Task ExecuteDue_ExpiredApproved_ShouldExpireNotExecute()
    {
        var f = new Fixture();
        var access = NewAccess(Guid.NewGuid());
        f.Db.McpAccesses.Add(access);
        // expiresUtc in the past — candidate filter excludes it, so it stays Approved
        // until the expiry sweep; verify the sweep path instead.
        var request = ApprovedRequest(access, DateTime.UtcNow.AddMinutes(-1));
        f.Db.AiActionRequests.Add(request);
        await f.Db.SaveChangesAsync(CancellationToken.None);

        var done = await f.Engine.ExecuteDueRequestsAsync(10, CancellationToken.None);
        Assert.Equal(0, done);
        Assert.Null(f.SeenMode);

        var expired = await f.Engine.ExpireOverdueRequestsAsync(DateTime.UtcNow, 10, CancellationToken.None);
        Assert.Equal(1, expired);
        Assert.Equal(AiActionRequestStatus.Expired, (await f.Db.AiActionRequests.AsNoTracking().FirstAsync()).Status);
    }

    /// <summary>ExpireOverdue ShouldExpirePendingAndApproved KeepTerminalAndClaimed.</summary>
    [Fact]
    public async Task ExpireOverdue_ShouldExpirePendingAndApproved_KeepTerminalAndClaimed()
    {
        var f = new Fixture();
        var access = NewAccess(Guid.NewGuid());
        f.Db.McpAccesses.Add(access);
        var pastDue = DateTime.UtcNow.AddHours(-1);
        var p = PendingRequest(access, pastDue);
        var a = ApprovedRequest(access, pastDue);
        var claimed = ApprovedRequest(access, pastDue);
        claimed.MarkClaimed(Guid.NewGuid(), DateTime.UtcNow);
        var rejected = PendingRequest(access, pastDue);
        rejected.Reject(access.OwnerUserId, null);
        var future = PendingRequest(access, DateTime.UtcNow.AddHours(1));
        f.Db.AiActionRequests.AddRange(p, a, claimed, rejected, future);
        await f.Db.SaveChangesAsync(CancellationToken.None);

        var expired = await f.Engine.ExpireOverdueRequestsAsync(DateTime.UtcNow, 10, CancellationToken.None);

        Assert.Equal(2, expired);
        var all = await f.Db.AiActionRequests.AsNoTracking().ToListAsync();
        Assert.Equal(AiActionRequestStatus.Expired, all.Single(x => x.Id == p.Id).Status);
        Assert.Equal(AiActionRequestStatus.Expired, all.Single(x => x.Id == a.Id).Status);
        Assert.Equal(AiActionRequestStatus.Approved, all.Single(x => x.Id == claimed.Id).Status);
        Assert.Equal(AiActionRequestStatus.Rejected, all.Single(x => x.Id == rejected.Id).Status);
        Assert.Equal(AiActionRequestStatus.Pending, all.Single(x => x.Id == future.Id).Status);
        Assert.Equal(2, f.Writes.Count(w => w.EventType == "ApprovalExpired"));
    }
}
