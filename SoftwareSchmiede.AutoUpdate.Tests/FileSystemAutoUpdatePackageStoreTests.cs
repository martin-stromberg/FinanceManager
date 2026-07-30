using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using SoftwareSchmiede.AutoUpdate;
using SoftwareSchmiede.AutoUpdate.Tests.TestSupport;

namespace SoftwareSchmiede.AutoUpdate.Tests;

public sealed class FileSystemAutoUpdatePackageStoreTests
{
    [Fact]
    public async Task Lock_CreateAndDelete_RoundTrips()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            var store = new FileSystemAutoUpdatePackageStore(new TestAutoUpdateEnvironment(dir.FullName), new AutoUpdateOptions { DownloadPath = "updates" }, TimeProvider.System);

            (await store.TryCreateLockAsync()).Should().BeTrue();
            (await store.TryCreateLockAsync()).Should().BeFalse();
            (await store.GetLockCreatedAtAsync()).Should().NotBeNull();
            (await store.DeleteLockAsync()).Should().BeTrue();
            (await store.GetLockCreatedAtAsync()).Should().BeNull();
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public void PendingPath_RejectsPathSegments()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            var store = new FileSystemAutoUpdatePackageStore(new TestAutoUpdateEnvironment(dir.FullName), new AutoUpdateOptions { DownloadPath = "updates" }, TimeProvider.System);

            var act = () => store.PendingAssetPath("../evil.zip");

            act.Should().Throw<InvalidOperationException>();
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task PendingPath_CreatesDirectoryLayoutOnEnsure()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            var store = new FileSystemAutoUpdatePackageStore(new TestAutoUpdateEnvironment(dir.FullName), new AutoUpdateOptions { DownloadPath = "updates" }, TimeProvider.System);

            await store.EnsureAsync();

            Directory.Exists(store.RootDirectory).Should().BeTrue();
            Directory.Exists(store.PendingDirectory).Should().BeTrue();
            Directory.Exists(store.StagingDirectory).Should().BeTrue();
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public void IsLockStale_WhenLockYoungerThanHealthTimeout_ReturnsFalse()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            var timeProvider = new FakeTimeProvider(DateTimeOffset.Parse("2026-07-28T12:00:00+00:00"));
            var store = new FileSystemAutoUpdatePackageStore(
                new TestAutoUpdateEnvironment(dir.FullName),
                new AutoUpdateOptions { DownloadPath = "updates", HealthTimeoutSeconds = 120 },
                timeProvider);

            var isStale = store.IsLockStale(timeProvider.GetUtcNow() - TimeSpan.FromSeconds(5));

            isStale.Should().BeFalse();
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public void IsLockStale_WhenLockOlderThanHealthTimeout_ReturnsTrue()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            var timeProvider = new FakeTimeProvider(DateTimeOffset.Parse("2026-07-28T12:00:00+00:00"));
            var store = new FileSystemAutoUpdatePackageStore(
                new TestAutoUpdateEnvironment(dir.FullName),
                new AutoUpdateOptions { DownloadPath = "updates", HealthTimeoutSeconds = 120 },
                timeProvider);

            var isStale = store.IsLockStale(timeProvider.GetUtcNow() - TimeSpan.FromSeconds(200));

            isStale.Should().BeTrue();
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }
}
