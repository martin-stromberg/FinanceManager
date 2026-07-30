namespace SoftwareSchmiede.AutoUpdate;

/// <summary>
/// Prepares and starts the installation of a downloaded update package.
/// </summary>
public interface IAutoUpdateInstaller
{
    /// <summary>
    /// Validates the downloaded package, resolves the installation target and generates the installation script.
    /// </summary>
    /// <param name="package">The package descriptor to install.</param>
    /// <param name="zipPath">The local file system path of the downloaded package.</param>
    /// <param name="ct">A token to observe for cancellation requests.</param>
    /// <returns>The full path of the generated installation script.</returns>
    Task<string> PrepareAsync(AutoUpdatePackageDescriptor package, string zipPath, CancellationToken ct = default);

    /// <summary>
    /// Starts the previously generated installation script.
    /// </summary>
    /// <param name="scriptPath">The full path of the installation script, as returned by <see cref="PrepareAsync"/>.</param>
    void Start(string scriptPath);
}
