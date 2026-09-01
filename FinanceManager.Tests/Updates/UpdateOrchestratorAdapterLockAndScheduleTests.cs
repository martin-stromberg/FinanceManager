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
    /// <summary>
    /// Verifies that resetting the update lock when no lock is currently recorded throws a typed
    /// <see cref="UpdateLockResetException"/> with <see cref="UpdateLockResetFailureKind.NoLock"/> and does not
    /// attempt to delete anything - an admin manually clearing a lock that has already gone away should get a clear
    /// "there was nothing to reset" error rather than a generic failure.
    /// </summary>
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

    /// <summary>
    /// Verifies that resetting a lock that is still recent (not yet considered stale) is refused with
    /// <see cref="UpdateLockResetFailureKind.LockNotStale"/> and the lock is left in place - manual lock reset is a
    /// last-resort admin action for a genuinely stuck update; it must not be able to interrupt an update that is
    /// still legitimately in progress.
    /// </summary>
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

    /// <summary>
    /// Verifies that when the lock is stale but the package store reports it could not delete the lock file
    /// (returns false rather than throwing), the adapter surfaces
    /// <see cref="UpdateLockResetFailureKind.LockDeleteFailed"/> attributed to
    /// <see cref="UpdateLockResetFailureSource.FinanceManager"/> - distinguishing this from an updater-side I/O
    /// failure lets the admin-facing error message point at the right layer.
    /// </summary>
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

    /// <summary>
    /// Verifies that when the lock is stale but deleting it throws an <see cref="IOException"/> from the updater
    /// library, the adapter wraps it as <see cref="UpdateLockResetFailureKind.LockDeleteFailed"/> attributed to
    /// <see cref="UpdateLockResetFailureSource.Updater"/> with the original exception preserved as the inner
    /// exception - the failure-source split (this test) vs. the store-returned-false split (the previous test) lets
    /// the caller tell "our code refused" apart from "the filesystem failed", which matters for diagnosing a locked
    /// file held by another process.
    /// </summary>
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

    /// <summary>
    /// Verifies that a failure to even read the lock's creation time (before the stale check can run) is reported
    /// as the broader <see cref="UpdateLockResetFailureKind.ResetFailed"/> rather than one of the more specific
    /// kinds - an early I/O failure means the code never learned enough about the lock to classify the failure more
    /// precisely.
    /// </summary>
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

    /// <summary>
    /// Verifies the success path: a genuinely stale lock is deleted, and the update status snapshot is updated to
    /// reflect the unlocked state with the reset reason recorded in <c>LastError</c> - so the reset is both
    /// effective and auditable from the status the UI displays afterward.
    /// </summary>
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

    /// <summary>
    /// Verifies that scheduling a new install time persists it via the settings store and immediately applies the
    /// saved settings onto the live <c>AutoUpdateOptions</c> - a scheduled install time set through the UI must take
    /// effect right away rather than only after the next application restart re-reads settings.
    /// </summary>
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
