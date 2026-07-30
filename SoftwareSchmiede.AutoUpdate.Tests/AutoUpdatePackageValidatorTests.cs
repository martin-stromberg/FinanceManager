using System.IO.Compression;
using System.Security.Cryptography;
using FluentAssertions;
using SoftwareSchmiede.AutoUpdate;

namespace SoftwareSchmiede.AutoUpdate.Tests;

public sealed class AutoUpdatePackageValidatorTests
{
    [Theory]
    [InlineData(null, "1.2.0", false)]
    [InlineData("", "1.2.0", false)]
    [InlineData("   ", "1.2.0", false)]
    [InlineData("not-a-version", "1.2.0", false)]
    [InlineData("1.1.0", "1.2.0", true)]
    [InlineData("1.2.0", "1.2.0", false)]
    [InlineData("1.3.0", "1.2.0", false)]
    public void IsNewerVersion_ComparesInstalledAndAvailableVersions(string? installed, string available, bool expected)
    {
        var validator = new AutoUpdatePackageValidator();

        validator.IsNewerVersion(installed, available).Should().Be(expected);
    }

    [Fact]
    public async Task ValidateDownloadedPackageAsync_AcceptsMatchingPackage()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            var zipPath = await CreateZipAsync(dir.FullName);
            var sha = await Sha256Async(zipPath);
            var package = new AutoUpdatePackageDescriptor("1.2.3", "windows", "win-x64", "release.zip", new Uri(zipPath), sha, new FileInfo(zipPath).Length);
            var validator = new AutoUpdatePackageValidator();

            var act = () => validator.ValidateDownloadedPackageAsync(package, zipPath, maxBytes: 1024 * 1024);

            await act.Should().NotThrowAsync();
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task ValidateDownloadedPackageAsync_RejectsChecksumMismatch()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            var zipPath = await CreateZipAsync(dir.FullName);
            var package = new AutoUpdatePackageDescriptor("1.2.3", "windows", "win-x64", "release.zip", new Uri(zipPath), new string('0', 64), new FileInfo(zipPath).Length);
            var validator = new AutoUpdatePackageValidator();

            var act = () => validator.ValidateDownloadedPackageAsync(package, zipPath, maxBytes: 1024 * 1024);

            await act.Should().ThrowAsync<InvalidOperationException>();
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task ValidateDownloadedPackageAsync_RejectsOversizedPackage()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            var zipPath = await CreateZipAsync(dir.FullName);
            var sha = await Sha256Async(zipPath);
            var package = new AutoUpdatePackageDescriptor("1.2.3", "windows", "win-x64", "release.zip", new Uri(zipPath), sha, new FileInfo(zipPath).Length);
            var validator = new AutoUpdatePackageValidator();

            var act = () => validator.ValidateDownloadedPackageAsync(package, zipPath, maxBytes: 1);

            await act.Should().ThrowAsync<InvalidOperationException>();
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Theory]
    [InlineData("../evil.txt")]
    [InlineData("nested/../../evil.txt")]
    [InlineData("/etc/passwd")]
    [InlineData("C:\\evil.txt")]
    [InlineData("..")]
    public async Task ValidateDownloadedPackageAsync_RejectsZipSlipEntry(string maliciousEntryName)
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            var zipPath = await CreateZipWithEntryAsync(dir.FullName, maliciousEntryName);
            var sha = await Sha256Async(zipPath);
            var package = new AutoUpdatePackageDescriptor("1.2.3", "windows", "win-x64", "release.zip", new Uri(zipPath), sha, new FileInfo(zipPath).Length);
            var validator = new AutoUpdatePackageValidator();

            var act = () => validator.ValidateDownloadedPackageAsync(package, zipPath, maxBytes: 1024 * 1024);

            await act.Should().ThrowAsync<InvalidOperationException>();
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task ValidateDownloadedPackageAsync_AcceptsNestedDirectoryEntry()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            var zipPath = await CreateZipWithEntryAsync(dir.FullName, "nested/app.txt");
            var sha = await Sha256Async(zipPath);
            var package = new AutoUpdatePackageDescriptor("1.2.3", "windows", "win-x64", "release.zip", new Uri(zipPath), sha, new FileInfo(zipPath).Length);
            var validator = new AutoUpdatePackageValidator();

            var act = () => validator.ValidateDownloadedPackageAsync(package, zipPath, maxBytes: 1024 * 1024);

            await act.Should().NotThrowAsync();
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    private static async Task<string> CreateZipAsync(string directory)
        => await CreateZipWithEntryAsync(directory, "app.txt");

    private static async Task<string> CreateZipWithEntryAsync(string directory, string entryName)
    {
        var zipPath = Path.Combine(directory, "release.zip");
        using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry(entryName);
            await using var entryStream = entry.Open();
            await using var writer = new StreamWriter(entryStream);
            await writer.WriteAsync("content");
        }

        return zipPath;
    }

    private static async Task<string> Sha256Async(string path)
    {
        await using var stream = File.OpenRead(path);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream)).ToLowerInvariant();
    }
}
