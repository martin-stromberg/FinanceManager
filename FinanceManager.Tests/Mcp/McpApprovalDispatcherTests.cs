using FinanceManager.Application.Exceptions;
using FinanceManager.Application.Mcp;
using FinanceManager.Application.Notifications;
using FinanceManager.Domain.Mcp;
using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Mcp;
using FinanceManager.Web.Mcp;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using System.Text.Json;
using Xunit;

namespace FinanceManager.Tests.Mcp;

/// <summary>
/// Dispatcher-level approval-gate wiring tests (F-0902, pipeline position 8): the gate
/// decision after the scope check decides between direct invoke and the queue redirect —
/// <c>RequiresApproval</c> parks the call as an <see cref="AiActionRequest"/> and answers
/// <c>{ queued: true, requestId, … }</c>; <c>Denied</c> maps to the defined error. As the
/// documented fallback a handler-level <c>requiresApproval</c> result is converted into the
/// same queue entry.
/// </summary>
public sealed class McpApprovalDispatcherTests
{
    private static (AppDbContext db, McpApprovalService approvals) NewQueue()
    {
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        var db = services.BuildServiceProvider().GetRequiredService<AppDbContext>();
        var writer = new McpAuditWriter(db, NullLogger<McpAuditWriter>.Instance);
        var options = new TestOptionsMonitor(new McpOptions());
        var approvals = new McpApprovalService(db, writer, Mock.Of<INotificationWriter>(),
            options, NullLogger<McpApprovalService>.Instance);
        return (db, approvals);
    }

    private static McpAccess NewAccess(Guid owner, McpApprovalMode mode = McpApprovalMode.PerAction)
        => new(owner, "acc", "HINT01", "pbkdf2|1|AA|AA", McpAccessLevel.Buchhalter, mode);

    private static IMcpAccessContext NewAccessContext(McpAccess access)
    {
        var ctx = new Mock<IMcpAccessContext>();
        ctx.SetupGet(c => c.Access).Returns(access);
        ctx.SetupGet(c => c.OwnerUserId).Returns(access.OwnerUserId);
        ctx.SetupGet(c => c.McpAccessId).Returns(access.Id);
        ctx.SetupGet(c => c.IsMcpAuthenticated).Returns(true);
        return ctx.Object;
    }

    private static IMcpToolDescriptor NewDescriptor(string name, McpToolSecurityClass securityClass)
    {
        var d = new Mock<IMcpToolDescriptor>();
        d.SetupGet(x => x.Name).Returns(name);
        d.SetupGet(x => x.Description).Returns("test tool");
        d.SetupGet(x => x.SecurityClass).Returns(securityClass);
        d.SetupGet(x => x.RequiredScopes).Returns(Array.Empty<McpToolScope>());
        d.SetupGet(x => x.MetaScope).Returns((string?)null); // data tool → scope loop is a no-op
        d.SetupGet(x => x.InputSchema).Returns(JsonDocument.Parse("{\"type\":\"object\"}"));
        return d.Object;
    }

    private static McpToolDispatcher NewDispatcher(
        IMcpToolDescriptor descriptor, IMcpAuditWriter auditWriter,
        IMcpApprovalGate gate, IMcpApprovalService approvals)
    {
        var registry = new Mock<IMcpToolRegistry>();
        registry.SetupGet(r => r.CatalogVersion).Returns("katalog-v1");
        registry.Setup(r => r.Find(descriptor.Name)).Returns(descriptor);
        var opts = new TestOptionsMonitor(new McpOptions());
        return new McpToolDispatcher(
            registry.Object, Mock.Of<IMcpScopeEvaluator>(), auditWriter,
            gate, approvals,
            new McpToolRateLimiter(opts),
            new McpAnomalyTracker(opts, Mock.Of<IServiceProvider>(), NullLogger<McpAnomalyTracker>.Instance),
            opts, NullLogger<McpToolDispatcher>.Instance);
    }

