using FinanceManager.Application.Securities.TrendReversal;
using FluentAssertions;

namespace FinanceManager.Tests.Securities;

/// <summary>
/// Unit tests for the indicator-based signals (MACD crossover, RSI divergence) of <see cref="TrendReversalAnalyzer"/>.
/// </summary>
public sealed class TrendReversalAnalyzerTests_Momentum
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

    /// <summary>A MACD line crossing from above to below the signal line yields a bearish crossover signal.</summary>
    [Fact]
    public void Analyze_MacdCrossesBelowSignal_BearishCrossover()
    {
        // Rise over ten days, then a decline: the fast EMA falls below the slow EMA and the MACD
        // line crosses below its signal line near the end of the series.
        var result = TrendReversalAnalyzer.Analyze(
            Series(100, 101, 102, 103, 104, 105, 106, 107, 108, 109, 107, 105, 103, 101, 99, 97), Options);

        result.Macd.Should().NotBeNull();
        result.Macd!.BearishCrossover.Should().BeTrue();
        result.Macd.Histogram.Should().BeLessThan(0);
        result.Macd.CrossoverDate.Should().NotBeNull();
        result.SignalList.Should().Contain(s => s.Type == TrendReversalSignalType.MacdBearishCrossover && s.Strength == TrendReversalSignalStrength.Moderate);
    }

    /// <summary>A steadily rising series keeps the MACD line above the signal line - no crossover signal.</summary>
    [Fact]
    public void Analyze_MacdAboveSignal_NoCrossover()
    {
        var result = TrendReversalAnalyzer.Analyze(
            Series(90, 91, 92, 93, 94, 95, 96, 97, 98, 99, 100, 101), Options);

        result.Macd!.BearishCrossover.Should().BeFalse();
        result.SignalList.Should().NotContain(s => s.Type == TrendReversalSignalType.MacdBearishCrossover);
    }

    /// <summary>A higher price swing high at a lower RSI value yields a bearish divergence signal.</summary>
    [Fact]
    public void Analyze_RsiLowerHighAtHigherPriceHigh_BearishDivergence()
    {
        // Strong rally to the first swing high (110), shallow pullback, weak grind to a slightly higher
        // high (111) with clearly lower RSI at the second high.
        var result = TrendReversalAnalyzer.Analyze(
            Series(90, 95, 100, 105, 110, 104, 106, 108, 111, 105, 100), Options);

        result.Rsi.Should().NotBeNull();
        result.Rsi!.BearishDivergence.Should().BeTrue();
        result.Rsi.DivergenceDate.Should().Be(new DateTime(2024, 1, 9));
        result.SignalList.Should().Contain(s => s.Type == TrendReversalSignalType.RsiBearishDivergence && s.Strength == TrendReversalSignalStrength.Moderate);
    }

    /// <summary>When the RSI confirms the higher price high there is no divergence signal.</summary>
    [Fact]
    public void Analyze_RsiConfirmsHighs_NoDivergence()
    {
        // First swing high arrives after a decline (low RSI), second higher high after fresh gains
        // (higher RSI) - the RSI confirms the price high.
        var result = TrendReversalAnalyzer.Analyze(
            Series(110, 105, 100, 108, 112, 106, 110, 114, 118, 116), Options);

        result.Rsi!.BearishDivergence.Should().BeFalse();
        result.SignalList.Should().NotContain(s => s.Type == TrendReversalSignalType.RsiBearishDivergence);
    }
}
