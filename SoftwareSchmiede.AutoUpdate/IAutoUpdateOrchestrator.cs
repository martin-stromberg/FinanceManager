namespace SoftwareSchmiede.AutoUpdate;

/// <summary>
/// Coordinates the full auto-update workflow: checking, downloading and installing, including event raising,
/// status persistence and error handling. Registered as a singleton.
/// </summary>
public interface IAutoUpdateOrchestrator
{
    /// <summary>
    /// Runs the full update workflow: check, and, depending on configuration, download and install.
    /// </summary>
    /// <param name="ct">A token to observe for cancellation requests.</param>
    /// <returns>The result of the workflow.</returns>
    Task<AutoUpdateResult> RunUpdateAsync(CancellationToken ct = default);

    /// <summary>
    /// Checks the configured source for a newer version, without downloading or installing it.
    /// </summary>
    /// <param name="ct">A token to observe for cancellation requests.</param>
    /// <returns>The result of the check.</returns>
    Task<AutoUpdateResult> CheckForUpdateAsync(CancellationToken ct = default);

    /// <summary>
    /// Downloads the previously discovered update package.
    /// </summary>
    /// <param name="ct">A token to observe for cancellation requests.</param>
    /// <returns>The result of the download.</returns>
    Task<AutoUpdateResult> DownloadAsync(CancellationToken ct = default);

    /// <summary>
    /// Installs the previously downloaded update package.
    /// </summary>
    /// <param name="confirmDowntime">Must be <see langword="true"/> to acknowledge that installation restarts the application.</param>
    /// <param name="ct">A token to observe for cancellation requests.</param>
    /// <returns>The result of the installation.</returns>
    Task<AutoUpdateResult> InstallAsync(bool confirmDowntime, CancellationToken ct = default);

    /// <summary>
    /// Gets the current status snapshot, reconciling it with the installed version after a restart if necessary.
    /// </summary>
    /// <param name="ct">A token to observe for cancellation requests.</param>
    /// <returns>The current status snapshot.</returns>
    Task<AutoUpdateStatusSnapshot> GetStatusAsync(CancellationToken ct = default);
}
