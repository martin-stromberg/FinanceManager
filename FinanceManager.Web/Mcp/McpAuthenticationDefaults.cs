namespace FinanceManager.Web.Mcp;

/// <summary>
/// Constants of the MCP authentication path (PAT scheme, claims, items).
/// </summary>
public static class McpAuthenticationDefaults
{
    /// <summary>Authentication scheme name of the MCP PAT handler.</summary>
    public const string Scheme = "McpPat";

    /// <summary>Authorization policy name that binds the <see cref="Scheme"/>.</summary>
    public const string Policy = "McpPatPolicy";

    /// <summary>Claim type carrying the authenticated <c>McpAccess</c> id.</summary>
    public const string McpAccessIdClaim = "mcp_access_id";

    /// <summary>Claim type carrying the owner user id of the access.</summary>
    public const string OwnerUserIdClaim = "mcp_owner_user_id";

    /// <summary>
    /// <see cref="HttpContext.Items"/> key under which the authenticated
    /// <see cref="FinanceManager.Domain.Mcp.McpAccess"/> record is stored by the auth handler.
    /// </summary>
    public const string McpAccessItemKey = "FinanceManager.Mcp.Access";
}
