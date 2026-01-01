using System.Net.Http.Json;
using System.Text.Json;

namespace FinanceManager.Shared;

public partial class ApiClient
{
    #region Securities

    /// <summary>
    /// Lists securities for the current user.
    /// </summary>
    /// <param name="onlyActive">When true, only active securities are returned; when false, all securities are returned.</param>
    /// <param name="ct">Cancellation token used to cancel the HTTP request.</param>
    /// <returns>A read-only list of <see cref="SecurityDto"/> instances. Returns an empty list when none exist.</returns>
    /// <exception cref="HttpRequestException">Thrown when the HTTP request fails or the server returns a non-success status code.</exception>
    public async Task<IReadOnlyList<SecurityDto>> Securities_ListAsync(bool onlyActive = true, CancellationToken ct = default)
    {
        var resp = await _http.GetAsync($"/api/securities?onlyActive={(onlyActive ? "true" : "false")}", ct);
        await EnsureSuccessOrSetErrorAsync(resp);
        return await resp.Content.ReadFromJsonAsync<IReadOnlyList<SecurityDto>>(cancellationToken: ct) ?? Array.Empty<SecurityDto>();
    }

    /// <summary>
    /// Returns the total count of securities or active securities.
    /// </summary>
    /// <param name="onlyActive">When true, counts only active securities.</param>
    /// <param name="ct">Cancellation token used to cancel the HTTP request.</param>
    /// <returns>Integer count of securities.</returns>
    /// <exception cref="HttpRequestException">Thrown when the HTTP request fails or the server returns a non-success status code.</exception>
    public async Task<int> Securities_CountAsync(bool onlyActive = true, CancellationToken ct = default)
    {
        var resp = await _http.GetAsync($"/api/securities/count?onlyActive={(onlyActive ? "true" : "false")}", ct);
        await EnsureSuccessOrSetErrorAsync(resp);
        var json = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        if (json.TryGetProperty("count", out var countProp) && countProp.TryGetInt32(out var cnt))
        {
            return cnt;
        }
        return 0;
    }

