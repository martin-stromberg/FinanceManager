using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace SoftwareSchmiede.AutoUpdate;

/// <summary>
/// <see cref="IAutoUpdateSource"/> implementation reading a release manifest (<c>update.json</c>) and its
/// packages from the latest release of a GitHub repository. Requires network access to <c>github.com</c>.
/// </summary>
public sealed partial class AutoUpdateGithubSource : IAutoUpdateSource, IDisposable
{
    private const string ManifestAssetName = "update.json";
    private static readonly TimeSpan DefaultHttpTimeout = TimeSpan.FromMinutes(5);

    [GeneratedRegex("^[A-Za-z0-9._-]+$", RegexOptions.Compiled)]
    private static partial Regex RepositorySegmentRegex();

    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;
    private readonly string _repositoryOwner;
    private readonly string _repositoryName;
    private readonly IAutoUpdatePlatformResolver _platformResolver;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="AutoUpdateGithubSource"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client used to query the GitHub release manifest and download assets.</param>
    /// <param name="repositoryOwner">The owner (user or organization) of the GitHub repository.</param>
    /// <param name="repositoryName">The name of the GitHub repository.</param>
    /// <param name="platformResolver">Used to select the package matching the current platform.</param>
    public AutoUpdateGithubSource(HttpClient httpClient, string repositoryOwner, string repositoryName, IAutoUpdatePlatformResolver? platformResolver = null)
        : this(httpClient, repositoryOwner, repositoryName, platformResolver, ownsHttpClient: false)
    {
    }

    private AutoUpdateGithubSource(HttpClient httpClient, string repositoryOwner, string repositoryName, IAutoUpdatePlatformResolver? platformResolver, bool ownsHttpClient)
    {
        _httpClient = httpClient;
        _ownsHttpClient = ownsHttpClient;
        _repositoryOwner = ValidateRepositorySegment(repositoryOwner, nameof(repositoryOwner));
        _repositoryName = ValidateRepositorySegment(repositoryName, nameof(repositoryName));
        _platformResolver = platformResolver ?? new AutoUpdatePlatformResolver();
    }

    /// <summary>
    /// Creates an <see cref="AutoUpdateGithubSource"/> with a default, internally owned <see cref="HttpClient"/>
    /// that is disposed together with this instance.
    /// </summary>
    /// <param name="repositoryOwner">The owner (user or organization) of the GitHub repository.</param>
    /// <param name="repositoryName">The name of the GitHub repository.</param>
    /// <returns>A new <see cref="AutoUpdateGithubSource"/> instance.</returns>
    public static AutoUpdateGithubSource Create(string repositoryOwner, string repositoryName)
    {
        var handler = new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(10) };
        var httpClient = new HttpClient(handler) { Timeout = DefaultHttpTimeout };
        var version = typeof(AutoUpdateGithubSource).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd($"SoftwareSchmiede.AutoUpdate/{version}");
        return new AutoUpdateGithubSource(httpClient, repositoryOwner, repositoryName, platformResolver: null, ownsHttpClient: true);
    }

    /// <summary>
    /// Releases the internally owned <see cref="HttpClient"/> created by <see cref="Create"/>. Has no effect for
    /// instances created via the public constructor with an externally owned <see cref="HttpClient"/>.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }

    private static string ValidateRepositorySegment(string value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value) || !RepositorySegmentRegex().IsMatch(value))
        {
            throw new ArgumentException("Value must be a non-empty GitHub owner/repository name segment (letters, digits, '.', '_' or '-').", paramName);
        }

        return value;
    }

    /// <inheritdoc />
    public async Task<AutoUpdateCheckResult> CheckAsync(CancellationToken ct = default)
    {
        var url = $"https://github.com/{_repositoryOwner}/{_repositoryName}/releases/latest/download/{Uri.EscapeDataString(ManifestAssetName)}";
        var manifest = await _httpClient.GetFromJsonAsync<GithubReleaseManifest>(url, JsonFileStore.JsonOptions, ct);
        if (manifest is null)
        {
            return new AutoUpdateCheckResult(null, null, null, null);
        }

        var release = new AutoUpdateReleaseInfo(
            manifest.Version,
            manifest.ReleaseNotes,
            manifest.PublishedAt,
            manifest.Assets
                .Select(asset => new AutoUpdatePackageDescriptor(
                    manifest.Version,
                    asset.Platform,
                    asset.RuntimeIdentifier,
                    asset.AssetName,
                    new Uri(asset.AssetUrl),
                    asset.Sha256,
                    asset.SizeBytes))
                .ToList());

        var selected = _platformResolver.SelectPackage(release);
        return new AutoUpdateCheckResult(release.Version, selected, release.ReleaseNotes, release.PublishedAt);
    }

    /// <inheritdoc />
    public async Task DownloadAsync(AutoUpdatePackageDescriptor package, string targetPath, long maxBytes, CancellationToken ct = default)
    {
        using var response = await _httpClient.GetAsync(package.Uri, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength is long length && length > maxBytes)
        {
            throw new InvalidOperationException("Update package exceeds the configured size limit.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        var tempPath = $"{targetPath}.{Guid.NewGuid():N}.tmp";
        try
        {
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
        catch
        {
            try
            {
                File.Delete(tempPath);
            }
            catch
            {
                // Best-effort cleanup only; the original exception must propagate.
            }

            throw;
        }
    }

    private sealed record GithubReleaseManifest(
        string Version,
        string? ReleaseNotes,
        DateTimeOffset? PublishedAt,
        [property: JsonPropertyName("assets")] IReadOnlyList<GithubReleaseAsset> Assets);

    private sealed record GithubReleaseAsset(
        string Platform,
        string RuntimeIdentifier,
        string AssetName,
        string AssetUrl,
        string Sha256,
        long SizeBytes);
}
