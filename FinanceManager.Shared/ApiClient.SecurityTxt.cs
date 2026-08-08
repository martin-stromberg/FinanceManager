using FinanceManager.Shared.Dtos.Admin;
using System.Net.Http.Json;

namespace FinanceManager.Shared;

/// <summary>
/// Security.txt admin API extensions for <see cref="ApiClient"/>.
/// </summary>
public partial class ApiClient
{
    /// <summary>Reads the current security.txt settings.</summary>
    public Task<SecurityTxtSettingsDto?> GetSecurityTxtSettingsAsync(CancellationToken ct = default)
        => _http.GetAsync("api/admin/security-txt", ct).ContinueWith(async t =>
        {
            var response = await t;
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<SecurityTxtSettingsDto>(cancellationToken: ct);
        }, ct).Unwrap();

    /// <summary>Updates the current security.txt settings.</summary>
    public async Task UpdateSecurityTxtSettingsAsync(SecurityTxtSettingsUpdateRequest request, CancellationToken ct = default)
    {
        var response = await _http.PutAsync("api/admin/security-txt", JsonContent.Create(request), ct);
        response.EnsureSuccessStatusCode();
    }
}
