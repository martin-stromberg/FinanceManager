namespace FinanceManager.Shared.Dtos.Mcp;

/// <summary>
/// Administrator-editable MCP settings (<c>GET/PUT /api/mcp-audit/settings</c>): the global
/// kill switch and the separate write-stop lid, both database-stored and effective
/// immediately — unlike the remaining <c>Mcp:*</c> options, which stay appsettings-bound.
/// </summary>
/// <param name="Enabled">Global kill switch of the <c>/mcp</c> endpoint.</param>
/// <param name="WriteAccessEnabled">Write-stop lid of the write-capable tool classes.</param>
public sealed record McpSettingsDto(bool Enabled, bool WriteAccessEnabled);

/// <summary>
/// Update request of the administrator-editable MCP settings.
/// </summary>
/// <param name="Enabled">Global kill switch of the <c>/mcp</c> endpoint.</param>
/// <param name="WriteAccessEnabled">Write-stop lid of the write-capable tool classes.</param>
public sealed record McpSettingsUpdateRequest(bool Enabled, bool WriteAccessEnabled);

/// <summary>
/// Lightweight feature status of the MCP integration (<c>GET /api/mcp-accesses/status</c>):
/// lets any authenticated client hide or annotate the MCP UI surface without loading
/// administrative data.
/// </summary>
/// <param name="Enabled">Whether the MCP endpoint is currently enabled.</param>
public sealed record McpStatusDto(bool Enabled);
