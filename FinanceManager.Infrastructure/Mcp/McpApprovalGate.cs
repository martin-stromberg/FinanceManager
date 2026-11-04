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
/// decision the gate applies the global write-stop lid (<c>Mcp:WriteAccess:Enabled</c>) and
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

    /// <summary>Creates the gate.</summary>
    /// <param name="db">Shared scoped database context.</param>
    /// <param name="options">MCP options (write-stop lid, approval limits).</param>
    public McpApprovalGate(AppDbContext db, IOptionsMonitor<McpOptions> options)
    {
        _db = db;
        _options = options;
    }

    /// <inheritdoc />
    public async Task<McpApprovalDecision> EvaluateAsync(McpAccess access, IMcpToolDescriptor descriptor, CancellationToken ct)
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
        if (!_options.CurrentValue.WriteAccess.Enabled)
        {
            return McpApprovalDecision.Deny("Err_Mcp_WriteAccessDisabled",
                "Write access via MCP is currently disabled.");
        }

        var requiresApproval = securityClass switch
        {
            // Final/irreversible effects always pass through a request — also under
            // PreApproved, where the request starts directly approved (approval-konzept.md §3.4).
            McpToolSecurityClass.Genehmigungspflichtig => true,
            // Write-preparing calls follow the access approval mode.
            McpToolSecurityClass.Schreibvorbereitend => access.ApprovalMode == McpApprovalMode.PerAction,
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
}
