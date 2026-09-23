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

/// <summary>Entry scenarios derived from a golden cross.</summary>
public enum GoldenCrossEntryScenarioDto
{
    /// <summary>Entry right after the crossing (0–5 days).</summary>
    Early = 0,
    /// <summary>Entry on the first pullback to a moving average (5–30 days).</summary>
    Average = 1,
    /// <summary>Entry after the trend structure has been confirmed (30–60 days).</summary>
    Late = 2
}

/// <summary>Temporal status of an entry window relative to the latest price.</summary>
public enum GoldenCrossWindowStatusDto
{
    /// <summary>The window has not started yet.</summary>
    Upcoming = 0,
    /// <summary>The latest price lies inside the window.</summary>
    Open = 1,
    /// <summary>The window lies in the past.</summary>
    Closed = 2
}

/// <summary>Outcome of the pullback (retest) analysis after a golden cross.</summary>
public enum GoldenCrossRetestStatusDto
{
    /// <summary>No pullback so far.</summary>
    None = 0,
    /// <summary>Price currently at or below the tested average.</summary>
    InProgress = 1,
    /// <summary>Price bounced off the average and resumed the trend.</summary>
    Successful = 2,
    /// <summary>Price fell through the long-term average.</summary>
    Failed = 3
}

/// <summary>Moving average tested by a pullback.</summary>
public enum GoldenCrossLineDto
{
    /// <summary>Short-term average.</summary>
    Short = 0,
    /// <summary>Long-term average.</summary>
    Long = 1
}

/// <summary>Classification of the price structure since the golden cross.</summary>
public enum GoldenCrossTrendStructureDto
{
    /// <summary>Not enough history since the cross.</summary>
    Unknown = 0,
    /// <summary>Higher highs and higher lows.</summary>
    StableUptrend = 1,
    /// <summary>Rising without a clean swing structure.</summary>
    WeakUptrend = 2,
    /// <summary>No directional progress.</summary>
    Sideways = 3
}

/// <summary>
/// One of the three typical entry windows after a golden cross.
/// </summary>
/// <param name="Scenario">Scenario identifier.</param>
/// <param name="FromDays">Start offset in calendar days after the cross.</param>
/// <param name="ToDays">End offset in calendar days after the cross.</param>
/// <param name="StartDate">Window start date.</param>
/// <param name="EndDate">Window end date.</param>
/// <param name="Status">Position of the latest price relative to the window.</param>
/// <param name="LowClose">Lowest close within the window, or <c>null</c>.</param>
/// <param name="HighClose">Highest close within the window, or <c>null</c>.</param>
/// <param name="SignalDate">Date the scenario condition was met, or <c>null</c>.</param>
/// <param name="Confirmed">Whether the scenario condition has been met.</param>
public sealed record GoldenCrossEntryWindowDto(
    GoldenCrossEntryScenarioDto Scenario,
    int FromDays,
    int ToDays,
    DateTime StartDate,
    DateTime EndDate,
    GoldenCrossWindowStatusDto Status,
    decimal? LowClose,
    decimal? HighClose,
    DateTime? SignalDate,
    bool Confirmed);

/// <summary>
/// Pullback (retest) analysis after a golden cross.
/// </summary>
/// <param name="Status">Retest outcome.</param>
/// <param name="Line">Tested moving average, or <c>null</c>.</param>
/// <param name="StartDate">First pullback date, or <c>null</c>.</param>
/// <param name="LowDate">Date of the pullback low, or <c>null</c>.</param>
/// <param name="LowClose">Pullback low close, or <c>null</c>.</param>
/// <param name="EndDate">First close back above the tested average, or <c>null</c>.</param>
public sealed record GoldenCrossRetestDto(
    GoldenCrossRetestStatusDto Status,
    GoldenCrossLineDto? Line,
    DateTime? StartDate,
    DateTime? LowDate,
    decimal? LowClose,
    DateTime? EndDate);

/// <summary>
/// Trend structure analysis since the golden cross.
/// </summary>
/// <param name="Structure">Classification.</param>
/// <param name="HigherHighs">Number of higher swing highs.</param>
/// <param name="HigherLows">Number of higher swing lows.</param>
/// <param name="SwingHighs">Total swing highs.</param>
/// <param name="SwingLows">Total swing lows.</param>
/// <param name="ChangeSinceCrossPercent">Close change since the cross in percent, or <c>null</c>.</param>
/// <param name="ConfirmedDate">Date the stable uptrend was first confirmed, or <c>null</c>.</param>
public sealed record GoldenCrossTrendDto(
    GoldenCrossTrendStructureDto Structure,
    int HigherHighs,
    int HigherLows,
    int SwingHighs,
    int SwingLows,
    decimal? ChangeSinceCrossPercent,
    DateTime? ConfirmedDate);

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
/// <param name="LastClose">Latest close, or <c>null</c>.</param>
/// <param name="DaysSinceCross">Calendar days since the cross, or <c>null</c>.</param>
/// <param name="EntryWindows">Early/average/late entry windows (empty unless crossed).</param>
/// <param name="Retest">Pullback analysis, or <c>null</c> unless crossed.</param>
/// <param name="Trend">Trend structure analysis, or <c>null</c> unless crossed.</param>
public sealed record GoldenCrossDto(
    GoldenCrossPhaseDto Phase,
    int ShortWindow,
    int LongWindow,
    decimal? ShortAverage,
    decimal? LongAverage,
    decimal? DistancePercent,
    DateTime? CrossDate,
    DateTime? AsOfDate,
    string CurrencyCode,
    decimal? LastClose = null,
    int? DaysSinceCross = null,
    IReadOnlyList<GoldenCrossEntryWindowDto>? EntryWindows = null,
    GoldenCrossRetestDto? Retest = null,
    GoldenCrossTrendDto? Trend = null);
