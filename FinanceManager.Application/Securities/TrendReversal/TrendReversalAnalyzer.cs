using FinanceManager.Domain.Securities;

namespace FinanceManager.Application.Securities.TrendReversal;

/// <summary>Type of a detected bearish trend reversal signal.</summary>
public enum TrendReversalSignalType
{
    /// <summary>Death cross: the short-term average crossed below the long-term average.</summary>
    DeathCross = 0,
    /// <summary>Price broke below the long-term moving average (short-term or structural).</summary>
    LongTermLineBreak = 1,
    /// <summary>Bearish market structure (lower highs and/or lower lows).</summary>
    BearishMarketStructure = 2,
    /// <summary>MACD line crossed below its signal line.</summary>
    MacdBearishCrossover = 3,
    /// <summary>Bearish RSI divergence (higher price high at lower RSI high).</summary>
    RsiBearishDivergence = 4
}

/// <summary>Strength of a reversal signal; the numeric value is its contribution to the signal score.</summary>
public enum TrendReversalSignalStrength
{
    /// <summary>Weak signal, contributes 1 point.</summary>
    Weak = 1,
    /// <summary>Moderate signal, contributes 2 points.</summary>
    Moderate = 2,
    /// <summary>Strong signal, contributes 3 points.</summary>
    Strong = 3
}

/// <summary>Classification of a break of the long-term moving average by its duration.</summary>
public enum LineBreakClassification
{
    /// <summary>The close is at or above the long-term average; no break.</summary>
    None = 0,
    /// <summary>The close is below the long-term average for fewer days than the structural threshold.</summary>
    ShortTermWeakness = 1,
    /// <summary>The close is below the long-term average for at least the structural threshold of days.</summary>
    StructuralReversal = 2
}

/// <summary>Classification of the market structure within the lookback window.</summary>
public enum MarketStructureClassification
{
    /// <summary>Not enough swing points to classify the structure.</summary>
    Unknown = 0,
    /// <summary>No bearish structure detected.</summary>
    Intact = 1,
    /// <summary>Sideways phase with a negative drift but no lower highs/lows sequence.</summary>
    SidewaysBearish = 2,
    /// <summary>Lower swing lows while swing highs are not falling; trend is losing strength.</summary>
    TrendLoss = 3,
    /// <summary>Both the last two swing highs and the last two swing lows are lower; emerging downtrend.</summary>
    EmergingDowntrend = 4
}

/// <summary>
/// One detected bearish reversal signal for the overview list.
/// </summary>
/// <param name="Type">Signal type.</param>
/// <param name="Strength">Signal strength (also its score contribution).</param>
/// <param name="Date">Date on which the signal materialized, or <c>null</c> when not determinable.</param>
/// <returns>The result.</returns>
public sealed record ReversalSignal(TrendReversalSignalType Type, TrendReversalSignalStrength Strength, DateTime? Date);

/// <summary>
/// Result of the death cross analysis (short-term average crossing below the long-term average).
/// </summary>
/// <param name="CrossDate">First date at which the short-term average is below the long-term average
/// (the death cross boundary), or <c>null</c> when not crossed or the crossing
/// lies outside the series.</param>
/// <param name="DaysSinceCross">Calendar days between the cross date and the latest price, or <c>null</c>.</param>
/// <param name="Active">Whether the short-term average is currently below the long-term average.</param>
/// <returns>The result.</returns>
public sealed record DeathCrossResult(DateTime? CrossDate, int? DaysSinceCross, bool Active);

/// <summary>
/// Result of the long-term moving average break analysis.
/// </summary>
/// <param name="Classification">Break classification.</param>
/// <param name="BreakDate">First date of the current sequence of closes below the long-term average, or <c>null</c>.</param>
/// <param name="ConsecutiveDaysBelow">Number of consecutive closes below their respective daily long-term average.</param>
/// <param name="PercentBelow">Relative distance of the latest close below the long-term average in percent, or <c>null</c>.</param>
/// <returns>The result.</returns>
public sealed record LongTermLineBreakResult(
    LineBreakClassification Classification,
    DateTime? BreakDate,
    int ConsecutiveDaysBelow,
    decimal? PercentBelow);

