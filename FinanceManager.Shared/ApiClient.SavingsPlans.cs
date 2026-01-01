using System.Net.Http.Json;
using System.Text.Json;

namespace FinanceManager.Shared;

public partial class ApiClient
{

    #region Savings Plans

    /// <summary>
    /// Lists savings plans for the current user.
    /// </summary>
    /// <param name="onlyActive">When <c>true</c>, returns only active plans; when <c>false</c>, returns all plans.</param>
    /// <param name="ct">Cancellation token used to cancel the HTTP request.</param>
    /// <returns>A read-only list of <see cref="SavingsPlanDto"/> instances. Returns an empty list when none are available.</returns>
    /// <exception cref="HttpRequestException">Thrown when the HTTP request fails or the server returns a non-success status code.</exception>
    public async Task<IReadOnlyList<SavingsPlanDto>> SavingsPlans_ListAsync(bool onlyActive = true, CancellationToken ct = default)
    {
        var resp = await _http.GetAsync($"/api/savings-plans?onlyActive={(onlyActive ? "true" : "false")}", ct);
        await EnsureSuccessOrSetErrorAsync(resp);
        return await resp.Content.ReadFromJsonAsync<IReadOnlyList<SavingsPlanDto>>(cancellationToken: ct) ?? Array.Empty<SavingsPlanDto>();
    }

    /// <summary>
    /// Returns the total count of savings plans (optionally only active plans).
    /// </summary>
    /// <param name="onlyActive">When <c>true</c>, counts only active plans.</param>
    /// <param name="ct">Cancellation token used to cancel the HTTP request.</param>
    /// <returns>Integer count of matching savings plans.</returns>
    /// <exception cref="HttpRequestException">Thrown when the HTTP request fails or the response cannot be parsed.</exception>
    public async Task<int> SavingsPlans_CountAsync(bool onlyActive = true, CancellationToken ct = default)
    {
        var resp = await _http.GetAsync($"/api/savings-plans/count?onlyActive={(onlyActive ? "true" : "false")}", ct);
        await EnsureSuccessOrSetErrorAsync(resp);
        var json = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        if (json.TryGetProperty("count", out var countProp) && countProp.TryGetInt32(out var cnt))
        {
            return cnt;
        }
        return 0;
    }

