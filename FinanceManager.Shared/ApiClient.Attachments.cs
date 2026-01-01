using System.Net.Http.Json;

namespace FinanceManager.Shared;

public partial class ApiClient
{
    #region Attachments

    /// <summary>
    /// Lists attachments for an entity.
    /// </summary>
    /// <param name="entityKind">Numeric entity kind (matches server enum).</param>
    /// <param name="entityId">Id of the entity to list attachments for.</param>
    /// <param name="skip">Number of items to skip for paging.</param>
    /// <param name="take">Number of items to take for paging.</param>
    /// <param name="categoryId">Optional category filter.</param>
    /// <param name="isUrl">Optional filter to only include URL attachments when true.</param>
    /// <param name="q">Optional search query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Page result containing attachment DTOs. Returns an empty page when none found.</returns>
    /// <exception cref="HttpRequestException">When the HTTP request fails.</exception>
    public async Task<PageResult<AttachmentDto>> Attachments_ListAsync(short entityKind, Guid entityId, int skip = 0, int take = 50, Guid? categoryId = null, bool? isUrl = null, string? q = null, CancellationToken ct = default)
    {
        var url = $"/api/attachments/{entityKind}/{entityId}?skip={skip}&take={take}";
        if (categoryId.HasValue) url += $"&categoryId={categoryId}";
        if (isUrl.HasValue) url += isUrl.Value ? "&isUrl=true" : "&isUrl=false";
        if (!string.IsNullOrWhiteSpace(q)) url += $"&q={Uri.EscapeDataString(q)}";
        var resp = await _http.GetAsync(url, ct);
        await EnsureSuccessOrSetErrorAsync(resp);
        return (await resp.Content.ReadFromJsonAsync<PageResult<AttachmentDto>>(cancellationToken: ct)) ?? new PageResult<AttachmentDto>();
    }

