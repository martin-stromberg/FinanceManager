namespace SoftwareSchmiede.AutoUpdate;

/// <summary>
/// Provides hosting-independent access to the application's file system layout.
/// </summary>
public interface IAutoUpdateEnvironment
{
    /// <summary>
    /// Gets the root directory the application is deployed to. Update packages are installed relative to this
    /// directory.
    /// </summary>
    string ApplicationDirectory { get; }
}
