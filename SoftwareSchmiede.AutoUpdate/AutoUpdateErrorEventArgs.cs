namespace SoftwareSchmiede.AutoUpdate;

/// <summary>
/// Event argument raised whenever an error occurs during the update workflow.
/// </summary>
public sealed class AutoUpdateErrorEventArgs : EventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AutoUpdateErrorEventArgs"/> class.
    /// </summary>
    /// <param name="error">The exception that occurred.</param>
    /// <param name="phase">A short identifier of the workflow phase the error occurred in.</param>
    public AutoUpdateErrorEventArgs(Exception error, string phase)
    {
        Error = error;
        Phase = phase;
    }

    /// <summary>
    /// Gets the exception that occurred.
    /// </summary>
    public Exception Error { get; }

    /// <summary>
    /// Gets a short identifier of the workflow phase the error occurred in (e.g. "Check", "Download", "Install").
    /// </summary>
    public string Phase { get; }
}
