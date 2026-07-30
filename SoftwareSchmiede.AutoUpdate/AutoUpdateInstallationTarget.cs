namespace SoftwareSchmiede.AutoUpdate;

/// <summary>
/// Describes the resolved installation target for the current platform, as produced by
/// <see cref="IAutoUpdateServiceResolver"/>.
/// </summary>
/// <param name="Platform">The platform identifier ("windows" or "linux").</param>
/// <param name="ServiceName">The name of the service to stop and restart, if applicable.</param>
/// <param name="ExecutablePath">The path of the executable to restart, if applicable.</param>
/// <returns>An immutable description of the resolved installation target.</returns>
public sealed record AutoUpdateInstallationTarget(
    string Platform,
    string? ServiceName,
    string? ExecutablePath);
