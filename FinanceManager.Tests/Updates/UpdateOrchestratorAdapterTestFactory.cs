using FinanceManager.Web.Services.Updates;
using Moq;
using SoftwareSchmiede.AutoUpdate;

namespace FinanceManager.Tests.Updates;

/// <summary>
/// Shared construction helpers for <see cref="UpdateOrchestratorAdapterTests"/> and
/// <see cref="UpdateOrchestratorAdapterLockAndScheduleTests"/>, avoiding a duplicated status-service factory and
/// repeated adapter construction in every test.
/// </summary>
internal static class UpdateOrchestratorAdapterTestFactory
{
    public static AutoUpdateStatusService CreateStatusService()
    {
        var stateStore = new Mock<IAutoUpdateStateStore>();
        stateStore.Setup(s => s.ReadAsync(It.IsAny<CancellationToken>())).ReturnsAsync((AutoUpdateStatusSnapshot?)null);
        var installedVersionProvider = new Mock<IInstalledVersionProvider>();
        installedVersionProvider.Setup(p => p.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InstalledReleaseInfo(null, null, null, null, null));
        return new AutoUpdateStatusService(stateStore.Object, installedVersionProvider.Object);
    }

    public static UpdateOrchestratorAdapter Create(
        IAutoUpdateOrchestrator? orchestrator = null,
        AutoUpdateStatusService? statusService = null,
        IUpdateSettingsStore? settingsStore = null,
        IAutoUpdatePackageStore? packageStore = null,
        IInstalledReleaseMetadataProvider? installedProvider = null,
        IAutoUpdatePlatformResolver? platformResolver = null)
    {
        var settings = settingsStore ?? new Mock<IUpdateSettingsStore>().Object;
        var mapper = new UpdateStatusMapper(
            installedProvider ?? new Mock<IInstalledReleaseMetadataProvider>().Object,
            platformResolver ?? new Mock<IAutoUpdatePlatformResolver>().Object,
            settings);

        return new UpdateOrchestratorAdapter(
            orchestrator ?? new Mock<IAutoUpdateOrchestrator>().Object,
            statusService ?? CreateStatusService(),
            settings,
            packageStore ?? new Mock<IAutoUpdatePackageStore>().Object,
            mapper);
    }
}
