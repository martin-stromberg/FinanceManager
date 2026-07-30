using System.Runtime.InteropServices;
using FluentAssertions;
using SoftwareSchmiede.AutoUpdate;

namespace SoftwareSchmiede.AutoUpdate.Tests;

public sealed class AutoUpdateLocalFolderSourceTests
{
    [Fact]
    public async Task Check_ReadsManifestFromFolder()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            var manifest = $$"""
            {
              "version": "3.0.0",
              "releaseNotes": "notes",
              "publishedAt": "2026-07-01T00:00:00+00:00",
              "packages": [
                { "version": "3.0.0", "platform": "windows", "runtimeIdentifier": "win-x64", "fileName": "app.zip", "uri": "https://example.test/app.zip", "sha256": "{{new string('a', 64)}}", "sizeBytes": 4 }
              ]
            }
            """;
            await File.WriteAllTextAsync(Path.Combine(dir.FullName, "update.json"), manifest);
            await File.WriteAllTextAsync(Path.Combine(dir.FullName, "app.zip"), "test");

            var source = new AutoUpdateLocalFolderSource(dir.FullName, new AutoUpdatePlatformResolver(p => p == OSPlatform.Windows, "win-x64"));

            var result = await source.CheckAsync();

            result.AvailableVersion.Should().Be("3.0.0");
            result.Package.Should().NotBeNull();
            result.Package!.FileName.Should().Be("app.zip");
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task Check_WhenFolderMissing_ReturnsNoUpdate()
    {
        var source = new AutoUpdateLocalFolderSource(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));

        var result = await source.CheckAsync();

        result.AvailableVersion.Should().BeNull();
        result.Package.Should().BeNull();
    }

    [Fact]
    public async Task Download_CopiesPackageToTarget()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            var sourceFile = Path.Combine(dir.FullName, "app.zip");
            await File.WriteAllTextAsync(sourceFile, "payload");
            var descriptor = new AutoUpdatePackageDescriptor("1.0.0", "windows", "win-x64", "app.zip", new Uri(sourceFile), new string('a', 64), 7);
            var source = new AutoUpdateLocalFolderSource(dir.FullName);
            var targetPath = Path.Combine(dir.FullName, "target", "app.zip");

            await source.DownloadAsync(descriptor, targetPath, maxBytes: 1024);

            File.Exists(targetPath).Should().BeTrue();
            (await File.ReadAllTextAsync(targetPath)).Should().Be("payload");
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }
}
