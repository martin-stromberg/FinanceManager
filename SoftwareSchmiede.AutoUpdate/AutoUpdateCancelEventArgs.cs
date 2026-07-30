namespace SoftwareSchmiede.AutoUpdate;

/// <summary>
/// Base event argument for cancellable auto-update lifecycle events, raised before the source is checked.
/// </summary>
public class AutoUpdateCancelEventArgs : EventArgs
{
    /// <summary>
    /// Gets or sets a value indicating whether the pending operation should be canceled. Set by a subscriber to
    /// <see langword="true"/> to cancel.
    /// </summary>
    public bool Cancel { get; set; }
}
