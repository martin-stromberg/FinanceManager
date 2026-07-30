namespace SoftwareSchmiede.AutoUpdate;

/// <summary>
/// Starts the generated installation script as an external process.
/// </summary>
public interface IAutoUpdateProcessRunner
{
    /// <summary>
    /// Ensures the systemd unit used to run the installation script is available, e.g. by resetting a previously
    /// failed unit on Linux, and throws if an update is already running.
    /// </summary>
    /// <param name="scriptPath">The full path of the installation script.</param>
    void EnsureUpdateUnitAvailable(string scriptPath);

    /// <summary>
    /// Starts the installation script as a detached process.
    /// </summary>
    /// <param name="scriptPath">The full path of the installation script.</param>
    void StartScript(string scriptPath);
}
