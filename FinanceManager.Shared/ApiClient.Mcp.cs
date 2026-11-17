using FinanceManager.Shared.Dtos.Mcp;
using System.Net.Http.Json;

namespace FinanceManager.Shared;

public partial class ApiClient
{
    #region MCP Accesses

    /// <summary>
    /// Returns the lightweight MCP feature status (whether the endpoint is enabled).
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The status DTO.</returns>
    public async Task<McpStatusDto> Mcp_GetStatusAsync(CancellationToken ct = default)
    {
        var resp = await _http.GetAsync("/api/mcp-accesses/status", ct);
        await EnsureSuccessOrSetErrorAsync(resp);
        return (await resp.Content.ReadFromJsonAsync<McpStatusDto>(cancellationToken: ct))!;
    }

    /// <summary>
    /// Lists the MCP accesses of the current user.
    /// </summary>
    /// <param name="search">Optional name search text.</param>
    /// <param name="skip">Items to skip.</param>
    /// <param name="take">Items to take.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The list of access list items.</returns>
    public async Task<IReadOnlyList<McpAccessListItemDto>> Mcp_ListAccessesAsync(string? search = null, int skip = 0, int take = 100, CancellationToken ct = default)
    {
        var url = $"/api/mcp-accesses?skip={skip}&take={take}" + (string.IsNullOrWhiteSpace(search) ? string.Empty : $"&q={Uri.EscapeDataString(search)}");
        var resp = await _http.GetAsync(url, ct);
        await EnsureSuccessOrSetErrorAsync(resp);
        return await resp.Content.ReadFromJsonAsync<IReadOnlyList<McpAccessListItemDto>>(cancellationToken: ct) ?? Array.Empty<McpAccessListItemDto>();
    }