/// <summary>
/// Result of the market structure analysis (swing highs/lows within the lookback window).
/// </summary>
/// <param name="Classification">Structure classification.</param>
/// <param name="LowerHighs">Number of swing highs below their predecessor.</param>
/// <param name="LowerLows">Number of swing lows below their predecessor.</param>
/// <param name="SwingHighs">Total swing highs detected.</param>
/// <param name="SwingLows">Total swing lows detected.</param>
/// <param name="ChangePercent">Close change over the lookback window in percent, or <c>null</c>.</param>
/// <returns>The result.</returns>
public sealed record MarketStructureResult(
    MarketStructureClassification Classification,
    int LowerHighs,
    int LowerLows,
    int SwingHighs,
    int SwingLows,
    decimal? ChangePercent);

/// <summary>
/// Result of the MACD analysis.
/// </summary>
/// <param name="Macd">Latest MACD value (fast EMA minus slow EMA), or <c>null</c> when not computable.</param>
/// <param name="Signal">Latest MACD signal line value, or <c>null</c> when not computable.</param>
/// <param name="Histogram">Latest MACD minus signal value, or <c>null</c> when not computable.</param>
/// <param name="BearishCrossover">Whether the MACD line is currently below the signal line.</param>
/// <param name="CrossoverDate">Date of the most recent cross of the MACD line from at/above to below the signal line, or <c>null</c>.</param>
/// <returns>The result.</returns>
public sealed record MacdResult(decimal? Macd, decimal? Signal, decimal? Histogram, bool BearishCrossover, DateTime? CrossoverDate);

/// <summary>
/// Result of the RSI divergence analysis.
/// </summary>
/// <param name="Rsi">Latest RSI value (Wilder), or <c>null</c> when not computable.</param>
/// <param name="BearishDivergence">Whether the last two swing highs show a higher price high at a lower RSI.</param>
/// <param name="DivergenceDate">Date of the second (higher) swing high forming the divergence, or <c>null</c>.</param>
/// <returns>The result.</returns>
public sealed record RsiResult(decimal? Rsi, bool BearishDivergence, DateTime? DivergenceDate);

/// <summary>
/// Aggregated result of the trend reversal analysis for a single price series.
/// </summary>
/// <param name="Status">Aggregated trend status.</param>
/// <param name="AsOfDate">Date of the latest price used, or <c>null</c> when the series is empty.</param>
/// <param name="ShortAverage">Short-term simple moving average, or <c>null</c> when not computable.</param>
/// <param name="LongAverage">Long-term simple moving average, or <c>null</c> when not computable.</param>
/// <param name="DistancePercent">Relative distance of the short-term to the long-term average in percent
/// (negative when the short-term average is below), or <c>null</c> when not computable.</param>
/// <param name="LastClose">Latest close, or <c>null</c> when the series is empty.</param>
/// <param name="DeathCross">Death cross analysis (<c>null</c> when insufficient data).</param>
/// <param name="LineBreak">Long-term line break analysis (<c>null</c> when insufficient data).</param>
/// <param name="MarketStructure">Market structure analysis (<c>null</c> when insufficient data).</param>
/// <param name="Macd">MACD analysis (<c>null</c> when insufficient data).</param>
/// <param name="Rsi">RSI analysis (<c>null</c> when insufficient data).</param>
/// <param name="Signals">Detected reversal signals (empty when none).</param>
/// <param name="SignalScore">Sum of the signal strengths.</param>
/// <returns>The result.</returns>
public sealed record TrendReversalAnalysis(
    TrendStatus Status,
    DateTime? AsOfDate,
    decimal? ShortAverage,
    decimal? LongAverage,
    decimal? DistancePercent,
    decimal? LastClose,
    DeathCrossResult? DeathCross = null,
    LongTermLineBreakResult? LineBreak = null,
    MarketStructureResult? MarketStructure = null,
    MacdResult? Macd = null,
    RsiResult? Rsi = null,
    IReadOnlyList<ReversalSignal>? Signals = null,
    int SignalScore = 0)
{
    /// <summary>Detected reversal signals, never <c>null</c>.</summary>
    /// <returns>The list of detected reversal signals.</returns>
    public IReadOnlyList<ReversalSignal> SignalList => Signals ?? [];
}