    /// <summary>
    /// Gets a savings plan by its identifier.
    /// </summary>
    /// <param name="id">The savings plan identifier.</param>
    /// <param name="ct">Cancellation token used to cancel the HTTP request.</param>
    /// <returns>The <see cref="SavingsPlanDto"/> when found; otherwise <c>null</c> when not found.</returns>
    /// <exception cref="HttpRequestException">Thrown when the HTTP request fails for reasons other than NotFound.</exception>
    public async Task<SavingsPlanDto?> SavingsPlans_GetAsync(Guid id, CancellationToken ct = default)
    {
        var resp = await _http.GetAsync($"/api/savings-plans/{id}", ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        await EnsureSuccessOrSetErrorAsync(resp);
        return await resp.Content.ReadFromJsonAsync<SavingsPlanDto>(cancellationToken: ct);
    }

    /// <summary>
    /// Creates a new savings plan.
    /// </summary>
    /// <param name="req">Creation request containing plan details.</param>
    /// <param name="ct">Cancellation token used to cancel the HTTP request.</param>
    /// <returns>The created <see cref="SavingsPlanDto"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="req"/> is <c>null</c>.</exception>
    /// <exception cref="HttpRequestException">Thrown when the HTTP request fails or the server returns a non-success status code.</exception>
    public async Task<SavingsPlanDto> SavingsPlans_CreateAsync(SavingsPlanCreateRequest req, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(req);
        var resp = await _http.PostAsJsonAsync("/api/savings-plans", req, ct);
        await EnsureSuccessOrSetErrorAsync(resp);
        return (await resp.Content.ReadFromJsonAsync<SavingsPlanDto>(cancellationToken: ct))!;
    }

    /// <summary>
    /// Updates an existing savings plan.
    /// </summary>
    /// <param name="id">Identifier of the savings plan to update.</param>
    /// <param name="req">Update request containing new values.</param>
    /// <param name="ct">Cancellation token used to cancel the HTTP request.</param>
    /// <returns>The updated <see cref="SavingsPlanDto"/> when the plan exists; otherwise <c>null</c> when not found.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="req"/> is <c>null</c>.</exception>
    /// <exception cref="HttpRequestException">Thrown when the HTTP request fails for reasons other than NotFound.</exception>
    public async Task<SavingsPlanDto?> SavingsPlans_UpdateAsync(Guid id, SavingsPlanCreateRequest req, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(req);
        var resp = await _http.PutAsJsonAsync($"/api/savings-plans/{id}", req, ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        await EnsureSuccessOrSetErrorAsync(resp);
        return await resp.Content.ReadFromJsonAsync<SavingsPlanDto>(cancellationToken: ct);
    }

    /// <summary>
    /// Returns analysis data for a savings plan.
    /// </summary>
    /// <param name="id">Savings plan identifier.</param>
    /// <param name="ct">Cancellation token used to cancel the HTTP request.</param>
    /// <returns>Analysis DTO for the savings plan.</returns>
    /// <exception cref="HttpRequestException">Thrown when the HTTP request fails or the server returns a non-success status code.</exception>
    public async Task<SavingsPlanAnalysisDto> SavingsPlans_AnalyzeAsync(Guid id, CancellationToken ct = default)
    {
        var resp = await _http.GetAsync($"/api/savings-plans/{id}/analysis", ct);
        await EnsureSuccessOrSetErrorAsync(resp);
        return (await resp.Content.ReadFromJsonAsync<SavingsPlanAnalysisDto>(cancellationToken: ct))!;
    }

    /// <summary>
    /// Archives a savings plan.
    /// </summary>
    /// <param name="id">Identifier of the savings plan to archive.</param>
    /// <param name="ct">Cancellation token used to cancel the HTTP request.</param>
    /// <returns><c>true</c> when archived; <c>false</c> when the plan was not found.</returns>
    /// <exception cref="HttpRequestException">Thrown when the HTTP request fails for reasons other than NotFound.</exception>
    public async Task<bool> SavingsPlans_ArchiveAsync(Guid id, CancellationToken ct = default)
    {
        var resp = await _http.PostAsync($"/api/savings-plans/{id}/archive", content: null, ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return false;
        await EnsureSuccessOrSetErrorAsync(resp);
        return true;
    }

    /// <summary>
    /// Deletes a savings plan.
    /// </summary>
    /// <param name="id">Identifier of the savings plan to delete.</param>
    /// <param name="ct">Cancellation token used to cancel the HTTP request.</param>
    /// <returns><c>true</c> when deleted; <c>false</c> when not found.</returns>
    /// <exception cref="HttpRequestException">Thrown when the HTTP request fails for reasons other than NotFound.</exception>
    public async Task<bool> SavingsPlans_DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var resp = await _http.DeleteAsync($"/api/savings-plans/{id}", ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return false;
        await EnsureSuccessOrSetErrorAsync(resp);
        return true;
    }

    /// <summary>
    /// Assigns a symbol attachment to a savings plan.
    /// </summary>
    /// <param name="id">Savings plan identifier.</param>
    /// <param name="attachmentId">Attachment identifier to use as symbol.</param>
    /// <param name="ct">Cancellation token used to cancel the HTTP request.</param>
    /// <returns><c>true</c> when the symbol was set; <c>false</c> when the plan was not found or the server rejected the request (see <see cref="LastError"/>).</returns>
    /// <exception cref="HttpRequestException">Thrown when the HTTP request fails for reasons other than NotFound or BadRequest.</exception>
    public async Task<bool> SavingsPlans_SetSymbolAsync(Guid id, Guid attachmentId, CancellationToken ct = default)
    {
        var resp = await _http.PostAsync($"/api/savings-plans/{id}/symbol/{attachmentId}", content: null, ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return false;
        if (resp.StatusCode == System.Net.HttpStatusCode.BadRequest)
        {
            LastError = await resp.Content.ReadAsStringAsync(ct);
            return false;
        }
        resp.EnsureSuccessStatusCode();
        return true;
    }

    /// <summary>
    /// Clears the symbol attachment from a savings plan.
    /// </summary>
    /// <param name="id">Savings plan identifier.</param>
    /// <param name="ct">Cancellation token used to cancel the HTTP request.</param>
    /// <returns><c>true</c> when cleared; <c>false</c> when the plan was not found or the server rejected the request (see <see cref="LastError"/>).</returns>
    /// <exception cref="HttpRequestException">Thrown when the HTTP request fails for reasons other than NotFound or BadRequest.</exception>
    public async Task<bool> SavingsPlans_ClearSymbolAsync(Guid id, CancellationToken ct = default)
    {
        var resp = await _http.DeleteAsync($"/api/savings-plans/{id}/symbol", ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return false;
        if (resp.StatusCode == System.Net.HttpStatusCode.BadRequest)
        {
            LastError = await resp.Content.ReadAsStringAsync(ct);
            return false;
        }
        resp.EnsureSuccessStatusCode();
        return true;
    }

    #endregion Savings Plans
}
