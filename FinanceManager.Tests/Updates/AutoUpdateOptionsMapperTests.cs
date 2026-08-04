using FinanceManager.Shared.Dtos.Update;
using FinanceManager.Web.Services.Updates;
using FluentAssertions;
using msTools.Updater;

namespace FinanceManager.Tests.Updates;

public sealed class AutoUpdateOptionsMapperTests
{
    [Fact]
    public void ApplySettings_CopiesRuntimeRelevantFieldsOntoOptions()
    {
        var options = new AutoUpdateOptions();
        var settings = new UpdateSettingsDto(true, "owner", "repo", "update.json", new TimeOnly(20, 0), new TimeOnly(6, 0), new TimeOnly(3, 30), "svc", "/path/exe", "custom-updates", 90, true);

        AutoUpdateOptionsMapper.ApplySettings(options, settings);

        options.Enabled.Should().BeTrue();
        options.SourceCheck.Interval.Should().Be(AutoUpdateOptionsMapper.DailySourceCheckIntervalMinutes);
        options.SourceCheck.TimeRanges.Should().HaveCount(14);
        options.ServiceName.Should().Be("svc");
        options.ExecutablePath.Should().Be("/path/exe");
        options.DownloadPath.Should().Be("custom-updates");
        options.HealthTimeoutSeconds.Should().Be(90);
        options.ScheduledInstallTime.Should().Be(new TimeOnly(3, 30));
        options.AllowPrereleaseUpdates.Should().BeTrue();
    }

    [Fact]
    public void ToSettingsDto_ReflectsCurrentOptionsState()
    {
        var options = new AutoUpdateOptions
        {
            Enabled = true,
            ServiceName = "svc",
            ExecutablePath = "/path/exe",
            DownloadPath = "custom-updates",
            HealthTimeoutSeconds = 90,
            ScheduledInstallTime = new TimeOnly(3, 30),
            AllowPrereleaseUpdates = true,
        };
        options.SourceCheck.Interval = AutoUpdateOptionsMapper.DailySourceCheckIntervalMinutes;
        options.SourceCheck.TimeRanges = AutoUpdateOptionsMapper.BuildSourceCheckTimeRanges(new TimeOnly(20, 0), new TimeOnly(6, 0)).ToList();

        var dto = AutoUpdateOptionsMapper.ToSettingsDto(options, "owner", "repo", "update.json");

        dto.Enabled.Should().BeTrue();
        dto.RepositoryOwner.Should().Be("owner");
        dto.RepositoryName.Should().Be("repo");
        dto.ManifestAssetName.Should().Be("update.json");
        dto.SourceCheckStartTime.Should().Be(new TimeOnly(20, 0));
        dto.SourceCheckEndTime.Should().Be(new TimeOnly(6, 0));
        dto.ServiceName.Should().Be("svc");
        dto.ExecutablePath.Should().Be("/path/exe");
        dto.WorkingDirectory.Should().Be("custom-updates");
        dto.HealthTimeoutSeconds.Should().Be(90);
        dto.ScheduledInstallTime.Should().Be(new TimeOnly(3, 30));
        dto.IncludePrereleases.Should().BeTrue();
    }

    [Fact]
    public void ApplySettings_ThenToSettingsDto_RoundTripsRuntimeRelevantFields()
    {
        var options = new AutoUpdateOptions();
        var original = new UpdateSettingsDto(false, "owner", "repo", "update.json", new TimeOnly(20, 0), new TimeOnly(6, 0), null, null, null, "updates", 30, true);

        AutoUpdateOptionsMapper.ApplySettings(options, original);
        var roundTripped = AutoUpdateOptionsMapper.ToSettingsDto(options, original.RepositoryOwner, original.RepositoryName, original.ManifestAssetName);

        roundTripped.Should().Be(original);
    }

    [Fact]
    public void ApplySettings_WhenSourceIsGithubSource_ReplacesSourceWithUpdatedRepository()
    {
        var options = new AutoUpdateOptions { Source = AutoUpdateGithubSource.Create("old-owner", "old-repo") };
        var previousSource = options.Source;
        var settings = new UpdateSettingsDto(true, "new-owner", "new-repo", "manifest.json", new TimeOnly(20, 0), new TimeOnly(6, 0), null, null, null, "updates", 120, true);

        AutoUpdateOptionsMapper.ApplySettings(options, settings);

        options.Source.Should().NotBeSameAs(previousSource);
        options.Source.Should().BeOfType<AutoUpdateGithubSource>();
        options.AllowPrereleaseUpdates.Should().BeTrue();
        ReadGithubIncludePrereleases((AutoUpdateGithubSource)options.Source!).Should().BeTrue();
    }

    [Fact]
    public void ApplySettings_WhenSourceIsNotGithubSource_LeavesSourceUnchanged()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            var localSource = new AutoUpdateLocalFolderSource(dir.FullName);
            var options = new AutoUpdateOptions { Source = localSource };
            var settings = new UpdateSettingsDto(true, "owner", "repo", "manifest.json", new TimeOnly(20, 0), new TimeOnly(6, 0), null, null, null, "updates", 120, true);

            AutoUpdateOptionsMapper.ApplySettings(options, settings);

            options.Source.Should().BeSameAs(localSource);
            options.AllowPrereleaseUpdates.Should().BeTrue();
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public void BuildSourceCheckTimeRanges_WhenWindowCrossesMidnight_AllowsBothPartsOnEachDay()
    {
        var ranges = AutoUpdateOptionsMapper.BuildSourceCheckTimeRanges(new TimeOnly(20, 0), new TimeOnly(6, 0));
        var evaluator = new SourceCheckWindowEvaluator();

        ranges.Should().HaveCount(14);
        evaluator.IsWithinWindow(ranges, new DateTimeOffset(2026, 8, 3, 21, 0, 0, TimeSpan.Zero)).Should().BeTrue();
        evaluator.IsWithinWindow(ranges, new DateTimeOffset(2026, 8, 4, 2, 0, 0, TimeSpan.Zero)).Should().BeTrue();
        evaluator.IsWithinWindow(ranges, new DateTimeOffset(2026, 8, 4, 12, 0, 0, TimeSpan.Zero)).Should().BeFalse();
    }

    private static bool ReadGithubIncludePrereleases(AutoUpdateGithubSource source)
        => (bool)typeof(AutoUpdateGithubSource)
            .GetField("_includePrereleases", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .GetValue(source)!;
}