/// <summary>
/// Pure calculation of bearish trend reversal signals for a price series: death cross, long-term line break,
/// market structure (lower highs/lows), MACD bearish crossover and RSI bearish divergence, aggregated into a
/// weighted signal score and an overall <see cref="TrendStatus"/>.
/// </summary>
public static class TrendReversalAnalyzer
{
    /// <summary>
    /// Analyzes a chronological price series.
    /// </summary>
    /// <param name="prices">Closing prices ordered by date ascending (one entry per trading day).</param>
    /// <param name="options">Analysis options (windows, periods, thresholds).</param>
    /// <returns>The analysis result.</returns>
    /// <exception cref="ArgumentException">Thrown when the option values are invalid.</exception>
    public static TrendReversalAnalysis Analyze(IReadOnlyList<(DateTime Date, decimal Close)> prices, TrendReversalOptions options)
    {
        ArgumentNullException.ThrowIfNull(prices);
        ArgumentNullException.ThrowIfNull(options);
        if (options.ShortWindow <= 0 || options.LongWindow <= 0 || options.ShortWindow >= options.LongWindow
            || options.StructuralBreakDays <= 0 || options.MacdFastPeriod <= 0 || options.MacdSlowPeriod <= 0
            || options.MacdFastPeriod >= options.MacdSlowPeriod || options.MacdSignalPeriod <= 0
            || options.RsiPeriod <= 0 || options.MarketStructureLookbackDays <= 0 || options.SwingLookaround <= 0
            || options.ReversalLikelyThreshold <= 0)
        {
            throw new ArgumentException("Invalid trend reversal options (windows/periods must be positive, short/fast smaller than long/slow).", nameof(options));
        }

        var n = prices.Count;
        var asOf = n > 0 ? prices[n - 1].Date : (DateTime?)null;
        var lastClose = n > 0 ? prices[n - 1].Close : (decimal?)null;
        if (n < options.LongWindow)
        {
            return new TrendReversalAnalysis(TrendStatus.InsufficientData, asOf, null, null, null, lastClose);
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
            return new TrendReversalAnalysis(TrendStatus.InsufficientData, asOf, shortAvg, longAvg, null, lastClose);
        }

        var distance = (shortAvg - longAvg) / longAvg * 100m;
        var signals = new List<ReversalSignal>();

        var deathCross = AnalyzeDeathCross(prices, options, shortAvg, longAvg, Sma);
        if (deathCross.Active)
        {
            signals.Add(new ReversalSignal(TrendReversalSignalType.DeathCross, TrendReversalSignalStrength.Strong, deathCross.CrossDate ?? asOf));
        }

        var lineBreak = AnalyzeLineBreak(prices, options, lastClose!.Value, longAvg, Sma);
        if (lineBreak.Classification == LineBreakClassification.StructuralReversal)
        {
            signals.Add(new ReversalSignal(TrendReversalSignalType.LongTermLineBreak, TrendReversalSignalStrength.Strong, lineBreak.BreakDate));
        }
        else if (lineBreak.Classification == LineBreakClassification.ShortTermWeakness)
        {
            signals.Add(new ReversalSignal(TrendReversalSignalType.LongTermLineBreak, TrendReversalSignalStrength.Weak, lineBreak.BreakDate));
        }

        var (marketStructure, swingHighs, swingLows) = AnalyzeMarketStructure(prices, options);
        if (marketStructure.Classification == MarketStructureClassification.EmergingDowntrend)
        {
            signals.Add(new ReversalSignal(TrendReversalSignalType.BearishMarketStructure, TrendReversalSignalStrength.Strong, LatestSwingDate(prices, swingHighs, swingLows)));
        }
        else if (marketStructure.Classification == MarketStructureClassification.TrendLoss)
        {
            signals.Add(new ReversalSignal(TrendReversalSignalType.BearishMarketStructure, TrendReversalSignalStrength.Moderate, LatestSwingDate(prices, swingHighs, swingLows)));
        }
        else if (marketStructure.Classification == MarketStructureClassification.SidewaysBearish)
        {
            signals.Add(new ReversalSignal(TrendReversalSignalType.BearishMarketStructure, TrendReversalSignalStrength.Weak, LatestSwingDate(prices, swingHighs, swingLows)));
        }

        var macd = AnalyzeMacd(prices, options);
        if (macd.BearishCrossover)
        {
            signals.Add(new ReversalSignal(TrendReversalSignalType.MacdBearishCrossover, TrendReversalSignalStrength.Moderate, macd.CrossoverDate ?? asOf));
        }

        var rsi = AnalyzeRsi(prices, options, swingHighs);
        if (rsi.BearishDivergence)
        {
            signals.Add(new ReversalSignal(TrendReversalSignalType.RsiBearishDivergence, TrendReversalSignalStrength.Moderate, rsi.DivergenceDate ?? asOf));
        }

        var score = signals.Sum(s => (int)s.Strength);
        var status = score == 0
            ? TrendStatus.Intact
            : score < options.ReversalLikelyThreshold ? TrendStatus.Weakened : TrendStatus.ReversalLikely;

        return new TrendReversalAnalysis(
            status, asOf, shortAvg, longAvg, distance, lastClose,
            deathCross, lineBreak, marketStructure, macd, rsi, signals, score);
    }

