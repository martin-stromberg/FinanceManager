using FinanceManager.Domain.Securities;

namespace FinanceManager.Application.Securities.GoldenCross;

/// <summary>Entry scenarios derived from a golden cross.</summary>
public enum GoldenCrossEntryScenario
{
    /// <summary>Entry right after the crossing (0–5 days).</summary>
    Early = 0,
    /// <summary>Entry on the first pullback to the short or long moving average (5–30 days).</summary>
    Average = 1,
    /// <summary>Entry after the trend structure has been confirmed (30–60 days).</summary>
    Late = 2
}

/// <summary>Temporal status of an entry window relative to the latest price.</summary>
public enum GoldenCrossWindowStatus
{
    /// <summary>The window has not started yet.</summary>
    Upcoming = 0,
    /// <summary>The latest price lies inside the window.</summary>
    Open = 1,
    /// <summary>The window lies in the past.</summary>
    Closed = 2
}

/// <summary>Outcome of the pullback (retest) analysis after a golden cross.</summary>
public enum GoldenCrossRetestStatus
{
    /// <summary>No pullback to a moving average has occurred so far.</summary>
    None = 0,
    /// <summary>The price is currently at or below the tested moving average.</summary>
    InProgress = 1,
    /// <summary>The price bounced off the moving average and resumed the trend.</summary>
    Successful = 2,
    /// <summary>The price fell through the long-term average after the pullback.</summary>
    Failed = 3
}

/// <summary>Moving average tested by a pullback.</summary>
public enum GoldenCrossLine
{
    /// <summary>Short-term average.</summary>
    Short = 0,
    /// <summary>Long-term average.</summary>
    Long = 1
}

/// <summary>Classification of the price structure since the golden cross.</summary>
public enum GoldenCrossTrendStructure
{
    /// <summary>Not enough price history since the cross.</summary>
    Unknown = 0,
    /// <summary>Consecutive higher highs and higher lows.</summary>
    StableUptrend = 1,
    /// <summary>Rising prices without a clean sequence of higher highs and lows.</summary>
    WeakUptrend = 2,
    /// <summary>No directional progress since the cross.</summary>
    Sideways = 3
}

/// <summary>
/// One of the three typical entry windows after a golden cross.
/// </summary>
/// <param name="Scenario">Scenario identifier.</param>
/// <param name="FromDays">Start offset in calendar days after the cross date.</param>
/// <param name="ToDays">End offset in calendar days after the cross date.</param>
/// <param name="StartDate">Start date of the window.</param>
/// <param name="EndDate">End date of the window.</param>
/// <param name="Status">Position of the latest price relative to the window.</param>
/// <param name="LowClose">Lowest close within the window (from available prices), or <c>null</c>.</param>
/// <param name="HighClose">Highest close within the window (from available prices), or <c>null</c>.</param>
/// <param name="SignalDate">Date on which the scenario-specific condition was met (cross, retest completed, trend confirmed), or <c>null</c>.</param>
/// <param name="Confirmed">Whether the scenario-specific condition has been met.</param>
public sealed record GoldenCrossEntryWindow(
    GoldenCrossEntryScenario Scenario,
    int FromDays,
    int ToDays,
    DateTime StartDate,
    DateTime EndDate,
    GoldenCrossWindowStatus Status,
    decimal? LowClose,
    decimal? HighClose,
    DateTime? SignalDate,
    bool Confirmed);

/// <summary>
/// Result of the pullback (retest) analysis.
/// </summary>
/// <param name="Status">Retest outcome.</param>
/// <param name="Line">Moving average that was tested, or <c>null</c> when none was.</param>
/// <param name="StartDate">First date of the pullback, or <c>null</c>.</param>
/// <param name="LowDate">Date of the lowest close during the pullback, or <c>null</c>.</param>
/// <param name="LowClose">Lowest close during the pullback, or <c>null</c>.</param>
/// <param name="EndDate">First date the price closed above the tested average again, or <c>null</c>.</param>
public sealed record GoldenCrossRetest(
    GoldenCrossRetestStatus Status,
    GoldenCrossLine? Line,
    DateTime? StartDate,
    DateTime? LowDate,
    decimal? LowClose,
    DateTime? EndDate);

