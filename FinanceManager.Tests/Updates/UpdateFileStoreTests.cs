using FinanceManager.Web.Services.Updates;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace FinanceManager.Tests.Updates;

public sealed class UpdateFileStoreTests
{
    [Fact]
    public async Task GetLockCreatedAtAsync_ReadsTimestampFromLockContent()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var fileStore = new UpdateFileStore(new TestWebHostEnvironment(root.FullName), Options.Create(new UpdateOptions { WorkingDirectory = "updates" }));
            await fileStore.EnsureAsync();
            var contentTimestamp = DateTimeOffset.UtcNow.AddMinutes(-10);
            await File.WriteAllTextAsync(fileStore.LockPath, contentTimestamp.ToString("O"));
            File.SetCreationTimeUtc(fileStore.LockPath, DateTime.UtcNow);

            var lockCreatedAt = await fileStore.GetLockCreatedAtAsync();

            lockCreatedAt.Should().NotBeNull();
            lockCreatedAt!.Value.Should().BeCloseTo(contentTimestamp, TimeSpan.FromSeconds(1));
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Fact]
    public void SettingsPath_AfterUseWorkingDirectory_StaysAtOriginallyConfiguredDirectory()
    {
        var root = Directory.CreateTempSubdirectory();
        var overrideDir = Directory.CreateTempSubdirectory();
        try
        {
            var fileStore = new UpdateFileStore(new TestWebHostEnvironment(root.FullName), Options.Create(new UpdateOptions { WorkingDirectory = "updates" }));
            var originalSettingsPath = fileStore.SettingsPath;

            fileStore.UseWorkingDirectory(overrideDir.FullName);

            fileStore.RootDirectory.Should().Be(Path.GetFullPath(overrideDir.FullName));
            fileStore.SettingsPath.Should().Be(originalSettingsPath,
                "settings.json must stay at the originally configured location so a restarted process can find " +
                "the persisted working directory before applying it");
        }
        finally
        {
            overrideDir.Delete(recursive: true);
            root.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task GetLockCreatedAtAsync_WhenContentUnparsable_FallsBackToLastWriteTime()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var fileStore = new UpdateFileStore(new TestWebHostEnvironment(root.FullName), Options.Create(new UpdateOptions { WorkingDirectory = "updates" }));
            await fileStore.EnsureAsync();
            await File.WriteAllTextAsync(fileStore.LockPath, "not-a-timestamp");
            var expected = File.GetLastWriteTimeUtc(fileStore.LockPath);

            var lockCreatedAt = await fileStore.GetLockCreatedAtAsync();

            lockCreatedAt.Should().NotBeNull();
            lockCreatedAt!.Value.UtcDateTime.Should().BeCloseTo(expected, TimeSpan.FromSeconds(1));
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task UpdateFileStore_LockLifecycle_TracksFreeActiveAndDeletedLock()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var fileStore = new UpdateFileStore(new TestWebHostEnvironment(root.FullName), Options.Create(new UpdateOptions { WorkingDirectory = "updates" }));

            (await fileStore.GetLockCreatedAtAsync()).Should().BeNull();
            (await fileStore.TryCreateLockAsync()).Should().BeTrue();
            (await fileStore.TryCreateLockAsync()).Should().BeFalse();
            (await fileStore.GetLockCreatedAtAsync()).Should().NotBeNull();
            (await fileStore.DeleteLockAsync()).Should().BeTrue();
            (await fileStore.DeleteLockAsync()).Should().BeFalse();
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

}
