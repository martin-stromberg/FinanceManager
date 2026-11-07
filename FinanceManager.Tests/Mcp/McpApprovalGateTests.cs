using FinanceManager.Application.Mcp;
using FinanceManager.Domain.Mcp;
using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Mcp;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using System.Text.Json;
using Xunit;

namespace FinanceManager.Tests.Mcp;

/// <summary>
/// Unit tests of <see cref="McpApprovalGate"/> (pipeline position 8, F-0902): the class ×
/// approval-mode decision matrix (lesend/vorschlagend direct, schreibvorbereitend
/// mode-dependent, genehmigungspflichtig always a request), the write-stop lid and the
/// per-access pending-request limit (<c>Err_Mcp_PendingLimit</c>).
/// </summary>
public sealed class McpApprovalGateTests
{
    private static (McpApprovalGate gate, AppDbContext db) Create(bool writeEnabled = true, int maxPending = 25)
    {
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        var db = services.BuildServiceProvider().GetRequiredService<AppDbContext>();
        var options = new TestOptionsMonitor(new McpOptions
        {
            Approval = new McpApprovalOptions { MaxPendingPerAccess = maxPending }
        });
        return (new McpApprovalGate(db, options, new TestMcpSettingsService(writeAccessEnabled: writeEnabled)), db);
    }

    private static McpAccess Access(McpApprovalMode mode)
        => new(Guid.NewGuid(), "acc", "HINT01", "pbkdf2|1|AA|AA", McpAccessLevel.Buchhalter, mode);

    private static IMcpToolDescriptor Descriptor(McpToolSecurityClass securityClass)
    {
        var d = new Mock<IMcpToolDescriptor>();
        d.SetupGet(x => x.SecurityClass).Returns(securityClass);
        d.SetupGet(x => x.Name).Returns("fm_test");
        d.SetupGet(x => x.RequiredScopes).Returns(Array.Empty<McpToolScope>());
        d.SetupGet(x => x.MetaScope).Returns((string?)null);
        d.SetupGet(x => x.InputSchema).Returns(JsonDocument.Parse("{}"));
        return d.Object;
    }

    /// <summary>InlineData.</summary>
    [Theory]
    [InlineData(McpToolSecurityClass.Lesend, McpApprovalMode.PerAction)]
    [InlineData(McpToolSecurityClass.Lesend, McpApprovalMode.PreApproved)]
    [InlineData(McpToolSecurityClass.Vorschlagend, McpApprovalMode.PerAction)]
    [InlineData(McpToolSecurityClass.Vorschlagend, McpApprovalMode.PreApproved)]
    [InlineData(McpToolSecurityClass.Schreibvorbereitend, McpApprovalMode.PreApproved)]
    public async Task Evaluate_ShouldDirectlyExecute(McpToolSecurityClass cls, McpApprovalMode mode)
    {
        var (gate, _) = Create();
        var decision = await gate.EvaluateAsync(Access(mode), Descriptor(cls), CancellationToken.None);
        Assert.Equal(McpApprovalDecisionKind.DirectExecution, decision.Kind);
    }

    /// <summary>InlineData.</summary>
    [Theory]
    [InlineData(McpToolSecurityClass.Schreibvorbereitend, McpApprovalMode.PerAction)]
    [InlineData(McpToolSecurityClass.Genehmigungspflichtig, McpApprovalMode.PerAction)]
    [InlineData(McpToolSecurityClass.Genehmigungspflichtig, McpApprovalMode.PreApproved)]
    public async Task Evaluate_ShouldRequireApproval(McpToolSecurityClass cls, McpApprovalMode mode)
    {
        var (gate, _) = Create();
        var decision = await gate.EvaluateAsync(Access(mode), Descriptor(cls), CancellationToken.None);
        Assert.Equal(McpApprovalDecisionKind.RequiresApproval, decision.Kind);
    }

    /// <summary>InlineData.</summary>
    [Theory]
    [InlineData(McpToolSecurityClass.Schreibvorbereitend)]
    [InlineData(McpToolSecurityClass.Genehmigungspflichtig)]
    public async Task Evaluate_WriteStopLid_ShouldDenyWriteClasses(McpToolSecurityClass cls)
    {
        var (gate, _) = Create(writeEnabled: false);
        var decision = await gate.EvaluateAsync(Access(McpApprovalMode.PerAction), Descriptor(cls), CancellationToken.None);
        Assert.Equal(McpApprovalDecisionKind.Denied, decision.Kind);
        Assert.Equal("Err_Mcp_WriteAccessDisabled", decision.ErrorCode);
    }

    /// <summary>Evaluate WriteStopLid ShouldKeepReadClassesDirect.</summary>
    [Fact]
    public async Task Evaluate_WriteStopLid_ShouldKeepReadClassesDirect()
    {
        var (gate, _) = Create(writeEnabled: false);
        var decision = await gate.EvaluateAsync(Access(McpApprovalMode.PerAction),
            Descriptor(McpToolSecurityClass.Lesend), CancellationToken.None);
        Assert.Equal(McpApprovalDecisionKind.DirectExecution, decision.Kind);
    }

    /// <summary>Evaluate PendingLimitReached ShouldDenyErrMcpPendingLimit.</summary>
    [Fact]
    public async Task Evaluate_PendingLimitReached_ShouldDenyErrMcpPendingLimit()
    {
        var (gate, db) = Create(maxPending: 1);
        var access = Access(McpApprovalMode.PerAction);
        db.AiActionRequests.Add(new AiActionRequest(
            access.OwnerUserId, access.Id, access.Name, "fm_x", "op", "katalog-v1",
            "{}", null, DateTime.UtcNow.AddHours(1), Guid.NewGuid(), "k1"));
        await db.SaveChangesAsync(CancellationToken.None);

        var decision = await gate.EvaluateAsync(access,
            Descriptor(McpToolSecurityClass.Schreibvorbereitend), CancellationToken.None);

        Assert.Equal(McpApprovalDecisionKind.Denied, decision.Kind);
        Assert.Equal("Err_Mcp_PendingLimit", decision.ErrorCode);
    }

    /// <summary>Evaluate PendingOfOtherAccess ShouldNotCountTowardsLimit.</summary>
    [Fact]
    public async Task Evaluate_PendingOfOtherAccess_ShouldNotCountTowardsLimit()
    {
        var (gate, db) = Create(maxPending: 1);
        var own = Access(McpApprovalMode.PerAction);
        var other = Access(McpApprovalMode.PerAction);
        db.AiActionRequests.Add(new AiActionRequest(
            other.OwnerUserId, other.Id, other.Name, "fm_x", "op", "katalog-v1",
            "{}", null, DateTime.UtcNow.AddHours(1), Guid.NewGuid(), "k1"));
        await db.SaveChangesAsync(CancellationToken.None);

        var decision = await gate.EvaluateAsync(own,
            Descriptor(McpToolSecurityClass.Schreibvorbereitend), CancellationToken.None);

        Assert.Equal(McpApprovalDecisionKind.RequiresApproval, decision.Kind);
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
