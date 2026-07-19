#pragma warning disable CS1591
using FinanceManager.Shared.Dtos.Update;

namespace FinanceManager.Web.Services.Updates;

public sealed class InstalledReleaseMetadataProvider : IInstalledReleaseMetadataProvider
{
    private readonly IWebHostEnvironment _environment;

    public InstalledReleaseMetadataProvider(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    public async Task<InstalledReleaseMetadataDto> GetAsync(CancellationToken ct = default)
    {
        var path = Path.Combine(_environment.ContentRootPath, "release-metadata.json");
        return await JsonFileStore.ReadAsync<InstalledReleaseMetadataDto>(path, ct)
            ?? new InstalledReleaseMetadataDto(null, null, null, null, null);
    }
}
#pragma warning restore CS1591
