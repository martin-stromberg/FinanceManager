using FluentAssertions;
using SoftwareSchmiede.AutoUpdate;

namespace SoftwareSchmiede.AutoUpdate.Tests;

public sealed class SourceCheckWindowEvaluatorTests
{
    [Fact]
    public void IsWithinWindow_WithoutRanges_AlwaysTrue()
    {
        var evaluator = new SourceCheckWindowEvaluator();

        evaluator.IsWithinWindow(new List<SourceCheckTimeRange>(), DateTimeOffset.UtcNow).Should().BeTrue();
    }

    [Fact]
    public void IsWithinWindow_InsideRange_ReturnsTrue()
    {
        var evaluator = new SourceCheckWindowEvaluator();
        var monday = new DateTimeOffset(2026, 7, 27, 10, 0, 0, TimeSpan.Zero);
        var ranges = new List<SourceCheckTimeRange>
        {
            new() { DayOfWeek = DayOfWeek.Monday, StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(18, 0) }
        };

        evaluator.IsWithinWindow(ranges, monday).Should().BeTrue();
    }

    [Fact]
    public void IsWithinWindow_WrongDayOfWeek_ReturnsFalse()
    {
        var evaluator = new SourceCheckWindowEvaluator();
        var tuesday = new DateTimeOffset(2026, 7, 28, 10, 0, 0, TimeSpan.Zero);
        var ranges = new List<SourceCheckTimeRange>
        {
            new() { DayOfWeek = DayOfWeek.Monday, StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(18, 0) }
        };

        evaluator.IsWithinWindow(ranges, tuesday).Should().BeFalse();
    }

    [Fact]
    public void IsWithinWindow_OutsideRange_ReturnsFalse()
    {
        var evaluator = new SourceCheckWindowEvaluator();
        var mondayEvening = new DateTimeOffset(2026, 7, 27, 20, 0, 0, TimeSpan.Zero);
        var ranges = new List<SourceCheckTimeRange>
        {
            new() { DayOfWeek = DayOfWeek.Monday, StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(18, 0) }
        };

        evaluator.IsWithinWindow(ranges, mondayEvening).Should().BeFalse();
    }
}
