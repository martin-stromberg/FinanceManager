namespace SoftwareSchmiede.AutoUpdate;

/// <summary>
/// UI-agnostic entry point for manually triggering update operations, as used by REST APIs, Razor components or
/// console commands.
/// </summary>
public interface IAutoUpdateCommandHandler
{
    /// <summary>
    /// Manually triggers a source check.
    /// </summary>
    /// <param name="ct">A token to observe for cancellation requests.</param>
    /// <returns>The result of the check.</returns>
    Task<AutoUpdateResult> CheckAsync(CancellationToken ct = default);

    /// <summary>
    /// Manually triggers a download of the previously discovered update package.
    /// </summary>
    /// <param name="ct">A token to observe for cancellation requests.</param>
    /// <returns>The result of the download.</returns>
    Task<AutoUpdateResult> DownloadAsync(CancellationToken ct = default);

    /// <summary>
    /// Manually triggers installation of the previously downloaded update package.
    /// </summary>
    /// <param name="confirmDowntime">Must be <see langword="true"/> to acknowledge that installation restarts the application.</param>
    /// <param name="ct">A token to observe for cancellation requests.</param>
    /// <returns>The result of the installation.</returns>
    Task<AutoUpdateResult> InstallAsync(bool confirmDowntime, CancellationToken ct = default);
}
