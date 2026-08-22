using FinanceManager.Shared.Dtos.Update;
using FinanceManager.Tests.TestHelpers;
using FinanceManager.Web.Services.Updates;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using msTools.Updater;

namespace FinanceManager.Tests.Updates;

public sealed class UpdateOrchestratorAdapterTests
{
    [Fact]
    public async Task Adapter_MapsSnapshotToUpdateStatusDto()
    {
        var package = new AutoUpdatePackageDescriptor("2.0.0", "windows", "win-x64", "app.zip", new Uri("https://example.test/app.zip"), new string('a', 64), 10);
        var snapshot = UpdateStatusTestData.ReadyToInstallSnapshot("2.0.0", package);

        var orchestrator = new Mock<IAutoUpdateOrchestrator>();
        orchestrator.Setup(o => o.GetStatusAsync(It.IsAny<CancellationToken>())).ReturnsAsync(snapshot);
        var settingsStore = new Mock<IUpdateSettingsStore>();
        settingsStore.Setup(s => s.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UpdateSettingsDto(true, "owner", "repo", "update.json", new TimeOnly(20, 0), new TimeOnly(6, 0), new TimeOnly(4, 0), "svc", null, "updates", 120, false));
        var installedProvider = new Mock<IInstalledReleaseMetadataProvider>();
        installedProvider.Setup(p => p.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InstalledReleaseMetadataDto("1.0.0", DateTimeOffset.UtcNow, "sha", "repo", "win-x64"));
        var platformResolver = new Mock<IAutoUpdatePlatformResolver>();
        platformResolver.SetupGet(p => p.CurrentRuntimeIdentifier).Returns("win-x64");
        var adapter = UpdateOrchestratorAdapterTestFactory.Create(
            orchestrator: orchestrator.Object,
            settingsStore: settingsStore.Object,
            installedProvider: installedProvider.Object,
            platformResolver: platformResolver.Object);

        var status = await adapter.GetStatusAsync();

        status.Status.Should().Be(UpdateStatusKind.Ready);
        status.InstalledVersion.Should().Be("1.0.0");
        status.AvailableVersion.Should().Be("2.0.0");
        status.CurrentPlatform.Should().Be("win-x64");
        status.DownloadedAssetName.Should().Be("release.zip");
        status.ScheduledInstallTime.Should().Be(new TimeOnly(4, 0));
        status.AvailableUpdate.Should().NotBeNull();
        status.AvailableUpdate!.Assets.Should().ContainSingle(asset => asset.AssetName == "app.zip");
    }

