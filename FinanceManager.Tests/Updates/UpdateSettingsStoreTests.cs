using FinanceManager.Shared.Dtos.Update;
using FinanceManager.Web.Services.Updates;
using FluentAssertions;
using Microsoft.Extensions.Options;
using msTools.Updater;

namespace FinanceManager.Tests.Updates;

/// <summary>
/// Covers <see cref="UpdateSettingsStore"/>: persisting and reloading update settings as JSON under the updater's
/// package store directory, migrating older settings.json files (from earlier releases) that stored separate
/// Windows/Linux service names or lacked the prerelease-opt-in field so an upgrade never loses or misreads existing
/// configuration, and applying loaded settings onto the live <see cref="AutoUpdateOptions"/> runtime object.
/// </summary>
public sealed class UpdateSettingsStoreTests
{
    /// <summary>
    /// Verifies that saving settings writes "settings.json" under the package store's root directory (not some
    /// other application-relative path) - the updater and the web app must agree on exactly where this file lives.
    /// </summary>
    [Fact]
    public async Task SaveAsync_PersistsUnderPackageStoreRootDirectory()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var (store, packageStore) = CreateStore(root.FullName);

            var settings = await store.SaveAsync(new UpdateSettingsUpdateRequest(
                true,
                "other-owner",
                "OtherRepo",
                "manifest.json",
                new TimeOnly(20, 0),
                new TimeOnly(6, 0),
                null,
                "FinanceManager",
                "C:\\app\\FinanceManager.exe",
                "custom-updates",
                30,
                false), TestContext.Current.CancellationToken);

            File.Exists(Path.Combine(packageStore.RootDirectory, "settings.json")).Should().BeTrue();
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Verifies that settings saved by one store instance are correctly read back by a freshly constructed store
    /// pointed at the same root directory - simulating an application restart to confirm settings genuinely survive
    /// on disk rather than only living in an in-memory cache.
    /// </summary>
    [Fact]
    public async Task GetAsync_PersistsAndReloadsSettings()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var (firstStore, _) = CreateStore(root.FullName);
            await firstStore.SaveAsync(new UpdateSettingsUpdateRequest(false, "martin-stromberg", "FinanceManager", "update.json", new TimeOnly(21, 0), new TimeOnly(5, 0), null, null, null, "updates", 120, true), TestContext.Current.CancellationToken);

            var (restartedStore, _) = CreateStore(root.FullName);
            var settings = await restartedStore.GetAsync(TestContext.Current.CancellationToken);

