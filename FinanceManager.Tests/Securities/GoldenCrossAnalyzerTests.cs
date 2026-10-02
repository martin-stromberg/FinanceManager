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

    private static readonly GoldenCrossOptions ExtendedOptions = new() { ShortWindow = 2, LongWindow = 10, ApproachThresholdPercent = 3m };

    private static IReadOnlyList<(DateTime Date, decimal Close)> CrossedSeries(params decimal[] afterCross)
    {
        // Short SMA (100) below long SMA (107) for the first ten prices; the jump to 130 crosses at index 10 (2024-01-11).
        var closes = Enumerable.Repeat(110m, 7).Concat(Enumerable.Repeat(100m, 3)).Concat(new[] { 130m }).Concat(afterCross).ToArray();
        return Series(closes);
    }

    /// <summary>Extended analysis is only populated when a cross date is known.</summary>
    [Fact]
    public void Analyze_WithoutCross_HasNoExtendedAnalysis()
    {
        var result = GoldenCrossAnalyzer.Analyze(Series(100, 100, 100, 80, 80, 80), Options);

        result.Windows.Should().BeEmpty();
        result.Retest.Should().BeNull();
        result.Trend.Should().BeNull();
        result.DaysSinceCross.Should().BeNull();
    }

    /// <summary>The three entry windows are derived from the cross date with fixed calendar-day offsets.</summary>
    [Fact]
    public void Analyze_AfterCross_BuildsThreeEntryWindows()
    {
        var result = GoldenCrossAnalyzer.Analyze(CrossedSeries(131, 132), ExtendedOptions);
        var cross = new DateTime(2024, 1, 11);

        result.CrossDate.Should().Be(cross);
        result.DaysSinceCross.Should().Be(2);
        result.Windows.Should().HaveCount(3);

        var early = result.Windows.Single(w => w.Scenario == GoldenCrossEntryScenario.Early);
        early.StartDate.Should().Be(cross);
        early.EndDate.Should().Be(cross.AddDays(5));
        early.Status.Should().Be(GoldenCrossWindowStatus.Open);
        early.Confirmed.Should().BeTrue();
        early.SignalDate.Should().Be(cross);
        early.LowClose.Should().Be(130m);
        early.HighClose.Should().Be(132m);

        var average = result.Windows.Single(w => w.Scenario == GoldenCrossEntryScenario.Average);
        average.StartDate.Should().Be(cross.AddDays(5));
        average.EndDate.Should().Be(cross.AddDays(30));
        average.Status.Should().Be(GoldenCrossWindowStatus.Upcoming);
        average.Confirmed.Should().BeFalse();

        var late = result.Windows.Single(w => w.Scenario == GoldenCrossEntryScenario.Late);
        late.StartDate.Should().Be(cross.AddDays(30));
        late.EndDate.Should().Be(cross.AddDays(60));
        late.Status.Should().Be(GoldenCrossWindowStatus.Upcoming);
    }

    /// <summary>Without a pullback the retest status is <see cref="GoldenCrossRetestStatus.None"/>.</summary>
    [Fact]
    public void Analyze_WithoutPullback_ReportsNoRetest()
    {
        var result = GoldenCrossAnalyzer.Analyze(CrossedSeries(135, 140, 145), ExtendedOptions);

        result.Retest!.Status.Should().Be(GoldenCrossRetestStatus.None);
        result.Retest.Line.Should().BeNull();
    }

    /// <summary>A pullback to the short average that bounces back confirms the average entry scenario.</summary>
    [Fact]
    public void Analyze_PullbackToShortLineWithBounce_ReportsSuccessfulRetest()
    {
        // After the cross: 134, 138, then a dip to 130 (touching the short SMA, well above the long SMA) and recovery to 142/146.
        var result = GoldenCrossAnalyzer.Analyze(CrossedSeries(134, 138, 130, 142, 146), ExtendedOptions);

        result.Retest!.Status.Should().Be(GoldenCrossRetestStatus.Successful);
        result.Retest.Line.Should().Be(GoldenCrossLine.Short);
        result.Retest.StartDate.Should().Be(new DateTime(2024, 1, 14));
        result.Retest.LowClose.Should().Be(130m);
        result.Retest.EndDate.Should().Be(new DateTime(2024, 1, 15));

        var average = result.Windows.Single(w => w.Scenario == GoldenCrossEntryScenario.Average);
        average.Confirmed.Should().BeTrue();
        average.SignalDate.Should().Be(result.Retest.EndDate);
    }

    /// <summary>A pullback that is still at or below the tested line is reported as in progress.</summary>
    [Fact]
    public void Analyze_OngoingPullback_ReportsInProgress()
    {
        var result = GoldenCrossAnalyzer.Analyze(CrossedSeries(134, 138, 130), ExtendedOptions);

        result.Retest!.Status.Should().Be(GoldenCrossRetestStatus.InProgress);
        result.Retest.EndDate.Should().BeNull();
    }

    /// <summary>Too few prices since the cross leave the trend structure unknown.</summary>
    [Fact]
    public void Analyze_ShortlyAfterCross_TrendIsUnknown()
    {
        var result = GoldenCrossAnalyzer.Analyze(CrossedSeries(131, 132), ExtendedOptions);

        result.Trend!.Structure.Should().Be(GoldenCrossTrendStructure.Unknown);
        result.Windows.Single(w => w.Scenario == GoldenCrossEntryScenario.Late).Confirmed.Should().BeFalse();
    }

    /// <summary>Higher highs and higher lows classify a stable uptrend and confirm the late entry scenario.</summary>
    [Fact]
    public void Analyze_HigherHighsAndLows_ReportsStableUptrend()
    {
        // Swing highs 140 -> 148 -> 155 and swing lows 135 -> 142 -> 149; shallow pullbacks keep the short SMA above the long SMA.
        var result = GoldenCrossAnalyzer.Analyze(
            CrossedSeries(132, 134, 136, 138, 140, 137, 135, 139, 142, 145, 148, 144, 142, 146, 149, 152, 155, 151, 149, 153, 156, 160),
            ExtendedOptions);

        result.Trend!.Structure.Should().Be(GoldenCrossTrendStructure.StableUptrend);
        result.Trend.HigherHighs.Should().BeGreaterThanOrEqualTo(1);
        result.Trend.HigherLows.Should().BeGreaterThanOrEqualTo(1);
        result.Trend.ConfirmedDate.Should().NotBeNull();
        result.Trend.ChangeSinceCrossPercent.Should().BeGreaterThan(0);

        var late = result.Windows.Single(w => w.Scenario == GoldenCrossEntryScenario.Late);
        late.Confirmed.Should().BeTrue();
        late.SignalDate.Should().Be(result.Trend.ConfirmedDate);
    }

    /// <summary>Prices without meaningful progress since the cross are classified as a sideways phase.</summary>
    [Fact]
    public void Analyze_NoProgressSinceCross_ReportsSideways()
    {
        var result = GoldenCrossAnalyzer.Analyze(
            CrossedSeries(130, 130, 130, 130, 130, 130, 130, 130, 130, 130, 130, 130, 131),
            ExtendedOptions);

        result.Trend!.Structure.Should().Be(GoldenCrossTrendStructure.Sideways);
    }
}