    private static IMcpApprovalGate GateReturning(McpApprovalDecision decision)
    {
        var gate = new Mock<IMcpApprovalGate>();
        gate.Setup(g => g.EvaluateAsync(It.IsAny<McpAccess>(), It.IsAny<IMcpToolDescriptor>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(decision);
        return gate.Object;
    }

    private static JsonElement Args(string json) => JsonDocument.Parse(json).RootElement;

    /// <summary>Gate=RequiresApproval parks the call: the handler never runs, the request is
    /// persisted and the answer carries the queued descriptor.</summary>
    [Fact]
    public async Task Dispatch_RequiresApproval_Enqueues_AndAnswersQueued()
    {
        var (db, approvals) = NewQueue();
        var audit = new McpAuditWriter(db, NullLogger<McpAuditWriter>.Instance);
        var access = NewAccess(Guid.NewGuid());
        var descriptor = NewDescriptor("fm_test_write", McpToolSecurityClass.Genehmigungspflichtig);

        var handler = new Mock<IMcpToolHandler>();
        handler.SetupGet(h => h.ToolName).Returns("fm_test_write");
        var services = new ServiceCollection().AddSingleton(handler.Object).BuildServiceProvider();

        var dispatcher = NewDispatcher(descriptor, audit, GateReturning(McpApprovalDecision.Approval), approvals);
        var result = await dispatcher.DispatchAsync(NewAccessContext(access), "fm_test_write",
            Args("{\"operation\":\"delete\",\"accountId\":\"11111111-1111-1111-1111-111111111111\",\"reasonText\":\"cleanup\"}"),
            services, "10.0.0.1", CancellationToken.None);

        Assert.Equal(McpDispatchResultKind.Success, result.Kind);
        var payload = JsonSerializer.SerializeToElement(result.Result);
        Assert.True(payload.GetProperty("queued").GetBoolean());
        var requestId = payload.GetProperty("requestId").GetGuid();
        Assert.Equal("fm_test_write", payload.GetProperty("toolName").GetString());
        Assert.Equal("delete", payload.GetProperty("actionKey").GetString());
        Assert.Equal("Pending", payload.GetProperty("status").GetString());
        Assert.True(payload.GetProperty("expiresUtc").GetDateTime() > DateTime.UtcNow);

        var request = await db.AiActionRequests.SingleAsync();
        Assert.Equal(requestId, request.Id);
        Assert.Equal("delete", request.ActionKey);
        Assert.Equal("cleanup", request.ReasonText);
        Assert.Contains("accountId", request.ParameterSnapshot);
        handler.Verify(h => h.InvokeAsync(It.IsAny<JsonElement>(), It.IsAny<McpToolCallContext>(),
            It.IsAny<CancellationToken>()), Times.Never);

        // ToolCall (RedirectedToApproval) + ApprovalEvent (ApprovalRequested) records.
        var events = await db.McpAuditEntries.AsNoTracking().ToListAsync();
        Assert.Contains(events, e => e.Category == McpAuditCategory.ToolCall && e.ResultStatus == "RedirectedToApproval");
        Assert.Contains(events, e => e.Category == McpAuditCategory.ApprovalEvent && e.EventType == "ApprovalRequested");
    }

    /// <summary>A retry with the same client idempotency key returns the same request id —
    /// no duplicate is created.</summary>
    [Fact]
    public async Task Dispatch_EnqueueRetry_SameIdempotencyKey_ReturnsSameRequest()
    {
        var (db, approvals) = NewQueue();
        var audit = new McpAuditWriter(db, NullLogger<McpAuditWriter>.Instance);
        var access = NewAccess(Guid.NewGuid());
        var descriptor = NewDescriptor("fm_test_write", McpToolSecurityClass.Genehmigungspflichtig);
        var dispatcher = NewDispatcher(descriptor, audit, GateReturning(McpApprovalDecision.Approval), approvals);

        var first = await dispatcher.DispatchAsync(NewAccessContext(access), "fm_test_write",
            Args("{\"idempotencyKey\":\"client-42\",\"op\":1}"), Mock.Of<IServiceProvider>(), null, CancellationToken.None);
        var second = await dispatcher.DispatchAsync(NewAccessContext(access), "fm_test_write",
            Args("{\"idempotencyKey\":\"client-42\",\"op\":1}"), Mock.Of<IServiceProvider>(), null, CancellationToken.None);

        var id1 = JsonSerializer.SerializeToElement(first.Result).GetProperty("requestId").GetGuid();
        var id2 = JsonSerializer.SerializeToElement(second.Result).GetProperty("requestId").GetGuid();
        Assert.Equal(id1, id2);
        Assert.Equal(1, await db.AiActionRequests.CountAsync());
    }

    /// <summary>A denied gate decision maps to the defined error (pending limit).</summary>
    [Fact]
    public async Task Dispatch_GateDenied_ReturnsDefinedError()
    {
        var (db, approvals) = NewQueue();
        var audit = new McpAuditWriter(db, NullLogger<McpAuditWriter>.Instance);
        var access = NewAccess(Guid.NewGuid());
        var descriptor = NewDescriptor("fm_test_write", McpToolSecurityClass.Genehmigungspflichtig);
        var dispatcher = NewDispatcher(descriptor, audit,
            GateReturning(McpApprovalDecision.Deny("Err_Mcp_PendingLimit", "limit reached")), approvals);

        var result = await dispatcher.DispatchAsync(NewAccessContext(access), "fm_test_write",
            Args("{}"), Mock.Of<IServiceProvider>(), null, CancellationToken.None);

        Assert.Equal(McpDispatchResultKind.DefinedError, result.Kind);
        Assert.Equal("Err_Mcp_PendingLimit", result.ErrorCode);
        Assert.Equal(0, await db.AiActionRequests.CountAsync());
        var entry = await db.McpAuditEntries.AsNoTracking().SingleAsync();
        Assert.Equal("Denied", entry.ResultStatus);
    }

    /// <summary>A pending-limit race at write time (gate passed, service throws) still maps
    /// to the defined error instead of a protocol failure.</summary>
    [Fact]
    public async Task Dispatch_EnqueuePendingLimitRace_ReturnsDefinedError()
    {
        var (db, _) = NewQueue();
        var audit = new McpAuditWriter(db, NullLogger<McpAuditWriter>.Instance);
        var access = NewAccess(Guid.NewGuid());
        var descriptor = NewDescriptor("fm_test_write", McpToolSecurityClass.Genehmigungspflichtig);
        var approvals = new Mock<IMcpApprovalService>();
        approvals.Setup(a => a.EnqueueAsync(It.IsAny<McpAccess>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<Guid>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DomainValidationException("Err_Mcp_PendingLimit", "limit reached"));
        var dispatcher = NewDispatcher(descriptor, audit,
            GateReturning(McpApprovalDecision.Approval), approvals.Object);

        var result = await dispatcher.DispatchAsync(NewAccessContext(access), "fm_test_write",
            Args("{}"), Mock.Of<IServiceProvider>(), null, CancellationToken.None);

        Assert.Equal(McpDispatchResultKind.DefinedError, result.Kind);
        Assert.Equal("Err_Mcp_PendingLimit", result.ErrorCode);
    }

    /// <summary>The handler-level fallback: gate=Direct but the handler answers
    /// requiresApproval (destructive sub-operation under a direct-execution mode) — the
    /// dispatcher enqueues instead of forwarding the handler result.</summary>
    [Fact]
    public async Task Dispatch_HandlerRequiresApprovalResult_EnqueuesAsFallback()
    {
        var (db, approvals) = NewQueue();
        var audit = new McpAuditWriter(db, NullLogger<McpAuditWriter>.Instance);
        // PreApproved access: the request starts directly approved (approval-konzept.md §3.4).
        var access = NewAccess(Guid.NewGuid(), McpApprovalMode.PreApproved);
        var descriptor = NewDescriptor("fm_test_write", McpToolSecurityClass.Schreibvorbereitend);

        var handler = new Mock<IMcpToolHandler>();
        handler.SetupGet(h => h.ToolName).Returns("fm_test_write");
        handler.Setup(h => h.InvokeAsync(It.IsAny<JsonElement>(), It.IsAny<McpToolCallContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new
            {
                requiresApproval = true,
                code = "Err_Mcp_ApprovalRequired",
                operation = "delete",
                effect = "delete account",
                message = "This operation is final/destructive and requires owner approval."
            });
        var services = new ServiceCollection().AddSingleton(handler.Object).BuildServiceProvider();

        var dispatcher = NewDispatcher(descriptor, audit, GateReturning(McpApprovalDecision.Direct), approvals);
        var result = await dispatcher.DispatchAsync(NewAccessContext(access), "fm_test_write",
            Args("{\"operation\":\"delete\",\"accountId\":\"11111111-1111-1111-1111-111111111111\"}"),
            services, null, CancellationToken.None);

        var payload = JsonSerializer.SerializeToElement(result.Result);
        Assert.True(payload.GetProperty("queued").GetBoolean());
        Assert.Equal("Approved", payload.GetProperty("status").GetString()); // pre-approved → directly queued
        var request = await db.AiActionRequests.SingleAsync();
        Assert.Equal(AiActionRequestStatus.Approved, request.Status);
        Assert.Equal("delete", request.ActionKey);
    }

    /// <summary>Gate=DirectExecution with a normal handler result stays a plain success.</summary>
    [Fact]
    public async Task Dispatch_DirectExecution_InvokesHandlerNormally()
    {
        var (db, approvals) = NewQueue();
        var audit = new McpAuditWriter(db, NullLogger<McpAuditWriter>.Instance);
        var access = NewAccess(Guid.NewGuid());
        var descriptor = NewDescriptor("fm_test_write", McpToolSecurityClass.Schreibvorbereitend);

        var handler = new Mock<IMcpToolHandler>();
        handler.SetupGet(h => h.ToolName).Returns("fm_test_write");
        handler.Setup(h => h.InvokeAsync(It.IsAny<JsonElement>(), It.IsAny<McpToolCallContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new { operation = "create", applied = true });
        var services = new ServiceCollection().AddSingleton(handler.Object).BuildServiceProvider();

        var dispatcher = NewDispatcher(descriptor, audit, GateReturning(McpApprovalDecision.Direct), approvals);
        var result = await dispatcher.DispatchAsync(NewAccessContext(access), "fm_test_write",
            Args("{\"operation\":\"create\"}"), services, null, CancellationToken.None);

        Assert.Equal(McpDispatchResultKind.Success, result.Kind);
        var payload = JsonSerializer.SerializeToElement(result.Result);
        Assert.True(payload.GetProperty("applied").GetBoolean());
        Assert.False(payload.TryGetProperty("queued", out _));
        Assert.Equal(0, await db.AiActionRequests.CountAsync());
    }

    /// <summary>The real gate matrix end to end: a write-preparing call on a PerAction
    /// access parks; on a PreApproved access the same call runs directly.</summary>
    [Fact]
    public async Task Dispatch_RealGate_PerActionEnqueues_PreApprovedExecutes()
    {
        var (db, approvals) = NewQueue();
        var audit = new McpAuditWriter(db, NullLogger<McpAuditWriter>.Instance);
        var owner = Guid.NewGuid();
        var perAction = NewAccess(owner, McpApprovalMode.PerAction);
        var preApproved = NewAccess(owner, McpApprovalMode.PreApproved);
        var descriptor = NewDescriptor("fm_test_write", McpToolSecurityClass.Schreibvorbereitend);
        var gate = new McpApprovalGate(db, new TestOptionsMonitor(new McpOptions()));

        var handler = new Mock<IMcpToolHandler>();
        handler.SetupGet(h => h.ToolName).Returns("fm_test_write");
        handler.Setup(h => h.InvokeAsync(It.IsAny<JsonElement>(), It.IsAny<McpToolCallContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new { applied = true });
        var services = new ServiceCollection().AddSingleton(handler.Object).BuildServiceProvider();

        var dispatcher = NewDispatcher(descriptor, audit, gate, approvals);

        var parked = await dispatcher.DispatchAsync(NewAccessContext(perAction), "fm_test_write",
            Args("{\"operation\":\"create\"}"), services, null, CancellationToken.None);
        var payload = JsonSerializer.SerializeToElement(parked.Result);
        Assert.True(payload.GetProperty("queued").GetBoolean());
        Assert.Equal("Pending", payload.GetProperty("status").GetString());
        handler.Verify(h => h.InvokeAsync(It.IsAny<JsonElement>(), It.IsAny<McpToolCallContext>(),
            It.IsAny<CancellationToken>()), Times.Never);

        var direct = await dispatcher.DispatchAsync(NewAccessContext(preApproved), "fm_test_write",
            Args("{\"operation\":\"create\"}"), services, null, CancellationToken.None);
        Assert.Equal(McpDispatchResultKind.Success, direct.Kind);
        Assert.False(JsonSerializer.SerializeToElement(direct.Result).TryGetProperty("queued", out _));
    }

    private sealed class TestOptionsMonitor : IOptionsMonitor<McpOptions>
    {
        private readonly McpOptions _value;

        public TestOptionsMonitor(McpOptions value)
        {
            _value = value;
        }

        public McpOptions CurrentValue => _value;
        public McpOptions Get(string? name) => _value;
        public IDisposable? OnChange(Action<McpOptions, string?> listener) => null;
    }
}
