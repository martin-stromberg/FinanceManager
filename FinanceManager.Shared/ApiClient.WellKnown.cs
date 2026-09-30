using FinanceManager.Shared.Dtos.Admin;
using System.Net.Http.Json;

namespace FinanceManager.Shared;

/// <summary>
/// Well-known admin API extensions for <see cref="ApiClient"/>.
/// </summary>
public partial class ApiClient
{
    /// <summary>Reads the current well-known settings.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The result.</returns>
    public async Task<WellKnownSettingsDto?> GetWellKnownSettingsAsync(CancellationToken ct = default)
    {
        var response = await _http.GetAsync("api/admin/well-known", ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<WellKnownSettingsDto>(cancellationToken: ct);
    }

    /// <summary>Updates the current well-known settings.</summary>
    /// <param name="request">Request payload.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task UpdateWellKnownSettingsAsync(WellKnownSettingsUpdateRequest request, CancellationToken ct = default)
    {
        var response = await _http.PutAsync("api/admin/well-known", JsonContent.Create(request), ct);
        response.EnsureSuccessStatusCode();
    }
}
