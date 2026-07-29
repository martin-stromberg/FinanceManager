namespace SoftwareSchmiede.AutoUpdate;

/// <summary>
/// Resolves the current platform and selects the matching package from a release manifest.
/// </summary>
public interface IAutoUpdatePlatformResolver
{
    /// <summary>
    /// Gets the .NET runtime identifier of the currently running process (e.g. "win-x64").
    /// </summary>
    string CurrentRuntimeIdentifier { get; }

    /// <summary>
    /// Gets the platform identifier of the currently running process (e.g. "windows", "linux").
    /// </summary>
    string CurrentPlatform { get; }

    /// <summary>
    /// Selects the package matching the current platform and runtime identifier from a release manifest.
    /// </summary>
    /// <param name="release">The release manifest to select a package from.</param>
    /// <returns>The matching package, or <see langword="null"/> if none matches the current platform.</returns>
    AutoUpdatePackageDescriptor? SelectPackage(AutoUpdateReleaseInfo release);
}