            settings.Enabled.Should().BeFalse();
            settings.SourceCheckStartTime.Should().Be(new TimeOnly(21, 0));
            settings.SourceCheckEndTime.Should().Be(new TimeOnly(5, 0));
            settings.IncludePrereleases.Should().BeTrue();
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Verifies that a legacy settings.json written by an older release - which stored separate
    /// "windowsServiceName"/"linuxServiceName" fields instead of a single platform-agnostic service name, and had no
    /// explicit source-check window - is read back with the correct service name for the current OS and the
    /// documented default 20:00-06:00 check window, so users upgrading from an older version keep a working
    /// configuration instead of it silently reverting to unset.
    /// </summary>
    [Fact]
    public async Task GetAsync_MigratesLegacyPlatformSpecificServiceNames()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var (store, packageStore) = CreateStore(root.FullName);
            await packageStore.EnsureAsync(TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(Path.Combine(packageStore.RootDirectory, "settings.json"), """
                {
                  "enabled": true,
                  "checkIntervalMinutes": 60,
                  "repositoryOwner": "martin-stromberg",
                  "repositoryName": "FinanceManager",
                  "manifestAssetName": "update.json",
                  "scheduledInstallTime": null,
                  "windowsServiceName": "FinanceManagerService",
                  "linuxServiceName": "financemanager.service",
                  "executablePath": null,
                  "workingDirectory": "updates",
                  "healthTimeoutSeconds": 120
                }
                """, TestContext.Current.CancellationToken);

            var settings = await store.GetAsync(TestContext.Current.CancellationToken);

            settings.ServiceName.Should().Be("FinanceManagerService");
            settings.SourceCheckStartTime.Should().Be(new TimeOnly(20, 0));
            settings.SourceCheckEndTime.Should().Be(new TimeOnly(6, 0));
            settings.IncludePrereleases.Should().BeFalse();
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Verifies that on a brand-new installation with no settings.json at all, reading settings still returns sane
    /// defaults - the documented 20:00-06:00 check window and prereleases disabled - so a fresh install is safe by
    /// default (it won't opt into unstable prerelease builds without the admin explicitly choosing to).
    /// </summary>
    [Fact]
    public async Task GetAsync_WhenNoSettingsExist_DefaultsIncludePrereleasesToFalse()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var (store, _) = CreateStore(root.FullName);

            var settings = await store.GetAsync(TestContext.Current.CancellationToken);

            settings.SourceCheckStartTime.Should().Be(new TimeOnly(20, 0));
            settings.SourceCheckEndTime.Should().Be(new TimeOnly(6, 0));
            settings.IncludePrereleases.Should().BeFalse();
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Verifies the narrower migration case of an existing settings.json (already using the newer single
    /// "serviceName" field) that predates the prerelease-opt-in feature and simply has no "includePrereleases"
    /// property at all - deserialization must default the missing field to false rather than leaving it in some
    /// undefined state, so upgrading does not silently enable prerelease updates for an existing installation.
    /// </summary>
    [Fact]
    public async Task GetAsync_WhenStoredJsonMissesIncludePrereleases_DefaultsToFalse()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var (store, packageStore) = CreateStore(root.FullName);
            await packageStore.EnsureAsync(TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(Path.Combine(packageStore.RootDirectory, "settings.json"), """
                {
                  "enabled": true,
                  "checkIntervalMinutes": 60,
                  "repositoryOwner": "martin-stromberg",
                  "repositoryName": "FinanceManager",
                  "manifestAssetName": "update.json",
                  "scheduledInstallTime": null,
                  "serviceName": null,
                  "executablePath": null,
                  "workingDirectory": "updates",
                  "healthTimeoutSeconds": 120
                }
                """, TestContext.Current.CancellationToken);

            var settings = await store.GetAsync(TestContext.Current.CancellationToken);

            settings.SourceCheckStartTime.Should().Be(new TimeOnly(20, 0));
            settings.SourceCheckEndTime.Should().Be(new TimeOnly(6, 0));
            settings.IncludePrereleases.Should().BeFalse();
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Verifies that applying saved settings transfers every runtime-relevant field onto the live
    /// <see cref="AutoUpdateOptions"/> instance - the same field set covered by
    /// <c>AutoUpdateOptionsMapperTests.ApplySettings_CopiesRuntimeRelevantFieldsOntoOptions</c>, but exercised
    /// through this store's own save-then-apply path rather than the mapper directly.
    /// </summary>
    [Fact]
    public async Task ApplyToOptions_TransfersSettingsIntoAutoUpdateOptions()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var (store, _) = CreateStore(root.FullName, out var autoUpdateOptions);
            var settings = await store.SaveAsync(new UpdateSettingsUpdateRequest(
                true,
                "martin-stromberg",
                "FinanceManager",
                "update.json",
                new TimeOnly(20, 0),
                new TimeOnly(6, 0),
                new TimeOnly(3, 0),
                "FinanceManagerService",
                null,
                "custom-updates",
                200,
                true), TestContext.Current.CancellationToken);

            store.ApplyToOptions(settings);

            autoUpdateOptions.Enabled.Should().BeTrue();
            autoUpdateOptions.SourceCheck.Interval.Should().Be(AutoUpdateOptionsMapper.DailySourceCheckIntervalMinutes);
            autoUpdateOptions.SourceCheck.TimeRanges.Should().HaveCount(14);
            autoUpdateOptions.ServiceName.Should().Be("FinanceManagerService");
            autoUpdateOptions.DownloadPath.Should().Be("custom-updates");
            autoUpdateOptions.HealthTimeoutSeconds.Should().Be(200);
            autoUpdateOptions.ScheduledInstallTime.Should().Be(new TimeOnly(3, 0));
            autoUpdateOptions.AllowPrereleaseUpdates.Should().BeTrue();
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    private static (UpdateSettingsStore Store, IAutoUpdatePackageStore PackageStore) CreateStore(string root)
        => CreateStore(root, out _);

    private static (UpdateSettingsStore Store, IAutoUpdatePackageStore PackageStore) CreateStore(string root, out AutoUpdateOptions autoUpdateOptions)
    {
        var environment = new AutoUpdateEnvironmentAdapter(new TestWebHostEnvironment(root));
        autoUpdateOptions = new AutoUpdateOptions { DownloadPath = "updates" };
        var packageStore = new FileSystemAutoUpdatePackageStore(environment, autoUpdateOptions, TimeProvider.System);
        var webOptions = Options.Create(new UpdateOptions());
        var store = new UpdateSettingsStore(webOptions, autoUpdateOptions, packageStore);
        return (store, packageStore);
    }

    private sealed class AutoUpdateEnvironmentAdapter : IAutoUpdateEnvironment
    {
        public AutoUpdateEnvironmentAdapter(TestWebHostEnvironment environment)
        {
            ApplicationDirectory = environment.ContentRootPath;
        }

        public string ApplicationDirectory { get; }
    }
}
