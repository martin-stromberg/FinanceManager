using System.Net.Http.Json;

namespace FinanceManager.Shared;

public partial class ApiClient
{
    #region Background tasks

    /// <summary>
    /// Enqueues a background task of the specified type.
    /// </summary>
    /// <param name="type">Type of background task to enqueue.</param>
    /// <param name="allowDuplicate">Allow enqueuing a duplicate task if one is already running.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Information about the enqueued background task.</returns>
    public async Task<BackgroundTaskInfo> BackgroundTasks_EnqueueAsync(BackgroundTaskType type, bool allowDuplicate = false, CancellationToken ct = default)
    {
        var url = $"/api/background-tasks/{type}?allowDuplicate={(allowDuplicate ? "true" : "false")}";
        var resp = await _http.PostAsync(url, content: null, ct);
        await EnsureSuccessOrSetErrorAsync(resp);
        return (await resp.Content.ReadFromJsonAsync<BackgroundTaskInfo>(cancellationToken: ct))!;
    }

    /// <summary>
    /// Gets the list of currently active background tasks.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of active background tasks.</returns>
    public async Task<IReadOnlyList<BackgroundTaskInfo>> BackgroundTasks_GetActiveAsync(CancellationToken ct = default)
    {
        var resp = await _http.GetAsync("/api/background-tasks/active", ct);
        await EnsureSuccessOrSetErrorAsync(resp);
        return await resp.Content.ReadFromJsonAsync<IReadOnlyList<BackgroundTaskInfo>>(cancellationToken: ct) ?? Array.Empty<BackgroundTaskInfo>();
    }

    /// <summary>
    /// Gets details for a specific background task or null when not found.
    /// </summary>
    /// <param name="id">Background task id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Background task details or null when not found.</returns>
    public async Task<BackgroundTaskInfo?> BackgroundTasks_GetDetailAsync(Guid id, CancellationToken ct = default)
    {
        var resp = await _http.GetAsync($"/api/background-tasks/{id}", ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        await EnsureSuccessOrSetErrorAsync(resp);
        return await resp.Content.ReadFromJsonAsync<BackgroundTaskInfo>(cancellationToken: ct);
    }

    /// <summary>
    /// Cancels or removes a background task. Returns false when not found or request is invalid.
    /// </summary>
    /// <param name="id">Background task id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True when the task was cancelled or removed successfully.</returns>
    public async Task<bool> BackgroundTasks_CancelOrRemoveAsync(Guid id, CancellationToken ct = default)
    {
        var resp = await _http.DeleteAsync($"/api/background-tasks/{id}", ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return false;
        if (resp.StatusCode == System.Net.HttpStatusCode.BadRequest) return false;
        await EnsureSuccessOrSetErrorAsync(resp);
        return true;
    }

    #endregion Background tasks
}