    private static DeathCrossResult AnalyzeDeathCross(
        IReadOnlyList<(DateTime Date, decimal Close)> prices,
        TrendReversalOptions options,
        decimal shortAvg,
        decimal longAvg,
        Func<int, int, decimal> sma)
    {
        var n = prices.Count;
        if (shortAvg >= longAvg)
        {
            return new DeathCrossResult(null, null, false);
        }

        // Walk back to the last point where the short average was still at or above the long average;
        // the death cross boundary lies between that index and the next one.
        for (var end = n; end > options.LongWindow; end--)
        {
            var prevShort = sma(end - 1, options.ShortWindow);
            var prevLong = sma(end - 1, options.LongWindow);
            if (prevShort >= prevLong)
            {
                var crossIdx = end - 1;
                var crossDate = prices[crossIdx].Date;
                var daysSince = (int)(prices[n - 1].Date.Date - crossDate.Date).TotalDays;
                return new DeathCrossResult(crossDate, daysSince, true);
            }
        }

        return new DeathCrossResult(null, null, true);
    }

    private static LongTermLineBreakResult AnalyzeLineBreak(
        IReadOnlyList<(DateTime Date, decimal Close)> prices,
        TrendReversalOptions options,
        decimal lastClose,
        decimal longAvg,
        Func<int, int, decimal> sma)
    {
        var n = prices.Count;
        if (lastClose >= longAvg)
        {
            return new LongTermLineBreakResult(LineBreakClassification.None, null, 0, null);
        }

        var consecutive = 0;
        for (var i = n - 1; i >= options.LongWindow - 1; i--)
        {
            if (prices[i].Close < sma(i + 1, options.LongWindow))
            {
                consecutive++;
            }
            else
            {
                break;
            }
        }

        var breakDate = prices[n - consecutive].Date;
        var percentBelow = (lastClose - longAvg) / longAvg * 100m;
        var classification = consecutive >= options.StructuralBreakDays
            ? LineBreakClassification.StructuralReversal
            : LineBreakClassification.ShortTermWeakness;
        return new LongTermLineBreakResult(classification, breakDate, consecutive, percentBelow);
    }

