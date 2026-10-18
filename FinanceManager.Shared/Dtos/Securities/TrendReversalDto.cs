namespace FinanceManager.Shared.Dtos.Securities;

/// <summary>
/// Aggregated trend status of a security as exposed by the API.
/// </summary>
public enum TrendStatusDto
{
    /// <summary>Not enough price history to evaluate the trend.</summary>
    InsufficientData = 0,
    /// <summary>No bearish reversal signal detected; the uptrend is intact.</summary>
    Intact = 1,
    /// <summary>Weak reversal signals detected; the trend appears weakened.</summary>
    Weakened = 2,
    /// <summary>Strong or multiple reversal signals detected; a trend reversal is likely.</summary>
    ReversalLikely = 3
}

/// <summary>Type of a detected bearish trend reversal signal.</summary>
public enum TrendReversalSignalTypeDto
{
    /// <summary>Death cross: the short-term average crossed below the long-term average.</summary>
    DeathCross = 0,
    /// <summary>Price broke below the long-term moving average.</summary>
    LongTermLineBreak = 1,
    /// <summary>Bearish market structure (lower highs and/or lower lows).</summary>
    BearishMarketStructure = 2,
    /// <summary>MACD line crossed below its signal line.</summary>
    MacdBearishCrossover = 3,
    /// <summary>Bearish RSI divergence.</summary>
    RsiBearishDivergence = 4
}

/// <summary>Strength of a reversal signal; the numeric value is its contribution to the signal score.</summary>
public enum TrendReversalSignalStrengthDto
{
    /// <summary>Weak signal, contributes 1 point.</summary>
    Weak = 1,
    /// <summary>Moderate signal, contributes 2 points.</summary>
    Moderate = 2,
    /// <summary>Strong signal, contributes 3 points.</summary>
    Strong = 3
}

/// <summary>Classification of a break of the long-term moving average by its duration.</summary>
public enum LineBreakClassificationDto
{
    /// <summary>No break.</summary>
    None = 0,
    /// <summary>Fewer consecutive days below the long-term average than the structural threshold.</summary>
    ShortTermWeakness = 1,
    /// <summary>At least the structural threshold of consecutive days below the long-term average.</summary>
    StructuralReversal = 2
}

/// <summary>Classification of the market structure within the lookback window.</summary>
public enum MarketStructureClassificationDto
{
    /// <summary>Not enough swing points to classify the structure.</summary>
    Unknown = 0,
    /// <summary>No bearish structure detected.</summary>
    Intact = 1,
    /// <summary>Sideways phase with a negative drift.</summary>
    SidewaysBearish = 2,
    /// <summary>Lower swing lows while swing highs are not falling.</summary>
    TrendLoss = 3,
    /// <summary>Lower swing highs and lower swing lows; emerging downtrend.</summary>
    EmergingDowntrend = 4
}

/// <summary>
/// One detected bearish reversal signal.
/// </summary>
/// <param name="Type">Signal type.</param>
/// <param name="Strength">Signal strength (also its score contribution).</param>
/// <param name="Date">Date on which the signal materialized, or <c>null</c> when not determinable.</param>
/// <returns>The result.</returns>
public sealed record ReversalSignalDto(TrendReversalSignalTypeDto Type, TrendReversalSignalStrengthDto Strength, DateTime? Date);

/// <summary>
/// Death cross analysis (short-term average crossing below the long-term average).
/// </summary>
/// <param name="CrossDate">Death cross date (first day the short average is below the long average), or <c>null</c>.</param>
/// <param name="DaysSinceCross">Calendar days since the cross, or <c>null</c>.</param>
/// <param name="Active">Whether the short-term average is currently below the long-term average.</param>
/// <returns>The result.</returns>
public sealed record DeathCrossResultDto(DateTime? CrossDate, int? DaysSinceCross, bool Active);

/// <summary>
/// Long-term moving average break analysis.
/// </summary>
/// <param name="Classification">Break classification.</param>
/// <param name="BreakDate">First date of the current below-average sequence, or <c>null</c>.</param>
/// <param name="ConsecutiveDaysBelow">Number of consecutive closes below their daily long-term average.</param>
/// <param name="PercentBelow">Relative distance of the latest close below the long-term average in percent, or <c>null</c>.</param>
/// <returns>The result.</returns>
public sealed record LongTermLineBreakResultDto(
    LineBreakClassificationDto Classification,
    DateTime? BreakDate,
    int ConsecutiveDaysBelow,
    decimal? PercentBelow);

