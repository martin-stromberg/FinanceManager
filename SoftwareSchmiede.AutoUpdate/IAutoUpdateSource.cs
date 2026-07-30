namespace SoftwareSchmiede.AutoUpdate;

/// <summary>
/// Abstracts the origin update packages are discovered and downloaded from. Implementations must be stateless
/// and thread-safe, since a single instance is shared as part of the singleton <see cref="AutoUpdateOptions"/>.
/// </summary>
public interface IAutoUpdateSource
{
    /// <summary>
    /// Queries the source for the latest available release.
    /// </summary>
    /// <param name="ct">A token to observe for cancellation requests.</param>
    /// <returns>The result of the check, including the available version and package descriptor, if any.</returns>
    Task<AutoUpdateCheckResult> CheckAsync(CancellationToken ct = default);

    /// <summary>
    /// Downloads the package described by <paramref name="package"/> to <paramref name="targetPath"/>.
    /// </summary>
    /// <param name="package">The package to download.</param>
    /// <param name="targetPath">The local file system path the package is written to.</param>
    /// <param name="maxBytes">The maximum accepted size, in bytes, of the downloaded package.</param>
    /// <param name="ct">A token to observe for cancellation requests.</param>
    /// <returns>A task that completes once the download has finished.</returns>
    Task DownloadAsync(AutoUpdatePackageDescriptor package, string targetPath, long maxBytes, CancellationToken ct = default);
}
