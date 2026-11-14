namespace FinanceManager.Domain.Mcp;

/// <summary>
/// Approval mode (Genehmigungsmodus) of an MCP access for approval-requiring actions.
/// </summary>
/// <remarks>
/// Persisted via <c>HasConversion&lt;short&gt;()</c>. Value list is normative
/// (datenmodell-erweiterungen.md §2.2) and must not be changed without updating the concept.
/// </remarks>
public enum McpApprovalMode
{
    /// <summary>
    /// Each approval-requiring action or batch requires an individual approval request.
    /// </summary>
    PerAction = 0,

    /// <summary>
    /// Approval-requiring actions are durably pre-approved by the access configuration.
    /// </summary>
    PreApproved = 1
}
