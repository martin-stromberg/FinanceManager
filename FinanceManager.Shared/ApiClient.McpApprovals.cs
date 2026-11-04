using FinanceManager.Shared.Dtos.Mcp;
using System.Net.Http.Json;

namespace FinanceManager.Shared;

public partial class ApiClient
{
    #region MCP Approvals (AiActionRequest)

    /// <summary>
    /// Lists the approval requests of the current user (inbox and history), newest first.
    /// </summary>
    /// <param name="status">Optional status filter (AiActionRequestStatus as short).</param>
    /// <param name="accessId">Optional requesting-access filter.</param>
    /// <param name="skip">Items to skip.</param>
    /// <param name="take">Items to take.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The matching request list items.</returns>
    public async Task<IReadOnlyList<AiActionRequestListItemDto>> Mcp_ListActionRequestsAsync(
        short? status = null, Guid? accessId = null, int skip = 0, int take = 100, CancellationToken ct = default)
    {
        var url = $"/api/mcp-approvals?skip={skip}&take={take}";
        if (status.HasValue)
        {
            url += $"&status={status.Value}";
        }
        if (accessId.HasValue)
        {
            url += $"&accessId={accessId.Value}";
        }
        var resp = await _http.GetAsync(url, ct);
        await EnsureSuccessOrSetErrorAsync(resp);
        return await resp.Content.ReadFromJsonAsync<IReadOnlyList<AiActionRequestListItemDto>>(cancellationToken: ct)
            ?? Array.Empty<AiActionRequestListItemDto>();
    }

    /// <summary>
    /// Gets one approval request detail or null when not found.
    /// </summary>
    /// <param name="id">Request id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The detail DTO or null.</returns>
    public async Task<AiActionRequestDetailDto?> Mcp_GetActionRequestAsync(Guid id, CancellationToken ct = default)
    {
        var resp = await _http.GetAsync($"/api/mcp-approvals/{id}", ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        await EnsureSuccessOrSetErrorAsync(resp);
        return await resp.Content.ReadFromJsonAsync<AiActionRequestDetailDto>(cancellationToken: ct);
    }

    /// <summary>
    /// Approves a pending request. Returns false when not found/not owned.
    /// </summary>
    /// <param name="id">Request id.</param>
    /// <param name="decisionComment">Optional decision comment.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True when approved.</returns>
    public async Task<bool> Mcp_ApproveRequestAsync(Guid id, string? decisionComment = null, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync($"/api/mcp-approvals/{id}/approve",
            new AiActionRequestDecisionRequest(decisionComment), ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return false;
        await EnsureSuccessOrSetErrorAsync(resp);
        return true;
    }

    /// <summary>
    /// Rejects a pending request. Returns false when not found/not owned.
    /// </summary>
    /// <param name="id">Request id.</param>
    /// <param name="decisionComment">Optional rejection reason (played back to the client).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True when rejected.</returns>
    public async Task<bool> Mcp_RejectRequestAsync(Guid id, string? decisionComment = null, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync($"/api/mcp-approvals/{id}/reject",
            new AiActionRequestDecisionRequest(decisionComment), ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return false;
        await EnsureSuccessOrSetErrorAsync(resp);
        return true;
    }

    /// <summary>
    /// Withdraws a still-pending request of the owner. Returns false when not found/not owned.
    /// </summary>
    /// <param name="id">Request id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True when withdrawn.</returns>
    public async Task<bool> Mcp_CancelRequestAsync(Guid id, CancellationToken ct = default)
    {
        var resp = await _http.PostAsync($"/api/mcp-approvals/{id}/cancel", content: null, ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return false;
        await EnsureSuccessOrSetErrorAsync(resp);
        return true;
    }

    #endregion MCP Approvals (AiActionRequest)
}
