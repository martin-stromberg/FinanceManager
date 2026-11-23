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
/// per-access pending-request limit (<c>Err_Mcp_PendingLimit</c>) — plus the granular
/// mode's action-catalog × grant evaluation.
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

    private static IMcpToolDescriptor Descriptor(McpToolSecurityClass securityClass, string name = "fm_test")
    {
        var d = new Mock<IMcpToolDescriptor>();
        d.SetupGet(x => x.SecurityClass).Returns(securityClass);
        d.SetupGet(x => x.Name).Returns(name);
        d.SetupGet(x => x.RequiredScopes).Returns(Array.Empty<McpToolScope>());
        d.SetupGet(x => x.MetaScope).Returns((string?)null);
        d.SetupGet(x => x.InputSchema).Returns(JsonDocument.Parse("{}"));
        return d.Object;
    }

    /// <summary>InlineData.</summary>
    /// <param name="cls">The security class under test.</param>
    /// <param name="mode">The approval mode under test.</param>
    [Theory]
    [InlineData(McpToolSecurityClass.Lesend, McpApprovalMode.PerAction)]
    [InlineData(McpToolSecurityClass.Lesend, McpApprovalMode.PreApproved)]
    [InlineData(McpToolSecurityClass.Vorschlagend, McpApprovalMode.PerAction)]
    [InlineData(McpToolSecurityClass.Vorschlagend, McpApprovalMode.PreApproved)]
    [InlineData(McpToolSecurityClass.Schreibvorbereitend, McpApprovalMode.PreApproved)]
    public async Task Evaluate_ShouldDirectlyExecute(McpToolSecurityClass cls, McpApprovalMode mode)
    {
        var (gate, _) = Create();
        var decision = await gate.EvaluateAsync(Access(mode), Descriptor(cls), "op", CancellationToken.None);
        Assert.Equal(McpApprovalDecisionKind.DirectExecution, decision.Kind);
    }

    /// <summary>InlineData.</summary>
    /// <param name="cls">The security class under test.</param>
    /// <param name="mode">The approval mode under test.</param>
    [Theory]
    [InlineData(McpToolSecurityClass.Schreibvorbereitend, McpApprovalMode.PerAction)]
    [InlineData(McpToolSecurityClass.Genehmigungspflichtig, McpApprovalMode.PerAction)]
    [InlineData(McpToolSecurityClass.Genehmigungspflichtig, McpApprovalMode.PreApproved)]
    [InlineData(McpToolSecurityClass.Genehmigungspflichtig, McpApprovalMode.Granular)]
    public async Task Evaluate_ShouldRequireApproval(McpToolSecurityClass cls, McpApprovalMode mode)
    {
        var (gate, _) = Create();
        var decision = await gate.EvaluateAsync(Access(mode), Descriptor(cls), "op", CancellationToken.None);
        Assert.Equal(McpApprovalDecisionKind.RequiresApproval, decision.Kind);
    }

    /// <summary>InlineData.</summary>
    /// <param name="cls">The write security class under test.</param>
    [Theory]
    [InlineData(McpToolSecurityClass.Schreibvorbereitend)]
    [InlineData(McpToolSecurityClass.Genehmigungspflichtig)]
    public async Task Evaluate_WriteStopLid_ShouldDenyWriteClasses(McpToolSecurityClass cls)
    {
        var (gate, _) = Create(writeEnabled: false);
        var decision = await gate.EvaluateAsync(Access(McpApprovalMode.PerAction), Descriptor(cls), "op", CancellationToken.None);
        Assert.Equal(McpApprovalDecisionKind.Denied, decision.Kind);
        Assert.Equal("Err_Mcp_WriteAccessDisabled", decision.ErrorCode);
    }

    /// <summary>Evaluate WriteStopLid ShouldKeepReadClassesDirect.</summary>
    [Fact]
    public async Task Evaluate_WriteStopLid_ShouldKeepReadClassesDirect()
    {
        var (gate, _) = Create(writeEnabled: false);
        var decision = await gate.EvaluateAsync(Access(McpApprovalMode.PerAction),
            Descriptor(McpToolSecurityClass.Lesend), "op", CancellationToken.None);
        Assert.Equal(McpApprovalDecisionKind.DirectExecution, decision.Kind);
    }

    /// <summary>The write-stop lid caps write classes under Granular too — a matching
    /// grant does not open the lid.</summary>
    [Fact]
    public async Task Evaluate_WriteStopLid_GranularWithGrant_ShouldDeny()
    {
        var (gate, db) = Create(writeEnabled: false);
        var access = Access(McpApprovalMode.Granular);
        db.McpAccessApprovalGrants.Add(new McpAccessApprovalGrant(
            access.Id, access.OwnerUserId, McpDataType.Administration, McpRightKind.Read));
        await db.SaveChangesAsync(CancellationToken.None);

        var decision = await gate.EvaluateAsync(access,
            Descriptor(McpToolSecurityClass.Schreibvorbereitend, "fm_admin_updates"), "status",
            CancellationToken.None);

        Assert.Equal(McpApprovalDecisionKind.Denied, decision.Kind);
        Assert.Equal("Err_Mcp_WriteAccessDisabled", decision.ErrorCode);
    }

    // ------------------------------------------------------------------
    // Granular mode: action catalog × grant matrix (approval-konzept.md §3.4)
    // ------------------------------------------------------------------

    /// <summary>Granular plus a matching grant releases routine operations directly —
    /// the example case: (Administration, Read) grant → admin status/check run now.</summary>
    /// <param name="toolName">The tool name under test.</param>
    /// <param name="actionKey">The operation key under test.</param>
    [Theory]
    [InlineData("fm_admin_updates", "status")]
    [InlineData("fm_admin_updates", "check")]
    [InlineData("fm_admin_backups", "list")]
    [InlineData("fm_admin_settings", "get")]
    [InlineData("fm_admin_notifications", "list")]
    public async Task Evaluate_GranularRoutineOp_WithMatchingGrant_ShouldExecuteDirectly(
        string toolName, string actionKey)
    {
        var (gate, db) = Create();
        var access = Access(McpApprovalMode.Granular);
        db.McpAccessApprovalGrants.Add(new McpAccessApprovalGrant(
            access.Id, access.OwnerUserId, McpDataType.Administration, McpRightKind.Read));
        await db.SaveChangesAsync(CancellationToken.None);

        var decision = await gate.EvaluateAsync(access,
            Descriptor(McpToolSecurityClass.Schreibvorbereitend, toolName), actionKey,
            CancellationToken.None);

        Assert.Equal(McpApprovalDecisionKind.DirectExecution, decision.Kind);
    }

    /// <summary>Granular without any grant behaves like PerAction — routine operations
    /// queue as pending approval requests.</summary>
    /// <param name="actionKey">The operation key under test.</param>
    [Theory]
    [InlineData("status")]
    [InlineData("install")]
    public async Task Evaluate_GranularOp_WithoutGrant_ShouldRequireApproval(string actionKey)
    {
        var (gate, _) = Create();

        var decision = await gate.EvaluateAsync(Access(McpApprovalMode.Granular),
            Descriptor(McpToolSecurityClass.Schreibvorbereitend, "fm_admin_updates"), actionKey,
            CancellationToken.None);

        Assert.Equal(McpApprovalDecisionKind.RequiresApproval, decision.Kind);
    }

    /// <summary>A grant on a different tuple does not cover the operation —
    /// (Administration, Read) does not release a Modify operation.</summary>
    [Fact]
    public async Task Evaluate_GranularMutation_WithReadGrantOnly_ShouldRequireApproval()
    {
        var (gate, db) = Create();
        var access = Access(McpApprovalMode.Granular);
        db.McpAccessApprovalGrants.Add(new McpAccessApprovalGrant(
            access.Id, access.OwnerUserId, McpDataType.Administration, McpRightKind.Read));
        await db.SaveChangesAsync(CancellationToken.None);

        var decision = await gate.EvaluateAsync(access,
            Descriptor(McpToolSecurityClass.Schreibvorbereitend, "fm_admin_updates"), "install",
            CancellationToken.None);

        Assert.Equal(McpApprovalDecisionKind.RequiresApproval, decision.Kind);
    }

    /// <summary>An approval-required operation still queues even with a covering grant —
    /// the request itself is the documented approval instance (it starts approved at
    /// enqueue via GrantPreApproved).</summary>
    [Fact]
    public async Task Evaluate_GranularApprovalRequiredOp_WithGrant_ShouldStillQueue()
    {
        var (gate, db) = Create();
        var access = Access(McpApprovalMode.Granular);
        db.McpAccessApprovalGrants.Add(new McpAccessApprovalGrant(
            access.Id, access.OwnerUserId, McpDataType.Administration, McpRightKind.Modify));
        await db.SaveChangesAsync(CancellationToken.None);

        var decision = await gate.EvaluateAsync(access,
            Descriptor(McpToolSecurityClass.Schreibvorbereitend, "fm_admin_notifications"), "dismiss",
            CancellationToken.None);

        Assert.Equal(McpApprovalDecisionKind.RequiresApproval, decision.Kind);
    }

    /// <summary>Unmapped (tool, action) pairs fail closed — a grant can never silently
    /// pre-approve an operation the catalog does not know.</summary>
    [Fact]
    public async Task Evaluate_GranularUnmappedAction_WithGrant_ShouldRequireApproval()
    {
        var (gate, db) = Create();
        var access = Access(McpApprovalMode.Granular);
        db.McpAccessApprovalGrants.Add(new McpAccessApprovalGrant(
            access.Id, access.OwnerUserId, McpDataType.Administration, McpRightKind.Read));
        await db.SaveChangesAsync(CancellationToken.None);

        var decision = await gate.EvaluateAsync(access,
            Descriptor(McpToolSecurityClass.Schreibvorbereitend, "fm_admin_updates"), "mystery_op",
            CancellationToken.None);

        Assert.Equal(McpApprovalDecisionKind.RequiresApproval, decision.Kind);
    }

    /// <summary>A genehmigungspflichtige tool stays approval-bound under Granular — the
    /// grant-covered variant starts approved at enqueue, never synchronously.</summary>
    [Fact]
    public async Task Evaluate_GranularGenehmigungspflichtig_AlwaysRequiresApproval()
    {
        var (gate, db) = Create();
        var access = Access(McpApprovalMode.Granular);
        db.McpAccessApprovalGrants.Add(new McpAccessApprovalGrant(
            access.Id, access.OwnerUserId, McpDataType.Contacts, McpRightKind.Modify));
        await db.SaveChangesAsync(CancellationToken.None);

        var decision = await gate.EvaluateAsync(access,
            Descriptor(McpToolSecurityClass.Genehmigungspflichtig, "fm_statement_draft_discard"),
            "fm_statement_draft_discard", CancellationToken.None);

        Assert.Equal(McpApprovalDecisionKind.RequiresApproval, decision.Kind);
    }

    /// <summary>Grants of a different access do not leak across accesses.</summary>
    [Fact]
    public async Task Evaluate_GranularGrantOfOtherAccess_ShouldNotApply()
    {
        var (gate, db) = Create();
        var access = Access(McpApprovalMode.Granular);
        var foreign = Access(McpApprovalMode.Granular);
        db.McpAccessApprovalGrants.Add(new McpAccessApprovalGrant(
            foreign.Id, foreign.OwnerUserId, McpDataType.Administration, McpRightKind.Read));
        await db.SaveChangesAsync(CancellationToken.None);

        var decision = await gate.EvaluateAsync(access,
            Descriptor(McpToolSecurityClass.Schreibvorbereitend, "fm_admin_updates"), "status",
            CancellationToken.None);

        Assert.Equal(McpApprovalDecisionKind.RequiresApproval, decision.Kind);
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
            Descriptor(McpToolSecurityClass.Schreibvorbereitend), "op", CancellationToken.None);

        Assert.Equal(McpApprovalDecisionKind.Denied, decision.Kind);
        Assert.Equal("Err_Mcp_PendingLimit", decision.ErrorCode);
    }

    /// <summary>The pending limit applies under Granular as well — uncovered operations
    /// queue and count toward the cap.</summary>
    [Fact]
    public async Task Evaluate_GranularPendingLimitReached_ShouldDeny()
    {
        var (gate, db) = Create(maxPending: 1);
        var access = Access(McpApprovalMode.Granular);
        db.AiActionRequests.Add(new AiActionRequest(
            access.OwnerUserId, access.Id, access.Name, "fm_x", "op", "katalog-v1",
            "{}", null, DateTime.UtcNow.AddHours(1), Guid.NewGuid(), "k1"));
        await db.SaveChangesAsync(CancellationToken.None);

        var decision = await gate.EvaluateAsync(access,
            Descriptor(McpToolSecurityClass.Schreibvorbereitend, "fm_admin_updates"), "install",
            CancellationToken.None);

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
            Descriptor(McpToolSecurityClass.Schreibvorbereitend), "op", CancellationToken.None);

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