    /// <summary>
    /// Gets one MCP access detail or null when not found.
    /// </summary>
    /// <param name="id">Access id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The detail DTO or null.</returns>
    public async Task<McpAccessDetailDto?> Mcp_GetAccessAsync(Guid id, CancellationToken ct = default)
    {
        var resp = await _http.GetAsync($"/api/mcp-accesses/{id}", ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        await EnsureSuccessOrSetErrorAsync(resp);
        return await resp.Content.ReadFromJsonAsync<McpAccessDetailDto>(cancellationToken: ct);
    }

    /// <summary>
    /// Creates a new MCP access. The response contains the plaintext token exactly once.
    /// </summary>
    /// <param name="request">Creation request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created access plus plaintext token.</returns>
    public async Task<McpAccessCreatedDto> Mcp_CreateAccessAsync(McpAccessCreateRequest request, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync("/api/mcp-accesses", request, ct);
        await EnsureSuccessOrSetErrorAsync(resp);
        return (await resp.Content.ReadFromJsonAsync<McpAccessCreatedDto>(cancellationToken: ct))!;
    }

    /// <summary>
    /// Updates an MCP access. Returns false when not found.
    /// </summary>
    /// <param name="id">Access id.</param>
    /// <param name="request">Update request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True when updated.</returns>
    public async Task<bool> Mcp_UpdateAccessAsync(Guid id, McpAccessUpdateRequest request, CancellationToken ct = default)
    {
        var resp = await _http.PutAsJsonAsync($"/api/mcp-accesses/{id}", request, ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return false;
        await EnsureSuccessOrSetErrorAsync(resp);
        return true;
    }

    /// <summary>
    /// Replaces the individual scope profile of an access. Returns false when not found.
    /// </summary>
    /// <param name="id">Access id.</param>
    /// <param name="request">Scope set request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True when applied.</returns>
    public async Task<bool> Mcp_SetScopesAsync(Guid id, McpScopeSetRequest request, CancellationToken ct = default)
    {
        var resp = await _http.PutAsJsonAsync($"/api/mcp-accesses/{id}/scopes", request, ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return false;
        await EnsureSuccessOrSetErrorAsync(resp);
        return true;
    }

    /// <summary>
    /// Replaces the filters of one scope row. Returns false when not found.
    /// </summary>
    /// <param name="id">Access id.</param>
    /// <param name="scopeId">Scope row id.</param>
    /// <param name="request">Filter set request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True when applied.</returns>
    public async Task<bool> Mcp_SetFiltersAsync(Guid id, Guid scopeId, McpFilterSetRequest request, CancellationToken ct = default)
    {
        var resp = await _http.PutAsJsonAsync($"/api/mcp-accesses/{id}/filters/{scopeId}", request, ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return false;
        await EnsureSuccessOrSetErrorAsync(resp);
        return true;
    }

    /// <summary>
    /// Activates an access. Returns false when not found.
    /// </summary>
    /// <param name="id">Access id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True when changed.</returns>
    public async Task<bool> Mcp_ActivateAccessAsync(Guid id, CancellationToken ct = default)
    {
        var resp = await _http.PostAsync($"/api/mcp-accesses/{id}/activate", content: null, ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return false;
        await EnsureSuccessOrSetErrorAsync(resp);
        return true;
    }

    /// <summary>
    /// Deactivates an access. Returns false when not found.
    /// </summary>
    /// <param name="id">Access id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True when changed.</returns>
    public async Task<bool> Mcp_DeactivateAccessAsync(Guid id, CancellationToken ct = default)
    {
        var resp = await _http.PostAsync($"/api/mcp-accesses/{id}/deactivate", content: null, ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return false;
        await EnsureSuccessOrSetErrorAsync(resp);
        return true;
    }

    /// <summary>
    /// Rotates the token of an access. Returns the new plaintext token exactly once, or null
    /// when the access was not found.
    /// </summary>
    /// <param name="id">Access id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The rotation result or null.</returns>
    public async Task<McpAccessRotatedDto?> Mcp_RotateTokenAsync(Guid id, CancellationToken ct = default)
    {
        var resp = await _http.PostAsync($"/api/mcp-accesses/{id}/rotate-token", content: null, ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        await EnsureSuccessOrSetErrorAsync(resp);
        return await resp.Content.ReadFromJsonAsync<McpAccessRotatedDto>(cancellationToken: ct);
    }

    /// <summary>
    /// Revokes (deletes) an access. Returns false when not found.
    /// </summary>
    /// <param name="id">Access id.</param>
    /// <param name="reason">Optional revocation reason.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True when revoked.</returns>
    public async Task<bool> Mcp_RevokeAccessAsync(Guid id, string? reason = null, CancellationToken ct = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/mcp-accesses/{id}")
        {
            Content = JsonContent.Create(new McpAccessRevokeRequest(reason))
        };
        var resp = await _http.SendAsync(request, ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return false;
        await EnsureSuccessOrSetErrorAsync(resp);
        return true;
    }

    /// <summary>
    /// Lists the server-side access level definitions with their default scope profiles.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The level definitions.</returns>
    public async Task<IReadOnlyList<McpAccessLevelDto>> Mcp_GetLevelsAsync(CancellationToken ct = default)
    {
        var resp = await _http.GetAsync("/api/mcp-accesses/levels", ct);
        await EnsureSuccessOrSetErrorAsync(resp);
        return await resp.Content.ReadFromJsonAsync<IReadOnlyList<McpAccessLevelDto>>(cancellationToken: ct) ?? Array.Empty<McpAccessLevelDto>();
    }

    /// <summary>
    /// Loads the UI options of the access management (scope matrix, categories, TLS flag).
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The options DTO.</returns>
    public async Task<McpAccessOptionsDto?> Mcp_GetOptionsAsync(CancellationToken ct = default)
    {
        var resp = await _http.GetAsync("/api/mcp-accesses/options", ct);
        await EnsureSuccessOrSetErrorAsync(resp);
        return await resp.Content.ReadFromJsonAsync<McpAccessOptionsDto>(cancellationToken: ct);
    }

    #endregion MCP Accesses
}
