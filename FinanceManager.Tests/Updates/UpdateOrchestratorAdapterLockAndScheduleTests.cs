using FinanceManager.Shared.Dtos.Update;
using FinanceManager.Web.Services.Updates;
using FluentAssertions;
using Moq;
using msTools.Updater;

namespace FinanceManager.Tests.Updates;

/// <summary>
/// Covers <see cref="FinanceManager.Web.Services.Updates.UpdateOrchestratorAdapter.ResetLockAsync"/> and
/// <see cref="FinanceManager.Web.Services.Updates.UpdateOrchestratorAdapter.ScheduleAsync"/>, the two members not
/// already exercised by <see cref="UpdateOrchestratorAdapterTests"/>.
/// </summary>
public sealed class UpdateOrchestratorAdapterLockAndScheduleTests
{
    [Fact]
    public async Task ResetLockAsync_WhenNoLockActive_ThrowsTypedNoLock()
    {
        var packageStore = new Mock<IAutoUpdatePackageStore>();
        packageStore.Setup(s => s.GetLockCreatedAtAsync(It.IsAny<CancellationToken>())).ReturnsAsync((DateTimeOffset?)null);
        var adapter = UpdateOrchestratorAdapterTestFactory.Create(packageStore: packageStore.Object);

        var act = () => adapter.ResetLockAsync("reason");

        var exception = await act.Should().ThrowAsync<UpdateLockResetException>();
        exception.Which.Kind.Should().Be(UpdateLockResetFailureKind.NoLock);
        exception.Which.FailureSource.Should().Be(UpdateLockResetFailureSource.FinanceManager);
        packageStore.Verify(s => s.DeleteLockAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResetLockAsync_WhenLockNotStale_ThrowsTypedLockNotStale()
    {
        var lockCreatedAt = DateTimeOffset.UtcNow;
        var packageStore = new Mock<IAutoUpdatePackageStore>();
        packageStore.Setup(s => s.GetLockCreatedAtAsync(It.IsAny<CancellationToken>())).ReturnsAsync(lockCreatedAt);
        packageStore.Setup(s => s.IsLockStale(lockCreatedAt)).Returns(false);
        var adapter = UpdateOrchestratorAdapterTestFactory.Create(packageStore: packageStore.Object);

        var act = () => adapter.ResetLockAsync("reason");

        var exception = await act.Should().ThrowAsync<UpdateLockResetException>();
        exception.Which.Kind.Should().Be(UpdateLockResetFailureKind.LockNotStale);
        exception.Which.LockCreatedAt.Should().Be(lockCreatedAt);
        packageStore.Verify(s => s.DeleteLockAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResetLockAsync_WhenDeleteReturnsFalse_ThrowsTypedLockDeleteFailed()
    {
        var lockCreatedAt = DateTimeOffset.UtcNow - TimeSpan.FromSeconds(200);
        var packageStore = new Mock<IAutoUpdatePackageStore>();
        packageStore.Setup(s => s.GetLockCreatedAtAsync(It.IsAny<CancellationToken>())).ReturnsAsync(lockCreatedAt);
        packageStore.Setup(s => s.IsLockStale(lockCreatedAt)).Returns(true);
        packageStore.Setup(s => s.DeleteLockAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var adapter = UpdateOrchestratorAdapterTestFactory.Create(packageStore: packageStore.Object);

        var act = () => adapter.ResetLockAsync("reason");

        var exception = await act.Should().ThrowAsync<UpdateLockResetException>();
        exception.Which.Kind.Should().Be(UpdateLockResetFailureKind.LockDeleteFailed);
        exception.Which.FailureSource.Should().Be(UpdateLockResetFailureSource.FinanceManager);
    }

    [Fact]
    public async Task ResetLockAsync_WhenDeleteThrowsIOException_ThrowsTypedLockDeleteFailed()
    {
        var lockCreatedAt = DateTimeOffset.UtcNow - TimeSpan.FromSeconds(200);
        var ioException = new IOException("delete failed");
        var packageStore = new Mock<IAutoUpdatePackageStore>();
        packageStore.Setup(s => s.GetLockCreatedAtAsync(It.IsAny<CancellationToken>())).ReturnsAsync(lockCreatedAt);
        packageStore.Setup(s => s.IsLockStale(lockCreatedAt)).Returns(true);
        packageStore.Setup(s => s.DeleteLockAsync(It.IsAny<CancellationToken>())).ThrowsAsync(ioException);
        var adapter = UpdateOrchestratorAdapterTestFactory.Create(packageStore: packageStore.Object);

        var act = () => adapter.ResetLockAsync("reason");

        var exception = await act.Should().ThrowAsync<UpdateLockResetException>();
        exception.Which.Kind.Should().Be(UpdateLockResetFailureKind.LockDeleteFailed);
        exception.Which.FailureSource.Should().Be(UpdateLockResetFailureSource.Updater);
        exception.Which.InnerException.Should().BeSameAs(ioException);
    }

    [Fact]
    public async Task ResetLockAsync_WhenGetLockCreatedAtThrowsIOException_ThrowsTypedResetFailed()
    {
        var ioException = new IOException("read failed");
        var packageStore = new Mock<IAutoUpdatePackageStore>();
        packageStore.Setup(s => s.GetLockCreatedAtAsync(It.IsAny<CancellationToken>())).ThrowsAsync(ioException);
        var adapter = UpdateOrchestratorAdapterTestFactory.Create(packageStore: packageStore.Object);

        var act = () => adapter.ResetLockAsync("reason");

        var exception = await act.Should().ThrowAsync<UpdateLockResetException>();
        exception.Which.Kind.Should().Be(UpdateLockResetFailureKind.ResetFailed);
        exception.Which.FailureSource.Should().Be(UpdateLockResetFailureSource.Updater);
        exception.Which.InnerException.Should().BeSameAs(ioException);
    }

    [Fact]
    public async Task ResetLockAsync_WhenLockStale_DeletesLockAndUpdatesStatus()
    {
        var lockCreatedAt = DateTimeOffset.UtcNow - TimeSpan.FromSeconds(200);
        var packageStore = new Mock<IAutoUpdatePackageStore>();
        packageStore.Setup(s => s.GetLockCreatedAtAsync(It.IsAny<CancellationToken>())).ReturnsAsync(lockCreatedAt);
        packageStore.Setup(s => s.IsLockStale(lockCreatedAt)).Returns(true);
        packageStore.Setup(s => s.DeleteLockAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var statusService = UpdateOrchestratorAdapterTestFactory.CreateStatusService();
        var adapter = UpdateOrchestratorAdapterTestFactory.Create(packageStore: packageStore.Object, statusService: statusService);

        await adapter.ResetLockAsync("stale lock cleared", TestContext.Current.CancellationToken);

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
        var savedSettings = new UpdateSettingsDto(true, "owner", "repo", "update.json", new TimeOnly(20, 0), new TimeOnly(6, 0), scheduledTime, "svc", null, "updates", 120, false);
        var settingsStore = new Mock<IUpdateSettingsStore>();
        settingsStore.Setup(s => s.SaveScheduleAsync(scheduledTime, It.IsAny<CancellationToken>())).ReturnsAsync(savedSettings);
        var applied = false;
        settingsStore.Setup(s => s.ApplyToOptions(savedSettings))
            .Callback(() => applied = true);
        var adapter = UpdateOrchestratorAdapterTestFactory.Create(settingsStore: settingsStore.Object);

        var result = await adapter.ScheduleAsync(scheduledTime, TestContext.Current.CancellationToken);

        result.Should().Be(savedSettings);
        applied.Should().BeTrue();
        settingsStore.Verify(s => s.SaveScheduleAsync(scheduledTime, It.IsAny<CancellationToken>()), Times.Once);
    }
}
