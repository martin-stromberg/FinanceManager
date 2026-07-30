using FinanceManager.Shared.Dtos.Update;
using FinanceManager.Web.Services.Updates;
using FluentAssertions;
using Microsoft.Extensions.Options;
using SoftwareSchmiede.AutoUpdate;

namespace FinanceManager.Tests.Updates;

public sealed class UpdateSettingsStoreTests
{
    [Fact]
    public async Task SaveAsync_PersistsUnderPackageStoreRootDirectory()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var (store, packageStore) = CreateStore(root.FullName);

            var settings = await store.SaveAsync(new UpdateSettingsUpdateRequest(
                true,
                30,
                "other-owner",
                "OtherRepo",
                "manifest.json",
                null,
                "FinanceManager",
                "C:\\app\\FinanceManager.exe",
                "custom-updates",
                30));

            File.Exists(Path.Combine(packageStore.RootDirectory, "settings.json")).Should().BeTrue();
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task GetAsync_PersistsAndReloadsSettings()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var (firstStore, _) = CreateStore(root.FullName);
            await firstStore.SaveAsync(new UpdateSettingsUpdateRequest(false, 60, "martin-stromberg", "FinanceManager", "update.json", null, null, null, "updates", 120));

            var (restartedStore, _) = CreateStore(root.FullName);
            var settings = await restartedStore.GetAsync();

            settings.Enabled.Should().BeFalse();
            settings.CheckIntervalMinutes.Should().Be(60);
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task GetAsync_MigratesLegacyPlatformSpecificServiceNames()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var (store, packageStore) = CreateStore(root.FullName);
            await packageStore.EnsureAsync();
            await File.WriteAllTextAsync(
                Path.Combine(packageStore.RootDirectory, "settings.json"),
                """
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
                """);

            var settings = await store.GetAsync();

            settings.ServiceName.Should().Be("FinanceManagerService");
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task ApplyToOptions_TransfersSettingsIntoAutoUpdateOptions()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var (store, _) = CreateStore(root.FullName, out var autoUpdateOptions);
            var settings = await store.SaveAsync(new UpdateSettingsUpdateRequest(
                true,
                45,
                "martin-stromberg",
                "FinanceManager",
                "update.json",
                new TimeOnly(3, 0),
                "FinanceManagerService",
                null,
                "custom-updates",
                200));

            store.ApplyToOptions(settings);

            autoUpdateOptions.Enabled.Should().BeTrue();
            autoUpdateOptions.SourceCheck.Interval.Should().Be(45);
            autoUpdateOptions.ServiceName.Should().Be("FinanceManagerService");
            autoUpdateOptions.DownloadPath.Should().Be("custom-updates");
            autoUpdateOptions.HealthTimeoutSeconds.Should().Be(200);
            autoUpdateOptions.ScheduledInstallTime.Should().Be(new TimeOnly(3, 0));
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
