using FinanceManager.Domain.Securities;

namespace FinanceManager.Application.Securities.GoldenCross;

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
public sealed record GoldenCrossAnalysis(
    GoldenCrossPhase Phase,
    DateTime? AsOfDate,
    decimal? ShortAverage,
    decimal? LongAverage,
    decimal? DistancePercent,
    DateTime? CrossDate);

/// <summary>
/// Pure calculation of short-/long-term moving averages and golden cross classification.
/// </summary>
public static class GoldenCrossAnalyzer
{
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
        if (n < options.LongWindow)
        {
            return new GoldenCrossAnalysis(GoldenCrossPhase.InsufficientData, asOf, null, null, null, null);
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
            return new GoldenCrossAnalysis(GoldenCrossPhase.InsufficientData, asOf, shortAvg, longAvg, null, null);
        }

        var distance = (shortAvg - longAvg) / longAvg * 100m;

        DateTime? crossDate = null;
        if (shortAvg >= longAvg)
        {
            for (var end = n; end > options.LongWindow; end--)
            {
                var prevShort = Sma(end - 1, options.ShortWindow);
                var prevLong = Sma(end - 1, options.LongWindow);
                if (prevShort < prevLong)
                {
                    crossDate = prices[end - 1].Date;
                    break;
                }
            }
        }

        var phase = shortAvg >= longAvg
            ? GoldenCrossPhase.Crossed
            : -distance <= options.ApproachThresholdPercent
                ? GoldenCrossPhase.Approaching
                : GoldenCrossPhase.Far;

        return new GoldenCrossAnalysis(phase, asOf, shortAvg, longAvg, distance, crossDate);
    }
}
