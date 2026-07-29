namespace SoftwareSchmiede.AutoUpdate;

/// <summary>
/// Provides thread-safe, read-only access to the current auto-update status.
/// </summary>
public interface IAutoUpdateStatusProvider
{
    /// <summary>
    /// Gets a consistent, immutable snapshot of the current auto-update status.
    /// </summary>
    /// <returns>The current status snapshot.</returns>
    AutoUpdateStatusSnapshot GetSnapshot();
}
