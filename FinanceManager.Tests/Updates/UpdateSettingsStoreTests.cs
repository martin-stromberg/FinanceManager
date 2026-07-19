using FinanceManager.Shared.Dtos.Update;
using FinanceManager.Web.Services.Updates;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;

namespace FinanceManager.Tests.Updates;

public sealed class UpdateSettingsStoreTests
{
    [Fact]
    public async Task SaveAsync_AppliesWorkingDirectoryToOperationalPaths()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var env = new TestEnvironment(root.FullName);
            var fileStore = new UpdateFileStore(env, Options.Create(new UpdateOptions { WorkingDirectory = "updates" }));
            var store = new UpdateSettingsStore(Options.Create(new UpdateOptions { WorkingDirectory = "updates" }), fileStore);

            await store.SaveAsync(new UpdateSettingsUpdateRequest(
                true,
                30,
                "martin-stromberg",
                "FinanceManager",
                "update.json",
                null,
                "FinanceManager",
                null,
                "custom-updates",
                120));

            fileStore.RootDirectory.Should().Be(Path.Combine(root.FullName, "custom-updates"));
            fileStore.LockPath.Should().Be(Path.Combine(root.FullName, "custom-updates", "update.lock"));
            fileStore.PendingDirectory.Should().Be(Path.Combine(root.FullName, "custom-updates", "pending"));
            fileStore.StagingDirectory.Should().Be(Path.Combine(root.FullName, "custom-updates", "staging"));
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task GetAsync_AppliesPersistedWorkingDirectoryAfterRestart()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var env = new TestEnvironment(root.FullName);
            var firstFileStore = new UpdateFileStore(env, Options.Create(new UpdateOptions { WorkingDirectory = "updates" }));
            var firstStore = new UpdateSettingsStore(Options.Create(new UpdateOptions { WorkingDirectory = "updates" }), firstFileStore);
            await firstStore.SaveAsync(new UpdateSettingsUpdateRequest(false, 60, "martin-stromberg", "FinanceManager", "update.json", null, null, null, "custom-updates", 120));
            await firstFileStore.WriteStatusAsync(new UpdateStatusDto(UpdateStatusKind.Ready, "1.0.0", null, "1.1.0", "win-x64", null, null, "release.zip", false, null, null, null));

            var restartedFileStore = new UpdateFileStore(env, Options.Create(new UpdateOptions { WorkingDirectory = "updates" }));
            var restartedStore = new UpdateSettingsStore(Options.Create(new UpdateOptions { WorkingDirectory = "updates" }), restartedFileStore);

            await restartedStore.GetAsync();
            var status = await restartedFileStore.ReadStatusAsync();

            restartedFileStore.RootDirectory.Should().Be(Path.Combine(root.FullName, "custom-updates"));
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
            var env = new TestEnvironment(root.FullName);
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

    private sealed class TestEnvironment : IWebHostEnvironment
    {
        public TestEnvironment(string root)
        {
            ContentRootPath = root;
            WebRootPath = root;
        }

        public string ApplicationName { get; set; } = "Tests";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; }
        public string EnvironmentName { get; set; } = "Development";
        public string WebRootPath { get; set; }
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
