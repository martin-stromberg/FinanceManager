using FinanceManager.Application.Mcp;
using FinanceManager.Application.Users;
using FinanceManager.Domain.Mcp;

namespace FinanceManager.Web.Mcp;

/// <summary>
/// MCP-specific guards of the administrative tools T34–T38 (fm_admin_*): the admin gate and
/// the approval gate of the final/effective sub-operations. Unlike the fachlichen
/// Pflege-Tools, admin tools are not bound by record-level scope filters — the binding
/// restriction is the owner's administrative role, evaluated against the regular
/// <see cref="IUserAdminService"/> contract at every invocation (interactive calls and
/// approved queue executions alike, so a role lost between approval and execution denies
/// the effect).
/// </summary>
internal static class McpAdminGuards
{
    /// <summary>
    /// Enforces the admin gate of the fm_admin_* tools: the owner of the calling access
    /// must resolve to a user with administrative privileges. Evaluated on every call —
    /// including <see cref="McpToolExecutionMode.ApprovedExecution"/> queue runs — so a
    /// revoked admin role stops both phases of an approval-bound operation.
    /// </summary>
    /// <param name="users">The user administration read contract.</param>
    /// <param name="ownerUserId">Owner user id of the calling access.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="McpToolException">The owner is not an administrator.</exception>
    public static async Task RequireAdminAsync(IUserAdminService users, Guid ownerUserId, CancellationToken ct)
    {
        var user = await users.GetAsync(ownerUserId, ct);
        if (user is null || !user.IsAdmin)
        {
            throw new McpToolException("Err_Mcp_AdminRequired",
                "This tool is restricted to owners with administrative privileges.");
        }
    }

    /// <summary>
    /// Verifies a scope right inside the handler for a non-approval write sub-operation
    /// (e.g. dismissing an own notification) — the pendant of the Löschung check of
    /// <see cref="McpMasterDataGuards.ApprovalRequiredAsync"/> for the fachlich honest
    /// right of the admin tools (Änderung/Erstellung instead of Löschung).
    /// </summary>
    /// <param name="scopeEvaluator">The scope evaluator of the request scope.</param>
    /// <param name="context">The tool call context.</param>
    /// <param name="dataType">Data type whose right is required.</param>
    /// <param name="right">The required right kind.</param>
    /// <param name="operation">The requested sub-operation (diagnostic).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="McpToolException">The right is not granted to the access.</exception>
    public static async Task RequireRightAsync(
        IMcpScopeEvaluator scopeEvaluator, McpToolCallContext context,
        McpDataType dataType, McpRightKind right, string operation, CancellationToken ct)
    {
        var entity = context.Access.Access;
        if (entity is not null
            && !await scopeEvaluator.IsAllowedAsync(entity, dataType, right, ct))
        {
            throw new McpToolException("Err_Mcp_ScopeDenied",
                $"Operation '{operation}' requires the scope ({dataType}, {right}).");
        }
    }

    /// <summary>
    /// Approval gate of the final/effective sub-operations of the admin tools — the
    /// <see cref="McpMasterDataGuards.ApprovalRequiredAsync"/> pattern parameterized by the
    /// fachlich honest right kind: install/schedule/set check (Administration, Änderung),
    /// create (Administration, Erstellung); the Löschung right stays reserved for the
    /// destructive pflege operations. In <see cref="McpToolExecutionMode.Interactive"/>
    /// mode the call answers <c>Err_Mcp_ApprovalRequired</c> (the dispatcher converts it
    /// into an <see cref="AiActionRequest"/>); in
    /// <see cref="McpToolExecutionMode.ApprovedExecution"/> mode the supplied delegate
    /// performs the real effect.
    /// </summary>
    /// <param name="scopeEvaluator">The scope evaluator of the request scope.</param>
    /// <param name="context">The tool call context (carries the execution mode).</param>
    /// <param name="dataType">Data type whose right is required.</param>
    /// <param name="requiredRight">The scope right the operation exercises.</param>
    /// <param name="operation">The requested sub-operation.</param>
    /// <param name="effect">Human-readable description of the effect.</param>
    /// <param name="parameters">Optional parameter snapshot for the queue.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <param name="executeApproved">
    /// The real service effect to run under <see cref="McpToolExecutionMode.ApprovedExecution"/>.
    /// </param>
    /// <returns>The structured approval-required result, or the execution result.</returns>
    /// <exception cref="McpToolException">
    /// The scope right is not granted, or the operation has no wired execution path.
    /// </exception>
    public static async Task<object> ApprovalRequiredAsync(
        IMcpScopeEvaluator scopeEvaluator, McpToolCallContext context,
        McpDataType dataType, McpRightKind requiredRight, string operation, string effect,
        object? parameters, CancellationToken ct,
        Func<CancellationToken, Task<object>>? executeApproved = null)
    {
        var entity = context.Access.Access;
        if (entity is not null
            && !await scopeEvaluator.IsAllowedAsync(entity, dataType, requiredRight, ct))
        {
            throw new McpToolException("Err_Mcp_ScopeDenied",
                $"Operation '{operation}' requires the scope ({dataType}, {requiredRight}).");
        }

        if (context.Mode == McpToolExecutionMode.ApprovedExecution)
        {
            if (executeApproved is null)
            {
                throw new McpToolException("Err_Mcp_ActionNotExecutable",
                    $"Operation '{operation}' has no execution path wired yet.");
            }
            return await executeApproved(ct);
        }

        return new
        {
            requiresApproval = true,
            code = "Err_Mcp_ApprovalRequired",
            operation,
            effect,
            parameters,
            message = "This administrative operation is final/effective and requires owner approval."
        };
    }
}
