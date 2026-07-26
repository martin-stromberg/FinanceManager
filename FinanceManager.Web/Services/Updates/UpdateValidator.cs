#pragma warning disable CS1591
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using FinanceManager.Shared.Dtos.Update;
using Microsoft.Extensions.Options;

namespace FinanceManager.Web.Services.Updates;

public sealed class UpdateValidator : IUpdateValidator
{
    private static readonly Regex Sha256Regex = new("^[a-fA-F0-9]{64}$", RegexOptions.Compiled);
    private static readonly IReadOnlyDictionary<string, string> RuntimePlatforms = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["win-x64"] = "windows",
        ["linux-x64"] = "linux"
    };

    private readonly UpdateOptions _options;

    public UpdateValidator(IOptions<UpdateOptions> options)
    {
        _options = options.Value;
    }

    public bool IsNewerVersion(string? installedVersion, string availableVersion)
    {
        if (string.IsNullOrWhiteSpace(installedVersion))
        {
            return false;
        }

        if (!Version.TryParse(NormalizeVersion(installedVersion), out var installed) ||
            !Version.TryParse(NormalizeVersion(availableVersion), out var available))
        {
            return false;
        }

        return available > installed;
    }

    public void ValidateManifest(UpdateMetadataDto manifest, UpdateSettingsDto settings, string currentPlatform)
    {
        if (string.IsNullOrWhiteSpace(manifest.Version) ||
            !Version.TryParse(NormalizeVersion(manifest.Version), out _))
        {
            throw new InvalidOperationException("Update manifest version is invalid.");
        }

        if (manifest.PublishedAt is null)
        {
            throw new InvalidOperationException("Update manifest publishedAt is missing.");
        }

        if (string.IsNullOrWhiteSpace(manifest.ReleaseNotes))
        {
            throw new InvalidOperationException("Update manifest release notes are missing.");
        }

        if (!string.Equals(manifest.RepositoryOwner, settings.RepositoryOwner, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(manifest.RepositoryName, settings.RepositoryName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Update manifest repository does not match the configured repository.");
        }

        if (manifest.Assets is null || manifest.Assets.Count == 0)
        {
            throw new InvalidOperationException("Update manifest does not contain any assets.");
        }

        var seenRuntimeIdentifiers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var asset in manifest.Assets)
        {
            ValidateManifestAsset(manifest, asset);
            if (!seenRuntimeIdentifiers.Add(asset.RuntimeIdentifier))
            {
                throw new InvalidOperationException($"Update manifest contains multiple assets for runtime '{asset.RuntimeIdentifier}'.");
            }
        }

        var hasCurrentPlatformAsset = manifest.Assets.Any(asset =>
            string.Equals(asset.Platform, currentPlatform, StringComparison.OrdinalIgnoreCase));
        if (!hasCurrentPlatformAsset)
        {
            throw new InvalidOperationException($"Update manifest does not contain an asset for platform '{currentPlatform}'.");
        }
    }

    public async Task ValidateDownloadedAssetAsync(UpdateAssetDto asset, string path, long maxBytes, CancellationToken ct = default)
    {
        var file = new FileInfo(path);
        if (!file.Exists || file.Length == 0)
        {
            throw new InvalidOperationException("Update package was not downloaded.");
        }

        if (file.Length > maxBytes || (asset.SizeBytes > 0 && file.Length != asset.SizeBytes))
        {
            throw new InvalidOperationException("Update package size does not match the manifest.");
        }

        var hash = await ComputeSha256Async(path, ct);
        if (!string.Equals(hash, asset.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Update package hash does not match the manifest.");
        }

        using var archive = ZipFile.OpenRead(path);
        if (archive.Entries.Count == 0)
        {
            throw new InvalidOperationException("Update package is empty.");
        }

        foreach (var entry in archive.Entries)
        {
            ValidateEntry(entry);
        }
    }

    private static void ValidateEntry(ZipArchiveEntry entry)
    {
        var name = entry.FullName;
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException("Update package contains an empty entry name.");
        }

        if (name.StartsWith("/", StringComparison.Ordinal) ||
            name.StartsWith("\\", StringComparison.Ordinal) ||
            Path.IsPathRooted(name) ||
            name.Contains(':', StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Update package contains an unsafe absolute entry path: {name}");
        }

        var isDirectory = name.EndsWith("/", StringComparison.Ordinal) || name.EndsWith("\\", StringComparison.Ordinal);
        var segments = name.Split(new[] { '/', '\\' }, StringSplitOptions.None);
        for (var i = 0; i < segments.Length; i++)
        {
            var segment = segments[i];
            var isTrailingDirectorySegment = isDirectory && i == segments.Length - 1;
            if ((segment.Length == 0 && !isTrailingDirectorySegment) || segment == "." || segment == "..")
            {
                throw new InvalidOperationException($"Update package contains an unsafe entry path: {name}");
            }
        }

        var mode = (entry.ExternalAttributes >> 16) & 0xF000;
        if (mode != 0 && mode != 0x4000 && mode != 0x8000)
        {
            throw new InvalidOperationException($"Update package contains an unsupported special file entry: {name}");
        }
    }

    private static void ValidateManifestAsset(UpdateMetadataDto manifest, UpdateAssetDto asset)
    {
        if (string.IsNullOrWhiteSpace(asset.Platform))
        {
            throw new InvalidOperationException("Update manifest asset platform is missing.");
        }

        if (string.IsNullOrWhiteSpace(asset.AssetName))
        {
            throw new InvalidOperationException("Update manifest asset name is missing.");
        }

        if (string.IsNullOrWhiteSpace(asset.AssetUrl))
        {
            throw new InvalidOperationException("Update manifest asset URL is missing.");
        }

        if (string.IsNullOrWhiteSpace(asset.Sha256))
        {
            throw new InvalidOperationException("Update manifest asset sha256 is invalid.");
        }

        if (string.IsNullOrWhiteSpace(asset.RuntimeIdentifier))
        {
            throw new InvalidOperationException("Update manifest asset runtime identifier is missing.");
        }

        if (!RuntimePlatforms.TryGetValue(asset.RuntimeIdentifier, out var expectedPlatform) ||
            !string.Equals(asset.Platform, expectedPlatform, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Update manifest asset platform '{asset.Platform}' does not match runtime '{asset.RuntimeIdentifier}'.");
        }

        var expectedAssetName = $"FinanceManager-v{manifest.Version}-{asset.RuntimeIdentifier}.zip";
        if (!string.Equals(asset.AssetName, expectedAssetName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Update manifest asset name '{asset.AssetName}' does not match the release schema.");
        }

        if (!Uri.TryCreate(asset.AssetUrl, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps ||
            !string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Update manifest asset URL must be an HTTPS GitHub release URL.");
        }

        var expectedPath = $"/{manifest.RepositoryOwner}/{manifest.RepositoryName}/releases/download/v{manifest.Version}/{asset.AssetName}";
        var actualPath = Uri.UnescapeDataString(uri.AbsolutePath);
        if (!string.Equals(actualPath, expectedPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Update manifest asset URL does not match the expected GitHub release asset path.");
        }

        if (!Sha256Regex.IsMatch(asset.Sha256 ?? string.Empty))
        {
            throw new InvalidOperationException("Update manifest asset sha256 is invalid.");
        }

        if (asset.SizeBytes <= 0)
        {
            throw new InvalidOperationException("Update manifest asset size must be positive.");
        }
    }

    private static string NormalizeVersion(string version)
        => version.Trim().TrimStart('v', 'V');

    private static async Task<string> ComputeSha256Async(string path, CancellationToken ct)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, ct);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
#pragma warning restore CS1591
