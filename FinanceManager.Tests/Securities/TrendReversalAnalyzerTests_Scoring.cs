using FinanceManager.Application.Securities.TrendReversal;
using FinanceManager.Domain.Securities;
using FluentAssertions;

namespace FinanceManager.Tests.Securities;

/// <summary>
/// Unit tests for the signal score aggregation and status classification of <see cref="TrendReversalAnalyzer"/>.
/// </summary>
public sealed class TrendReversalAnalyzerTests_Scoring
{
    private static readonly TrendReversalOptions Options = new()
    {
        ShortWindow = 3,
        LongWindow = 6,
        StructuralBreakDays = 3,
        // MACD/RSI deliberately inactive for the scoring fixtures (periods longer than the series).
        MacdFastPeriod = 2,
        MacdSlowPeriod = 10,
        MacdSignalPeriod = 2,
        RsiPeriod = 10,
        MarketStructureLookbackDays = 30,
        SwingLookaround = 1,
        ReversalLikelyThreshold = 4
    };

    private static IReadOnlyList<(DateTime Date, decimal Close)> Series(params decimal[] closes)
    {
        var start = new DateTime(2024, 1, 1);
        return closes.Select((c, i) => (start.AddDays(i), c)).ToList();
    }

    /// <summary>A series without any signal yields score 0 and <see cref="TrendStatus.Intact"/>.</summary>
    [Fact]
    public void Analyze_NoSignals_StatusIntact()
    {
        // Monotone rise: short average above long average, close above the line, no swing structure,
        // no bearish indicator signal.
        var result = TrendReversalAnalyzer.Analyze(Series(90, 92, 94, 96, 98, 100, 102), Options);

        result.Status.Should().Be(TrendStatus.Intact);
        result.SignalList.Should().BeEmpty();
        result.SignalScore.Should().Be(0);
    }

    /// <summary>A single weak signal yields a score below the threshold and <see cref="TrendStatus.Weakened"/>.</summary>
    [Fact]
    public void Analyze_SingleWeakSignal_StatusWeakened()
    {
        // Only the latest close dropped below the long average (one day, below the structural threshold):
        // short-term weakness, score 1.
        var result = TrendReversalAnalyzer.Analyze(Series(90, 100, 110, 110, 110, 90), Options);

        result.Status.Should().Be(TrendStatus.Weakened);
        result.SignalScore.Should().Be((int)TrendReversalSignalStrength.Weak);
        result.SignalList.Should().ContainSingle();
        result.SignalList[0].Type.Should().Be(TrendReversalSignalType.LongTermLineBreak);
        result.SignalList[0].Strength.Should().Be(TrendReversalSignalStrength.Weak);
    }

    /// <summary>Combined strong signals reach the threshold and yield <see cref="TrendStatus.ReversalLikely"/>.</summary>
    [Fact]
    public void Analyze_CombinedStrongSignals_StatusReversalLikely()
    {
        // Oscillating decline: death cross plus structural line break plus emerging downtrend.
        var result = TrendReversalAnalyzer.Analyze(Series(100, 105, 98, 102, 95, 100, 90, 96, 85), Options);

        result.Status.Should().Be(TrendStatus.ReversalLikely);
        result.SignalScore.Should().BeGreaterThanOrEqualTo(Options.ReversalLikelyThreshold);
        result.SignalList.Should().Contain(s => s.Type == TrendReversalSignalType.DeathCross);
        result.SignalList.Should().Contain(s => s.Type == TrendReversalSignalType.LongTermLineBreak);
        result.SignalList.Should().Contain(s => s.Type == TrendReversalSignalType.BearishMarketStructure);
    }
}
