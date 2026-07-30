using FinanceManager.Shared.Dtos.Update;
using FinanceManager.Web.Services.Updates;
using FluentAssertions;
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
            .ReturnsAsync(new UpdateSettingsDto(true, 60, "owner", "repo", "update.json", new TimeOnly(4, 0), "svc", null, "updates", 120));
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
        orchestrator.Setup(o => o.InstallAsync(true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AutoUpdateResult(AutoUpdateOutcome.Failed, AutoUpdateState.Failed, exception.Message, exception));
        var adapter = UpdateOrchestratorAdapterTestFactory.Create(orchestrator: orchestrator.Object);

        var act = () => adapter.StartInstallAsync(true);

        var thrown = await act.Should().ThrowAsync<Exception>();
        thrown.Which.GetType().Should().Be(exceptionType);
    }

    [Fact]
    public async Task Adapter_CheckAsync_MapsSuccessOutcomeToUpdateCheckResultDto()
    {
        var orchestrator = new Mock<IAutoUpdateOrchestrator>();
        orchestrator.Setup(o => o.CheckForUpdateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AutoUpdateResult(AutoUpdateOutcome.Success, AutoUpdateState.UpdateAvailable, "found an update", null));
        var settingsStore = new Mock<IUpdateSettingsStore>();
        settingsStore.Setup(s => s.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UpdateSettingsDto(true, 60, "owner", "repo", "update.json", null, "svc", null, "updates", 120));
        var installedProvider = new Mock<IInstalledReleaseMetadataProvider>();
        installedProvider.Setup(p => p.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InstalledReleaseMetadataDto(null, null, null, null, null));
        var adapter = UpdateOrchestratorAdapterTestFactory.Create(
            orchestrator: orchestrator.Object,
            settingsStore: settingsStore.Object,
            installedProvider: installedProvider.Object);

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
        var adapter = UpdateOrchestratorAdapterTestFactory.Create(settingsStore: settingsStore.Object);

        var result = await adapter.SaveSettingsAsync(new UpdateSettingsUpdateRequest(true, 45, "owner", "repo", "update.json", null, "svc", null, "custom", 200));

        result.Should().Be(savedSettings);
        applied.Should().BeTrue();
    }
}
