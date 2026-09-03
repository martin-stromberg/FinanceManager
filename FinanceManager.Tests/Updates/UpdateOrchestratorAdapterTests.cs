using FinanceManager.Shared.Dtos.Update;
using FinanceManager.Tests.TestHelpers;
using FinanceManager.Web.Services.Updates;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using msTools.Updater;

namespace FinanceManager.Tests.Updates;

/// <summary>
/// Covers the core of <see cref="UpdateOrchestratorAdapter"/>: mapping the msTools.Updater's status snapshot,
/// check/download results, and settings into the DTOs the web UI consumes; translating a failed
/// <c>InstallAsync</c>/<c>CheckForUpdateAsync</c> result back into the original exception type (or a friendlier
/// message for a recognizable GitHub rate-limit failure); and the post-install lock-cleanup diagnostics that warn
/// when the updater's lock file unexpectedly survives a successful install. Lock reset and schedule handling are
/// covered separately by <see cref="UpdateOrchestratorAdapterLockAndScheduleTests"/>.
/// </summary>
public sealed class UpdateOrchestratorAdapterTests
{
    /// <summary>
    /// Verifies that a "ready to install" orchestrator snapshot is mapped into an <c>UpdateStatusDto</c> with the
    /// installed and available versions, current runtime platform, downloaded asset name, scheduled install time,
    /// and the available update's asset list all populated from their respective sources (orchestrator snapshot,
    /// installed-release provider, settings store, platform resolver) - the status endpoint aggregates several
    /// independent sources into one DTO, so each source's contribution needs its own coverage.
    /// </summary>
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

        var status = await adapter.GetStatusAsync(TestContext.Current.CancellationToken);

