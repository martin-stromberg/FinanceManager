namespace FinanceManager.Domain.Mcp;

/// <summary>
/// Category of an MCP audit entry (audit-konzept.md; datenmodell-erweiterungen.md §6.1).
/// </summary>
/// <remarks>
/// Persisted via <c>HasConversion&lt;short&gt;()</c>. Value list is normative
/// (datenmodell-erweiterungen.md §2.2) and must not be changed without updating the concept.
/// </remarks>
public enum McpAuditCategory
{
    /// <summary>A tool was invoked via the MCP endpoint.</summary>
    ToolCall = 0,

    /// <summary>A data change was caused by an MCP action.</summary>
    DataChange = 1,

    /// <summary>An access lifecycle event (creation, rotation, revocation, configuration change).</summary>
    AccessEvent = 2,

    /// <summary>An approval request event (created, decided, executed).</summary>
    ApprovalEvent = 3,

    /// <summary>A security-relevant event (authentication failure, rate limiting, IP block).</summary>
    SecurityEvent = 4
}
