using FinanceManager.Shared.Dtos.Admin;

namespace FinanceManager.Application.Security;

/// <summary>
/// Abstraction for reading, updating and rendering security.txt settings.
/// </summary>
public interface ISecurityTxtSettingsService
{
    /// <summary>Returns the configured settings for admin editing.</summary>
    Task<SecurityTxtSettingsDto> GetAsync(CancellationToken ct);
    /// <summary>Persists the configured settings.</summary>
    Task UpdateAsync(SecurityTxtSettingsUpdateRequest request, CancellationToken ct);
    /// <summary>Builds the public content for the given output format.</summary>
    Task<string?> BuildContentAsync(SecurityTxtFormat format, CancellationToken ct);
}