        status.Status.Should().Be(UpdateStatusKind.Ready);
        status.InstalledVersion.Should().Be("1.0.0");
        status.AvailableVersion.Should().Be("2.0.0");
        status.CurrentPlatform.Should().Be("win-x64");
        status.DownloadedAssetName.Should().Be("release.zip");
        status.ScheduledInstallTime.Should().Be(new TimeOnly(4, 0));
        status.AvailableUpdate.Should().NotBeNull();
        status.AvailableUpdate!.Assets.Should().ContainSingle(asset => asset.AssetName == "app.zip");
    }

    /// <summary>
    /// Verifies that when the underlying orchestrator's install attempt fails, the adapter rethrows the original
    /// exception instance/type carried on the <c>AutoUpdateResult</c> rather than wrapping it in a generic failure -
    /// callers (and their exception handling/logging) rely on seeing the real exception type (e.g. a
    /// <see cref="FileNotFoundException"/> vs. an <see cref="IOException"/>) to react appropriately.
    /// </summary>
    /// <param name="exceptionType">The exception type the mocked install failure carries, to verify it survives unchanged.</param>
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

    /// <summary>
    /// Verifies that when a check discovers an available update, the adapter's <c>CheckAsync</c> automatically
    /// triggers the download and returns a result reflecting the post-download "ready to install" status - so the UI
    /// only needs to call check once to end up with a downloaded, installable update, instead of orchestrating a
    /// separate download step itself.
    /// </summary>
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

        var result = await adapter.CheckAsync(TestContext.Current.CancellationToken);

        result.UpdateAvailable.Should().BeTrue();
        result.Message.Should().Be("ready to install");
        result.Status.Should().NotBeNull();
        result.Status.Status.Should().Be(UpdateStatusKind.Ready);
        orchestrator.Verify(o => o.DownloadAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Verifies the complementary case to the auto-download test: when the check finds no update available, the
    /// adapter must not call <c>DownloadAsync</c> at all - downloading is only ever appropriate once an update has
    /// actually been found.
    /// </summary>
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

        var result = await adapter.CheckAsync(TestContext.Current.CancellationToken);

        result.UpdateAvailable.Should().BeFalse();
        result.Message.Should().Be("no update");
        orchestrator.Verify(o => o.DownloadAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Verifies that saving update settings through the adapter both persists them via the settings store and
    /// immediately applies them to the live runtime options - mirroring
    /// <see cref="UpdateOrchestratorAdapterLockAndScheduleTests.ScheduleAsync_SavesScheduleAndAppliesToAutoUpdateOptions"/>
    /// for the full settings form rather than just the scheduled install time.
    /// </summary>
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

        var result = await adapter.SaveSettingsAsync(new UpdateSettingsUpdateRequest(true, "owner", "repo", "update.json", new TimeOnly(20, 0), new TimeOnly(6, 0), null, "svc", null, "custom", 200, true), TestContext.Current.CancellationToken);

        result.Should().Be(savedSettings);
        applied.Should().BeTrue();
    }

    /// <summary>
    /// Verifies that a GitHub API rate-limit failure (a 403 with a specific message pattern) is translated into
    /// <see cref="UpdateErrorMessageMapper.GithubRateLimitMessage"/>, both in the check result and in the status's
    /// <c>LastError</c> - the raw exception text is technical and unhelpful to an end user, while rate limiting is
    /// common enough (frequent update checks against the public GitHub API) to deserve a specific, actionable
    /// message instead of a generic error.
    /// </summary>
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

        var result = await adapter.CheckAsync(TestContext.Current.CancellationToken);

        result.UpdateAvailable.Should().BeFalse();
        result.Message.Should().Be(UpdateErrorMessageMapper.GithubRateLimitMessage);
        result.Status.LastError.Should().Be(UpdateErrorMessageMapper.GithubRateLimitMessage);
    }

    /// <summary>
    /// Verifies that when a successful install leaves no lock file behind (the expected, healthy outcome), the
    /// adapter's post-install cleanup check logs nothing - the diagnostic logging introduced for the
    /// lock-leak/lock-cleanup checks below must stay silent on the happy path.
    /// </summary>
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

        await adapter.StartInstallAsync(true, TestContext.Current.CancellationToken);

        logger.Entries.Should().BeEmpty();
    }

    /// <summary>
    /// Verifies that starting an install when the status is only "update available" (not yet downloaded) makes the
    /// adapter download the package first and only then install it - a caller can invoke install directly without
    /// having explicitly called check/download beforehand, and the adapter must fill that gap itself rather than
    /// failing or installing a stale/nonexistent package.
    /// </summary>
    [Fact]
    public async Task Adapter_StartInstallAsync_WhenUpdateAvailableWithoutDownload_DownloadsBeforeInstall()
    {
        var package = CreatePackage("2.0.0");
        var statusService = UpdateOrchestratorAdapterTestFactory.CreateStatusService();
        await statusService.UpdateAsync(_ => UpdateStatusTestData.UpdateAvailableSnapshot("2.0.0", package), TestContext.Current.CancellationToken);
        var orchestrator = new Mock<IAutoUpdateOrchestrator>();
        orchestrator.Setup(o => o.DownloadAsync(It.IsAny<CancellationToken>()))
            .Callback(() => statusService.UpdateAsync(_ => UpdateStatusTestData.ReadyToInstallSnapshot("2.0.0", package)).GetAwaiter().GetResult())
            .ReturnsAsync(new AutoUpdateResult(AutoUpdateOutcome.Success, AutoUpdateState.ReadyToInstall, "ready to install", null));
        orchestrator.Setup(o => o.InstallAsync(true, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AutoUpdateResult(AutoUpdateOutcome.Success, AutoUpdateState.Success, "installed", null));
        var packageStore = new Mock<IAutoUpdatePackageStore>();
        packageStore.Setup(s => s.GetLockCreatedAtAsync(It.IsAny<CancellationToken>())).ReturnsAsync((DateTimeOffset?)null);
        var adapter = CreateAdapterForInstall(orchestrator.Object, packageStore.Object, new CapturingLogger<UpdateOrchestratorAdapter>(), statusService);

        await adapter.StartInstallAsync(true, TestContext.Current.CancellationToken);

        orchestrator.Verify(o => o.DownloadAsync(It.IsAny<CancellationToken>()), Times.Once);
        orchestrator.Verify(o => o.InstallAsync(true, false, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Verifies that a successful install always triggers the post-install lock check exactly once - the adapter
    /// must proactively verify the updater cleaned up its lock file rather than only checking it reactively when
    /// something looks wrong.
    /// </summary>
    [Fact]
    public async Task Adapter_StartInstallAsync_WhenSuccess_ValidatesLockCleanup()
    {
        var orchestrator = new Mock<IAutoUpdateOrchestrator>();
        orchestrator.Setup(o => o.InstallAsync(true, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AutoUpdateResult(AutoUpdateOutcome.Success, AutoUpdateState.Success, "installed", null));
        var packageStore = new Mock<IAutoUpdatePackageStore>();
        packageStore.Setup(s => s.GetLockCreatedAtAsync(It.IsAny<CancellationToken>())).ReturnsAsync((DateTimeOffset?)null);
        var adapter = CreateAdapterForInstall(orchestrator.Object, packageStore.Object, new CapturingLogger<UpdateOrchestratorAdapter>());

        await adapter.StartInstallAsync(true, TestContext.Current.CancellationToken);

        packageStore.Verify(s => s.GetLockCreatedAtAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Verifies that when the lock file is unexpectedly still present after a reported-successful install, the
    /// adapter logs a warning - a lingering lock after "success" is a symptom worth surfacing to operators (it could
    /// block the next update cycle), even though the install itself is not treated as failed.
    /// </summary>
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

        await adapter.StartInstallAsync(true, TestContext.Current.CancellationToken);

        logger.Entries.Should().ContainSingle(entry => entry.Level == LogLevel.Warning);
    }

    /// <summary>
    /// Verifies that if the post-install lock check itself throws an <see cref="IOException"/> (e.g. transient
    /// filesystem contention while probing the lock), the adapter still returns the successful install status and
    /// merely logs a warning instead of letting the diagnostic check's own failure mask a successful install as an
    /// error.
    /// </summary>
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

        var status = await adapter.StartInstallAsync(true, TestContext.Current.CancellationToken);

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
