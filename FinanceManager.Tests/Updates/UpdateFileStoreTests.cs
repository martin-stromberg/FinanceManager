using FinanceManager.Web.Services.Updates;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
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
            var fileStore = new UpdateFileStore(new TestEnvironment(root.FullName), Options.Create(new UpdateOptions { WorkingDirectory = "updates" }));
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
            var fileStore = new UpdateFileStore(new TestEnvironment(root.FullName), Options.Create(new UpdateOptions { WorkingDirectory = "updates" }));
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
            var fileStore = new UpdateFileStore(new TestEnvironment(root.FullName), Options.Create(new UpdateOptions { WorkingDirectory = "updates" }));
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

    private sealed class TestEnvironment : IWebHostEnvironment
    {
        public TestEnvironment(string root)
        {
            ContentRootPath = root;
            WebRootPath = root;
        }

        public string ApplicationName { get; set; } = "Tests";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; }
        public string EnvironmentName { get; set; } = "Development";
        public string WebRootPath { get; set; }
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
