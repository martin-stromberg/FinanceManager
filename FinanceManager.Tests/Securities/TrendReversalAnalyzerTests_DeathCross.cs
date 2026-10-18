using FinanceManager.Application.Securities.TrendReversal;
using FinanceManager.Domain.Securities;
using FluentAssertions;

namespace FinanceManager.Tests.Securities;

/// <summary>
/// Unit tests for the death cross detection of <see cref="TrendReversalAnalyzer"/>.
/// </summary>
public sealed class TrendReversalAnalyzerTests_DeathCross
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

    /// <summary>When the short average drops below the long average the most recent crossing date is reported.</summary>
    [Fact]
    public void Analyze_BelowLongAverage_FindsDeathCrossDate()
    {
        // Uptrend then collapse: at index 6 the short SMA (110) is still above the long SMA (106.67),
        // at index 7 it has fallen below (100 < 101.67) - the death cross date is the first day below (2024-01-08).
        var result = TrendReversalAnalyzer.Analyze(Series(90, 90, 90, 110, 110, 110, 110, 80, 70), Options);

        result.DeathCross.Should().NotBeNull();
        result.DeathCross!.Active.Should().BeTrue();
        result.DeathCross.CrossDate.Should().Be(new DateTime(2024, 1, 8));
        result.DeathCross.DaysSinceCross.Should().Be(1);
        result.SignalList.Should().Contain(s => s.Type == TrendReversalSignalType.DeathCross && s.Strength == TrendReversalSignalStrength.Strong);
    }

    /// <summary>A short average at or above the long average produces no death cross signal.</summary>
    [Fact]
    public void Analyze_ShortAboveLong_NoDeathCross()
    {
        var result = TrendReversalAnalyzer.Analyze(Series(90, 90, 90, 110, 110, 110), Options);

        result.DeathCross!.Active.Should().BeFalse();
        result.DeathCross.CrossDate.Should().BeNull();
        result.SignalList.Should().NotContain(s => s.Type == TrendReversalSignalType.DeathCross);
    }

    /// <summary>Fewer prices than the long window yield <see cref="TrendStatus.InsufficientData"/>.</summary>
    [Fact]
    public void Analyze_TooFewPrices_ReturnsInsufficientData()
    {
        var result = TrendReversalAnalyzer.Analyze(Series(10, 11, 12, 13, 14), Options);

        result.Status.Should().Be(TrendStatus.InsufficientData);
        result.ShortAverage.Should().BeNull();
        result.LongAverage.Should().BeNull();
        result.AsOfDate.Should().Be(new DateTime(2024, 1, 5));
        result.SignalList.Should().BeEmpty();
        result.SignalScore.Should().Be(0);
        result.DeathCross.Should().BeNull();
    }

    /// <summary>A short window not smaller than the long window is rejected.</summary>
    [Fact]
    public void Analyze_WithShortWindowNotSmallerThanLong_Throws()
    {
        var bad = new TrendReversalOptions { ShortWindow = 10, LongWindow = 10 };

        var act = () => TrendReversalAnalyzer.Analyze(Series(1, 2, 3), bad);

        act.Should().Throw<ArgumentException>();
    }

    /// <summary>A MACD fast period not smaller than the slow period is rejected.</summary>
    [Fact]
    public void Analyze_WithMacdFastNotSmallerThanSlow_Throws()
    {
        var bad = new TrendReversalOptions { MacdFastPeriod = 26, MacdSlowPeriod = 12 };

        var act = () => TrendReversalAnalyzer.Analyze(Series(1, 2, 3), bad);

        act.Should().Throw<ArgumentException>();
    }

    /// <summary>A non-positive reversal threshold is rejected.</summary>
    [Fact]
    public void Analyze_WithNonPositiveReversalThreshold_Throws()
    {
        var bad = new TrendReversalOptions { ReversalLikelyThreshold = 0 };

        var act = () => TrendReversalAnalyzer.Analyze(Series(1, 2, 3), bad);

        act.Should().Throw<ArgumentException>();
    }
}
