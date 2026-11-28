namespace FinanceManager.Domain.Mcp;

/// <summary>
/// Pre-approval discriminator of an <see cref="AiActionRequest"/> — which documented
/// pre-approval source placed the request directly into the queue as
/// <see cref="AiActionRequestStatus.Approved"/> instead of filing it pending
/// (approval-konzept.md §3.5, granular mode).
/// </summary>
/// <remarks>
/// Persisted via <c>HasConversion&lt;short&gt;()</c>. The value distinguishes
/// access-level pre-approval (<see cref="McpApprovalMode.PreApproved"/>) from
/// grant-level pre-approval (<see cref="McpApprovalMode.Granular"/> +
/// <see cref="McpAccessApprovalGrant"/>) so the grant-removal withdrawal only touches
/// grant-preapproved requests — it must never depend on matching
/// <see cref="AiActionRequest.DecisionNote"/> strings.
/// </remarks>
public enum PreApprovalKind
{
    /// <summary>No pre-approval — a normally decided request (pending or owner-approved).</summary>
    None = 0,

    /// <summary>
    /// Pre-approved by the access configuration (<see cref="McpApprovalMode.PreApproved"/>).
    /// </summary>
    AccessPreApproved = 1,

    /// <summary>
    /// Pre-approved by a matching <see cref="McpAccessApprovalGrant"/> under
    /// <see cref="McpApprovalMode.Granular"/>. Removing the grant system-withdraws
    /// still-open requests of this kind whose covered tuple vanished.
    /// </summary>
    GrantPreApproved = 2
}
