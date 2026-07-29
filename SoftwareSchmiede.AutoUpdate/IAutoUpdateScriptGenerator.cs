namespace SoftwareSchmiede.AutoUpdate;

/// <summary>
/// Generates the platform-specific installation script that extracts a downloaded package and restarts the
/// application.
/// </summary>
public interface IAutoUpdateScriptGenerator
{
    /// <summary>
    /// Generates the installation script for the current platform.
    /// </summary>
    /// <param name="package">The package descriptor being installed.</param>
    /// <param name="zipPath">The local file system path of the downloaded package.</param>
    /// <param name="target">The resolved installation target.</param>
    /// <param name="ct">A token to observe for cancellation requests.</param>
    /// <returns>The full path of the generated script.</returns>
    Task<string> GenerateAsync(AutoUpdatePackageDescriptor package, string zipPath, AutoUpdateInstallationTarget target, CancellationToken ct = default);
}
