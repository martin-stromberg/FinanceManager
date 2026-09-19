using FinanceManager.Shared.Dtos.Admin;

namespace FinanceManager.Application.WellKnown;

/// <summary>
/// Abstraction for reading and updating well-known endpoint settings.
/// </summary>
public interface IWellKnownSettingsService
{
    /// <summary>Returns the configured settings for admin editing.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The result.</returns>
    Task<WellKnownSettingsDto> GetAsync(CancellationToken ct);
    /// <summary>Persists the configured settings.</summary>
    /// <param name="request">Request payload.</param>
    /// <param name="ct">Cancellation token.</param>
    Task UpdateAsync(WellKnownSettingsUpdateRequest request, CancellationToken ct);
    /// <summary>Returns the effective redirect target for the change-password well-known endpoint.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The result.</returns>
    Task<string> GetChangePasswordUrlAsync(CancellationToken ct);
}
