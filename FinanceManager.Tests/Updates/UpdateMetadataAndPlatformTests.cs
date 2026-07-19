using System.Runtime.InteropServices;
using FinanceManager.Shared.Dtos.Update;
using FinanceManager.Web.Services.Updates;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;

namespace FinanceManager.Tests.Updates;

public sealed class UpdateMetadataAndPlatformTests
{
    [Fact]
    public async Task InstalledReleaseMetadataProvider_WhenMetadataFileExists_ReadsInstalledRelease()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(root.FullName, "release-metadata.json"),
                """
                {
                  "version": "2.3.4",
                  "publishedAt": "2026-07-19T10:15:00+00:00",
                  "commitSha": "abc123",
                  "repository": "FinanceManager",
                  "runtimeIdentifier": "win-x64"
                }
                """);
            var provider = new InstalledReleaseMetadataProvider(new TestEnvironment(root.FullName));

            var metadata = await provider.GetAsync();

            metadata.Version.Should().Be("2.3.4");
            metadata.CommitSha.Should().Be("abc123");
            metadata.Repository.Should().Be("FinanceManager");
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Fact]
    public void UpdatePlatformResolver_SelectAsset_WhenWindows_SelectsWindowsRuntimeAsset()
    {
        var resolver = new UpdatePlatformResolver(platform => platform == OSPlatform.Windows, "ignored");
        var manifest = Manifest();

        var asset = resolver.SelectAsset(manifest);

        resolver.CurrentPlatform.Should().Be("windows");
        resolver.CurrentRuntimeIdentifier.Should().Be("win-x64");
        asset!.AssetName.Should().Be("windows.zip");
    }

    [Fact]
    public void UpdatePlatformResolver_SelectAsset_WhenLinux_SelectsLinuxRuntimeAsset()
    {
        var resolver = new UpdatePlatformResolver(platform => platform == OSPlatform.Linux, "ignored");
        var manifest = Manifest();

        var asset = resolver.SelectAsset(manifest);

        resolver.CurrentPlatform.Should().Be("linux");
        resolver.CurrentRuntimeIdentifier.Should().Be("linux-x64");
        asset!.AssetName.Should().Be("linux.zip");
    }

    [Fact]
    public async Task UpdateFileStore_LockLifecycle_TracksFreeActiveAndDeletedLock()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var fileStore = new UpdateFileStore(new TestEnvironment(root.FullName), Microsoft.Extensions.Options.Options.Create(new UpdateOptions { WorkingDirectory = "updates" }));

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

    private static UpdateMetadataDto Manifest()
        => new(
            "1.0.0",
            null,
            null,
            "owner",
            "repo",
            new[]
            {
                new UpdateAssetDto("linux", "linux-x64", "linux.zip", "https://example.test/linux.zip", "hash", 1),
                new UpdateAssetDto("windows", "win-x64", "windows.zip", "https://example.test/windows.zip", "hash", 1)
            });

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
