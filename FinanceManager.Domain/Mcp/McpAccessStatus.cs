namespace FinanceManager.Domain.Mcp;

/// <summary>
/// Lifecycle status of an MCP access. Expiration is computed from
/// <see cref="McpAccess.ExpiresUtc"/> and is intentionally not a separate status.
/// </summary>
/// <remarks>
/// Persisted via <c>HasConversion&lt;short&gt;()</c>. Value list is normative
/// (datenmodell-erweiterungen.md §2.2) and must not be changed without updating the concept.
/// </remarks>
public enum McpAccessStatus
{
    /// <summary>Access is active and usable (subject to <see cref="McpAccess.ExpiresUtc"/>).</summary>
    Active = 0,

    /// <summary>Access is temporarily deactivated by the owner; tokens are rejected.</summary>
    Deactivated = 1,

    /// <summary>
    /// Access is revoked (terminal state); tokens are rejected. Reactivation requires a
    /// token rotation which issues a fresh credential.
    /// </summary>
    Revoked = 2
}
