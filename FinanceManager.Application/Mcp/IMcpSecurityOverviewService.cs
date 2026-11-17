using FinanceManager.Shared.Dtos.Mcp;

namespace FinanceManager.Application.Mcp;

/// <summary>
/// Admin security overview of the MCP integration (F-1005; ui-konzept.md §5.2,
/// audit-konzept.md §6.2): instance-wide security metadata for the administrator —
/// access metadata of every owner, pending-request counters, the global switch state and
/// recent security events.
/// </summary>
/// <remarks>
/// The overview is deliberately metadata-only: it exposes no token material, no scope
/// tuples, no parameter snapshots, no audit payloads and no business content of foreign
/// users. Owner references are opaque user ids. The endpoint is restricted to the
/// <c>Admin</c> role.
/// </remarks>
public interface IMcpSecurityOverviewService
{
    /// <summary>
    /// Builds the instance-wide security overview.
    /// </summary>
    /// <param name="recentTake">Number of newest security events to include (server-clamped).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The metadata-only overview DTO.</returns>
    Task<McpSecurityOverviewDto> GetOverviewAsync(int recentTake, CancellationToken ct);
}
