namespace FinanceManager.Application.Mcp;

/// <summary>
/// Execution mode of a tool handler invocation (<see cref="McpToolCallContext.Mode"/>).
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Interactive"/> is the regular dispatcher path of a client call. A handler may
/// answer an approval-gated operation with the structured <c>Err_Mcp_ApprovalRequired</c>
/// result; the approval gate (pipeline position 8) routes such calls through an
/// <see cref="FinanceManager.Domain.Mcp.AiActionRequest"/>.
/// </para>
/// <para>
/// <see cref="ApprovedExecution"/> is the queue-executor path: the owner already approved
/// the frozen parameter snapshot, so approval-gated sub-operations must perform their real
/// service effect instead of answering <c>requiresApproval</c>. The executor still re-runs
/// all guards (access state, scope, write-stop) before invoking — approval never replaces
/// the security checks (sicherheitskonzept.md §7).
/// </para>
/// </remarks>
public enum McpToolExecutionMode
{
    /// <summary>Regular client call via the dispatcher.</summary>
    Interactive = 0,

    /// <summary>Execution of an approved <c>AiActionRequest</c> by the queue executor.</summary>
    ApprovedExecution = 1
}
