namespace SoftwareSchmiede.AutoUpdate;

/// <summary>
/// Event argument raised before an update package is downloaded.
/// </summary>
public sealed class BeforeDownloadEventArgs : AutoUpdateCancelEventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BeforeDownloadEventArgs"/> class.
    /// </summary>
    /// <param name="sourceUri">The location the package will be downloaded from.</param>
    public BeforeDownloadEventArgs(Uri sourceUri)
    {
        SourceUri = sourceUri;
    }

    /// <summary>
    /// Gets the location the package will be downloaded from.
    /// </summary>
    public Uri SourceUri { get; }
}
