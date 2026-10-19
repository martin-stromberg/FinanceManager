namespace FinanceManager.Application.Securities.TrendReversal;

/// <summary>
/// Configuration for the trend reversal analysis (moving average windows, indicator periods,
/// break duration threshold, market structure lookback and score threshold).
/// </summary>
public sealed class TrendReversalOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "TrendReversal";

    /// <summary>Number of trading days used for the short-term simple moving average. Default: 50.</summary>
    public int ShortWindow { get; set; } = 50;

    /// <summary>Number of trading days used for the long-term simple moving average. Default: 200.</summary>
    public int LongWindow { get; set; } = 200;

    /// <summary>
    /// Minimum number of consecutive closes below the long-term moving average for the break to be
    /// classified as a structural trend reversal instead of short-term weakness. Default: 5.
    /// </summary>
    public int StructuralBreakDays { get; set; } = 5;

    /// <summary>Fast EMA period of the MACD indicator. Default: 12.</summary>
    public int MacdFastPeriod { get; set; } = 12;

    /// <summary>Slow EMA period of the MACD indicator. Default: 26.</summary>
    public int MacdSlowPeriod { get; set; } = 26;

    /// <summary>EMA period of the MACD signal line. Default: 9.</summary>
    public int MacdSignalPeriod { get; set; } = 9;

    /// <summary>Period of the RSI indicator (Wilder smoothing). Default: 14.</summary>
    public int RsiPeriod { get; set; } = 14;

    /// <summary>
    /// Number of trailing trading days examined for the market structure analysis
    /// (lower swing highs / lower swing lows). Default: 120.
    /// </summary>
    public int MarketStructureLookbackDays { get; set; } = 120;

    /// <summary>Number of neighbours on each side that a close must dominate to count as a swing point. Default: 3.</summary>
    public int SwingLookaround { get; set; } = 3;

    /// <summary>
    /// Signal score (sum of signal strengths; weak = 1, moderate = 2, strong = 3) at or above which the
    /// trend status is classified as reversal likely. Below (but above zero) the trend is weakened. Default: 4.
    /// </summary>
    public int ReversalLikelyThreshold { get; set; } = 4;
}
