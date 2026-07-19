using System.IO.Compression;
using FinanceManager.Shared.Dtos.Update;
using FinanceManager.Web.Services.Updates;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;

namespace FinanceManager.Tests.Updates;

public sealed class UpdateOrchestratorTests
{
    [Fact]
    public async Task ResetLockAsync_WhenInstallRuns_RefusesReset()
    {
        var context = TestContext.Create();
        context.Executor.IsInstallRunning = true;
        await context.FileStore.TryCreateLockAsync();

        var act = () => context.Orchestrator.ResetLockAsync("admin");

        await act.Should().ThrowAsync<IOException>()
            .WithMessage("The current process still owns an update installation.");
        File.Exists(context.FileStore.LockPath).Should().BeTrue();
        context.Dispose();
    }

    [Fact]
    public async Task ResetLockAsync_WhenNoLockExists_RefusesReset()
    {
        var context = TestContext.Create();

        var act = () => context.Orchestrator.ResetLockAsync("admin");

        await act.Should().ThrowAsync<IOException>()
            .WithMessage("No update lock is active.");
        context.Dispose();
    }

    [Fact]
    public async Task ResetLockAsync_WhenLockIsFresh_RefusesReset()
    {
        var context = TestContext.Create(healthTimeoutSeconds: 120);
        await context.FileStore.TryCreateLockAsync();

        var act = () => context.Orchestrator.ResetLockAsync("admin");

        await act.Should().ThrowAsync<IOException>()
            .WithMessage("The update lock is not old enough to be considered stale.");
        File.Exists(context.FileStore.LockPath).Should().BeTrue();
        context.Dispose();
    }

    [Fact]
    public async Task ResetLockAsync_WhenLockIsStale_DeletesLockAndWritesReason()
    {
        var context = TestContext.Create(healthTimeoutSeconds: 60);
        await context.FileStore.TryCreateLockAsync();
        File.SetCreationTimeUtc(context.FileStore.LockPath, DateTime.UtcNow.AddMinutes(-3));

        await context.Orchestrator.ResetLockAsync("verified stale");

        File.Exists(context.FileStore.LockPath).Should().BeFalse();
        var status = await context.FileStore.ReadStatusAsync();
        status.Should().NotBeNull();
        status!.IsLocked.Should().BeFalse();
        status.LockCreatedAt.Should().BeNull();
        status.LastError.Should().Be("Lock reset: verified stale");
        context.Dispose();
    }

    [Fact]
    public async Task StartInstallAsync_WhenDowntimeIsNotConfirmed_ThrowsBadRequestCause()
    {
        var context = TestContext.Create();

        var act = () => context.Orchestrator.StartInstallAsync(confirmDowntime: false);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Downtime confirmation is required.*");
        context.Dispose();
    }

    [Fact]
    public async Task StartInstallAsync_WhenUpdateIsNotReady_ThrowsNotReadyCause()
    {
        var context = TestContext.Create();

        var act = () => context.Orchestrator.StartInstallAsync(confirmDowntime: true);

        await act.Should().ThrowAsync<FileNotFoundException>()
            .WithMessage("No ready update package is available.");
        context.Dispose();
    }

    [Fact]
    public async Task StartInstallAsync_WhenReady_DelegatesToExecutorAndReturnsInstalling()
    {
        var context = TestContext.Create();
        var ready = TestData.ReadyStatus();
        await context.FileStore.WriteStatusAsync(ready);

        var status = await context.Orchestrator.StartInstallAsync(confirmDowntime: true);

        status.Status.Should().Be(UpdateStatusKind.Installing);
        context.Executor.StartCalls.Should().Be(1);
        context.Dispose();
    }

    [Fact]
    public async Task CheckAsync_WhenManifestHasNewerVersion_WritesCheckingThenReady()
    {
        var context = TestContext.Create(manifest: TestData.Manifest(version: "1.2.4"));

        var result = await context.Orchestrator.CheckAsync();

        result.UpdateAvailable.Should().BeTrue();
        result.Status.Status.Should().Be(UpdateStatusKind.Ready);
        result.Status.DownloadedAssetName.Should().Be("release.zip");
        context.FileStore.ReadStatusAsync().Result!.Status.Should().Be(UpdateStatusKind.Ready);
        context.ManifestClient.DownloadCalls.Should().Be(1);
        context.Dispose();
    }

    [Fact]
    public async Task CheckAsync_WhenManifestClientFails_WritesFailedStatus()
    {
        var context = TestContext.Create(manifestFailure: new InvalidOperationException("manifest unavailable"));

        var result = await context.Orchestrator.CheckAsync();

        result.UpdateAvailable.Should().BeFalse();
        result.Status.Status.Should().Be(UpdateStatusKind.Failed);
        result.Status.LastError.Should().Be("manifest unavailable");
        context.Dispose();
    }

    private sealed class TestContext : IDisposable
    {
        private readonly DirectoryInfo _root;

        private TestContext(DirectoryInfo root, UpdateFileStore fileStore, TestExecutor executor, TestManifestClient manifestClient, UpdateOrchestrator orchestrator)
        {
            _root = root;
            FileStore = fileStore;
            Executor = executor;
            ManifestClient = manifestClient;
            Orchestrator = orchestrator;
        }

