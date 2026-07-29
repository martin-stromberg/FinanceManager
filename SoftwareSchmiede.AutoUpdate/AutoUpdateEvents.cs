using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace SoftwareSchmiede.AutoUpdate;

/// <summary>
/// Thread-safe implementation of <see cref="IAutoUpdateEventAggregator"/>. Raise methods invoke all subscribers
/// even if one throws; exceptions from subscribers are reported via <see cref="ErrorOccured"/> instead of
/// propagating, and do not count as a cancellation vote.
/// </summary>
public sealed class AutoUpdateEvents : IAutoUpdateEventAggregator
{
    private readonly Lock _gate = new();
    private readonly ILogger<AutoUpdateEvents> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AutoUpdateEvents"/> class.
    /// </summary>
    /// <param name="logger">Used to log exceptions thrown by <see cref="ErrorOccured"/> subscribers, which cannot otherwise be reported. Defaults to a no-op logger.</param>
    public AutoUpdateEvents(ILogger<AutoUpdateEvents>? logger = null)
    {
        _logger = logger ?? NullLogger<AutoUpdateEvents>.Instance;
    }

    private EventHandler<AutoUpdateCancelEventArgs>? _beforeCheckSource;
    private EventHandler<BeforeDownloadEventArgs>? _beforeDownload;
    private EventHandler<BeforeInstallEventArgs>? _beforeInstall;
    private EventHandler<BeforeStartUpdateScriptEventArgs>? _beforeStartUpdateScript;
    private EventHandler? _afterStartUpdateScript;
    private EventHandler<AutoUpdateErrorEventArgs>? _errorOccured;

    /// <inheritdoc />
    public event EventHandler<AutoUpdateCancelEventArgs>? BeforeCheckSource
    {
        add { lock (_gate) { _beforeCheckSource += value; } }
        remove { lock (_gate) { _beforeCheckSource -= value; } }
    }

    /// <inheritdoc />
    public event EventHandler<BeforeDownloadEventArgs>? BeforeDownload
    {
        add { lock (_gate) { _beforeDownload += value; } }
        remove { lock (_gate) { _beforeDownload -= value; } }
    }

    /// <inheritdoc />
    public event EventHandler<BeforeInstallEventArgs>? BeforeInstall
    {
        add { lock (_gate) { _beforeInstall += value; } }
        remove { lock (_gate) { _beforeInstall -= value; } }
    }

    /// <inheritdoc />
    public event EventHandler<BeforeStartUpdateScriptEventArgs>? BeforeStartUpdateScript
    {
        add { lock (_gate) { _beforeStartUpdateScript += value; } }
        remove { lock (_gate) { _beforeStartUpdateScript -= value; } }
    }

    /// <inheritdoc />
    public event EventHandler? AfterStartUpdateScript
    {
        add { lock (_gate) { _afterStartUpdateScript += value; } }
        remove { lock (_gate) { _afterStartUpdateScript -= value; } }
    }

    /// <inheritdoc />
    public event EventHandler<AutoUpdateErrorEventArgs>? ErrorOccured
    {
        add { lock (_gate) { _errorOccured += value; } }
        remove { lock (_gate) { _errorOccured -= value; } }
    }

    /// <summary>
    /// Raises <see cref="BeforeCheckSource"/> and returns whether any subscriber requested cancellation.
    /// </summary>
    /// <param name="sender">The object raising the event.</param>
    /// <returns><see langword="true"/> if a subscriber canceled the operation.</returns>
    public bool RaiseBeforeCheckSource(object sender)
    {
        EventHandler<AutoUpdateCancelEventArgs>? handler;
        lock (_gate) { handler = _beforeCheckSource; }
        return RaiseCancelable(sender, static () => new AutoUpdateCancelEventArgs(), handler, "BeforeCheckSource");
    }

    /// <summary>
    /// Raises <see cref="BeforeDownload"/> and returns whether any subscriber requested cancellation.
    /// </summary>
    /// <param name="sender">The object raising the event.</param>
    /// <param name="sourceUri">The location the package will be downloaded from.</param>
    /// <returns><see langword="true"/> if a subscriber canceled the operation.</returns>
    public bool RaiseBeforeDownload(object sender, Uri sourceUri)
    {
        EventHandler<BeforeDownloadEventArgs>? handler;
        lock (_gate) { handler = _beforeDownload; }
        return RaiseCancelable(sender, () => new BeforeDownloadEventArgs(sourceUri), handler, "BeforeDownload");
    }

