namespace SoftwareSchmiede.AutoUpdate;

/// <summary>
/// Detects the service(s) the current process is running as, used to auto-detect the installation target when
/// none is configured explicitly.
/// </summary>
public interface IAutoUpdateServiceProbe
{
    /// <summary>
    /// Finds the Windows services the current process belongs to.
    /// </summary>
    /// <returns>The matching service names, empty if none were found or the platform is not Windows.</returns>
    IReadOnlyList<string> FindWindowsServicesForCurrentProcess();

    /// <summary>
    /// Finds the Linux systemd services the current process belongs to.
    /// </summary>
    /// <returns>The matching service names, empty if none were found or the platform is not Linux.</returns>
    IReadOnlyList<string> FindLinuxServicesForCurrentProcess();
}