    /// <summary>
    /// Gets a single security by id.
    /// </summary>
    /// <param name="id">Security identifier.</param>
    /// <param name="ct">Cancellation token used to cancel the HTTP request.</param>
    /// <returns>The <see cref="SecurityDto"/> when found; otherwise null when not found.</returns>
    /// <exception cref="HttpRequestException">Thrown when the HTTP request fails for reasons other than NotFound.</exception>
    public async Task<SecurityDto?> Securities_GetAsync(Guid id, CancellationToken ct = default)
    {
        var resp = await _http.GetAsync($"/api/securities/{id}", ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        await EnsureSuccessOrSetErrorAsync(resp);
        return await resp.Content.ReadFromJsonAsync<SecurityDto>(cancellationToken: ct);
    }

    /// <summary>
    /// Creates a new security.
    /// </summary>
    /// <param name="req">Request object describing the security to create.</param>
    /// <param name="ct">Cancellation token used to cancel the HTTP request.</param>
    /// <returns>The created <see cref="SecurityDto"/>.</returns>
    /// <exception cref="HttpRequestException">Thrown when the HTTP request fails or the server returns a non-success status code.</exception>
    public async Task<SecurityDto> Securities_CreateAsync(SecurityRequest req, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync("/api/securities", req, ct);
        await EnsureSuccessOrSetErrorAsync(resp);
        return (await resp.Content.ReadFromJsonAsync<SecurityDto>(cancellationToken: ct))!;
    }

    /// <summary>
    /// Updates an existing security.
    /// </summary>
    /// <param name="id">Security identifier to update.</param>
    /// <param name="req">Update request containing new values.</param>
    /// <param name="ct">Cancellation token used to cancel the HTTP request.</param>
    /// <returns>The updated <see cref="SecurityDto"/> when the security exists; otherwise null when not found.</returns>
    /// <exception cref="HttpRequestException">Thrown when the HTTP request fails for reasons other than NotFound.</exception>
    public async Task<SecurityDto?> Securities_UpdateAsync(Guid id, SecurityRequest req, CancellationToken ct = default)
    {
        var resp = await _http.PutAsJsonAsync($"/api/securities/{id}", req, ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        await EnsureSuccessOrSetErrorAsync(resp);
        return await resp.Content.ReadFromJsonAsync<SecurityDto>(cancellationToken: ct);
    }

    /// <summary>
    /// Archives a security.
    /// </summary>
    /// <param name="id">Security identifier to archive.</param>
    /// <param name="ct">Cancellation token used to cancel the HTTP request.</param>
    /// <returns>True when the operation succeeded; false when the security was not found.</returns>
    /// <exception cref="HttpRequestException">Thrown when the HTTP request fails for reasons other than NotFound.</exception>
    public async Task<bool> Securities_ArchiveAsync(Guid id, CancellationToken ct = default)
    {
        var resp = await _http.PostAsync($"/api/securities/{id}/archive", content: null, ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return false;
        resp.EnsureSuccessStatusCode();
        return true;
    }

    /// <summary>
    /// Deletes a security.
    /// </summary>
    /// <param name="id">Security identifier to delete.</param>
    /// <param name="ct">Cancellation token used to cancel the HTTP request.</param>
    /// <returns>True when deleted; false when not found.</returns>
    /// <exception cref="HttpRequestException">Thrown when the HTTP request fails for reasons other than NotFound.</exception>
    public async Task<bool> Securities_DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var resp = await _http.DeleteAsync($"/api/securities/{id}", ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return false;
        resp.EnsureSuccessStatusCode();
        return true;
    }

    /// <summary>
    /// Assigns a symbol attachment to a security.
    /// </summary>
    /// <param name="id">Security identifier.</param>
    /// <param name="attachmentId">Attachment identifier to assign as symbol.</param>
    /// <param name="ct">Cancellation token used to cancel the HTTP request.</param>
    /// <returns>True when assignment succeeded; false when the security was not found.</returns>
    /// <exception cref="HttpRequestException">Thrown when the HTTP request fails for reasons other than NotFound.</exception>
    public async Task<bool> Securities_SetSymbolAsync(Guid id, Guid attachmentId, CancellationToken ct = default)
    {
        var resp = await _http.PostAsync($"/api/securities/{id}/symbol/{attachmentId}", content: null, ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return false;
        resp.EnsureSuccessStatusCode();
        return true;
    }

    /// <summary>
    /// Clears the symbol attachment from a security.
    /// </summary>
    /// <param name="id">Security identifier.</param>
    /// <param name="ct">Cancellation token used to cancel the HTTP request.</param>
    /// <returns>True when cleared; false when the security was not found.</returns>
    /// <exception cref="HttpRequestException">Thrown when the HTTP request fails for reasons other than NotFound.</exception>
    public async Task<bool> Securities_ClearSymbolAsync(Guid id, CancellationToken ct = default)
    {
        var resp = await _http.DeleteAsync($"/api/securities/{id}/symbol", ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return false;
        resp.EnsureSuccessStatusCode();
        return true;
    }

    /// <summary>
    /// Uploads a symbol file for a security and returns the created attachment metadata.
    /// </summary>
    /// <param name="id">Security identifier.</param>
    /// <param name="fileStream">Stream containing the file contents.</param>
    /// <param name="fileName">Name of the file.</param>
    /// <param name="contentType">Optional MIME type of the file.</param>
    /// <param name="categoryId">Optional category id to associate with the attachment.</param>
    /// <param name="ct">Cancellation token used to cancel the HTTP request.</param>
    /// <returns>The created <see cref="AttachmentDto"/> representing the uploaded symbol.</returns>
    /// <exception cref="HttpRequestException">Thrown when the HTTP request fails or the server returns a non-success status code.</exception>
    public async Task<AttachmentDto> Securities_UploadSymbolAsync(Guid id, Stream fileStream, string fileName, string? contentType = null, Guid? categoryId = null, CancellationToken ct = default)
    {
        using var content = new MultipartFormDataContent();
        var part = new StreamContent(fileStream);
        part.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType);
        content.Add(part, "file", fileName);
        if (categoryId.HasValue) content.Add(new StringContent(categoryId.Value.ToString()), "categoryId");
        var resp = await _http.PostAsync($"/api/securities/{id}/symbol", content, ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.BadRequest)
        {
            LastError = await resp.Content.ReadAsStringAsync(ct);
        }
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<AttachmentDto>(cancellationToken: ct))!;
    }

    /// <summary>
    /// Gets aggregated historical data points for a security.
    /// </summary>
    /// <param name="securityId">Security identifier.</param>
    /// <param name="period">Aggregation period (e.g. "Month").</param>
    /// <param name="take">Maximum number of data points to return.</param>
    /// <param name="maxYearsBack">Optional maximum years back to include in the aggregation.</param>
    /// <param name="ct">Cancellation token used to cancel the HTTP request.</param>
    /// <returns>List of aggregate points or null when the security is not found.</returns>
    public async Task<IReadOnlyList<AggregatePointDto>?> Securities_GetAggregatesAsync(Guid securityId, string period = "Month", int take = 36, int? maxYearsBack = null, CancellationToken ct = default)
    {
        var url = $"/api/securities/{securityId}/aggregates?period={Uri.EscapeDataString(period)}&take={take}";
        if (maxYearsBack.HasValue) url += $"&maxYearsBack={maxYearsBack.Value}";
        var resp = await _http.GetAsync(url, ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<IReadOnlyList<AggregatePointDto>>(cancellationToken: ct);
    }

    /// <summary>
    /// Gets historical price data for a security.
    /// </summary>
    /// <param name="id">Security identifier.</param>
    /// <param name="skip">Number of items to skip for paging.</param>
    /// <param name="take">Number of items to take for paging.</param>
    /// <param name="ct">Cancellation token used to cancel the HTTP request.</param>
    /// <returns>List of <see cref="SecurityPriceDto"/> or null when the security is not found.</returns>
    public async Task<IReadOnlyList<SecurityPriceDto>?> Securities_GetPricesAsync(Guid id, int skip = 0, int take = 50, CancellationToken ct = default)
    {
        var resp = await _http.GetAsync($"/api/securities/{id}/prices?skip={skip}&take={take}", ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<IReadOnlyList<SecurityPriceDto>>(cancellationToken: ct);
    }

    /// <summary>
    /// Enqueues a background task to backfill security data.
    /// </summary>
    /// <param name="securityId">Optional security id to backfill; when null, backfills all securities.</param>
    /// <param name="fromDateUtc">Optional from-date (UTC) to limit backfill start.</param>
    /// <param name="toDateUtc">Optional to-date (UTC) to limit backfill end.</param>
    /// <param name="ct">Cancellation token used to cancel the HTTP request.</param>
    /// <returns>Information about the enqueued background task.</returns>
    public async Task<BackgroundTaskInfo> Securities_EnqueueBackfillAsync(Guid? securityId, DateTime? fromDateUtc, DateTime? toDateUtc, CancellationToken ct = default)
    {
        var payload = new SecurityBackfillRequest(securityId, fromDateUtc, toDateUtc);
        var resp = await _http.PostAsJsonAsync("/api/securities/backfill", payload, ct);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<BackgroundTaskInfo>(cancellationToken: ct))!;
    }

    /// <summary>
    /// Lists upcoming or past dividend aggregate points for securities.
    /// </summary>
    /// <param name="period">Optional period string (e.g. "Year").</param>
    /// <param name="take">Optional maximum number of items to return.</param>
    /// <param name="ct">Cancellation token used to cancel the HTTP request.</param>
    /// <returns>List of aggregate points describing dividends.</returns>
    public async Task<IReadOnlyList<AggregatePointDto>> Securities_GetDividendsAsync(string? period = null, int? take = null, CancellationToken ct = default)
    {
        var url = "/api/securities/dividends";
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(period)) query.Add($"period={Uri.EscapeDataString(period)}");
        if (take.HasValue) query.Add($"take={take.Value}");
        if (query.Count > 0) url += "?" + string.Join("&", query);
        var resp = await _http.GetAsync(url, ct);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<IReadOnlyList<AggregatePointDto>>(cancellationToken: ct))!;
    }

    #endregion Securities
}
