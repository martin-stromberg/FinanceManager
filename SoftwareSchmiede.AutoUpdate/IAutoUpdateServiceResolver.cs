namespace SoftwareSchmiede.AutoUpdate;

/// <summary>
/// Resolves the service or executable that must be stopped and restarted during installation.
/// </summary>
public interface IAutoUpdateServiceResolver
{
    /// <summary>
    /// Resolves the installation target for the current platform, using the configured
    /// <see cref="AutoUpdateOptions.ServiceName"/>/<see cref="AutoUpdateOptions.ExecutablePath"/> or, if neither
    /// is configured, auto-detection via <see cref="IAutoUpdateServiceProbe"/>.
    /// </summary>
    /// <returns>The resolved installation target.</returns>
    AutoUpdateInstallationTarget Resolve();
}
