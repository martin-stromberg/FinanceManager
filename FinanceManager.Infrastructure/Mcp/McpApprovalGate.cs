using FinanceManager.Application.Mcp;
using FinanceManager.Domain.Mcp;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FinanceManager.Infrastructure.Mcp;

/// <summary>
/// Infrastructure implementation of <see cref="IMcpApprovalGate"/> (pipeline position 8,
/// approval-konzept.md §4): the class logic — <c>Lesend</c>/<c>Vorschlagend</c> run
/// directly; <c>Schreibvorbereitend</c> follows the access approval mode
/// (<see cref="McpApprovalMode.PerAction"/> → request, <see cref="McpApprovalMode.PreApproved"/>
/// → direct); <c>Genehmigungspflichtig</c> always requires a request (the request itself is
/// then created pending or already-approved depending on the mode). On top of the class
/// decision the gate applies the global write-stop lid (database-stored
/// <see cref="McpSettings.WriteAccessEnabled"/>) and
/// the per-access pending-request limit (<c>Mcp:Approval:MaxPendingPerAccess</c>,
/// sicherheitskonzept.md §7.2 — protection against approval spam).
/// </summary>
/// <remarks>
/// The gate is decision-only and mutates nothing; <see cref="IMcpApprovalService.EnqueueAsync"/>
/// re-applies the pending limit at write time.
/// </remarks>
public sealed class McpApprovalGate : IMcpApprovalGate
{
    private readonly AppDbContext _db;
    private readonly IOptionsMonitor<McpOptions> _options;
    private readonly IMcpSettingsService _settings;

    /// <summary>Creates the gate.</summary>
    /// <param name="db">Shared scoped database context.</param>
    /// <param name="options">MCP options (approval limits).</param>
    /// <param name="settings">Database-stored MCP switches (write-stop lid).</param>
    public McpApprovalGate(AppDbContext db, IOptionsMonitor<McpOptions> options, IMcpSettingsService settings)
    {
        _db = db;
        _options = options;
        _settings = settings;
    }

    /// <inheritdoc />
    public async Task<McpApprovalDecision> EvaluateAsync(McpAccess access, IMcpToolDescriptor descriptor,
        string? actionKey, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(access);
        ArgumentNullException.ThrowIfNull(descriptor);

        var securityClass = descriptor.SecurityClass;

        // Read/suggest classes never need approval — they have no write effect.
        if (securityClass is McpToolSecurityClass.Lesend or McpToolSecurityClass.Vorschlagend)
        {
            return McpApprovalDecision.Direct;
        }

        // The write-stop lid caps every write class (stufe-3-Deckel). The dispatcher checks
        // it earlier already; the gate repeats it so the decision is correct standalone.
        if (!await _settings.IsWriteAccessEnabledAsync(ct))
        {
            return McpApprovalDecision.Deny("Err_Mcp_WriteAccessDisabled",
                "Write access via MCP is currently disabled.");
        }

        var requiresApproval = securityClass switch
        {
            // Final/irreversible effects always pass through a request — under every mode.
            // A matching granular grant does not skip the request: the request record IS
            // the documented approval instance, it merely starts approved
            // (PreApprovalKind.GrantPreApproved) at enqueue time.
            McpToolSecurityClass.Genehmigungspflichtig => true,
            // Write-preparing calls follow the access approval mode.
            McpToolSecurityClass.Schreibvorbereitend => access.ApprovalMode switch
            {
                McpApprovalMode.PerAction => true,
                McpApprovalMode.PreApproved => false,
                // Granular: a routine (non-approval-required) operation covered by a
                // matching grant runs directly; everything else queues — unmapped
                // operations fail closed.
                McpApprovalMode.Granular => !await IsGrantCoveredRoutineAsync(access, descriptor.Name, actionKey, ct),
                _ => true
            },
            _ => true
        };

        if (!requiresApproval)
        {
            return McpApprovalDecision.Direct;
        }

        var pending = await _db.AiActionRequests.AsNoTracking()
            .CountAsync(r => r.McpAccessId == access.Id && r.Status == AiActionRequestStatus.Pending, ct);
        if (pending >= _options.CurrentValue.Approval.MaxPendingPerAccess)
        {
            return McpApprovalDecision.Deny("Err_Mcp_PendingLimit",
                "The limit of open approval requests for this access is reached.");
        }

        return McpApprovalDecision.Approval;
    }

    /// <summary>
    /// Granular-mode check: the (tool, action) profile exists, marks the operation
    /// routine (not approval-required), and a persisted grant covers its
    /// (DataType, Right) tuple. Grant rows are queried fresh — the access entity handed
    /// in does not necessarily carry the loaded navigation.
    /// </summary>
    /// <param name="access">The calling access entity.</param>
    /// <param name="toolName">The requested tool name.</param>
    /// <param name="actionKey">The operation key of the call.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see langword="true"/> when a matching grant covers a routine operation.</returns>
    private async Task<bool> IsGrantCoveredRoutineAsync(McpAccess access, string toolName,
        string? actionKey, CancellationToken ct)
    {
        var profile = McpApprovalActionCatalog.Find(toolName, actionKey);
        if (profile is null || profile.RequiresApproval)
        {
            return false;
        }
        return await _db.McpAccessApprovalGrants.AsNoTracking().AnyAsync(
            g => g.McpAccessId == access.Id
                && g.DataType == profile.DataType
                && g.Right == profile.Right, ct);
    }
}
