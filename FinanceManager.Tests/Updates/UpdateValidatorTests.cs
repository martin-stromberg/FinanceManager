using System.IO.Compression;
using System.Security.Cryptography;
using FinanceManager.Shared.Dtos.Update;
using FinanceManager.Web.Services.Updates;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace FinanceManager.Tests.Updates;

public sealed class UpdateValidatorTests
{
    [Fact]
    public void IsNewerVersion_RequiresInstalledVersion()
    {
        var validator = new UpdateValidator(Options.Create(new UpdateOptions()));

        validator.IsNewerVersion(null, "1.2.0").Should().BeFalse();
        validator.IsNewerVersion("1.1.0", "1.2.0").Should().BeTrue();
        validator.IsNewerVersion("1.2.0", "1.2.0").Should().BeFalse();
    }

    [Fact]
    public async Task ValidateDownloadedAssetAsync_VerifiesSizeHashAndZip()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            var zipPath = Path.Combine(dir.FullName, "release.zip");
            using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                var entry = archive.CreateEntry("app.txt");
                await using var entryStream = entry.Open();
                await using var writer = new StreamWriter(entryStream);
                await writer.WriteAsync("content");
            }

            var sha = await Sha256Async(zipPath);
            var asset = new UpdateAssetDto("windows", "win-x64", "release.zip", "https://example.test/release.zip", sha, new FileInfo(zipPath).Length);
            var validator = new UpdateValidator(Options.Create(new UpdateOptions()));

            await validator.ValidateDownloadedAssetAsync(asset, zipPath, maxBytes: 1024 * 1024);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Theory]
    [InlineData("../evil.txt")]
    [InlineData("/tmp/evil.txt")]
    [InlineData("C:/tmp/evil.txt")]
    [InlineData("folder/../../evil.txt")]
    public async Task ValidateDownloadedAssetAsync_RejectsUnsafeEntryPaths(string entryName)
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            var zipPath = Path.Combine(dir.FullName, "release.zip");
            using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                var entry = archive.CreateEntry(entryName);
                await using var entryStream = entry.Open();
                await using var writer = new StreamWriter(entryStream);
                await writer.WriteAsync("content");
            }

            var asset = new UpdateAssetDto("windows", "win-x64", "release.zip", "https://example.test/release.zip", await Sha256Async(zipPath), new FileInfo(zipPath).Length);
            var validator = new UpdateValidator(Options.Create(new UpdateOptions()));

            await Assert.ThrowsAsync<InvalidOperationException>(() => validator.ValidateDownloadedAssetAsync(asset, zipPath, maxBytes: 1024 * 1024));
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task ValidateDownloadedAssetAsync_RejectsUnixSymlinkEntry()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            var zipPath = Path.Combine(dir.FullName, "release.zip");
            using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                var entry = archive.CreateEntry("link");
                entry.ExternalAttributes = unchecked((int)0xA0000000);
                await using var entryStream = entry.Open();
                await using var writer = new StreamWriter(entryStream);
                await writer.WriteAsync("target");
            }

            var asset = new UpdateAssetDto("linux", "linux-x64", "release.zip", "https://example.test/release.zip", await Sha256Async(zipPath), new FileInfo(zipPath).Length);
            var validator = new UpdateValidator(Options.Create(new UpdateOptions()));

            await Assert.ThrowsAsync<InvalidOperationException>(() => validator.ValidateDownloadedAssetAsync(asset, zipPath, maxBytes: 1024 * 1024));
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Theory]
    [MemberData(nameof(InvalidManifestCases))]
    public void ValidateManifest_RejectsInvalidManifestFields(UpdateMetadataDto manifest, string expectedMessage)
    {
        var validator = new UpdateValidator(Options.Create(new UpdateOptions()));

        var act = () => validator.ValidateManifest(manifest, Settings(), "windows");

        act.Should().Throw<InvalidOperationException>().WithMessage(expectedMessage);
    }

    [Fact]
    public void ValidateManifest_RejectsPlatformRuntimeMismatch()
    {
        var validator = new UpdateValidator(Options.Create(new UpdateOptions()));
        var manifest = ValidManifest() with
        {
            Assets = new[]
            {
                ValidAsset() with { Platform = "linux", RuntimeIdentifier = "win-x64" }
            }
        };

        var act = () => validator.ValidateManifest(manifest, Settings(), "windows");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Update manifest asset platform 'linux' does not match runtime 'win-x64'.");
    }

    public static TheoryData<UpdateMetadataDto, string> InvalidManifestCases()
        => new()
        {
            { ValidManifest() with { Version = "" }, "Update manifest version is invalid." },
            { ValidManifest() with { PublishedAt = null }, "Update manifest publishedAt is missing." },
            { ValidManifest() with { ReleaseNotes = "" }, "Update manifest release notes are missing." },
            { ValidManifest() with { RepositoryOwner = "other" }, "Update manifest repository does not match the configured repository." },
            { ValidManifest() with { Assets = Array.Empty<UpdateAssetDto>() }, "Update manifest does not contain any assets." },
            { ValidManifest() with { Assets = new[] { ValidAsset() with { AssetName = "wrong.zip" } } }, "Update manifest asset name 'wrong.zip' does not match the release schema." },
            { ValidManifest() with { Assets = new[] { ValidAsset() with { AssetUrl = "http://github.com/martin-stromberg/FinanceManager/releases/download/v1.2.3/FinanceManager-v1.2.3-win-x64.zip" } } }, "Update manifest asset URL must be an HTTPS GitHub release URL." },
            { ValidManifest() with { Assets = new[] { ValidAsset() with { Sha256 = "abc" } } }, "Update manifest asset sha256 is invalid." },
            { ValidManifest() with { Assets = new[] { ValidAsset() with { SizeBytes = 0 } } }, "Update manifest asset size must be positive." }
        };

    private static async Task<string> Sha256Async(string path)
    {
        await using var stream = File.OpenRead(path);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream)).ToLowerInvariant();
    }

    private static UpdateSettingsDto Settings()
        => new(false, 60, "martin-stromberg", "FinanceManager", "update.json", null, "FinanceManager", null, "updates", 120);

    private static UpdateMetadataDto ValidManifest()
        => new("1.2.3", "Release notes", DateTimeOffset.Parse("2026-07-19T00:00:00Z"), "martin-stromberg", "FinanceManager", new[] { ValidAsset() });

    private static UpdateAssetDto ValidAsset()
        => new(
            "windows",
            "win-x64",
            "FinanceManager-v1.2.3-win-x64.zip",
            "https://github.com/martin-stromberg/FinanceManager/releases/download/v1.2.3/FinanceManager-v1.2.3-win-x64.zip",
            new string('a', 64),
            42);
}
