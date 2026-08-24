using System.Security.Cryptography;

namespace FinanceManager.Web.Services.Help;

/// <summary>
/// Checks runtime help assets against the build manifest.
/// </summary>
public sealed class HelpAssetIntegrityValidator : IHelpAssetIntegrityValidator
{
    private const string ManifestRelativePath = "help/help-assets.sha256";
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<HelpAssetIntegrityValidator> _logger;
    private readonly Lazy<IReadOnlyDictionary<string, string>> _manifest;

    /// <summary>
    /// Initializes a new instance of the help asset integrity validator.
    /// </summary>
    /// <param name="environment">The host environment used to locate the manifest.</param>
    /// <param name="logger">Logger for integrity failures.</param>
    public HelpAssetIntegrityValidator(IWebHostEnvironment environment, ILogger<HelpAssetIntegrityValidator> logger)
    {
        _environment = environment;
        _logger = logger;
        _manifest = new Lazy<IReadOnlyDictionary<string, string>>(LoadManifest);
    }

    /// <inheritdoc />
    public bool IsTrustedHelpFile(string fullPath)
    {
        if (string.IsNullOrWhiteSpace(fullPath) || !File.Exists(fullPath))
        {
            return false;
        }

        var normalizedPath = Path.GetFullPath(fullPath);
        return ValidateFile(normalizedPath);
    }

    private bool ValidateFile(string fullPath)
    {
        var key = GetManifestKey(fullPath);
        if (!_manifest.Value.TryGetValue(key, out var expectedHash))
        {
            _logger.LogWarning("Help file is not listed in the asset manifest: {RelativePath}", key);
            return false;
        }

        var actualHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(fullPath)));
        if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Help file hash mismatch for {RelativePath}", key);
            return false;
        }

        return true;
    }

    private IReadOnlyDictionary<string, string> LoadManifest()
    {
        var manifestPath = Path.Combine(_environment.WebRootPath, ManifestRelativePath);
        if (!File.Exists(manifestPath))
        {
            var outputManifestPath = Path.Combine(AppContext.BaseDirectory, "wwwroot", ManifestRelativePath);
            if (!File.Exists(outputManifestPath))
            {
                _logger.LogWarning("Help asset manifest not found at {ManifestPath}", manifestPath);
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            manifestPath = outputManifestPath;
        }

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in File.ReadAllLines(manifestPath))
        {
            var parts = line.Split('|', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2 || parts[0].Length == 0 || parts[1].Length == 0)
            {
                continue;
            }

            result[NormalizeManifestPath(parts[0])] = parts[1].ToUpperInvariant();
        }

        return result;
    }

    private static string NormalizeManifestPath(string path)
    {
        return path.Replace('\\', '/').TrimStart('/');
    }

    private string GetManifestKey(string fullPath)
    {
        var outputWebRootPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "wwwroot"));
        if (Path.GetRelativePath(outputWebRootPath, fullPath) is { } outputWebRootRelativePath
            && !outputWebRootRelativePath.Equals("..", StringComparison.Ordinal)
            && !outputWebRootRelativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            && !outputWebRootRelativePath.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal)
            && !Path.IsPathRooted(outputWebRootRelativePath))
        {
            return NormalizeManifestPath(Path.Combine("wwwroot", outputWebRootRelativePath));
        }

        var webRootPath = Path.GetFullPath(_environment.WebRootPath);
        if (Path.GetRelativePath(webRootPath, fullPath) is { } webRootRelativePath
            && !webRootRelativePath.Equals("..", StringComparison.Ordinal)
            && !webRootRelativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            && !webRootRelativePath.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal)
            && !Path.IsPathRooted(webRootRelativePath))
        {
            return NormalizeManifestPath(Path.Combine("wwwroot", webRootRelativePath));
        }

        return NormalizeManifestPath(Path.GetRelativePath(_environment.ContentRootPath, fullPath));
    }
}
