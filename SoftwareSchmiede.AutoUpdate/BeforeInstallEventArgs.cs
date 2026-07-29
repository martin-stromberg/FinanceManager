namespace SoftwareSchmiede.AutoUpdate;

/// <summary>
/// Event argument raised before an installation is started.
/// </summary>
public sealed class BeforeInstallEventArgs : AutoUpdateCancelEventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BeforeInstallEventArgs"/> class.
    /// </summary>
    /// <param name="packageFile">The downloaded package file about to be installed.</param>
    public BeforeInstallEventArgs(FileInfo packageFile)
    {
        PackageFile = packageFile;
    }

    /// <summary>
    /// Gets the downloaded package file about to be installed.
    /// </summary>
    public FileInfo PackageFile { get; }
}
