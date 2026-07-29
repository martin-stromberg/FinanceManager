namespace SoftwareSchmiede.AutoUpdate;

/// <summary>
/// Default <see cref="IAutoUpdateSource"/> implementation reading a release manifest (<c>update.json</c>) and
/// its packages from a local directory. Deterministic and offline-capable; used as the fallback source when
/// none is configured.
/// </summary>
public sealed class AutoUpdateLocalFolderSource : IAutoUpdateSource
{
    private const string ManifestFileName = "update.json";

    private readonly string _sourceDirectory;
    private readonly IAutoUpdatePlatformResolver _platformResolver;

    /// <summary>
    /// Initializes a new instance of the <see cref="AutoUpdateLocalFolderSource"/> class.
    /// </summary>
    /// <param name="sourceDirectory">The local directory the release manifest and packages are read from.</param>
    /// <param name="platformResolver">Used to select the package matching the current platform.</param>
    public AutoUpdateLocalFolderSource(string sourceDirectory, IAutoUpdatePlatformResolver? platformResolver = null)
    {
        if (string.IsNullOrWhiteSpace(sourceDirectory))
        {
            throw new ArgumentException("Source directory must not be empty.", nameof(sourceDirectory));
        }

        _sourceDirectory = sourceDirectory;
        _platformResolver = platformResolver ?? new AutoUpdatePlatformResolver();
    }

    /// <inheritdoc />
    public async Task<AutoUpdateCheckResult> CheckAsync(CancellationToken ct = default)
    {
        var manifestPath = Path.Combine(_sourceDirectory, ManifestFileName);
        if (!Directory.Exists(_sourceDirectory) || !File.Exists(manifestPath))
        {
            return new AutoUpdateCheckResult(null, null, null, null);
        }

        var release = await JsonFileStore.ReadAsync<AutoUpdateReleaseInfo>(manifestPath, ct);
        if (release is null)
        {
            return new AutoUpdateCheckResult(null, null, null, null);
        }

        var localized = release with
        {
            Packages = release.Packages
                .Select(package => package with { Uri = new Uri(Path.Combine(_sourceDirectory, package.FileName)) })
                .ToList()
        };

        var selected = _platformResolver.SelectPackage(localized);
        return new AutoUpdateCheckResult(localized.Version, selected, localized.ReleaseNotes, localized.PublishedAt);
    }

    /// <inheritdoc />
    public async Task DownloadAsync(AutoUpdatePackageDescriptor package, string targetPath, long maxBytes, CancellationToken ct = default)
    {
        var sourcePath = package.Uri.IsFile ? package.Uri.LocalPath : Path.Combine(_sourceDirectory, package.FileName);
        var sourceInfo = new FileInfo(sourcePath);
        if (!sourceInfo.Exists)
        {
            throw new FileNotFoundException("The update package was not found in the source directory.", sourcePath);
        }

        if (sourceInfo.Length > maxBytes)
        {
            throw new InvalidOperationException("Update package exceeds the configured size limit.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        await using var source = File.OpenRead(sourcePath);
        await using var target = File.Create(targetPath);
        await source.CopyToAsync(target, ct);
    }
}