        public UpdateFileStore FileStore { get; }
        public TestExecutor Executor { get; }
        public TestManifestClient ManifestClient { get; }
        public UpdateOrchestrator Orchestrator { get; }

        public static TestContext Create(int healthTimeoutSeconds = 120, UpdateMetadataDto? manifest = null, Exception? manifestFailure = null)
        {
            var root = Directory.CreateTempSubdirectory();
            var options = new UpdateOptions { WorkingDirectory = "updates", HealthTimeoutSeconds = healthTimeoutSeconds, MaxAssetBytes = 1024 * 1024 };
            var fileStore = new UpdateFileStore(new TestEnvironment(root.FullName), Options.Create(options));
            var executor = new TestExecutor();
            var manifestClient = new TestManifestClient(fileStore, manifest ?? TestData.Manifest(), manifestFailure);
            var orchestrator = new UpdateOrchestrator(
                new TestSettingsStore(),
                new TestInstalledProvider(),
                manifestClient,
                new TestPlatformResolver(),
                fileStore,
                new TestValidator(),
                executor,
                Options.Create(options));
            return new TestContext(root, fileStore, executor, manifestClient, orchestrator);
        }

        public void Dispose() => _root.Delete(recursive: true);
    }

    private static class TestData
    {
        public static UpdateStatusDto ReadyStatus()
            => new(
                UpdateStatusKind.Ready,
                "1.2.3",
                null,
                "1.2.4",
                "win-x64",
                DateTimeOffset.UtcNow,
                null,
                "release.zip",
                false,
                null,
                null,
                Manifest(version: "1.2.4"));

        public static UpdateMetadataDto Manifest(string version = "1.2.4")
            => new(version, null, null, "martin-stromberg", "FinanceManager", new[] { new UpdateAssetDto("windows", "win-x64", "release.zip", "https://example.test/release.zip", "hash", 3) });
    }

    private sealed class TestInstalledProvider : IInstalledReleaseMetadataProvider
    {
        public Task<InstalledReleaseMetadataDto> GetAsync(CancellationToken ct = default)
            => Task.FromResult(new InstalledReleaseMetadataDto("1.2.3", null, null, null, null));
    }

    private sealed class TestSettingsStore : IUpdateSettingsStore
    {
        private UpdateSettingsDto _settings = new(false, 60, "martin-stromberg", "FinanceManager", "update.json", null, "FinanceManager", "financemanager", null, "updates", 120);

        public Task<UpdateSettingsDto> GetAsync(CancellationToken ct = default) => Task.FromResult(_settings);
        public Task<UpdateSettingsDto> SaveAsync(UpdateSettingsUpdateRequest request, CancellationToken ct = default) => Task.FromResult(_settings);
        public Task<UpdateSettingsDto> SaveScheduleAsync(TimeOnly? scheduledInstallTime, CancellationToken ct = default)
        {
            _settings = _settings with { ScheduledInstallTime = scheduledInstallTime };
            return Task.FromResult(_settings);
        }
    }

    private sealed class TestManifestClient : IUpdateManifestClient
    {
        private readonly IUpdateFileStore _fileStore;
        private readonly UpdateMetadataDto _manifest;
        private readonly Exception? _failure;

        public TestManifestClient(IUpdateFileStore fileStore, UpdateMetadataDto manifest, Exception? failure)
        {
            _fileStore = fileStore;
            _manifest = manifest;
            _failure = failure;
        }

        public int DownloadCalls { get; private set; }

        public Task<UpdateMetadataDto> GetManifestAsync(UpdateSettingsDto settings, CancellationToken ct = default)
            => _failure is null ? Task.FromResult(_manifest) : Task.FromException<UpdateMetadataDto>(_failure);

        public async Task DownloadAssetAsync(UpdateAssetDto asset, string targetPath, long maxBytes, CancellationToken ct = default)
        {
            DownloadCalls++;
            Directory.CreateDirectory(_fileStore.PendingDirectory);
            await File.WriteAllTextAsync(targetPath, "zip", ct);
        }
    }

    private sealed class TestPlatformResolver : IUpdatePlatformResolver
    {
        public string CurrentRuntimeIdentifier => "win-x64";
        public string CurrentPlatform => "windows";
        public UpdateAssetDto? SelectAsset(UpdateMetadataDto manifest) => manifest.Assets.FirstOrDefault();
    }

    private sealed class TestExecutor : IUpdateExecutor
    {
        public bool IsInstallRunning { get; set; }
        public int StartCalls { get; private set; }

        public async Task<UpdateStatusDto> StartAsync(UpdateSettingsDto settings, UpdateStatusDto status, CancellationToken ct = default)
        {
            StartCalls++;
            IsInstallRunning = true;
            return status with { Status = UpdateStatusKind.Installing, IsLocked = true, LockCreatedAt = DateTimeOffset.UtcNow };
        }
    }

    private sealed class TestValidator : IUpdateValidator
    {
        public bool IsNewerVersion(string? installedVersion, string availableVersion) => availableVersion != installedVersion;
        public void ValidateManifest(UpdateMetadataDto manifest, UpdateSettingsDto settings, string currentPlatform)
        {
        }

        public Task ValidateDownloadedAssetAsync(UpdateAssetDto asset, string path, long maxBytes, CancellationToken ct = default)
            => Task.CompletedTask;
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
