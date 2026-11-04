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
    /// <param name="toolName">Optional exact <c>fm_*</c> tool name filter (server-side).</param>
    /// <param name="skip">Items to skip.</param>
    /// <param name="take">Items to take.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The matching request list items.</returns>
    public async Task<IReadOnlyList<AiActionRequestListItemDto>> Mcp_ListActionRequestsAsync(
        short? status = null, Guid? accessId = null, string? toolName = null, int skip = 0, int take = 100, CancellationToken ct = default)
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
        if (!string.IsNullOrWhiteSpace(toolName))
        {
            url += $"&toolName={Uri.EscapeDataString(toolName.Trim())}";
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

    /// <summary>
    /// Returns the number of currently pending approval requests of the current user
    /// (approval-center badge counter, F-1001).
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The pending request count.</returns>
    public async Task<int> Mcp_GetPendingApprovalCountAsync(CancellationToken ct = default)
    {
        var resp = await _http.GetAsync("/api/mcp-approvals/count", ct);
        await EnsureSuccessOrSetErrorAsync(resp);
        return await resp.Content.ReadFromJsonAsync<int>(cancellationToken: ct);
    }

    /// <summary>
    /// Applies one decision (approve or reject) to a set of own pending requests
    /// (F-1001); the response carries one outcome row per request id.
    /// </summary>
    /// <param name="requestIds">Requests to decide.</param>
    /// <param name="approve">True approves, false rejects.</param>
    /// <param name="decisionComment">Optional decision comment.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Per-request outcome rows.</returns>
    public async Task<IReadOnlyList<AiActionRequestBatchItemDto>> Mcp_DecideActionRequestsBatchAsync(
        IReadOnlyList<Guid> requestIds, bool approve, string? decisionComment = null, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync("/api/mcp-approvals/batch",
            new AiActionRequestBatchRequest(requestIds, approve, decisionComment), ct);
        await EnsureSuccessOrSetErrorAsync(resp);
        return await resp.Content.ReadFromJsonAsync<IReadOnlyList<AiActionRequestBatchItemDto>>(cancellationToken: ct)
            ?? Array.Empty<AiActionRequestBatchItemDto>();
    }

    /// <summary>
    /// Loads the instance-wide MCP security overview (Admin role only, F-1005):
    /// metadata only — no business content of foreign users.
    /// </summary>
    /// <param name="recent">Optional size of the security-events excerpt.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The security overview DTO or null when the caller is not an admin.</returns>
    public async Task<McpSecurityOverviewDto?> Admin_McpSecurityOverviewAsync(int recent = 25, CancellationToken ct = default)
    {
        var resp = await _http.GetAsync($"/api/mcp-audit/security-overview?recent={recent}", ct);
        if (resp.StatusCode is System.Net.HttpStatusCode.Forbidden or System.Net.HttpStatusCode.Unauthorized)
        {
            return null;
        }
        await EnsureSuccessOrSetErrorAsync(resp);
        return await resp.Content.ReadFromJsonAsync<McpSecurityOverviewDto>(cancellationToken: ct);
    }

    #endregion MCP Approvals (AiActionRequest)
}
