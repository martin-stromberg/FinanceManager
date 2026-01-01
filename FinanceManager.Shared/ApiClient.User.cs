using System.Net.Http.Json;

namespace FinanceManager.Shared;

public partial class ApiClient
{
    #region User Settings

    /// <summary>
    /// Gets the current user's profile settings.
    /// </summary>
    /// <param name="ct">Cancellation token used to cancel the HTTP request.</param>
    /// <returns>
    /// A <see cref="UserProfileSettingsDto"/> containing the user's profile settings, or <c>null</c>
    /// if the server response was not successful.
    /// </returns>
    /// <exception cref="HttpRequestException">Thrown when the underlying HTTP request fails.</exception>
    public async Task<UserProfileSettingsDto?> UserSettings_GetProfileAsync(CancellationToken ct = default)
    {
        var resp = await _http.GetAsync("/api/user/settings/profile", ct);
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadFromJsonAsync<UserProfileSettingsDto>(cancellationToken: ct);
    }

    /// <summary>
    /// Updates the current user's profile settings.
    /// </summary>
    /// <param name="request">The update request containing new profile values.</param>
    /// <param name="ct">Cancellation token used to cancel the HTTP request.</param>
    /// <returns><c>true</c> when the update was accepted by the server; otherwise <c>false</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is <c>null</c>.</exception>
    /// <exception cref="HttpRequestException">Thrown when the underlying HTTP request fails.</exception>
    public async Task<bool> UserSettings_UpdateProfileAsync(UserProfileSettingsUpdateRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var resp = await _http.PutAsJsonAsync("/api/user/settings/profile", request, ct);
        return resp.IsSuccessStatusCode;
    }

    /// <summary>
    /// Gets the import split settings for the current user.
    /// </summary>
    /// <param name="ct">Cancellation token used to cancel the HTTP request.</param>
    /// <returns>
    /// An <see cref="ImportSplitSettingsDto"/> with the user's import split settings, or <c>null</c>
    /// when the server response was not successful.
    /// </returns>
    /// <exception cref="HttpRequestException">Thrown when the underlying HTTP request fails.</exception>
    public async Task<ImportSplitSettingsDto?> UserSettings_GetImportSplitAsync(CancellationToken ct = default)
    {
        var resp = await _http.GetAsync("/api/user/settings/import-split", ct);
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadFromJsonAsync<ImportSplitSettingsDto>(cancellationToken: ct);
    }

    /// <summary>
    /// Updates the user's import split settings.
    /// </summary>
    /// <param name="request">Update request containing new import split configuration.</param>
    /// <param name="ct">Cancellation token used to cancel the HTTP request.</param>
    /// <returns><c>true</c> when the update was accepted by the server; otherwise <c>false</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is <c>null</c>.</exception>
    /// <exception cref="HttpRequestException">Thrown when the underlying HTTP request fails.</exception>
    public async Task<bool> UserSettings_UpdateImportSplitAsync(ImportSplitSettingsUpdateRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var resp = await _http.PutAsJsonAsync("/api/user/settings/import-split", request, ct);
        return resp.IsSuccessStatusCode;
    }

    /// <summary>
    /// Gets the user's notification settings.
    /// </summary>
    /// <param name="ct">Cancellation token used to cancel the HTTP request.</param>
    /// <returns>
    /// A <see cref="NotificationSettingsDto"/> containing notification preferences, or <c>null</c>
    /// when the server response was not successful.
    /// </returns>
    /// <exception cref="HttpRequestException">Thrown when the underlying HTTP request fails.</exception>
    public async Task<NotificationSettingsDto?> User_GetNotificationSettingsAsync(CancellationToken ct = default)
    {
        var resp = await _http.GetAsync("/api/user/settings/notifications", ct);
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadFromJsonAsync<NotificationSettingsDto>(cancellationToken: ct);
    }

    /// <summary>
    /// Updates the user's notification settings (monthly reminders and holiday provider selection).
    /// </summary>
    /// <param name="monthlyEnabled">Whether monthly reminders are enabled.</param>
    /// <param name="hour">Hour of day for monthly reminder (0-23) or <c>null</c> to leave unchanged/clear.</param>
    /// <param name="minute">Minute of hour for monthly reminder (0-59) or <c>null</c> to leave unchanged/clear.</param>
    /// <param name="provider">Holiday provider identifier, or <c>null</c> to unset.</param>
    /// <param name="country">Holiday country ISO code, or <c>null</c> to unset.</param>
    /// <param name="subdivision">Holiday subdivision code, or <c>null</c> to unset.</param>
    /// <param name="ct">Cancellation token used to cancel the HTTP request.</param>
    /// <returns><c>true</c> when the update was accepted by the server; otherwise <c>false</c>.</returns>
    /// <exception cref="HttpRequestException">Thrown when the underlying HTTP request fails.</exception>
    public async Task<bool> User_UpdateNotificationSettingsAsync(bool monthlyEnabled, int? hour, int? minute, string? provider, string? country, string? subdivision, CancellationToken ct = default)
    {
        var payload = new
        {
            MonthlyReminderEnabled = monthlyEnabled,
            MonthlyReminderHour = hour,
            MonthlyReminderMinute = minute,
            HolidayProvider = provider,
            HolidayCountryCode = country,
            HolidaySubdivisionCode = subdivision
        };
        var resp = await _http.PutAsJsonAsync("/api/user/settings/notifications", payload, ct);
        return resp.IsSuccessStatusCode;
    }

    #endregion User Settings
}