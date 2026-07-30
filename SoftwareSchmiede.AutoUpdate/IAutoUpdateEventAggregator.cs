namespace SoftwareSchmiede.AutoUpdate;

/// <summary>
/// Registers and raises the lifecycle events of the auto-update workflow. Implementations must be thread-safe.
/// </summary>
public interface IAutoUpdateEventAggregator
{
    /// <summary>
    /// Raised before the update source is checked for a newer version. Subscribers can cancel the check.
    /// </summary>
    event EventHandler<AutoUpdateCancelEventArgs>? BeforeCheckSource;

    /// <summary>
    /// Raised before an update package is downloaded. Subscribers can cancel the download.
    /// </summary>
    event EventHandler<BeforeDownloadEventArgs>? BeforeDownload;

    /// <summary>
    /// Raised before an installation is started. Subscribers can cancel the installation.
    /// </summary>
    event EventHandler<BeforeInstallEventArgs>? BeforeInstall;

    /// <summary>
    /// Raised before the installation script is started. Subscribers can cancel the script start.
    /// </summary>
    event EventHandler<BeforeStartUpdateScriptEventArgs>? BeforeStartUpdateScript;

    /// <summary>
    /// Raised after the installation script has been started successfully.
    /// </summary>
    event EventHandler? AfterStartUpdateScript;

    /// <summary>
    /// Raised whenever an error occurs during the update workflow, including errors thrown by other event
    /// subscribers.
    /// </summary>
    event EventHandler<AutoUpdateErrorEventArgs>? ErrorOccurred;

    /// <summary>
    /// Raises <see cref="BeforeCheckSource"/> and returns whether any subscriber requested cancellation.
    /// </summary>
    /// <param name="sender">The object raising the event.</param>
    /// <returns><see langword="true"/> if a subscriber canceled the operation.</returns>
    bool RaiseBeforeCheckSource(object sender);

    /// <summary>
    /// Raises <see cref="BeforeDownload"/> and returns whether any subscriber requested cancellation.
    /// </summary>
    /// <param name="sender">The object raising the event.</param>
    /// <param name="sourceUri">The location the package will be downloaded from.</param>
    /// <returns><see langword="true"/> if a subscriber canceled the operation.</returns>
    bool RaiseBeforeDownload(object sender, Uri sourceUri);

    /// <summary>
    /// Raises <see cref="BeforeInstall"/> and returns whether any subscriber requested cancellation.
    /// </summary>
    /// <param name="sender">The object raising the event.</param>
    /// <param name="packageFile">The downloaded package file about to be installed.</param>
    /// <returns><see langword="true"/> if a subscriber canceled the operation.</returns>
    bool RaiseBeforeInstall(object sender, FileInfo packageFile);

    /// <summary>
    /// Raises <see cref="BeforeStartUpdateScript"/> and returns whether any subscriber requested cancellation.
    /// </summary>
    /// <param name="sender">The object raising the event.</param>
    /// <param name="scriptFile">The generated installation script about to be started.</param>
    /// <returns><see langword="true"/> if a subscriber canceled the operation.</returns>
    bool RaiseBeforeStartUpdateScript(object sender, FileInfo scriptFile);

    /// <summary>
    /// Raises <see cref="AfterStartUpdateScript"/>. This event has no cancellation semantics.
    /// </summary>
    /// <param name="sender">The object raising the event.</param>
    void RaiseAfterStartUpdateScript(object sender);

    /// <summary>
    /// Raises <see cref="ErrorOccurred"/>.
    /// </summary>
    /// <param name="sender">The object raising the event.</param>
    /// <param name="error">The exception that occurred.</param>
    /// <param name="phase">A short identifier of the workflow phase the error occurred in.</param>
    void RaiseErrorOccurred(object sender, Exception error, string phase);
}