    /// <summary>
    /// Raises <see cref="BeforeInstall"/> and returns whether any subscriber requested cancellation.
    /// </summary>
    /// <param name="sender">The object raising the event.</param>
    /// <param name="packageFile">The downloaded package file about to be installed.</param>
    /// <returns><see langword="true"/> if a subscriber canceled the operation.</returns>
    public bool RaiseBeforeInstall(object sender, FileInfo packageFile)
    {
        EventHandler<BeforeInstallEventArgs>? handler;
        lock (_gate) { handler = _beforeInstall; }
        return RaiseCancelable(sender, () => new BeforeInstallEventArgs(packageFile), handler, "BeforeInstall");
    }

    /// <summary>
    /// Raises <see cref="BeforeStartUpdateScript"/> and returns whether any subscriber requested cancellation.
    /// </summary>
    /// <param name="sender">The object raising the event.</param>
    /// <param name="scriptFile">The generated installation script about to be started.</param>
    /// <returns><see langword="true"/> if a subscriber canceled the operation.</returns>
    public bool RaiseBeforeStartUpdateScript(object sender, FileInfo scriptFile)
    {
        EventHandler<BeforeStartUpdateScriptEventArgs>? handler;
        lock (_gate) { handler = _beforeStartUpdateScript; }
        return RaiseCancelable(sender, () => new BeforeStartUpdateScriptEventArgs(scriptFile), handler, "BeforeStartUpdateScript");
    }

    /// <summary>
    /// Raises <see cref="AfterStartUpdateScript"/>. This event has no cancellation semantics.
    /// </summary>
    /// <param name="sender">The object raising the event.</param>
    public void RaiseAfterStartUpdateScript(object sender)
    {
        EventHandler? handler;
        lock (_gate) { handler = _afterStartUpdateScript; }
        if (handler is null)
        {
            return;
        }

        foreach (var subscriber in handler.GetInvocationList().Cast<EventHandler>())
        {
            try
            {
                subscriber(sender, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                RaiseErrorOccured(sender, ex, "AfterStartUpdateScript");
            }
        }
    }

    /// <summary>
    /// Raises <see cref="ErrorOccured"/>.
    /// </summary>
    /// <param name="sender">The object raising the event.</param>
    /// <param name="error">The exception that occurred.</param>
    /// <param name="phase">A short identifier of the workflow phase the error occurred in.</param>
    public void RaiseErrorOccured(object sender, Exception error, string phase)
    {
        EventHandler<AutoUpdateErrorEventArgs>? handler;
        lock (_gate) { handler = _errorOccured; }
        if (handler is null)
        {
            return;
        }

        var args = new AutoUpdateErrorEventArgs(error, phase);
        foreach (var subscriber in handler.GetInvocationList().Cast<EventHandler<AutoUpdateErrorEventArgs>>())
        {
            try
            {
                subscriber(sender, args);
            }
            catch (Exception ex)
            {
                // A failing ErrorOccured subscriber must not destabilize the library further; log for diagnostics only.
                _logger.LogWarning(ex, "An ErrorOccured subscriber threw while handling a {Phase} error.", phase);
            }
        }
    }

    private bool RaiseCancelable<TArgs>(object sender, Func<TArgs> createArgs, EventHandler<TArgs>? handler, string phase)
        where TArgs : AutoUpdateCancelEventArgs
    {
        if (handler is null)
        {
            return false;
        }

        var canceled = false;
        foreach (var subscriber in handler.GetInvocationList().Cast<EventHandler<TArgs>>())
        {
            // Each subscriber gets its own args instance so an earlier subscriber's cancellation cannot be
            // observed - and accidentally undone - by a later one.
            var args = createArgs();
            try
            {
                subscriber(sender, args);
                if (args.Cancel)
                {
                    canceled = true;
                }
            }
            catch (Exception ex)
            {
                RaiseErrorOccured(sender, ex, phase);
            }
        }

        return canceled;
    }
}
