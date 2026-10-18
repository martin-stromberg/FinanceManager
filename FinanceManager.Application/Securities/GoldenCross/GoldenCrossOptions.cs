namespace FinanceManager.Application.Securities.GoldenCross;

/// <summary>
/// Configuration for the golden cross analysis (moving average windows and proximity threshold).
/// </summary>
public sealed class GoldenCrossOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "GoldenCross";

    /// <summary>Number of trading days used for the short-term simple moving average. Default: 50.</summary>
    public int ShortWindow { get; set; } = 50;

    /// <summary>Number of trading days used for the long-term simple moving average. Default: 200.</summary>
    public int LongWindow { get; set; } = 200;

    /// <summary>
    /// Maximum relative distance (in percent of the long-term average) at which the short-term average
    /// below the long-term average is considered "approaching" a golden cross. Default: 3 (%).
    /// </summary>
    public decimal ApproachThresholdPercent { get; set; } = 3m;
}