    private static (MarketStructureResult Result, List<(int Idx, decimal Close)> Highs, List<(int Idx, decimal Close)> Lows)
        AnalyzeMarketStructure(IReadOnlyList<(DateTime Date, decimal Close)> prices, TrendReversalOptions options)
    {
        var n = prices.Count;
        var lookback = Math.Min(options.MarketStructureLookbackDays, n);
        var start = n - lookback;
        var look = options.SwingLookaround;

        var firstClose = prices[start].Close;
        var change = firstClose > 0 ? (prices[n - 1].Close - firstClose) / firstClose * 100m : (decimal?)null;

        var highs = new List<(int Idx, decimal Close)>();
        var lows = new List<(int Idx, decimal Close)>();
        for (var i = start + look; i < n - look; i++)
        {
            var c = prices[i].Close;
            bool isHigh = true, isLow = true;
            for (var k = 1; k <= look && (isHigh || isLow); k++)
            {
                if (prices[i - k].Close >= c || prices[i + k].Close >= c) { isHigh = false; }
                if (prices[i - k].Close <= c || prices[i + k].Close <= c) { isLow = false; }
            }
            if (isHigh) { highs.Add((i, c)); }
            if (isLow) { lows.Add((i, c)); }
        }

        var lowerHighs = 0;
        var lowerLows = 0;
        for (var i = 1; i < highs.Count; i++)
        {
            if (highs[i].Close < highs[i - 1].Close) { lowerHighs++; }
        }
        for (var i = 1; i < lows.Count; i++)
        {
            if (lows[i].Close < lows[i - 1].Close) { lowerLows++; }
        }

        MarketStructureClassification classification;
        if (highs.Count < 2 || lows.Count < 2)
        {
            classification = MarketStructureClassification.Unknown;
        }
        else
        {
            var lastHighsLower = highs[^1].Close < highs[^2].Close;
            var lastLowsLower = lows[^1].Close < lows[^2].Close;
            classification = lastHighsLower && lastLowsLower
                ? MarketStructureClassification.EmergingDowntrend
                : lastLowsLower
                    ? MarketStructureClassification.TrendLoss
                    : change < 0 ? MarketStructureClassification.SidewaysBearish : MarketStructureClassification.Intact;
        }

        return (new MarketStructureResult(classification, lowerHighs, lowerLows, highs.Count, lows.Count, change), highs, lows);
    }

    private static DateTime? LatestSwingDate(
        IReadOnlyList<(DateTime Date, decimal Close)> prices,
        List<(int Idx, decimal Close)> highs,
        List<(int Idx, decimal Close)> lows)
    {
        // The most recent evidence for a bearish structure is the latest of the detected swing points.
        var idx = Math.Max(
            highs.Count > 0 ? highs[^1].Idx : -1,
            lows.Count > 0 ? lows[^1].Idx : -1);
        return idx >= 0 ? prices[idx].Date : null;
    }