    /// <summary>
    /// Uploads a file as an attachment for the specified entity.
    /// </summary>
    /// <param name="entityKind">Numeric entity kind.</param>
    /// <param name="entityId">Target entity id.</param>
    /// <param name="fileStream">Stream containing file contents.</param>
    /// <param name="fileName">File name to use on the server.</param>
    /// <param name="contentType">MIME content type.</param>
    /// <param name="categoryId">Optional attachment category id.</param>
    /// <param name="role">Optional role identifier for the attachment.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created <see cref="AttachmentDto"/> on success.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="fileStream"/> or <paramref name="fileName"/> is null.</exception>
    public async Task<AttachmentDto> Attachments_UploadFileAsync(short entityKind, Guid entityId, Stream fileStream, string fileName, string contentType, Guid? categoryId = null, short? role = null, CancellationToken ct = default)
    {
        using var content = new MultipartFormDataContent();
        var part = new StreamContent(fileStream);
        part.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType);
        content.Add(part, "file", fileName);
        if (categoryId.HasValue) content.Add(new StringContent(categoryId.Value.ToString()), "categoryId");
        var url = $"/api/attachments/{entityKind}/{entityId}";
        if (role.HasValue) url += $"?role={role.Value}";
        var resp = await _http.PostAsync(url, content, ct);
        await EnsureSuccessOrSetErrorAsync(resp);
        return (await resp.Content.ReadFromJsonAsync<AttachmentDto>(cancellationToken: ct))!;
    }

    /// <summary>
    /// Creates a URL attachment for the specified entity.
    /// </summary>
    /// <param name="entityKind">Numeric entity kind.</param>
    /// <param name="entityId">Target entity id.</param>
    /// <param name="url">Remote URL to attach.</param>
    /// <param name="categoryId">Optional category id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created <see cref="AttachmentDto"/>.</returns>
    public async Task<AttachmentDto> Attachments_CreateUrlAsync(short entityKind, Guid entityId, string url, Guid? categoryId = null, CancellationToken ct = default)
    {
        var payload = new { file = (string?)null, categoryId, url };
        var resp = await _http.PostAsJsonAsync($"/api/attachments/{entityKind}/{entityId}", payload, ct);
        await EnsureSuccessOrSetErrorAsync(resp);
        return (await resp.Content.ReadFromJsonAsync<AttachmentDto>(cancellationToken: ct))!;
    }

    /// <summary>
    /// Deletes an attachment by id.
    /// </summary>
    /// <param name="id">Attachment id to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if deleted; false when not found.</returns>
    public async Task<bool> Attachments_DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var resp = await _http.DeleteAsync($"/api/attachments/{id}", ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return false;
        await EnsureSuccessOrSetErrorAsync(resp);
        return true;
    }

    /// <summary>
    /// Updates core properties of an attachment (e.g. file name, category).
    /// </summary>
    /// <param name="id">Attachment id.</param>
    /// <param name="fileName">New file name or null to keep existing.</param>
    /// <param name="categoryId">New category id or null to clear.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True when update succeeded; false when not found.</returns>
    public async Task<bool> Attachments_UpdateCoreAsync(Guid id, string? fileName, Guid? categoryId, CancellationToken ct = default)
    {
        var req = new AttachmentUpdateCoreRequest(fileName, categoryId);
        var resp = await _http.PutAsJsonAsync($"/api/attachments/{id}", req, ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return false;
        await EnsureSuccessOrSetErrorAsync(resp);
        return true;
    }

    /// <summary>
    /// Updates the category of an attachment.
    /// </summary>
    /// <param name="id">Attachment id.</param>
    /// <param name="categoryId">New category id or null to clear.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True when update succeeded; false when not found.</returns>
    public async Task<bool> Attachments_UpdateCategoryAsync(Guid id, Guid? categoryId, CancellationToken ct = default)
    {
        var req = new AttachmentUpdateCategoryRequest(categoryId);
        var resp = await _http.PutAsJsonAsync($"/api/attachments/{id}/category", req, ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return false;
        await EnsureSuccessOrSetErrorAsync(resp);
        return true;
    }

    /// <summary>
    /// Lists all attachment categories.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of <see cref="AttachmentCategoryDto"/> objects.</returns>
    public async Task<IReadOnlyList<AttachmentCategoryDto>> Attachments_ListCategoriesAsync(CancellationToken ct = default)
    {
        var resp = await _http.GetAsync("/api/attachments/categories", ct);
        await EnsureSuccessOrSetErrorAsync(resp);
        return await resp.Content.ReadFromJsonAsync<IReadOnlyList<AttachmentCategoryDto>>(cancellationToken: ct) ?? Array.Empty<AttachmentCategoryDto>();
    }

    /// <summary>
    /// Creates a new attachment category.
    /// </summary>
    /// <param name="name">Category name.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created <see cref="AttachmentCategoryDto"/>.</returns>
    public async Task<AttachmentCategoryDto> Attachments_CreateCategoryAsync(string name, CancellationToken ct = default)
    {
        var req = new AttachmentCreateCategoryRequest(name);
        var resp = await _http.PostAsJsonAsync("/api/attachments/categories", req, ct);
        await EnsureSuccessOrSetErrorAsync(resp);
        return (await resp.Content.ReadFromJsonAsync<AttachmentCategoryDto>(cancellationToken: ct))!;
    }

    /// <summary>
    /// Updates the name of an attachment category.
    /// </summary>
    /// <param name="id">Category id.</param>
    /// <param name="name">New name.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Updated category DTO or null when not found.</returns>
    public async Task<AttachmentCategoryDto?> Attachments_UpdateCategoryNameAsync(Guid id, string name, CancellationToken ct = default)
    {
        var req = new AttachmentUpdateCategoryNameRequest(name);
        var resp = await _http.PutAsJsonAsync($"/api/attachments/categories/{id}", req, ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        await EnsureSuccessOrSetErrorAsync(resp);
        return await resp.Content.ReadFromJsonAsync<AttachmentCategoryDto>(cancellationToken: ct);
    }

    /// <summary>
    /// Deletes an attachment category.
    /// </summary>
    /// <param name="id">Category id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True when deleted; false when not found.</returns>
    public async Task<bool> Attachments_DeleteCategoryAsync(Guid id, CancellationToken ct = default)
    {
        var resp = await _http.DeleteAsync($"/api/attachments/categories/{id}", ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return false;
        await EnsureSuccessOrSetErrorAsync(resp);
        return true;
    }

    /// <summary>
    /// Creates a short-lived download token for an attachment.
    /// </summary>
    /// <param name="id">Attachment id.</param>
    /// <param name="validSeconds">Token validity in seconds.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Download token DTO or null when not found.</returns>
    public async Task<AttachmentDownloadTokenDto?> Attachments_CreateDownloadTokenAsync(Guid id, int validSeconds = 60, CancellationToken ct = default)
    {
        var resp = await _http.PostAsync($"/api/attachments/{id}/download-token?validSeconds={validSeconds}", content: null, ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        await EnsureSuccessOrSetErrorAsync(resp);
        return await resp.Content.ReadFromJsonAsync<AttachmentDownloadTokenDto>(cancellationToken: ct);
    }

    #endregion Attachments
}
