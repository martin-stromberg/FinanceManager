namespace FinanceManager.Application.Securities.TrendReversal;

/// <summary>
/// Provides localized notification texts for the trend reversal feature.
/// Implemented in the Web layer using <c>IStringLocalizer</c>; in tests a simple stub is used.
/// </summary>
public interface ITrendReversalLocalizer
{
    /// <summary>
    /// Returns the localized and formatted string for the given resource key and arguments in the given culture.
    /// </summary>
    /// <param name="key">Resource key.</param>
    /// <param name="cultureName">Culture name (e.g. "de" or "en") or <c>null</c> for the default culture.</param>
    /// <param name="args">Format arguments.</param>
    /// <returns>The localized text.</returns>
    string Format(string key, string? cultureName, params object[] args);
}
