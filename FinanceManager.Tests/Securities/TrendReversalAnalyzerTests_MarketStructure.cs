using FinanceManager.Application.Securities.TrendReversal;
using FluentAssertions;

namespace FinanceManager.Tests.Securities;

/// <summary>
/// Unit tests for the market structure (swing highs/lows) analysis of <see cref="TrendReversalAnalyzer"/>.
/// </summary>
public sealed class TrendReversalAnalyzerTests_MarketStructure
{
    private static readonly TrendReversalOptions Options = new()
    {
        ShortWindow = 3,
        LongWindow = 8,
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

    /// <summary>Two lower swing highs and two lower swing lows classify an emerging downtrend.</summary>
    [Fact]
    public void Analyze_LowerHighsAndLows_EmergingDowntrend()
    {
        // Swing highs 105, 102, 100, 96 and swing lows 98, 95, 90 - all falling.
        var result = TrendReversalAnalyzer.Analyze(Series(100, 105, 98, 102, 95, 100, 90, 96, 85), Options);

        result.MarketStructure!.Classification.Should().Be(MarketStructureClassification.EmergingDowntrend);
        result.MarketStructure.SwingHighs.Should().BeGreaterThanOrEqualTo(2);
        result.MarketStructure.SwingLows.Should().BeGreaterThanOrEqualTo(2);
        result.MarketStructure.LowerHighs.Should().BeGreaterThanOrEqualTo(1);
        result.MarketStructure.LowerLows.Should().BeGreaterThanOrEqualTo(1);
        result.SignalList.Should().Contain(s => s.Type == TrendReversalSignalType.BearishMarketStructure && s.Strength == TrendReversalSignalStrength.Strong);
    }

    /// <summary>Lower swing lows while swing highs keep rising classify a trend loss.</summary>
    [Fact]
    public void Analyze_LowerLowsOnly_TrendLoss()
    {
        // Swing highs 105, 110, 115, 120 rising; swing lows 98, 90, 85 falling.
        var result = TrendReversalAnalyzer.Analyze(Series(100, 105, 98, 110, 90, 115, 85, 120, 95), Options);

        result.MarketStructure!.Classification.Should().Be(MarketStructureClassification.TrendLoss);
        result.SignalList.Should().Contain(s => s.Type == TrendReversalSignalType.BearishMarketStructure && s.Strength == TrendReversalSignalStrength.Moderate);
    }

    /// <summary>A negative drift without a lower-low sequence is classified as a bearish sideways phase.</summary>
    [Fact]
    public void Analyze_NegativeDriftWithoutStructure_SidewaysBearish()
    {
        // Swing highs falling (104, 102, 101, 100) but swing lows rising (96, 97, 98), overall change negative.
        var result = TrendReversalAnalyzer.Analyze(Series(100, 104, 96, 102, 97, 101, 98, 100, 97), Options);

        result.MarketStructure!.Classification.Should().Be(MarketStructureClassification.SidewaysBearish);
        result.MarketStructure.ChangePercent.Should().BeLessThan(0);
        result.SignalList.Should().Contain(s => s.Type == TrendReversalSignalType.BearishMarketStructure && s.Strength == TrendReversalSignalStrength.Weak);
    }

    /// <summary>Higher highs and higher lows with a positive drift leave the structure intact.</summary>
    [Fact]
    public void Analyze_Uptrend_Intact()
    {
        var result = TrendReversalAnalyzer.Analyze(Series(90, 95, 92, 100, 97, 105, 102, 110, 108, 112), Options);

        result.MarketStructure!.Classification.Should().Be(MarketStructureClassification.Intact);
        result.SignalList.Should().NotContain(s => s.Type == TrendReversalSignalType.BearishMarketStructure);
    }
}
