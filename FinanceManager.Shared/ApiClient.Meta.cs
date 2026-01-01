using System.Net.Http.Json;

namespace FinanceManager.Shared;

public partial class ApiClient
{
    #region Meta Holidays

    /// <summary>
    /// Returns available holiday provider identifiers.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Array of provider names.</returns>
    public async Task<string[]> Meta_GetHolidayProvidersAsync(CancellationToken ct = default)
    {
        var resp = await _http.GetAsync("/api/meta/holiday-providers", ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<string[]>(cancellationToken: ct) ?? Array.Empty<string>();
    }

    /// <summary>
    /// Returns supported holiday country ISO codes for the configured providers.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Array of country ISO codes.</returns>
    public async Task<string[]> Meta_GetHolidayCountriesAsync(CancellationToken ct = default)
    {
        var resp = await _http.GetAsync("/api/meta/holiday-countries", ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<string[]>(cancellationToken: ct) ?? Array.Empty<string>();
    }

    /// <summary>
    /// Returns subdivision (state/region) codes for the given provider and country.
    /// </summary>
    /// <param name="provider">Holiday provider identifier.</param>
    /// <param name="country">ISO country code.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Array of subdivision codes or empty when unsupported.</returns>
    public async Task<string[]> Meta_GetHolidaySubdivisionsAsync(string provider, string country, CancellationToken ct = default)
    {
        var url = $"/api/meta/holiday-subdivisions?provider={Uri.EscapeDataString(provider ?? string.Empty)}&country={Uri.EscapeDataString(country ?? string.Empty)}";
        var resp = await _http.GetAsync(url, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<string[]>(cancellationToken: ct) ?? Array.Empty<string>();
    }

    #endregion Meta Holidays
}