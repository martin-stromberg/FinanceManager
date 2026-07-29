using FinanceManager.Shared.Dtos.Update;
using FinanceManager.Web.Services.Updates;
using FluentAssertions;
using Moq;
using SoftwareSchmiede.AutoUpdate;

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
            .ReturnsAsync(new UpdateSettingsDto(true, 60, "owner", "repo", "update.json", new TimeOnly(4, 0), "svc", null, "updates", 120));
        var installedProvider = new Mock<IInstalledReleaseMetadataProvider>();
        installedProvider.Setup(p => p.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InstalledReleaseMetadataDto("1.0.0", DateTimeOffset.UtcNow, "sha", "repo", "win-x64"));
        var platformResolver = new Mock<IAutoUpdatePlatformResolver>();
        platformResolver.SetupGet(p => p.CurrentRuntimeIdentifier).Returns("win-x64");
        var adapter = new UpdateOrchestratorAdapter(
            orchestrator.Object,
            new Mock<IAutoUpdateCommandHandler>().Object,
            new Mock<IAutoUpdateStatusProvider>().Object,
            settingsStore.Object,
            installedProvider.Object,
            platformResolver.Object,
            new Mock<IAutoUpdatePackageStore>().Object,
            CreateStatusService(),
            new AutoUpdateOptions(),
            TimeProvider.System);

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
        var commandHandler = new Mock<IAutoUpdateCommandHandler>();
        commandHandler.Setup(c => c.InstallAsync(true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AutoUpdateResult(AutoUpdateOutcome.Failed, AutoUpdateState.Failed, exception.Message, exception));
        var adapter = new UpdateOrchestratorAdapter(
            new Mock<IAutoUpdateOrchestrator>().Object,
            commandHandler.Object,
            new Mock<IAutoUpdateStatusProvider>().Object,
            new Mock<IUpdateSettingsStore>().Object,
            new Mock<IInstalledReleaseMetadataProvider>().Object,
            new Mock<IAutoUpdatePlatformResolver>().Object,
            new Mock<IAutoUpdatePackageStore>().Object,
            CreateStatusService(),
            new AutoUpdateOptions(),
            TimeProvider.System);

        var act = () => adapter.StartInstallAsync(true);

        var thrown = await act.Should().ThrowAsync<Exception>();
        thrown.Which.GetType().Should().Be(exceptionType);
    }

    [Fact]
    public async Task Adapter_CheckAsync_MapsSuccessOutcomeToUpdateCheckResultDto()
    {
        var commandHandler = new Mock<IAutoUpdateCommandHandler>();
        commandHandler.Setup(c => c.CheckAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AutoUpdateResult(AutoUpdateOutcome.Success, AutoUpdateState.UpdateAvailable, "found an update", null));
        var statusProvider = new Mock<IAutoUpdateStatusProvider>();
        statusProvider.Setup(p => p.GetSnapshot()).Returns(AutoUpdateStatusSnapshot.Idle(null));
        var settingsStore = new Mock<IUpdateSettingsStore>();
        settingsStore.Setup(s => s.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UpdateSettingsDto(true, 60, "owner", "repo", "update.json", null, "svc", null, "updates", 120));
        var installedProvider = new Mock<IInstalledReleaseMetadataProvider>();
        installedProvider.Setup(p => p.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InstalledReleaseMetadataDto(null, null, null, null, null));
        var adapter = new UpdateOrchestratorAdapter(
            new Mock<IAutoUpdateOrchestrator>().Object,
            commandHandler.Object,
            statusProvider.Object,
            settingsStore.Object,
            installedProvider.Object,
            new Mock<IAutoUpdatePlatformResolver>().Object,
            new Mock<IAutoUpdatePackageStore>().Object,
            CreateStatusService(),
            new AutoUpdateOptions(),
            TimeProvider.System);

        var result = await adapter.CheckAsync();

        result.UpdateAvailable.Should().BeTrue();
        result.Message.Should().Be("found an update");
        result.Status.Should().NotBeNull();
    }

    [Fact]
    public async Task Adapter_SaveSettings_AppliesToAutoUpdateOptions()
    {
        var savedSettings = new UpdateSettingsDto(true, 45, "owner", "repo", "update.json", null, "svc", null, "custom", 200);
        var settingsStore = new Mock<IUpdateSettingsStore>();
        settingsStore.Setup(s => s.SaveAsync(It.IsAny<UpdateSettingsUpdateRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(savedSettings);
        var applied = false;
        settingsStore.Setup(s => s.ApplyToOptions(savedSettings))
            .Callback(() => applied = true);
        var adapter = new UpdateOrchestratorAdapter(
            new Mock<IAutoUpdateOrchestrator>().Object,
            new Mock<IAutoUpdateCommandHandler>().Object,
            new Mock<IAutoUpdateStatusProvider>().Object,
            settingsStore.Object,
            new Mock<IInstalledReleaseMetadataProvider>().Object,
            new Mock<IAutoUpdatePlatformResolver>().Object,
            new Mock<IAutoUpdatePackageStore>().Object,
            CreateStatusService(),
            new AutoUpdateOptions(),
            TimeProvider.System);

        var result = await adapter.SaveSettingsAsync(new UpdateSettingsUpdateRequest(true, 45, "owner", "repo", "update.json", null, "svc", null, "custom", 200));

        result.Should().Be(savedSettings);
        applied.Should().BeTrue();
    }

    private static AutoUpdateStatusService CreateStatusService()
    {
        var stateStore = new Mock<IAutoUpdateStateStore>();
        stateStore.Setup(s => s.ReadAsync(It.IsAny<CancellationToken>())).ReturnsAsync((AutoUpdateStatusSnapshot?)null);
        var installedVersionProvider = new Mock<IInstalledVersionProvider>();
        installedVersionProvider.Setup(p => p.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InstalledReleaseInfo(null, null, null, null, null));
        return new AutoUpdateStatusService(stateStore.Object, installedVersionProvider.Object);
    }
}
