using System.IO.Compression;
using System.Security.Cryptography;

namespace SoftwareSchmiede.AutoUpdate;

/// <summary>
/// Default <see cref="IAutoUpdatePackageValidator"/> implementation: compares semantic versions and validates
/// downloaded packages by size, SHA256 checksum and ZIP archive integrity.
/// </summary>
public sealed class AutoUpdatePackageValidator : IAutoUpdatePackageValidator
{
    /// <inheritdoc />
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

    /// <inheritdoc />
    public async Task ValidateDownloadedPackageAsync(AutoUpdatePackageDescriptor package, string path, long maxBytes, CancellationToken ct = default)
    {
        var file = new FileInfo(path);
        if (!file.Exists || file.Length == 0)
        {
            throw new InvalidOperationException("Update package was not downloaded.");
        }

        if (file.Length > maxBytes || (package.SizeBytes > 0 && file.Length != package.SizeBytes))
        {
            throw new InvalidOperationException("Update package size does not match the release manifest.");
        }

        var hash = await ComputeSha256Async(path, ct);
        if (!string.Equals(hash, package.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Update package hash does not match the release manifest.");
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
        var segments = name.Split(['/', '\\'], StringSplitOptions.None);
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

    private static string NormalizeVersion(string version) => version.Trim().TrimStart('v', 'V');

    private static async Task<string> ComputeSha256Async(string path, CancellationToken ct)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, ct);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
