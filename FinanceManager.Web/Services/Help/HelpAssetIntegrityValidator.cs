using System.Security.Cryptography;

namespace FinanceManager.Web.Services.Help;

/// <summary>
/// Checks runtime help assets against the build manifest.
/// </summary>
public sealed class HelpAssetIntegrityValidator : IHelpAssetIntegrityValidator
{
    private const string ManifestRelativePath = "wwwroot/help/help-assets.sha256";
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
        var key = NormalizeManifestPath(Path.GetRelativePath(_environment.ContentRootPath, fullPath));
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
        var manifestPath = Path.Combine(_environment.ContentRootPath, ManifestRelativePath);
        if (!File.Exists(manifestPath))
        {
            _logger.LogWarning("Help asset manifest not found at {ManifestPath}", manifestPath);
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
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
}
