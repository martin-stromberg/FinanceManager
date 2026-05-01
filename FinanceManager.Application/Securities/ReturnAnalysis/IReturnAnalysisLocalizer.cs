namespace FinanceManager.Application.Securities.ReturnAnalysis;

/// <summary>
/// Provides localized strings for the <see cref="IReturnAnalysisService"/> KPI labels,
/// formula texts, group names, item notes and warnings.
/// Implemented in the Web layer using <c>IStringLocalizer</c>; in tests a simple stub is used.
/// </summary>
public interface IReturnAnalysisLocalizer
{
    /// <summary>Returns the localized string for the given resource key.</summary>
    string this[string key] { get; }

    /// <summary>Returns the localized and formatted string for the given resource key and arguments.</summary>
    string Format(string key, params object[] args);
}
