using FluentAssertions;
using SoftwareSchmiede.AutoUpdate.Tests.TestSupport;

namespace SoftwareSchmiede.AutoUpdate.Tests;

public sealed class AutoUpdateOptionsValidationTests
{
    [Fact]
    public void Validate_WithInvalidInterval_Fails()
    {
        var options = ValidOptions();
        options.SourceCheck.Interval = 0;

        var result = new AutoUpdateOptionsValidator().Validate(null, options);

        result.Failed.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithInvertedTimeRange_Fails()
    {
        var options = ValidOptions();
        options.SourceCheck.TimeRanges.Add(new SourceCheckTimeRange { DayOfWeek = DayOfWeek.Monday, StartTime = new TimeOnly(18, 0), EndTime = new TimeOnly(8, 0) });

        var result = new AutoUpdateOptionsValidator().Validate(null, options);

        result.Failed.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithEmptyDownloadPath_Fails()
    {
        var options = ValidOptions();
        options.DownloadPath = "";

        var result = new AutoUpdateOptionsValidator().Validate(null, options);

        result.Failed.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithNonPositiveMaxAssetBytes_Fails()
    {
        var options = ValidOptions();
        options.MaxAssetBytes = 0;

        var result = new AutoUpdateOptionsValidator().Validate(null, options);

        result.Failed.Should().BeTrue();
    }

    private static AutoUpdateOptions ValidOptions() => new()
    {
        DownloadPath = "updates",
        Source = new FakeAutoUpdateSource(),
        MaxAssetBytes = 1024,
        SourceCheck = new SourceCheckOptions { Interval = 60 }
    };
}