/// <summary>
/// Result of the trend structure analysis since the cross.
/// </summary>
/// <param name="Structure">Classification.</param>
/// <param name="HigherHighs">Number of swing highs above their predecessor.</param>
/// <param name="HigherLows">Number of swing lows above their predecessor.</param>
/// <param name="SwingHighs">Total swing highs detected.</param>
/// <param name="SwingLows">Total swing lows detected.</param>
/// <param name="ChangeSinceCrossPercent">Close change since the cross date in percent, or <c>null</c>.</param>
/// <param name="ConfirmedDate">Date on which the structure first qualified as a stable uptrend, or <c>null</c>.</param>
public sealed record GoldenCrossTrend(
    GoldenCrossTrendStructure Structure,
    int HigherHighs,
    int HigherLows,
    int SwingHighs,
    int SwingLows,
    decimal? ChangeSinceCrossPercent,
    DateTime? ConfirmedDate);

/// <summary>
/// Result of the golden cross analysis for a single price series.
/// </summary>
/// <param name="Phase">Classified phase.</param>
/// <param name="AsOfDate">Date of the latest price used, or <c>null</c> when the series is empty.</param>
/// <param name="ShortAverage">Short-term simple moving average, or <c>null</c> when not computable.</param>
/// <param name="LongAverage">Long-term simple moving average, or <c>null</c> when not computable.</param>
/// <param name="DistancePercent">Relative distance of the short-term to the long-term average in percent
/// (positive when the short-term average is above), or <c>null</c> when not computable.</param>
/// <param name="CrossDate">Date on which the short-term average last crossed above the long-term average
/// while currently being above it, or <c>null</c> when not above or the crossing lies outside the series.</param>
/// <param name="LastClose">Latest close, or <c>null</c> when the series is empty.</param>
/// <param name="DaysSinceCross">Calendar days between cross date and latest price, or <c>null</c>.</param>
/// <param name="EntryWindows">The three entry windows (empty unless a cross date is known).</param>
/// <param name="Retest">Pullback analysis (<c>null</c> unless a cross date is known).</param>
/// <param name="Trend">Trend structure analysis (<c>null</c> unless a cross date is known).</param>
public sealed record GoldenCrossAnalysis(
    GoldenCrossPhase Phase,
    DateTime? AsOfDate,
    decimal? ShortAverage,
    decimal? LongAverage,
    decimal? DistancePercent,
    DateTime? CrossDate,
    decimal? LastClose = null,
    int? DaysSinceCross = null,
    IReadOnlyList<GoldenCrossEntryWindow>? EntryWindows = null,
    GoldenCrossRetest? Retest = null,
    GoldenCrossTrend? Trend = null)
{
    /// <summary>Entry windows, never <c>null</c>.</summary>
    public IReadOnlyList<GoldenCrossEntryWindow> Windows => EntryWindows ?? Array.Empty<GoldenCrossEntryWindow>();
}

/// <summary>
/// Pure calculation of short-/long-term moving averages, golden cross classification and the
/// extended entry/retest/trend analysis after a cross.
/// </summary>
public static class GoldenCrossAnalyzer
{
    /// <summary>Calendar-day boundaries of the early entry window.</summary>
    public const int EarlyEntryFromDays = 0, EarlyEntryToDays = 5;
    /// <summary>Calendar-day boundaries of the average entry window.</summary>
    public const int AverageEntryFromDays = 5, AverageEntryToDays = 30;
    /// <summary>Calendar-day boundaries of the late entry window.</summary>
    public const int LateEntryFromDays = 30, LateEntryToDays = 60;

    /// <summary>Relative tolerance (fraction) for a close to count as touching a moving average.</summary>
    private const decimal TouchTolerance = 0.01m;
    /// <summary>Number of neighbours on each side that a close must dominate to be a swing point.</summary>
    private const int SwingLookaround = 3;
    /// <summary>Minimum trading days after the cross before the trend structure is classified.</summary>
    private const int MinTrendDays = 10;

