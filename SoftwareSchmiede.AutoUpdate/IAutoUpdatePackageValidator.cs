namespace SoftwareSchmiede.AutoUpdate;

/// <summary>
/// Compares versions and validates downloaded update packages.
/// </summary>
public interface IAutoUpdatePackageValidator
{
    /// <summary>
    /// Determines whether <paramref name="availableVersion"/> is a newer semantic version than
    /// <paramref name="installedVersion"/>.
    /// </summary>
    /// <param name="installedVersion">The currently installed version, or <see langword="null"/> or blank if unknown.</param>
    /// <param name="availableVersion">The version reported by the update source.</param>
    /// <returns>
    /// <see langword="true"/> if <paramref name="availableVersion"/> is newer; otherwise <see langword="false"/>.
    /// When <paramref name="installedVersion"/> is <see langword="null"/>, blank, or not parseable as a version,
    /// this always returns <see langword="false"/> (no update is considered available) rather than treating the
    /// installed version as arbitrarily old - an unreadable <see cref="InstalledReleaseInfo.Version"/> is treated
    /// as a data problem to investigate, not as an automatic trigger to update.
    /// </returns>
    bool IsNewerVersion(string? installedVersion, string availableVersion);

    /// <summary>
    /// Validates a downloaded update package: size, checksum and ZIP archive integrity.
    /// </summary>
    /// <param name="package">The descriptor the package was downloaded from.</param>
    /// <param name="path">The local file system path of the downloaded package.</param>
    /// <param name="maxBytes">The maximum accepted size, in bytes.</param>
    /// <param name="ct">A token to observe for cancellation requests.</param>
    /// <returns>A task that completes once validation has finished, or throws if the package is invalid.</returns>
    Task ValidateDownloadedPackageAsync(AutoUpdatePackageDescriptor package, string path, long maxBytes, CancellationToken ct = default);
}
