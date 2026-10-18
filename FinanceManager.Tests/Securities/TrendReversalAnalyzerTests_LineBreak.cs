using FinanceManager.Application.Securities.TrendReversal;
using FluentAssertions;

namespace FinanceManager.Tests.Securities;

/// <summary>
/// Unit tests for the long-term moving average break analysis of <see cref="TrendReversalAnalyzer"/>.
/// </summary>
public sealed class TrendReversalAnalyzerTests_LineBreak
{
    private static readonly TrendReversalOptions Options = new()
    {
        ShortWindow = 3,
        LongWindow = 6,
        StructuralBreakDays = 3,
        MacdFastPeriod = 2,
        MacdSlowPeriod = 4,
        MacdSignalPeriod = 2,
        RsiPeriod = 3,
        MarketStructureLookbackDays = 30,
        SwingLookaround = 1,
        ReversalLikelyThreshold = 4
    };

    private static IReadOnlyList<(DateTime Date, decimal Close)> Series(params decimal[] closes)
    {
        var start = new DateTime(2024, 1, 1);
        return closes.Select((c, i) => (start.AddDays(i), c)).ToList();
    }

    /// <summary>A close below the long average for fewer days than the threshold is short-term weakness.</summary>
    [Fact]
    public void Analyze_CloseBelowLongFewDays_ShortTermWeakness()
    {
        // Short SMA (101.67) stays above the long SMA (100.83): no death cross, single day below the line.
        var result = TrendReversalAnalyzer.Analyze(Series(90, 95, 100, 105, 110, 115, 80), Options);

        result.LineBreak!.Classification.Should().Be(LineBreakClassification.ShortTermWeakness);
        result.LineBreak.ConsecutiveDaysBelow.Should().Be(1);
        result.LineBreak.BreakDate.Should().Be(new DateTime(2024, 1, 7));
        result.LineBreak.PercentBelow.Should().BeLessThan(0);
        result.SignalList.Should().Contain(s => s.Type == TrendReversalSignalType.LongTermLineBreak && s.Strength == TrendReversalSignalStrength.Weak);
    }

    /// <summary>A close below the long average for at least the threshold days is a structural reversal.</summary>
    [Fact]
    public void Analyze_CloseBelowLongManyDays_StructuralReversal()
    {
        var result = TrendReversalAnalyzer.Analyze(Series(90, 95, 100, 105, 110, 115, 85, 80, 75), Options);

        result.LineBreak!.Classification.Should().Be(LineBreakClassification.StructuralReversal);
        result.LineBreak.ConsecutiveDaysBelow.Should().Be(3);
        result.LineBreak.BreakDate.Should().Be(new DateTime(2024, 1, 7));
        result.SignalList.Should().Contain(s => s.Type == TrendReversalSignalType.LongTermLineBreak && s.Strength == TrendReversalSignalStrength.Strong);
    }

    /// <summary>A close above the long average produces no line break signal.</summary>
    [Fact]
    public void Analyze_CloseAboveLong_NoBreak()
    {
        var result = TrendReversalAnalyzer.Analyze(Series(90, 95, 100, 105, 110, 115, 120), Options);

        result.LineBreak!.Classification.Should().Be(LineBreakClassification.None);
        result.LineBreak.ConsecutiveDaysBelow.Should().Be(0);
        result.LineBreak.BreakDate.Should().BeNull();
        result.SignalList.Should().NotContain(s => s.Type == TrendReversalSignalType.LongTermLineBreak);
    }
}
