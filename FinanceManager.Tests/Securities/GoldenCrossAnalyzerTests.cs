using FinanceManager.Application.Securities.GoldenCross;
using FinanceManager.Domain.Securities;
using FluentAssertions;

namespace FinanceManager.Tests.Securities;

/// <summary>
/// Unit tests for <see cref="GoldenCrossAnalyzer"/> covering moving-average computation and phase classification.
/// </summary>
public sealed class GoldenCrossAnalyzerTests
{
    private static readonly GoldenCrossOptions Options = new() { ShortWindow = 3, LongWindow = 6, ApproachThresholdPercent = 3m };

    private static IReadOnlyList<(DateTime Date, decimal Close)> Series(params decimal[] closes)
    {
        var start = new DateTime(2024, 1, 1);
        return closes.Select((c, i) => (start.AddDays(i), c)).ToList();
    }

    /// <summary>Fewer prices than the long window yield <see cref="GoldenCrossPhase.InsufficientData"/>.</summary>
    [Fact]
    public void Analyze_WithTooFewPrices_ReturnsInsufficientData()
    {
        var result = GoldenCrossAnalyzer.Analyze(Series(10, 11, 12, 13, 14), Options);

        result.Phase.Should().Be(GoldenCrossPhase.InsufficientData);
        result.ShortAverage.Should().BeNull();
        result.LongAverage.Should().BeNull();
        result.AsOfDate.Should().Be(new DateTime(2024, 1, 5));
    }

    /// <summary>Averages are computed over the trailing windows of the series.</summary>
    [Fact]
    public void Analyze_ComputesTrailingMovingAverages()
    {
        // long window (6): 10,10,10,10,10,10 -> 10; short window (3): 10,10,10 -> 10
        var result = GoldenCrossAnalyzer.Analyze(Series(50, 50, 10, 10, 10, 10, 10, 10), Options);

        result.ShortAverage.Should().Be(10m);
        result.LongAverage.Should().Be(10m);
        result.DistancePercent.Should().Be(0m);
        result.Phase.Should().Be(GoldenCrossPhase.Crossed);
    }

    /// <summary>A short average far below the long average is classified as <see cref="GoldenCrossPhase.Far"/>.</summary>
    [Fact]
    public void Analyze_ShortWellBelowLong_ReturnsFar()
    {
        // long: (100+100+100+80+80+80)/6 = 90; short: 80 -> -11.1%
        var result = GoldenCrossAnalyzer.Analyze(Series(100, 100, 100, 80, 80, 80), Options);

        result.Phase.Should().Be(GoldenCrossPhase.Far);
        result.DistancePercent.Should().BeLessThan(-3m);
        result.CrossDate.Should().BeNull();
    }

    /// <summary>A short average slightly below the long average is classified as <see cref="GoldenCrossPhase.Approaching"/>.</summary>
    [Fact]
    public void Analyze_ShortSlightlyBelowLong_ReturnsApproaching()
    {
        // long: (102+102+102+99+99+99)/6 = 100.5; short: 99 -> -1.49%
        var result = GoldenCrossAnalyzer.Analyze(Series(102, 102, 102, 99, 99, 99), Options);

        result.Phase.Should().Be(GoldenCrossPhase.Approaching);
        result.DistancePercent.Should().BeInRange(-3m, 0m);
    }

    /// <summary>When the short average rises above the long average the most recent crossing date is reported.</summary>
    [Fact]
    public void Analyze_ShortAboveLong_ReturnsCrossedWithCrossDate()
    {
        // Downtrend then strong recovery: day 7 short=90 < long=91.67, day 8 short=100 > long=95 -> cross on day 8.
        var result = GoldenCrossAnalyzer.Analyze(Series(100, 100, 100, 90, 90, 90, 90, 120, 130), Options);

        result.Phase.Should().Be(GoldenCrossPhase.Crossed);
        result.ShortAverage.Should().BeGreaterThan(result.LongAverage!.Value);
        result.CrossDate.Should().Be(new DateTime(2024, 1, 8));
    }

    /// <summary>Invalid window configuration is rejected.</summary>
    [Fact]
    public void Analyze_WithShortWindowNotSmallerThanLong_Throws()
    {
        var bad = new GoldenCrossOptions { ShortWindow = 10, LongWindow = 10 };

        var act = () => GoldenCrossAnalyzer.Analyze(Series(1, 2, 3), bad);

        act.Should().Throw<ArgumentException>();
    }
}
