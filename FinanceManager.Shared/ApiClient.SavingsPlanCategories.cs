using System.Net.Http.Json;

namespace FinanceManager.Shared;

public partial class ApiClient
{
    #region Savings Plan Categories

    /// <summary>
    /// Lists savings plan categories for the current user.
    /// </summary>
    /// <param name="ct">Cancellation token used to cancel the HTTP request.</param>
    /// <returns>A read-only list of <see cref="SavingsPlanCategoryDto"/>. Returns an empty list when none exist.</returns>
    /// <exception cref="HttpRequestException">Thrown when the HTTP request fails or the server returns a non-success status code.</exception>
    public async Task<IReadOnlyList<SavingsPlanCategoryDto>> SavingsPlanCategories_ListAsync(CancellationToken ct = default)
    {
        var resp = await _http.GetAsync("/api/savings-plan-categories", ct);
        await EnsureSuccessOrSetErrorAsync(resp);
        return await resp.Content.ReadFromJsonAsync<IReadOnlyList<SavingsPlanCategoryDto>>(cancellationToken: ct) ?? Array.Empty<SavingsPlanCategoryDto>();
    }

    /// <summary>
    /// Gets a single savings plan category by id.
    /// </summary>
    /// <param name="id">Identifier of the savings plan category.</param>
    /// <param name="ct">Cancellation token used to cancel the HTTP request.</param>
    /// <returns>The <see cref="SavingsPlanCategoryDto"/> when found; otherwise <c>null</c> when not found.</returns>
    /// <exception cref="HttpRequestException">Thrown when the HTTP request fails for reasons other than NotFound.</exception>
    public async Task<SavingsPlanCategoryDto?> SavingsPlanCategories_GetAsync(Guid id, CancellationToken ct = default)
    {
        var resp = await _http.GetAsync($"/api/savings-plan-categories/{id}", ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        await EnsureSuccessOrSetErrorAsync(resp);
        return await resp.Content.ReadFromJsonAsync<SavingsPlanCategoryDto>(cancellationToken: ct);
    }

    /// <summary>
    /// Creates a new savings plan category. On server-side validation failure, <see cref="LastError"/> is set and <c>null</c> is returned.
    /// </summary>
    /// <param name="dto">DTO describing the savings plan category to create.</param>
    /// <param name="ct">Cancellation token used to cancel the HTTP request.</param>
    /// <returns>The created <see cref="SavingsPlanCategoryDto"/> or <c>null</c> when the server rejected the request (validation error).</returns>
    /// <exception cref="HttpRequestException">Thrown when the HTTP request fails for reasons other than validation errors (which return a non-success status code and set <see cref="LastError"/>).</exception>
    public async Task<SavingsPlanCategoryDto?> SavingsPlanCategories_CreateAsync(SavingsPlanCategoryDto dto, CancellationToken ct = default)
    {
        LastError = null;
        var resp = await _http.PostAsJsonAsync("/api/savings-plan-categories", dto, ct);
        if (!resp.IsSuccessStatusCode)
        {
            LastError = await resp.Content.ReadAsStringAsync(ct);
            return null;
        }
        return await resp.Content.ReadFromJsonAsync<SavingsPlanCategoryDto>(cancellationToken: ct);
    }

    /// <summary>
    /// Updates an existing savings plan category.
    /// </summary>
    /// <param name="id">Identifier of the category to update.</param>
    /// <param name="dto">DTO containing updated values.</param>
    /// <param name="ct">Cancellation token used to cancel the HTTP request.</param>
    /// <returns>The updated <see cref="SavingsPlanCategoryDto"/> when successful; <c>null</c> when the category was not found or validation failed (in which case <see cref="LastError"/> is set).</returns>
    /// <exception cref="HttpRequestException">Thrown when the HTTP request fails for reasons other than NotFound or validation errors.</exception>
    public async Task<SavingsPlanCategoryDto?> SavingsPlanCategories_UpdateAsync(Guid id, SavingsPlanCategoryDto dto, CancellationToken ct = default)
    {
        LastError = null;
        var resp = await _http.PutAsJsonAsync($"/api/savings-plan-categories/{id}", dto, ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
        if (!resp.IsSuccessStatusCode)
        {
            LastError = await resp.Content.ReadAsStringAsync(ct);
            return null;
        }
        return await resp.Content.ReadFromJsonAsync<SavingsPlanCategoryDto>(cancellationToken: ct);
    }

    /// <summary>
    /// Deletes a savings plan category.
    /// </summary>
    /// <param name="id">Identifier of the category to delete.</param>
    /// <param name="ct">Cancellation token used to cancel the HTTP request.</param>
    /// <returns><c>true</c> when deletion succeeded; <c>false</c> when the category was not found or the server rejected the request (in which case <see cref="LastError"/> is set).</returns>
    /// <exception cref="HttpRequestException">Thrown when the HTTP request fails for reasons other than NotFound or BadRequest.</exception>
    public async Task<bool> SavingsPlanCategories_DeleteAsync(Guid id, CancellationToken ct = default)
    {
        LastError = null;
        var resp = await _http.DeleteAsync($"/api/savings-plan-categories/{id}", ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
        if (resp.StatusCode == System.Net.HttpStatusCode.BadRequest)
        {
            LastError = await resp.Content.ReadAsStringAsync(ct);
            return false;
        }
        await EnsureSuccessOrSetErrorAsync(resp);
        return true;
    }

    /// <summary>
    /// Assigns a symbol attachment to a savings plan category.
    /// </summary>
    /// <param name="id">Category identifier.</param>
    /// <param name="attachmentId">Attachment identifier to set as symbol.</param>
    /// <param name="ct">Cancellation token used to cancel the HTTP request.</param>
    /// <returns><c>true</c> when the symbol was assigned; <c>false</c> when the category was not found or the server rejected the request (in which case <see cref="LastError"/> is set).</returns>
    /// <exception cref="HttpRequestException">Thrown when the HTTP request fails for reasons other than NotFound or BadRequest.</exception>
    public async Task<bool> SavingsPlanCategories_SetSymbolAsync(Guid id, Guid attachmentId, CancellationToken ct = default)
    {
        LastError = null;
        var resp = await _http.PostAsync($"/api/savings-plan-categories/{id}/symbol/{attachmentId}", content: null, ct);
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
    /// Clears the symbol attachment from a savings plan category.
    /// </summary>
    /// <param name="id">Category identifier.</param>
    /// <param name="ct">Cancellation token used to cancel the HTTP request.</param>
    /// <returns><c>true</c> when the symbol was cleared; <c>false</c> when the category was not found or the server rejected the request (in which case <see cref="LastError"/> is set).</returns>
    /// <exception cref="HttpRequestException">Thrown when the HTTP request fails for reasons other than NotFound or BadRequest.</exception>
    public async Task<bool> SavingsPlanCategories_ClearSymbolAsync(Guid id, CancellationToken ct = default)
    {
        LastError = null;
        var resp = await _http.DeleteAsync($"/api/savings-plan-categories/{id}/symbol", ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return false;
        if (resp.StatusCode == System.Net.HttpStatusCode.BadRequest)
        {
            LastError = await resp.Content.ReadAsStringAsync(ct);
            return false;
        }
        resp.EnsureSuccessStatusCode();
        return true;
    }

    #endregion Savings Plan Categories
}