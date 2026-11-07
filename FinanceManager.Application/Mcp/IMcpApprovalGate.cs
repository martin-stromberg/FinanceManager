using FinanceManager.Domain.Mcp;

namespace FinanceManager.Application.Mcp;

/// <summary>
/// Decision of the approval gate for one tool call (pipeline position 8,
/// architekturkonzept.md §5.1): whether the call executes directly, must pass through an
/// <see cref="AiActionRequest"/>, or is denied.
/// </summary>
public enum McpApprovalDecisionKind
{
    /// <summary>The call executes synchronously in the request (read/suggest classes; write-preparing under a pre-approved mode).</summary>
    DirectExecution = 0,

    /// <summary>The call is parked as an <see cref="AiActionRequest"/>; the client gets the request id and polls the result.</summary>
    RequiresApproval = 1,

    /// <summary>The call is denied (e.g. write-stop lid or pending-request limit).</summary>
    Denied = 2
}

/// <summary>
/// Result of <see cref="IMcpApprovalGate.EvaluateAsync"/>.
/// </summary>
/// <param name="Kind">The decision kind.</param>
/// <param name="ErrorCode">Stable <c>Err_Mcp_*</c> code when <see cref="Kind"/> is Denied.</param>
/// <param name="ErrorMessage">Client-visible message when <see cref="Kind"/> is Denied.</param>
public sealed record McpApprovalDecision(
    McpApprovalDecisionKind Kind,
    string? ErrorCode = null,
    string? ErrorMessage = null)
{
    /// <summary>The direct-execution decision.</summary>
    public static readonly McpApprovalDecision Direct = new(McpApprovalDecisionKind.DirectExecution);

    /// <summary>The requires-approval decision.</summary>
    public static readonly McpApprovalDecision Approval = new(McpApprovalDecisionKind.RequiresApproval);

    /// <summary>Creates a denial decision with a stable error code.</summary>
    /// <param name="code">Stable <c>Err_Mcp_*</c> code.</param>
    /// <param name="message">Client-visible message.</param>
    /// <returns>The denial decision.</returns>
    public static McpApprovalDecision Deny(string code, string message)
        => new(McpApprovalDecisionKind.Denied, code, message);
}

/// <summary>
/// The approval gate of the MCP pipeline (position 8; approval-konzept.md §4,
/// architekturkonzept.md §5.1): applies the security-class logic against the approval mode
/// of the calling access — read/suggest tools run directly, write-preparing tools follow the
/// access approval mode, genehmigungspflichtige tools always require a request — plus the
/// global write-stop lid and the per-access pending-request limit (rate-limit dimension
/// against approval spam, sicherheitskonzept.md §7.2).
/// </summary>
/// <remarks>
/// The gate only <i>decides</i>; it never mutates state. Request creation is the job of
/// <see cref="IMcpApprovalService.EnqueueAsync"/>, which re-applies the same limits at
/// write time (defense in depth).
/// </remarks>
public interface IMcpApprovalGate
{
    /// <summary>
    /// Evaluates the approval decision for one tool call.
    /// </summary>
    /// <param name="access">The authenticated access entity.</param>
    /// <param name="descriptor">The descriptor of the requested tool.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The gate decision.</returns>
    Task<McpApprovalDecision> EvaluateAsync(McpAccess access, IMcpToolDescriptor descriptor, CancellationToken ct);
}
