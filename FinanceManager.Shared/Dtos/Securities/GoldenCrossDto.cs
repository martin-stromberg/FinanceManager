namespace FinanceManager.Shared.Dtos.Securities;

/// <summary>
/// Golden cross phase of a security as exposed by the API.
/// </summary>
public enum GoldenCrossPhaseDto
{
    /// <summary>Not enough price history.</summary>
    InsufficientData = 0,
    /// <summary>Short-term average clearly below the long-term average.</summary>
    Far = 1,
    /// <summary>Short-term average approaching the long-term average from below.</summary>
    Approaching = 2,
    /// <summary>Short-term average at or above the long-term average.</summary>
    Crossed = 3
}

/// <summary>
/// Golden cross statistics for the security detail page.
/// </summary>
/// <param name="Phase">Classified phase.</param>
/// <param name="ShortWindow">Trading days of the short-term average.</param>
/// <param name="LongWindow">Trading days of the long-term average.</param>
/// <param name="ShortAverage">Short-term simple moving average, or <c>null</c> when not computable.</param>
/// <param name="LongAverage">Long-term simple moving average, or <c>null</c> when not computable.</param>
/// <param name="DistancePercent">Relative distance of the short-term to the long-term average in percent, or <c>null</c>.</param>
/// <param name="CrossDate">Date of the most recent golden cross while the short-term average is still above, or <c>null</c>.</param>
/// <param name="AsOfDate">Date of the latest price used, or <c>null</c>.</param>
/// <param name="CurrencyCode">ISO currency code of the security.</param>
public sealed record GoldenCrossDto(
    GoldenCrossPhaseDto Phase,
    int ShortWindow,
    int LongWindow,
    decimal? ShortAverage,
    decimal? LongAverage,
    decimal? DistancePercent,
    DateTime? CrossDate,
    DateTime? AsOfDate,
    string CurrencyCode);
