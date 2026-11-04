using FinanceManager.Application.Mcp;
using FinanceManager.Domain.Mcp;
using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Mcp;
using FinanceManager.Shared.Dtos.Mcp;
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
/// Pipeline-level audit tests (F-0301): transactional coupling of write effects to their
/// audit record (<see cref="McpAccessService"/>), the non-blocking audit failure rule of
/// read calls and the fail rule of write-class calls (<see cref="McpToolDispatcher"/>), and
/// the end-to-end <see cref="McpAuditCategory.ToolCall"/> record incl. CorrelationId and
/// call parameters.
/// </summary>
public sealed class McpAuditPipelineTests
{
    private static AppDbContext NewDb()
    {
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        return services.BuildServiceProvider().GetRequiredService<AppDbContext>();
    }

    private static McpAccess NewAccess(Guid owner)
        => new(owner, "acc", "HINT01", "pbkdf2|1|AA|AA", McpAccessLevel.Buchhalter, McpApprovalMode.PerAction);

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
        d.SetupGet(x => x.MetaScope).Returns("TestMeta"); // meta scope → no scope evaluation
        d.SetupGet(x => x.InputSchema).Returns(JsonDocument.Parse("{\"type\":\"object\"}"));
        return d.Object;
    }

    private static McpToolDispatcher NewDispatcher(
        IMcpToolDescriptor descriptor,
        IMcpAuditWriter auditWriter,
        McpOptions? options = null)
    {
        var registry = new Mock<IMcpToolRegistry>();
        registry.SetupGet(r => r.CatalogVersion).Returns("katalog-v1");
        registry.Setup(r => r.Find(descriptor.Name)).Returns(descriptor);
        var evaluator = new Mock<IMcpScopeEvaluator>();
        var gate = new Mock<IMcpApprovalGate>();
        gate.Setup(g => g.EvaluateAsync(It.IsAny<McpAccess>(), It.IsAny<IMcpToolDescriptor>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(McpApprovalDecision.Direct);
        var opts = new TestOptionsMonitor(options ?? new McpOptions());
        return new McpToolDispatcher(
            registry.Object, evaluator.Object, auditWriter,
            gate.Object, Mock.Of<IMcpApprovalService>(),
            new McpToolRateLimiter(opts),
            new McpAnomalyTracker(opts, Mock.Of<IServiceProvider>(), NullLogger<McpAnomalyTracker>.Instance),
            opts, NullLogger<McpToolDispatcher>.Instance);
    }

    private static IServiceProvider ServicesWith(IMcpToolHandler handler)
    {
        var services = new ServiceCollection();
        services.AddSingleton(handler);
        return services.BuildServiceProvider();
    }

    /// <summary>A read-class call succeeds even when the audit write fails (F-0301 AK).</summary>
    [Fact]
    public async Task Dispatch_ReadCall_Succeeds_WhenAuditWriteFails()
    {
        var access = NewAccess(Guid.NewGuid());
        var audit = new Mock<IMcpAuditWriter>();
        audit.Setup(a => a.WriteAsync(It.IsAny<McpAuditWriteRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("audit store down"));

        var descriptor = NewDescriptor("fm_test_read", McpToolSecurityClass.Lesend);
        var handler = new Mock<IMcpToolHandler>();
        handler.SetupGet(h => h.ToolName).Returns("fm_test_read");
        handler.Setup(h => h.InvokeAsync(It.IsAny<JsonElement>(), It.IsAny<McpToolCallContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new { ok = true });

        var dispatcher = NewDispatcher(descriptor, audit.Object);
        var result = await dispatcher.DispatchAsync(
            NewAccessContext(access), "fm_test_read", JsonDocument.Parse("{}").RootElement,
            ServicesWith(handler.Object), "10.0.0.1", CancellationToken.None);

        Assert.Equal(McpDispatchResultKind.Success, result.Kind);
    }

    /// <summary>A write-class call fails when its audit record cannot be persisted (F-0301 AK).</summary>
    [Fact]
    public async Task Dispatch_WriteClassCall_Fails_WhenAuditWriteFails()
    {
        var access = NewAccess(Guid.NewGuid());
        var audit = new Mock<IMcpAuditWriter>();
        audit.Setup(a => a.WriteAsync(It.IsAny<McpAuditWriteRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("audit store down"));

        var descriptor = NewDescriptor("fm_test_write", McpToolSecurityClass.Schreibvorbereitend);
        var handler = new Mock<IMcpToolHandler>();
        handler.SetupGet(h => h.ToolName).Returns("fm_test_write");
        handler.Setup(h => h.InvokeAsync(It.IsAny<JsonElement>(), It.IsAny<McpToolCallContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new { ok = true });

        var dispatcher = NewDispatcher(descriptor, audit.Object);
        var result = await dispatcher.DispatchAsync(
            NewAccessContext(access), "fm_test_write", JsonDocument.Parse("{}").RootElement,
            ServicesWith(handler.Object), "10.0.0.1", CancellationToken.None);

        Assert.Equal(McpDispatchResultKind.DefinedError, result.Kind);
        Assert.Equal("Err_Mcp_AuditFailed", result.ErrorCode);
    }

    /// <summary>A dispatched call persists a ToolCall record with parameters and correlation id.</summary>
    [Fact]
    public async Task Dispatch_SuccessfulCall_WritesToolCallRecord_WithParametersAndCorrelation()
    {
        var db = NewDb();
        var access = NewAccess(Guid.NewGuid());
        var writer = new McpAuditWriter(db, NullLogger<McpAuditWriter>.Instance);

        var descriptor = NewDescriptor("fm_test_read", McpToolSecurityClass.Lesend);
        var handler = new Mock<IMcpToolHandler>();
        handler.SetupGet(h => h.ToolName).Returns("fm_test_read");
        handler.Setup(h => h.InvokeAsync(It.IsAny<JsonElement>(), It.IsAny<McpToolCallContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new { items = 7 });

        var dispatcher = NewDispatcher(descriptor, writer);
        var result = await dispatcher.DispatchAsync(
            NewAccessContext(access), "fm_test_read",
            JsonDocument.Parse("{\"accountId\":\"11111111-1111-1111-1111-111111111111\",\"take\":50}").RootElement,
            ServicesWith(handler.Object), "10.0.0.1", CancellationToken.None);

        Assert.Equal(McpDispatchResultKind.Success, result.Kind);
        var entry = await db.McpAuditEntries.AsNoTracking().SingleAsync();
        Assert.Equal(McpAuditCategory.ToolCall, entry.Category);
        Assert.Equal("ToolCall", entry.EventType);
        Assert.Equal("Success", entry.ResultStatus);
        Assert.Equal("fm_test_read", entry.ToolName);
        Assert.Equal("katalog-v1", entry.CatalogVersion);
        Assert.Equal(access.OwnerUserId, entry.OwnerUserId);
        Assert.Equal(access.Id, entry.McpAccessId);
        Assert.Equal("acc", entry.McpAccessName);
        Assert.Equal("10.0.0.1", entry.SourceIp);
        Assert.NotNull(entry.CorrelationId);
        Assert.Contains("take", entry.PayloadJson); // call parameters recorded …
        Assert.DoesNotContain("\"items\"", entry.PayloadJson); // … but never the result payload
        Assert.Equal(1, entry.SequenceNo);
    }

    /// <summary>Scope denials write a denied ToolCall record and never throw on audit errors.</summary>
    [Fact]
    public async Task Dispatch_ScopeDenied_StillReturnsDefinedError_WhenAuditFails()
    {
        var access = NewAccess(Guid.NewGuid());
        var audit = new Mock<IMcpAuditWriter>();
        audit.Setup(a => a.WriteAsync(It.IsAny<McpAuditWriteRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("audit store down"));

        var descriptor = new Mock<IMcpToolDescriptor>();
        descriptor.SetupGet(x => x.Name).Returns("fm_test_read");
        descriptor.SetupGet(x => x.SecurityClass).Returns(McpToolSecurityClass.Lesend);
        descriptor.SetupGet(x => x.RequiredScopes)
            .Returns(new[] { new McpToolScope(McpDataType.Postings, McpRightKind.Read) });
        descriptor.SetupGet(x => x.MetaScope).Returns((string?)null);
        descriptor.SetupGet(x => x.InputSchema).Returns(JsonDocument.Parse("{}"));

        var registry = new Mock<IMcpToolRegistry>();
        registry.SetupGet(r => r.CatalogVersion).Returns("katalog-v1");
        registry.Setup(r => r.Find("fm_test_read")).Returns(descriptor.Object);
        var evaluator = new Mock<IMcpScopeEvaluator>();
        evaluator.Setup(e => e.IsAllowedAsync(access, McpDataType.Postings, McpRightKind.Read, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var gate = new Mock<IMcpApprovalGate>();
        gate.Setup(g => g.EvaluateAsync(It.IsAny<McpAccess>(), It.IsAny<IMcpToolDescriptor>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(McpApprovalDecision.Direct);
        var opts = new TestOptionsMonitor(new McpOptions());
        var dispatcher = new McpToolDispatcher(
            registry.Object, evaluator.Object, audit.Object,
            gate.Object, Mock.Of<IMcpApprovalService>(),
            new McpToolRateLimiter(opts),
            new McpAnomalyTracker(opts, Mock.Of<IServiceProvider>(), NullLogger<McpAnomalyTracker>.Instance),
            opts, NullLogger<McpToolDispatcher>.Instance);

        var result = await dispatcher.DispatchAsync(
            NewAccessContext(access), "fm_test_read", JsonDocument.Parse("{}").RootElement,
            Mock.Of<IServiceProvider>(), "10.0.0.1", CancellationToken.None);

        Assert.Equal(McpDispatchResultKind.DefinedError, result.Kind);
        Assert.Equal("Err_Mcp_ScopeDenied", result.ErrorCode);
    }

    /// <summary>McpAccessService commits the access change and its AccessEvent record atomically.</summary>
    [Fact]
    public async Task AccessService_Create_CommitsAccessAndAuditRecord_Atomically()
    {
        var db = NewDb();
        var options = new TestOptionsMonitor(new McpOptions { TokenHash = new McpTokenHashOptions { Iterations = 1000 } });
        var issuer = new McpTokenIssuer(new McpTokenHasher(options));
        var writer = new McpAuditWriter(db, NullLogger<McpAuditWriter>.Instance);
        var svc = new McpAccessService(db, issuer, new McpScopeEvaluator(db), writer,
            Mock.Of<IMcpApprovalService>(),
            NullLogger<McpAccessService>.Instance);
        var owner = Guid.NewGuid();

        var created = await svc.CreateAsync(owner,
            new McpAccessCreateRequest("client-a", null, (short)McpAccessLevel.Analyse,
                (short)McpApprovalMode.PerAction, null, null, null),
            CancellationToken.None);

        Assert.NotNull(created);
        var entry = await db.McpAuditEntries.AsNoTracking().SingleAsync();
        Assert.Equal(McpAuditCategory.AccessEvent, entry.Category);
        Assert.Equal("AccessCreated", entry.EventType);
        Assert.Equal(owner, entry.OwnerUserId);
        Assert.Equal(created.Access.Id, entry.McpAccessId);
        Assert.Equal("Ui", entry.Context);
    }

    /// <summary>McpAccessService fails the write effect when the audit record cannot be staged.</summary>
    [Fact]
    public async Task AccessService_Create_Fails_WhenAuditCannotBeWritten()
    {
        var db = NewDb();
        var options = new TestOptionsMonitor(new McpOptions { TokenHash = new McpTokenHashOptions { Iterations = 1000 } });
        var issuer = new McpTokenIssuer(new McpTokenHasher(options));
        var audit = new Mock<IMcpAuditWriter>();
        audit.Setup(a => a.StageAsync(It.IsAny<McpAuditWriteRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("audit store down"));
        var svc = new McpAccessService(db, issuer, new McpScopeEvaluator(db), audit.Object,
            Mock.Of<IMcpApprovalService>(),
            NullLogger<McpAccessService>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CreateAsync(Guid.NewGuid(),
            new McpAccessCreateRequest("client-a", null, (short)McpAccessLevel.Analyse,
                (short)McpApprovalMode.PerAction, null, null, null),
            CancellationToken.None));

        // The write effect must not survive without its audit record.
        Assert.Equal(0, await db.McpAccesses.CountAsync());
        Assert.Equal(0, await db.McpAuditEntries.CountAsync());
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
