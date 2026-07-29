using FinanceManager.Shared.Dtos.Update;
using FinanceManager.Web.Services.Updates;
using FluentAssertions;
using Moq;
using SoftwareSchmiede.AutoUpdate;

namespace FinanceManager.Tests.Updates;

/// <summary>
/// Covers <see cref="UpdateOrchestratorAdapter.ResetLockAsync"/> and <see cref="UpdateOrchestratorAdapter.ScheduleAsync"/>,
/// the two members not already exercised by <see cref="UpdateOrchestratorAdapterTests"/>.
/// </summary>
public sealed class UpdateOrchestratorAdapterTests_LockAndSchedule
{
    /// <summary>
    /// Minimal <see cref="TimeProvider"/> test double returning a fixed instant, avoiding a dependency on the
    /// <c>Microsoft.Extensions.TimeProvider.Testing</c> package (not referenced by this test project).
    /// </summary>
    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _now;

        public FixedTimeProvider(DateTimeOffset now) => _now = now;

        public override DateTimeOffset GetUtcNow() => _now;
    }

    [Fact]
    public async Task ResetLockAsync_WhenNoLockActive_ThrowsIOException()
    {
        var packageStore = new Mock<IAutoUpdatePackageStore>();
        packageStore.Setup(s => s.GetLockCreatedAtAsync(It.IsAny<CancellationToken>())).ReturnsAsync((DateTimeOffset?)null);
        var adapter = CreateAdapter(packageStore.Object, new FixedTimeProvider(DateTimeOffset.UtcNow), out _);

        var act = () => adapter.ResetLockAsync("reason");

        await act.Should().ThrowAsync<IOException>();
        packageStore.Verify(s => s.DeleteLockAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResetLockAsync_WhenLockYoungerThanHealthTimeout_ThrowsIOExceptionAndKeepsLock()
    {
        var now = new DateTimeOffset(2026, 7, 28, 12, 0, 0, TimeSpan.Zero);
        var timeProvider = new FixedTimeProvider(now);
        var packageStore = new Mock<IAutoUpdatePackageStore>();
        packageStore.Setup(s => s.GetLockCreatedAtAsync(It.IsAny<CancellationToken>())).ReturnsAsync(now - TimeSpan.FromSeconds(5));
        var adapter = CreateAdapter(packageStore.Object, timeProvider, out _, healthTimeoutSeconds: 120);

        var act = () => adapter.ResetLockAsync("reason");

        await act.Should().ThrowAsync<IOException>();
        packageStore.Verify(s => s.DeleteLockAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResetLockAsync_WhenLockOlderThanHealthTimeout_DeletesLockAndUpdatesStatus()
    {
        var now = new DateTimeOffset(2026, 7, 28, 12, 0, 0, TimeSpan.Zero);
        var timeProvider = new FixedTimeProvider(now);
        var packageStore = new Mock<IAutoUpdatePackageStore>();
        packageStore.Setup(s => s.GetLockCreatedAtAsync(It.IsAny<CancellationToken>())).ReturnsAsync(now - TimeSpan.FromSeconds(200));
        packageStore.Setup(s => s.DeleteLockAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var adapter = CreateAdapter(packageStore.Object, timeProvider, out var statusService, healthTimeoutSeconds: 120);

        await adapter.ResetLockAsync("stale lock cleared");

        packageStore.Verify(s => s.DeleteLockAsync(It.IsAny<CancellationToken>()), Times.Once);
        var snapshot = statusService.GetSnapshot();
        snapshot.IsLocked.Should().BeFalse();
        snapshot.LockCreatedAt.Should().BeNull();
        snapshot.LastError.Should().Be("Lock reset: stale lock cleared");
    }

    [Fact]
    public async Task ScheduleAsync_SavesScheduleAndAppliesToAutoUpdateOptions()
    {
        var scheduledTime = new TimeOnly(3, 0);
        var savedSettings = new UpdateSettingsDto(true, 60, "owner", "repo", "update.json", scheduledTime, "svc", null, "updates", 120);
        var settingsStore = new Mock<IUpdateSettingsStore>();
        settingsStore.Setup(s => s.SaveScheduleAsync(scheduledTime, It.IsAny<CancellationToken>())).ReturnsAsync(savedSettings);
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

        var result = await adapter.ScheduleAsync(scheduledTime);

        result.Should().Be(savedSettings);
        applied.Should().BeTrue();
        settingsStore.Verify(s => s.SaveScheduleAsync(scheduledTime, It.IsAny<CancellationToken>()), Times.Once);
    }

    private static UpdateOrchestratorAdapter CreateAdapter(
        IAutoUpdatePackageStore packageStore,
        TimeProvider timeProvider,
        out AutoUpdateStatusService statusService,
        int healthTimeoutSeconds = 120)
    {
        statusService = CreateStatusService();
        return new UpdateOrchestratorAdapter(
            new Mock<IAutoUpdateOrchestrator>().Object,
            new Mock<IAutoUpdateCommandHandler>().Object,
            new Mock<IAutoUpdateStatusProvider>().Object,
            new Mock<IUpdateSettingsStore>().Object,
            new Mock<IInstalledReleaseMetadataProvider>().Object,
            new Mock<IAutoUpdatePlatformResolver>().Object,
            packageStore,
            statusService,
            new AutoUpdateOptions { HealthTimeoutSeconds = healthTimeoutSeconds },
            timeProvider);
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
