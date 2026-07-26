#pragma warning disable CS1591
using FinanceManager.Shared.Dtos.Update;

namespace FinanceManager.Web.Services.Updates;

public sealed class UpdateManifestClient : IUpdateManifestClient
{
    private readonly HttpClient _httpClient;

    public UpdateManifestClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<UpdateMetadataDto> GetManifestAsync(UpdateSettingsDto settings, CancellationToken ct = default)
    {
        var url = $"https://github.com/{settings.RepositoryOwner}/{settings.RepositoryName}/releases/latest/download/{Uri.EscapeDataString(settings.ManifestAssetName)}";
        var manifest = await _httpClient.GetFromJsonAsync<UpdateMetadataDto>(url, JsonFileStore.JsonOptions, ct);
        if (manifest is null)
        {
            throw new InvalidOperationException("Update manifest is empty.");
        }

        return manifest;
    }

    public async Task DownloadAssetAsync(UpdateAssetDto asset, string targetPath, long maxBytes, CancellationToken ct = default)
    {
        using var response = await _httpClient.GetAsync(asset.AssetUrl, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength is long length && length > maxBytes)
        {
            throw new InvalidOperationException("Update package exceeds the configured size limit.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        var tempPath = $"{targetPath}.{Guid.NewGuid():N}.tmp";
        await using (var source = await response.Content.ReadAsStreamAsync(ct))
        await using (var target = File.Create(tempPath))
        {
            var buffer = new byte[81920];
            long copied = 0;
            while (true)
            {
                var read = await source.ReadAsync(buffer, ct);
                if (read == 0)
                {
                    break;
                }

                copied += read;
                if (copied > maxBytes)
                {
                    throw new InvalidOperationException("Update package exceeds the configured size limit.");
                }

                await target.WriteAsync(buffer.AsMemory(0, read), ct);
            }
        }

        File.Move(tempPath, targetPath, overwrite: true);
    }
}
#pragma warning restore CS1591
