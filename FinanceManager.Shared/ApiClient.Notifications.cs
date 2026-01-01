using System.Net.Http.Json;

namespace FinanceManager.Shared;

public partial class ApiClient
{
    #region Notifications

    /// <summary>
    /// Lists currently active notifications for the signed-in user (filtered server-side by current UTC time).
    /// </summary>
    /// <param name="ct">Cancellation token used to cancel the HTTP request.</param>
    /// <returns>A read-only list of <see cref="NotificationDto"/> instances. Returns an empty list when no notifications exist.</returns>
    /// <exception cref="HttpRequestException">Thrown when the HTTP request fails or the server returns a non-success status code.</exception>
    public async Task<IReadOnlyList<NotificationDto>> Notifications_ListAsync(CancellationToken ct = default)
    {
        var resp = await _http.GetAsync("/api/notifications", ct);
        await EnsureSuccessOrSetErrorAsync(resp);
        return await resp.Content.ReadFromJsonAsync<IReadOnlyList<NotificationDto>>(cancellationToken: ct) ?? Array.Empty<NotificationDto>();
    }

    /// <summary>
    /// Dismisses a notification by its id for the current user.
    /// </summary>
    /// <param name="id">The unique identifier of the notification to dismiss.</param>
    /// <param name="ct">Cancellation token used to cancel the HTTP request.</param>
    /// <returns>True if the notification existed and was dismissed; false when the notification was not found.</returns>
    /// <exception cref="HttpRequestException">Thrown when the HTTP request fails for reasons other than NotFound.</exception>
    public async Task<bool> Notifications_DismissAsync(Guid id, CancellationToken ct = default)
    {
        var resp = await _http.PostAsync($"/api/notifications/{id}/dismiss", content: null, ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return false;
        resp.EnsureSuccessStatusCode();
        return true;
    }

    #endregion Notifications
}