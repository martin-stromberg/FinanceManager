using FinanceManager.Shared.Dtos.Update;
using SoftwareSchmiede.AutoUpdate;

namespace FinanceManager.Web.Services.Updates;

/// <summary>
/// Default <see cref="IInstalledReleaseMetadataProvider"/> implementation, delegating to the auto-update
/// library's <see cref="IInstalledVersionProvider"/> and mapping the result onto <see cref="InstalledReleaseMetadataDto"/>.
/// </summary>
/// <remarks>
/// Deliberately kept as a thin mapping layer rather than removed: it shields the Web layer (e.g. <c>LoginStatus.razor</c>
/// via <see cref="IInstalledReleaseMetadataProvider"/>) from depending directly on <c>SoftwareSchmiede.AutoUpdate</c>
/// library types, keeping the library an implementation detail of the update subsystem.
/// </remarks>
public sealed class InstalledReleaseMetadataProvider : IInstalledReleaseMetadataProvider
{
    private readonly IInstalledVersionProvider _installedVersionProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="InstalledReleaseMetadataProvider"/> class.
    /// </summary>
    /// <param name="installedVersionProvider">The auto-update library's installed version provider.</param>
    public InstalledReleaseMetadataProvider(IInstalledVersionProvider installedVersionProvider)
    {
        _installedVersionProvider = installedVersionProvider;
    }

    /// <inheritdoc />
    public async Task<InstalledReleaseMetadataDto> GetAsync(CancellationToken ct = default)
    {
        var installed = await _installedVersionProvider.GetAsync(ct);
        return new InstalledReleaseMetadataDto(installed.Version, installed.PublishedAt, installed.CommitSha, installed.Repository, installed.RuntimeIdentifier);
    }
}
