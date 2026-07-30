namespace SoftwareSchmiede.AutoUpdate;

/// <summary>
/// Default <see cref="IInstalledVersionProvider"/> implementation reading <c>release-metadata.json</c> from the
/// application's root directory.
/// </summary>
public sealed class ReleaseMetadataInstalledVersionProvider : IInstalledVersionProvider
{
    private readonly IAutoUpdateEnvironment _environment;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReleaseMetadataInstalledVersionProvider"/> class.
    /// </summary>
    /// <param name="environment">Provides the application's root directory.</param>
    public ReleaseMetadataInstalledVersionProvider(IAutoUpdateEnvironment environment)
    {
        _environment = environment;
    }

    /// <inheritdoc />
    public async Task<InstalledReleaseInfo> GetAsync(CancellationToken ct = default)
    {
        var path = Path.Combine(_environment.ApplicationDirectory, "release-metadata.json");
        return await JsonFileStore.ReadAsync<InstalledReleaseInfo>(path, ct)
            ?? new InstalledReleaseInfo(null, null, null, null, null);
    }
}
