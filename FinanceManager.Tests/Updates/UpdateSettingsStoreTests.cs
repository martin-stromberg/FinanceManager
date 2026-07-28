using FinanceManager.Shared.Dtos.Update;
using FinanceManager.Web.Services.Updates;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace FinanceManager.Tests.Updates;

public sealed class UpdateSettingsStoreTests
{
    [Fact]
    public async Task SaveAsync_NormalizesRemovedSettingsAndAppliesFixedWorkingDirectoryToOperationalPaths()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var env = new TestWebHostEnvironment(root.FullName);
            var fileStore = new UpdateFileStore(env, Options.Create(new UpdateOptions { WorkingDirectory = "updates" }));
            var store = new UpdateSettingsStore(Options.Create(new UpdateOptions { WorkingDirectory = "updates" }), fileStore);

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

            settings.RepositoryOwner.Should().Be("martin-stromberg");
            settings.RepositoryName.Should().Be("FinanceManager");
            settings.ManifestAssetName.Should().Be("update.json");
            settings.ExecutablePath.Should().BeNull();
            settings.WorkingDirectory.Should().Be("updates");
            settings.HealthTimeoutSeconds.Should().Be(120);
            fileStore.RootDirectory.Should().Be(Path.Combine(root.FullName, "updates"));
            fileStore.LockPath.Should().Be(Path.Combine(root.FullName, "updates", "update.lock"));
            fileStore.PendingDirectory.Should().Be(Path.Combine(root.FullName, "updates", "pending"));
            fileStore.StagingDirectory.Should().Be(Path.Combine(root.FullName, "updates", "staging"));
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task GetAsync_NormalizesPersistedWorkingDirectoryAfterRestart()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var env = new TestWebHostEnvironment(root.FullName);
            var firstFileStore = new UpdateFileStore(env, Options.Create(new UpdateOptions { WorkingDirectory = "updates" }));
            var firstStore = new UpdateSettingsStore(Options.Create(new UpdateOptions { WorkingDirectory = "updates" }), firstFileStore);
            await firstStore.SaveAsync(new UpdateSettingsUpdateRequest(false, 60, "martin-stromberg", "FinanceManager", "update.json", null, null, null, "custom-updates", 120));
            await firstFileStore.WriteStatusAsync(new UpdateStatusDto(UpdateStatusKind.Ready, "1.0.0", null, "1.1.0", "win-x64", null, null, "release.zip", false, null, null, null));

            var restartedFileStore = new UpdateFileStore(env, Options.Create(new UpdateOptions { WorkingDirectory = "updates" }));
            var restartedStore = new UpdateSettingsStore(Options.Create(new UpdateOptions { WorkingDirectory = "updates" }), restartedFileStore);

            await restartedStore.GetAsync();
            var status = await restartedFileStore.ReadStatusAsync();

            restartedFileStore.RootDirectory.Should().Be(Path.Combine(root.FullName, "updates"));
            status.Should().NotBeNull();
            status!.Status.Should().Be(UpdateStatusKind.Ready);
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
            var env = new TestWebHostEnvironment(root.FullName);
            var fileStore = new UpdateFileStore(env, Options.Create(new UpdateOptions { WorkingDirectory = "updates" }));
            await fileStore.EnsureAsync();
            await File.WriteAllTextAsync(
                fileStore.SettingsPath,
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

            var store = new UpdateSettingsStore(Options.Create(new UpdateOptions { WorkingDirectory = "updates" }), fileStore);

            var settings = await store.GetAsync();

            settings.ServiceName.Should().Be("FinanceManagerService");
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

}
