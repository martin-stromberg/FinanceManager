namespace SoftwareSchmiede.AutoUpdate;

/// <summary>
/// Stops the hosting application after the installation script has been started, when
/// <see cref="AutoUpdateOptions.StopHostAfterScriptStart"/> is enabled.
/// </summary>
public interface IAutoUpdateHostTerminator
{
    /// <summary>
    /// Requests that the host application shuts down.
    /// </summary>
    void StopApplication();
}