    [Theory]
    [InlineData(typeof(FileNotFoundException))]
    [InlineData(typeof(IOException))]
    [InlineData(typeof(ArgumentException))]
    public async Task Adapter_MapsFailedResultToExpectedException(Type exceptionType)
    {
        var exception = (Exception)Activator.CreateInstance(exceptionType, "boom")!;
        var orchestrator = new Mock<IAutoUpdateOrchestrator>();
        orchestrator.Setup(o => o.InstallAsync(true, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AutoUpdateResult(AutoUpdateOutcome.Failed, AutoUpdateState.Failed, exception.Message, exception));
        var adapter = UpdateOrchestratorAdapterTestFactory.Create(orchestrator: orchestrator.Object);

        var act = () => adapter.StartInstallAsync(true);

        var thrown = await act.Should().ThrowAsync<Exception>();
        thrown.Which.GetType().Should().Be(exceptionType);
    }

    [Fact]
    public async Task Adapter_CheckAsync_MapsSuccessOutcomeToUpdateCheckResultDto()
    {
        var package = CreatePackage("2.0.0");
        var statusService = UpdateOrchestratorAdapterTestFactory.CreateStatusService();
        var orchestrator = new Mock<IAutoUpdateOrchestrator>();
        orchestrator.Setup(o => o.CheckForUpdateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AutoUpdateResult(AutoUpdateOutcome.Success, AutoUpdateState.UpdateAvailable, "found an update", null));
        orchestrator.Setup(o => o.DownloadAsync(It.IsAny<CancellationToken>()))
            .Callback(() => statusService.UpdateAsync(_ => UpdateStatusTestData.ReadyToInstallSnapshot("2.0.0", package)).GetAwaiter().GetResult())
            .ReturnsAsync(new AutoUpdateResult(AutoUpdateOutcome.Success, AutoUpdateState.ReadyToInstall, "ready to install", null));
        var settingsStore = new Mock<IUpdateSettingsStore>();
        settingsStore.Setup(s => s.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UpdateSettingsDto(true, "owner", "repo", "update.json", new TimeOnly(20, 0), new TimeOnly(6, 0), null, "svc", null, "updates", 120, false));
        var installedProvider = new Mock<IInstalledReleaseMetadataProvider>();
        installedProvider.Setup(p => p.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InstalledReleaseMetadataDto(null, null, null, null, null));
        var adapter = UpdateOrchestratorAdapterTestFactory.Create(
            orchestrator: orchestrator.Object,
            statusService: statusService,
            settingsStore: settingsStore.Object,
            installedProvider: installedProvider.Object);

        var result = await adapter.CheckAsync();

        result.UpdateAvailable.Should().BeTrue();
        result.Message.Should().Be("ready to install");
        result.Status.Should().NotBeNull();
        result.Status.Status.Should().Be(UpdateStatusKind.Ready);
        orchestrator.Verify(o => o.DownloadAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Adapter_CheckAsync_WhenNoUpdate_DoesNotDownload()
    {
        var orchestrator = new Mock<IAutoUpdateOrchestrator>();
        orchestrator.Setup(o => o.CheckForUpdateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AutoUpdateResult(AutoUpdateOutcome.NoUpdate, AutoUpdateState.Idle, "no update", null));
        var settingsStore = new Mock<IUpdateSettingsStore>();
        settingsStore.Setup(s => s.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UpdateSettingsDto(true, "owner", "repo", "update.json", new TimeOnly(20, 0), new TimeOnly(6, 0), null, "svc", null, "updates", 120, false));
        var installedProvider = new Mock<IInstalledReleaseMetadataProvider>();
        installedProvider.Setup(p => p.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InstalledReleaseMetadataDto(null, null, null, null, null));
        var adapter = UpdateOrchestratorAdapterTestFactory.Create(
            orchestrator: orchestrator.Object,
            settingsStore: settingsStore.Object,
            installedProvider: installedProvider.Object);

        var result = await adapter.CheckAsync();

        result.UpdateAvailable.Should().BeFalse();
        result.Message.Should().Be("no update");
        orchestrator.Verify(o => o.DownloadAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Adapter_SaveSettings_AppliesToAutoUpdateOptions()
    {
        var savedSettings = new UpdateSettingsDto(true, "owner", "repo", "update.json", new TimeOnly(20, 0), new TimeOnly(6, 0), null, "svc", null, "custom", 200, true);
        var settingsStore = new Mock<IUpdateSettingsStore>();
        settingsStore.Setup(s => s.SaveAsync(It.IsAny<UpdateSettingsUpdateRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(savedSettings);
        var applied = false;
        settingsStore.Setup(s => s.ApplyToOptions(savedSettings))
            .Callback(() => applied = true);
        var adapter = UpdateOrchestratorAdapterTestFactory.Create(settingsStore: settingsStore.Object);

        var result = await adapter.SaveSettingsAsync(new UpdateSettingsUpdateRequest(true, "owner", "repo", "update.json", new TimeOnly(20, 0), new TimeOnly(6, 0), null, "svc", null, "custom", 200, true));

        result.Should().Be(savedSettings);
        applied.Should().BeTrue();
    }

    [Fact]
    public async Task Adapter_CheckAsync_WhenRateLimitedResult_ReturnsFriendlyMessage()
    {
        var raw = "Response status code does not indicate success: 403 (rate limit exceeded).";
        var orchestrator = new Mock<IAutoUpdateOrchestrator>();
        orchestrator.Setup(o => o.CheckForUpdateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AutoUpdateResult(AutoUpdateOutcome.Failed, AutoUpdateState.Failed, raw, new HttpRequestException(raw)));
        var settingsStore = new Mock<IUpdateSettingsStore>();
        settingsStore.Setup(s => s.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UpdateSettingsDto(true, "owner", "repo", "update.json", new TimeOnly(20, 0), new TimeOnly(6, 0), null, "svc", null, "updates", 120, false));
        var installedProvider = new Mock<IInstalledReleaseMetadataProvider>();
        installedProvider.Setup(p => p.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InstalledReleaseMetadataDto(null, null, null, null, null));
        var adapter = UpdateOrchestratorAdapterTestFactory.Create(
            orchestrator: orchestrator.Object,
            settingsStore: settingsStore.Object,
            installedProvider: installedProvider.Object);

        var result = await adapter.CheckAsync();

        result.UpdateAvailable.Should().BeFalse();
        result.Message.Should().Be(UpdateErrorMessageMapper.GithubRateLimitMessage);
        result.Status.LastError.Should().Be(UpdateErrorMessageMapper.GithubRateLimitMessage);
    }

    [Fact]
    public async Task Adapter_StartInstallAsync_WhenLockAbsentAfterInstall_DoesNotLog()
    {
        var orchestrator = new Mock<IAutoUpdateOrchestrator>();
        orchestrator.Setup(o => o.InstallAsync(true, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AutoUpdateResult(AutoUpdateOutcome.Success, AutoUpdateState.Success, "installed", null));
        var packageStore = new Mock<IAutoUpdatePackageStore>();
        packageStore.Setup(s => s.GetLockCreatedAtAsync(It.IsAny<CancellationToken>())).ReturnsAsync((DateTimeOffset?)null);
        var logger = new CapturingLogger<UpdateOrchestratorAdapter>();
        var adapter = CreateAdapterForInstall(orchestrator.Object, packageStore.Object, logger);

        await adapter.StartInstallAsync(true);

        logger.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task Adapter_StartInstallAsync_WhenUpdateAvailableWithoutDownload_DownloadsBeforeInstall()
    {
        var package = CreatePackage("2.0.0");
        var statusService = UpdateOrchestratorAdapterTestFactory.CreateStatusService();
        await statusService.UpdateAsync(_ => UpdateStatusTestData.UpdateAvailableSnapshot("2.0.0", package));
        var orchestrator = new Mock<IAutoUpdateOrchestrator>();
        orchestrator.Setup(o => o.DownloadAsync(It.IsAny<CancellationToken>()))
            .Callback(() => statusService.UpdateAsync(_ => UpdateStatusTestData.ReadyToInstallSnapshot("2.0.0", package)).GetAwaiter().GetResult())
            .ReturnsAsync(new AutoUpdateResult(AutoUpdateOutcome.Success, AutoUpdateState.ReadyToInstall, "ready to install", null));
        orchestrator.Setup(o => o.InstallAsync(true, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AutoUpdateResult(AutoUpdateOutcome.Success, AutoUpdateState.Success, "installed", null));
        var packageStore = new Mock<IAutoUpdatePackageStore>();
        packageStore.Setup(s => s.GetLockCreatedAtAsync(It.IsAny<CancellationToken>())).ReturnsAsync((DateTimeOffset?)null);
        var adapter = CreateAdapterForInstall(orchestrator.Object, packageStore.Object, new CapturingLogger<UpdateOrchestratorAdapter>(), statusService);

        await adapter.StartInstallAsync(true);

        orchestrator.Verify(o => o.DownloadAsync(It.IsAny<CancellationToken>()), Times.Once);
        orchestrator.Verify(o => o.InstallAsync(true, false, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Adapter_StartInstallAsync_WhenSuccess_ValidatesLockCleanup()
    {
        var orchestrator = new Mock<IAutoUpdateOrchestrator>();
        orchestrator.Setup(o => o.InstallAsync(true, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AutoUpdateResult(AutoUpdateOutcome.Success, AutoUpdateState.Success, "installed", null));
        var packageStore = new Mock<IAutoUpdatePackageStore>();
        packageStore.Setup(s => s.GetLockCreatedAtAsync(It.IsAny<CancellationToken>())).ReturnsAsync((DateTimeOffset?)null);
        var adapter = CreateAdapterForInstall(orchestrator.Object, packageStore.Object, new CapturingLogger<UpdateOrchestratorAdapter>());

        await adapter.StartInstallAsync(true);

        packageStore.Verify(s => s.GetLockCreatedAtAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Adapter_StartInstallAsync_WhenLockStillPresentAfterInstall_LogsWarning()
    {
        var orchestrator = new Mock<IAutoUpdateOrchestrator>();
        orchestrator.Setup(o => o.InstallAsync(true, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AutoUpdateResult(AutoUpdateOutcome.Success, AutoUpdateState.Success, "installed", null));
        var packageStore = new Mock<IAutoUpdatePackageStore>();
        packageStore.Setup(s => s.GetLockCreatedAtAsync(It.IsAny<CancellationToken>())).ReturnsAsync(DateTimeOffset.UtcNow);
        var logger = new CapturingLogger<UpdateOrchestratorAdapter>();
        var adapter = CreateAdapterForInstall(orchestrator.Object, packageStore.Object, logger);

        await adapter.StartInstallAsync(true);

        logger.Entries.Should().ContainSingle(entry => entry.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task Adapter_StartInstallAsync_WhenLockCleanupCheckThrowsIOException_StillReturnsSuccessStatus()
    {
        var orchestrator = new Mock<IAutoUpdateOrchestrator>();
        orchestrator.Setup(o => o.InstallAsync(true, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AutoUpdateResult(AutoUpdateOutcome.Success, AutoUpdateState.Success, "installed", null));
        var packageStore = new Mock<IAutoUpdatePackageStore>();
        packageStore.Setup(s => s.GetLockCreatedAtAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new IOException("read failed"));
        var logger = new CapturingLogger<UpdateOrchestratorAdapter>();
        var adapter = CreateAdapterForInstall(orchestrator.Object, packageStore.Object, logger);

        var status = await adapter.StartInstallAsync(true);

        status.Should().NotBeNull();
        logger.Entries.Should().ContainSingle(entry => entry.Level == LogLevel.Warning);
    }

    private static UpdateOrchestratorAdapter CreateAdapterForInstall(
        IAutoUpdateOrchestrator orchestrator,
        IAutoUpdatePackageStore packageStore,
        ILogger<UpdateOrchestratorAdapter> logger,
        AutoUpdateStatusService? statusService = null)
    {
        var settingsStore = new Mock<IUpdateSettingsStore>();
        settingsStore.Setup(s => s.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UpdateSettingsDto(true, "owner", "repo", "update.json", new TimeOnly(20, 0), new TimeOnly(6, 0), null, "svc", null, "updates", 120, false));
        var installedProvider = new Mock<IInstalledReleaseMetadataProvider>();
        installedProvider.Setup(p => p.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InstalledReleaseMetadataDto(null, null, null, null, null));

        return UpdateOrchestratorAdapterTestFactory.Create(
            orchestrator: orchestrator,
            statusService: statusService,
            settingsStore: settingsStore.Object,
            packageStore: packageStore,
            installedProvider: installedProvider.Object,
            logger: logger);
    }

    private static AutoUpdatePackageDescriptor CreatePackage(string version)
        => new(version, "windows", "win-x64", "app.zip", new Uri("https://example.test/app.zip"), new string('a', 64), 10);
}