    private static MacdResult AnalyzeMacd(IReadOnlyList<(DateTime Date, decimal Close)> prices, TrendReversalOptions options)
    {
        var n = prices.Count;
        var closes = new decimal[n];
        for (var i = 0; i < n; i++)
        {
            closes[i] = prices[i].Close;
        }

        var emaFast = Ema(closes, options.MacdFastPeriod);
        var emaSlow = Ema(closes, options.MacdSlowPeriod);

        // MACD line: defined from the first index where both EMAs exist.
        var macdStart = options.MacdSlowPeriod - 1;
        var macdLen = n - macdStart;
        if (macdLen <= 0)
        {
            return new MacdResult(null, null, null, false, null);
        }

        var macdLine = new decimal[macdLen];
        for (var i = 0; i < macdLen; i++)
        {
            macdLine[i] = emaFast[macdStart + i]!.Value - emaSlow[macdStart + i]!.Value;
        }

        var signalLine = Ema(macdLine, options.MacdSignalPeriod);
        var lastIdx = macdLen - 1;
        var macd = macdLine[lastIdx];
        var signal = signalLine[lastIdx];
        if (!signal.HasValue)
        {
            return new MacdResult(macd, null, null, false, null);
        }

        // Find the last index where the MACD line crossed from at/above to below the signal line.
        var crossoverIdx = -1;
        for (var i = 1; i < macdLen; i++)
        {
            if (signalLine[i].HasValue && signalLine[i - 1].HasValue
                && macdLine[i - 1] >= signalLine[i - 1]!.Value && macdLine[i] < signalLine[i]!.Value)
            {
                crossoverIdx = i;
            }
        }

        var bearish = macd < signal.Value;
        return new MacdResult(
            macd,
            signal,
            macd - signal.Value,
            bearish,
            bearish && crossoverIdx >= 0 ? prices[macdStart + crossoverIdx].Date : null);
    }

    private static RsiResult AnalyzeRsi(
        IReadOnlyList<(DateTime Date, decimal Close)> prices,
        TrendReversalOptions options,
        List<(int Idx, decimal Close)> swingHighs)
    {
        var n = prices.Count;
        var rsi = Rsi(prices, options.RsiPeriod);
        var latest = rsi[n - 1];

        // Bearish divergence: last price swing high above the previous one, but RSI at the second high lower.
        for (var i = swingHighs.Count - 1; i >= 1; i--)
        {
            var prev = swingHighs[i - 1];
            var curr = swingHighs[i];
            var prevRsi = rsi[prev.Idx];
            var currRsi = rsi[curr.Idx];
            if (!prevRsi.HasValue || !currRsi.HasValue)
            {
                continue;
            }
            if (curr.Close > prev.Close && currRsi.Value < prevRsi.Value)
            {
                return new RsiResult(latest, true, prices[curr.Idx].Date);
            }
            // Only the most recent comparable pair is evaluated.
            break;
        }

        return new RsiResult(latest, false, null);
    }

    private static decimal?[] Ema(decimal[] values, int period)
    {
        var result = new decimal?[values.Length];
        if (values.Length < period)
        {
            return result;
        }

        decimal sum = 0;
        for (var i = 0; i < period; i++)
        {
            sum += values[i];
        }
        var ema = sum / period;
        result[period - 1] = ema;

        var k = 2m / (period + 1);
        for (var i = period; i < values.Length; i++)
        {
            ema = (values[i] - ema) * k + ema;
            result[i] = ema;
        }
        return result;
    }

    private static decimal?[] Rsi(IReadOnlyList<(DateTime Date, decimal Close)> prices, int period)
    {
        var n = prices.Count;
        var result = new decimal?[n];
        if (n <= period)
        {
            return result;
        }

        decimal gainSum = 0, lossSum = 0;
        for (var i = 1; i <= period; i++)
        {
            var diff = prices[i].Close - prices[i - 1].Close;
            if (diff > 0) { gainSum += diff; }
            else { lossSum += -diff; }
        }
        var avgGain = gainSum / period;
        var avgLoss = lossSum / period;
        result[period] = Compute(avgGain, avgLoss);

        for (var i = period + 1; i < n; i++)
        {
            var diff = prices[i].Close - prices[i - 1].Close;
            var gain = diff > 0 ? diff : 0m;
            var loss = diff < 0 ? -diff : 0m;
            avgGain = (avgGain * (period - 1) + gain) / period;
            avgLoss = (avgLoss * (period - 1) + loss) / period;
            result[i] = Compute(avgGain, avgLoss);
        }
        return result;

        static decimal Compute(decimal avgGain, decimal avgLoss)
            => avgLoss == 0m ? 100m : 100m - 100m / (1m + avgGain / avgLoss);
    }
}
