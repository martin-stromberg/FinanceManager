using FinanceManager.Domain.Mcp;

namespace FinanceManager.Application.Mcp;

/// <summary>
/// Provides the authenticated MCP access context of the current request — the MCP-side
/// counterpart of <see cref="ICurrentUserService"/>. Tool adapters must resolve the
/// <see cref="OwnerUserId"/> exclusively from this context, never from client-supplied
/// parameters (no client-controlled owner id, architekturkonzept.md §5.1).
/// </summary>
public interface IMcpAccessContext
{
    /// <summary>
    /// Whether the current request was authenticated via the MCP PAT scheme (<c>McpPat</c>).
    /// </summary>
    bool IsMcpAuthenticated { get; }

    /// <summary>
    /// Identifier of the authenticated <see cref="McpAccess"/>; <see cref="Guid.Empty"/> when
    /// the request is not MCP-authenticated.
    /// </summary>
    Guid McpAccessId { get; }

    /// <summary>
    /// Owner of the authenticated access; <see cref="Guid.Empty"/> when the request is not
    /// MCP-authenticated. All service calls of tool adapters run with this id.
    /// </summary>
    Guid OwnerUserId { get; }

    /// <summary>
    /// The <see cref="McpAccess"/> record loaded and re-validated during authentication, or
    /// <see langword="null"/> when the request is not MCP-authenticated.
    /// </summary>
    McpAccess? Access { get; }
}