/// <summary>
/// Market structure analysis (swing highs/lows within the lookback window).
/// </summary>
/// <param name="Classification">Structure classification.</param>
/// <param name="LowerHighs">Number of swing highs below their predecessor.</param>
/// <param name="LowerLows">Number of swing lows below their predecessor.</param>
/// <param name="SwingHighs">Total swing highs detected.</param>
/// <param name="SwingLows">Total swing lows detected.</param>
/// <param name="ChangePercent">Close change over the lookback window in percent, or <c>null</c>.</param>
/// <returns>The result.</returns>
public sealed record MarketStructureResultDto(
    MarketStructureClassificationDto Classification,
    int LowerHighs,
    int LowerLows,
    int SwingHighs,
    int SwingLows,
    decimal? ChangePercent);

/// <summary>
/// MACD analysis.
/// </summary>
/// <param name="Macd">Latest MACD value, or <c>null</c> when not computable.</param>
/// <param name="Signal">Latest signal line value, or <c>null</c> when not computable.</param>
/// <param name="Histogram">Latest MACD minus signal value, or <c>null</c> when not computable.</param>
/// <param name="BearishCrossover">Whether the MACD line is currently below the signal line.</param>
/// <param name="CrossoverDate">Date of the most recent bearish crossover, or <c>null</c>.</param>
/// <returns>The result.</returns>
public sealed record MacdResultDto(decimal? Macd, decimal? Signal, decimal? Histogram, bool BearishCrossover, DateTime? CrossoverDate);

/// <summary>
/// RSI divergence analysis.
/// </summary>
/// <param name="Rsi">Latest RSI value, or <c>null</c> when not computable.</param>
/// <param name="BearishDivergence">Whether a bearish divergence was detected.</param>
/// <param name="DivergenceDate">Date of the second (higher) price high forming the divergence, or <c>null</c>.</param>
/// <returns>The result.</returns>
public sealed record RsiResultDto(decimal? Rsi, bool BearishDivergence, DateTime? DivergenceDate);

/// <summary>
/// Trend reversal statistics for the security detail page.
/// </summary>
/// <param name="Status">Aggregated trend status.</param>
/// <param name="ShortWindow">Trading days of the short-term average.</param>
/// <param name="LongWindow">Trading days of the long-term average.</param>
/// <param name="ShortAverage">Short-term simple moving average, or <c>null</c> when not computable.</param>
/// <param name="LongAverage">Long-term simple moving average, or <c>null</c> when not computable.</param>
/// <param name="DistancePercent">Relative distance of the short-term to the long-term average in percent, or <c>null</c>.</param>
/// <param name="AsOfDate">Date of the latest price used, or <c>null</c>.</param>
/// <param name="CurrencyCode">ISO currency code of the security.</param>
/// <param name="LastClose">Latest close, or <c>null</c>.</param>
/// <param name="DeathCross">Death cross analysis, or <c>null</c> when insufficient data.</param>
/// <param name="LineBreak">Long-term line break analysis, or <c>null</c> when insufficient data.</param>
/// <param name="MarketStructure">Market structure analysis, or <c>null</c> when insufficient data.</param>
/// <param name="Macd">MACD analysis, or <c>null</c> when insufficient data.</param>
/// <param name="Rsi">RSI analysis, or <c>null</c> when insufficient data.</param>
/// <param name="Signals">Detected reversal signals (empty when none).</param>
/// <param name="SignalScore">Sum of the signal strengths.</param>
/// <returns>The result.</returns>
public sealed record TrendReversalDto(
    TrendStatusDto Status,
    int ShortWindow,
    int LongWindow,
    decimal? ShortAverage,
    decimal? LongAverage,
    decimal? DistancePercent,
    DateTime? AsOfDate,
    string CurrencyCode,
    decimal? LastClose = null,
    DeathCrossResultDto? DeathCross = null,
    LongTermLineBreakResultDto? LineBreak = null,
    MarketStructureResultDto? MarketStructure = null,
    MacdResultDto? Macd = null,
    RsiResultDto? Rsi = null,
    IReadOnlyList<ReversalSignalDto>? Signals = null,
    int SignalScore = 0);
