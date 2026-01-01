using FinanceManager.Domain.Notifications;

namespace FinanceManager.Application.Notifications;

/// <summary>
/// Resolves a holiday provider implementation for a given provider kind.
/// </summary>
public interface IHolidayProviderResolver
{
    /// <summary>
    /// Returns the holiday provider instance for the specified kind.
    /// </summary>
    /// <param name="kind">Holiday provider kind.</param>
    /// <returns>Resolved IHolidayProvider instance.</returns>
    IHolidayProvider Resolve(HolidayProviderKind kind);
}
