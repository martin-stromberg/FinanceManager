using FinanceManager.Application.Mcp;
using FinanceManager.Domain.Mcp;

namespace FinanceManager.Infrastructure.Mcp;

/// <summary>
/// Synthetic <see cref="IMcpAccessContext"/> for queue-executor runs: wraps the
/// <see cref="McpAccess"/> entity re-loaded at execution time so tool adapters resolve the
/// owner exactly like on the dispatcher path (no HTTP context involved).
/// </summary>
internal sealed class McpExecutorAccessContext : IMcpAccessContext
{
    private readonly McpAccess _access;

    /// <summary>Creates the context.</summary>
    /// <param name="access">The re-validated access entity of the request.</param>
    public McpExecutorAccessContext(McpAccess access)
    {
        _access = access;
    }

    /// <inheritdoc />
    public bool IsMcpAuthenticated => true;

    /// <inheritdoc />
    public Guid McpAccessId => _access.Id;

    /// <inheritdoc />
    public Guid OwnerUserId => _access.OwnerUserId;

    /// <inheritdoc />
    public McpAccess? Access => _access;
}
