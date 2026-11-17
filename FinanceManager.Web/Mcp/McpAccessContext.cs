using FinanceManager.Application.Mcp;
using FinanceManager.Domain.Mcp;

namespace FinanceManager.Web.Mcp;

/// <summary>
/// HTTP-context-based <see cref="IMcpAccessContext"/>: exposes the access authenticated by
/// <see cref="McpAuthHandler"/> to tool adapters. Reads the stored <see cref="McpAccess"/>
/// from <see cref="HttpContext.Items"/> (set during authentication) and falls back to the
/// claims of the MCP principal.
/// </summary>
public sealed class McpAccessContext : IMcpAccessContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>
    /// Creates a new instance of <see cref="McpAccessContext"/>.
    /// </summary>
    /// <param name="httpContextAccessor">HTTP context accessor.</param>
    public McpAccessContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private HttpContext? Context => _httpContextAccessor.HttpContext;

    /// <inheritdoc />
    public bool IsMcpAuthenticated => Access is not null || McpAccessId != Guid.Empty;

    /// <inheritdoc />
    public Guid McpAccessId
    {
        get
        {
            var value = Context?.User.FindFirst(McpAuthenticationDefaults.McpAccessIdClaim)?.Value;
            return Guid.TryParse(value, out var id) ? id : Guid.Empty;
        }
    }

    /// <inheritdoc />
    public Guid OwnerUserId
    {
        get
        {
            var value = Context?.User.FindFirst(McpAuthenticationDefaults.OwnerUserIdClaim)?.Value;
            return Guid.TryParse(value, out var id) ? id : Guid.Empty;
        }
    }

    /// <inheritdoc />
    public McpAccess? Access
        => Context?.Items.TryGetValue(McpAuthenticationDefaults.McpAccessItemKey, out var value) == true
            ? value as McpAccess
            : null;
}
