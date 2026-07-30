namespace SoftwareSchmiede.AutoUpdate;

/// <summary>
/// Determines the currently installed release.
/// </summary>
public interface IInstalledVersionProvider
{
    /// <summary>
    /// Reads the metadata of the currently installed release.
    /// </summary>
    /// <param name="ct">A token to observe for cancellation requests.</param>
    /// <returns>The installed release metadata. Individual fields are <see langword="null"/> if unknown.</returns>
    Task<InstalledReleaseInfo> GetAsync(CancellationToken ct = default);
}
