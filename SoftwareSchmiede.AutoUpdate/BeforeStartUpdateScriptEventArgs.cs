namespace SoftwareSchmiede.AutoUpdate;

/// <summary>
/// Event argument raised before the installation script is started.
/// </summary>
public sealed class BeforeStartUpdateScriptEventArgs : AutoUpdateCancelEventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BeforeStartUpdateScriptEventArgs"/> class.
    /// </summary>
    /// <param name="scriptFile">The generated installation script about to be started.</param>
    public BeforeStartUpdateScriptEventArgs(FileInfo scriptFile)
    {
        ScriptFile = scriptFile;
    }

    /// <summary>
    /// Gets the generated installation script about to be started.
    /// </summary>
    public FileInfo ScriptFile { get; }
}