    /// <summary>
    /// Analyzes a chronological price series.
    /// </summary>
    /// <param name="prices">Closing prices ordered by date ascending (one entry per trading day).</param>
    /// <param name="options">Analysis options (windows and threshold).</param>
    /// <returns>The analysis result.</returns>
    /// <exception cref="ArgumentException">Thrown when the option windows are invalid.</exception>
    public static GoldenCrossAnalysis Analyze(IReadOnlyList<(DateTime Date, decimal Close)> prices, GoldenCrossOptions options)
    {
        ArgumentNullException.ThrowIfNull(prices);
        ArgumentNullException.ThrowIfNull(options);
        if (options.ShortWindow <= 0 || options.LongWindow <= 0 || options.ShortWindow >= options.LongWindow)
        {
            throw new ArgumentException("ShortWindow must be positive and smaller than LongWindow.", nameof(options));
        }

        var n = prices.Count;
        var asOf = n > 0 ? prices[n - 1].Date : (DateTime?)null;
        var lastClose = n > 0 ? prices[n - 1].Close : (decimal?)null;
        if (n < options.LongWindow)
        {
            return new GoldenCrossAnalysis(GoldenCrossPhase.InsufficientData, asOf, null, null, null, null, lastClose);
        }

        var prefix = new decimal[n + 1];
        for (var i = 0; i < n; i++)
        {
            prefix[i + 1] = prefix[i] + prices[i].Close;
        }

        decimal Sma(int endExclusive, int window) => (prefix[endExclusive] - prefix[endExclusive - window]) / window;

        var shortAvg = Sma(n, options.ShortWindow);
        var longAvg = Sma(n, options.LongWindow);
        if (longAvg <= 0m)
        {
            return new GoldenCrossAnalysis(GoldenCrossPhase.InsufficientData, asOf, shortAvg, longAvg, null, null, lastClose);
        }

        var distance = (shortAvg - longAvg) / longAvg * 100m;

        DateTime? crossDate = null;
        var crossIdx = -1;
        if (shortAvg >= longAvg)
        {
            for (var end = n; end > options.LongWindow; end--)
            {
                var prevShort = Sma(end - 1, options.ShortWindow);
                var prevLong = Sma(end - 1, options.LongWindow);
                if (prevShort < prevLong)
                {
                    crossIdx = end - 1;
                    crossDate = prices[crossIdx].Date;
                    break;
                }
            }
        }

        var phase = shortAvg >= longAvg
            ? GoldenCrossPhase.Crossed
            : -distance <= options.ApproachThresholdPercent
                ? GoldenCrossPhase.Approaching
                : GoldenCrossPhase.Far;

        if (crossIdx < 0)
        {
            return new GoldenCrossAnalysis(phase, asOf, shortAvg, longAvg, distance, crossDate, lastClose);
        }

        var cross = crossDate!.Value;
        var daysSince = (int)(asOf!.Value.Date - cross.Date).TotalDays;
        var retest = AnalyzeRetest(prices, crossIdx, options, Sma);
        var trend = AnalyzeTrend(prices, crossIdx);

        var windows = new List<GoldenCrossEntryWindow>
        {
            BuildWindow(prices, cross, asOf.Value, GoldenCrossEntryScenario.Early, EarlyEntryFromDays, EarlyEntryToDays, cross, true),
            BuildWindow(prices, cross, asOf.Value, GoldenCrossEntryScenario.Average, AverageEntryFromDays, AverageEntryToDays,
                retest.Status == GoldenCrossRetestStatus.Successful ? retest.EndDate : null,
                retest.Status == GoldenCrossRetestStatus.Successful),
            BuildWindow(prices, cross, asOf.Value, GoldenCrossEntryScenario.Late, LateEntryFromDays, LateEntryToDays,
                trend.ConfirmedDate,
                trend.Structure == GoldenCrossTrendStructure.StableUptrend)
        };

        return new GoldenCrossAnalysis(phase, asOf, shortAvg, longAvg, distance, crossDate, lastClose, daysSince, windows, retest, trend);
    }

    private static GoldenCrossEntryWindow BuildWindow(
        IReadOnlyList<(DateTime Date, decimal Close)> prices,
        DateTime cross,
        DateTime asOf,
        GoldenCrossEntryScenario scenario,
        int fromDays,
        int toDays,
        DateTime? signalDate,
        bool confirmed)
    {
        var start = cross.Date.AddDays(fromDays);
        var end = cross.Date.AddDays(toDays);
        var status = asOf.Date < start
            ? GoldenCrossWindowStatus.Upcoming
            : asOf.Date > end ? GoldenCrossWindowStatus.Closed : GoldenCrossWindowStatus.Open;

        decimal? low = null, high = null;
        for (var i = prices.Count - 1; i >= 0; i--)
        {
            var d = prices[i].Date.Date;
            if (d < start)
            {
                break;
            }
            if (d > end)
            {
                continue;
            }
            var c = prices[i].Close;
            low = low is null || c < low ? c : low;
            high = high is null || c > high ? c : high;
        }

        return new GoldenCrossEntryWindow(scenario, fromDays, toDays, start, end, status, low, high, signalDate, confirmed);
    }

