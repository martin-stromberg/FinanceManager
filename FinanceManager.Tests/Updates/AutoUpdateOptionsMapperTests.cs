using FinanceManager.Shared.Dtos.Update;
using FinanceManager.Web.Services.Updates;
using FluentAssertions;
using msTools.Updater;

namespace FinanceManager.Tests.Updates;

/// <summary>
/// Covers <see cref="AutoUpdateOptionsMapper"/>, the translation layer between the user-facing
/// <see cref="UpdateSettingsDto"/> saved via the settings UI and the msTools.Updater runtime's
/// <see cref="AutoUpdateOptions"/>: field mapping in both directions, preserving a non-GitHub update source (used
/// for local/dev testing) instead of overwriting it, and building the twice-daily source-check time windows
/// including the midnight-crossing case.
/// </summary>
public sealed class AutoUpdateOptionsMapperTests
{
    /// <summary>
    /// Verifies that applying a settings DTO copies every runtime-relevant field (service name, executable path,
    /// download path, health timeout, scheduled install time, prerelease opt-in) onto the options object, and
    /// derives the source-check interval/time-ranges from the configured start/end window.
    /// </summary>
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

    /// <summary>
    /// Verifies the inverse mapping: converting the current <see cref="AutoUpdateOptions"/> back into an
    /// <see cref="UpdateSettingsDto"/> for display in the settings UI reflects every field, including the
    /// repository identity that is not stored on the options object itself and must be threaded through by the
    /// caller.
    /// </summary>
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

    /// <summary>
    /// Verifies that applying a settings DTO and immediately mapping the resulting options back to a DTO reproduces
    /// the original values exactly - the round-trip guarantee that keeps the settings UI from silently drifting
    /// from what was saved after a save/reload cycle.
    /// </summary>
    [Fact]
    public void ApplySettings_ThenToSettingsDto_RoundTripsRuntimeRelevantFields()
    {
        var options = new AutoUpdateOptions();
        var original = new UpdateSettingsDto(false, "owner", "repo", "update.json", new TimeOnly(20, 0), new TimeOnly(6, 0), null, null, null, "updates", 30, true);

        AutoUpdateOptionsMapper.ApplySettings(options, original);
        var roundTripped = AutoUpdateOptionsMapper.ToSettingsDto(options, original.RepositoryOwner, original.RepositoryName, original.ManifestAssetName);

        roundTripped.Should().Be(original);
    }

    /// <summary>
    /// Verifies that when the current update source is already a GitHub source, saving new repository settings
    /// replaces it with a freshly constructed <see cref="AutoUpdateGithubSource"/> for the new owner/repo/prerelease
    /// combination - a GitHub source is immutable per repository, so changing the repository requires swapping the
    /// instance rather than mutating it in place.
    /// </summary>
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

    /// <summary>
    /// Verifies that when the configured update source is not a GitHub source (e.g. a local-folder source used for
    /// development/testing update packages), saving repository settings does not touch it - the settings UI only
    /// edits GitHub-related fields, and must not accidentally clobber a deliberately configured local override
    /// source with a GitHub source built from stale/default repository values.
    /// </summary>
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

    /// <summary>
    /// Verifies that a source-check window spanning midnight (e.g. 20:00 to 06:00) is split into the two daily
    /// segments needed so the evaluator treats both "before midnight" and "after midnight" times as within the
    /// window, while a time clearly outside the window (midday) is correctly rejected - a naive single-range
    /// comparison would incorrectly exclude one side of a midnight-crossing window.
    /// </summary>
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
