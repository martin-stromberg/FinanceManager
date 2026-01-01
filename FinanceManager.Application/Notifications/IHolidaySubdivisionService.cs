using FinanceManager.Domain.Notifications;

namespace FinanceManager.Application.Notifications;

/// <summary>
/// Service to provide administrative subdivisions (states/provinces) for a holiday provider and country.
/// </summary>
public interface IHolidaySubdivisionService
{
    /// <summary>
    /// Returns the subdivision codes for the specified provider and country.
    /// </summary>
    /// <param name="provider">Holiday provider kind.</param>
    /// <param name="countryCode">Two-letter ISO country code (e.g. "DE").</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Array of subdivision identifiers.</returns>
    Task<string[]> GetSubdivisionsAsync(HolidayProviderKind provider, string countryCode, CancellationToken ct);
}