    private static GoldenCrossRetest AnalyzeRetest(
        IReadOnlyList<(DateTime Date, decimal Close)> prices,
        int crossIdx,
        GoldenCrossOptions options,
        Func<int, int, decimal> sma)
    {
        var n = prices.Count;
        var startIdx = -1;
        GoldenCrossLine line = GoldenCrossLine.Short;

        for (var i = crossIdx + 1; i < n; i++)
        {
            var close = prices[i].Close;
            var s = sma(i + 1, options.ShortWindow);
            var l = sma(i + 1, options.LongWindow);
            if (close <= l * (1 + TouchTolerance))
            {
                startIdx = i;
                line = GoldenCrossLine.Long;
                break;
            }
            if (close <= s * (1 + TouchTolerance))
            {
                startIdx = i;
                line = GoldenCrossLine.Short;
                break;
            }
        }

        if (startIdx < 0)
        {
            return new GoldenCrossRetest(GoldenCrossRetestStatus.None, null, null, null, null, null);
        }

        var window = line == GoldenCrossLine.Long ? options.LongWindow : options.ShortWindow;
        var lowIdx = startIdx;
        var endIdx = -1;
        for (var i = startIdx; i < n; i++)
        {
            var close = prices[i].Close;
            if (close < prices[lowIdx].Close)
            {
                lowIdx = i;
            }
            if (close > sma(i + 1, window) * (1 + TouchTolerance))
            {
                endIdx = i;
                break;
            }
        }

        var lastClose = prices[n - 1].Close;
        var lastLong = sma(n, options.LongWindow);
        GoldenCrossRetestStatus status;
        if (endIdx < 0)
        {
            status = lastClose < lastLong ? GoldenCrossRetestStatus.Failed : GoldenCrossRetestStatus.InProgress;
        }
        else
        {
            status = lastClose < lastLong ? GoldenCrossRetestStatus.Failed : GoldenCrossRetestStatus.Successful;
        }

        return new GoldenCrossRetest(
            status,
            line,
            prices[startIdx].Date,
            prices[lowIdx].Date,
            prices[lowIdx].Close,
            endIdx >= 0 ? prices[endIdx].Date : null);
    }

    private static GoldenCrossTrend AnalyzeTrend(IReadOnlyList<(DateTime Date, decimal Close)> prices, int crossIdx)
    {
        var n = prices.Count;
        var count = n - crossIdx;
        var crossClose = prices[crossIdx].Close;
        var change = crossClose > 0 ? (prices[n - 1].Close - crossClose) / crossClose * 100m : (decimal?)null;

        if (count < MinTrendDays)
        {
            return new GoldenCrossTrend(GoldenCrossTrendStructure.Unknown, 0, 0, 0, 0, change, null);
        }

        var highs = new List<(int Idx, decimal Close)>();
        var lows = new List<(int Idx, decimal Close)>();
        for (var i = crossIdx + SwingLookaround; i < n - SwingLookaround; i++)
        {
            var c = prices[i].Close;
            bool isHigh = true, isLow = true;
            for (var k = 1; k <= SwingLookaround && (isHigh || isLow); k++)
            {
                if (prices[i - k].Close >= c || prices[i + k].Close >= c) { isHigh = false; }
                if (prices[i - k].Close <= c || prices[i + k].Close <= c) { isLow = false; }
            }
            if (isHigh) { highs.Add((i, c)); }
            if (isLow) { lows.Add((i, c)); }
        }

        var higherHighs = 0;
        var higherLows = 0;
        DateTime? confirmed = null;
        for (var i = 1; i < highs.Count; i++)
        {
            if (highs[i].Close > highs[i - 1].Close) { higherHighs++; }
        }
        for (var i = 1; i < lows.Count; i++)
        {
            if (lows[i].Close > lows[i - 1].Close) { higherLows++; }
        }

        var lastHighUp = highs.Count >= 2 && highs[^1].Close > highs[^2].Close;
        var lastLowUp = lows.Count >= 2 && lows[^1].Close > lows[^2].Close;
        var stable = lastHighUp && lastLowUp && higherHighs >= 1 && higherLows >= 1 && change > 0;

        if (stable)
        {
            var hi = highs[^1].Idx;
            var lo = lows[^1].Idx;
            confirmed = prices[Math.Max(hi, lo) + SwingLookaround].Date;
        }

        var structure = stable
            ? GoldenCrossTrendStructure.StableUptrend
            : change > 1m ? GoldenCrossTrendStructure.WeakUptrend : GoldenCrossTrendStructure.Sideways;

        return new GoldenCrossTrend(structure, higherHighs, higherLows, highs.Count, lows.Count, change, confirmed);
    }
}
