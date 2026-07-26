using System.Text.RegularExpressions;

namespace FinanceManager.Web.Services.Help;

/// <summary>
/// Resolves safe help route paths to markdown files below Docs/help.
/// </summary>
public static partial class HelpDocumentPathResolver
{
    /// <summary>
    /// Gets the shared markdown source directory used by build and runtime help rendering.
    /// </summary>
    /// <param name="environment">The web host environment.</param>
    /// <returns>The absolute Docs/help path.</returns>
    public static string GetHelpSourcePath(IWebHostEnvironment environment)
    {
        return Path.GetFullPath(Path.Combine(environment.ContentRootPath, "..", "Docs", "help"));
    }

    /// <summary>
    /// Normalizes and validates a help route path.
    /// </summary>
    /// <param name="helpPath">The incoming route path.</param>
    /// <param name="normalizedHelpPath">The normalized route path.</param>
    /// <returns><c>true</c> when the route path is safe to resolve.</returns>
    public static bool TryNormalizeHelpPath(string? helpPath, out string normalizedHelpPath)
    {
        normalizedHelpPath = (helpPath ?? string.Empty).Trim().Trim('/').ToLowerInvariant();
        if (normalizedHelpPath.Length == 0 || normalizedHelpPath.Length > 200)
        {
            return false;
        }

        var segments = normalizedHelpPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0 || segments.Length > 4)
        {
            return false;
        }

        return segments.All(segment => HelpRouteSegmentRegex().IsMatch(segment));
    }

    /// <summary>
    /// Finds the markdown file for a normalized help route path.
    /// </summary>
    /// <param name="docsPath">The absolute Docs/help path.</param>
    /// <param name="language">The normalized language.</param>
    /// <param name="helpPath">The normalized help route path.</param>
    /// <returns>The selected markdown file path, or <c>null</c>.</returns>
    public static string? FindMarkdownFile(string docsPath, string language, string helpPath)
    {
        var segments = helpPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            return null;
        }

        if (segments.Length == 1)
        {
            var featureId = segments[0];
            var featureDirectory = Path.Combine(docsPath, featureId);
            if (Directory.Exists(featureDirectory))
            {
                var candidates = Directory.GetFiles(featureDirectory, "*.md", SearchOption.TopDirectoryOnly);
                return SelectMarkdownFile(candidates, language, featureId);
            }

            var topLevelCandidates = Directory.GetFiles(docsPath, $"{featureId}*.md", SearchOption.TopDirectoryOnly);
            return SelectMarkdownFile(topLevelCandidates, language, featureId);
        }

        var documentName = segments[^1];
        var directoryPath = Path.Combine(new[] { docsPath }.Concat(segments[..^1]).ToArray());
        if (!Directory.Exists(directoryPath))
        {
            return null;
        }

        var documentCandidates = Directory.GetFiles(directoryPath, "*.md", SearchOption.TopDirectoryOnly);
        return documentName.Equals("index", StringComparison.OrdinalIgnoreCase)
            ? SelectMarkdownFile(documentCandidates, language, segments[^2])
            : SelectMarkdownDocumentFile(documentCandidates, language, documentName);
    }

    private static string? SelectMarkdownFile(IEnumerable<string> candidates, string language, string featureId)
    {
        var files = candidates.ToArray();
        if (files.Length == 0)
        {
            return null;
        }

        var localizedIndexName = $"index.{language}.md";
        var localizedFeatureName = $"{featureId}.{language}.md";

        return files.FirstOrDefault(file => Path.GetFileName(file).Equals(localizedIndexName, StringComparison.OrdinalIgnoreCase))
            ?? files.FirstOrDefault(file => Path.GetFileName(file).Equals(localizedFeatureName, StringComparison.OrdinalIgnoreCase))
            ?? files.FirstOrDefault(file => Path.GetFileName(file).Equals("index.md", StringComparison.OrdinalIgnoreCase))
            ?? files.FirstOrDefault(file => Path.GetFileName(file).Equals($"{featureId}.md", StringComparison.OrdinalIgnoreCase))
            ?? files.FirstOrDefault(file => !Path.GetFileName(file).EndsWith(".en.md", StringComparison.OrdinalIgnoreCase))
            ?? files.FirstOrDefault();
    }

    private static string? SelectMarkdownDocumentFile(IEnumerable<string> candidates, string language, string documentName)
    {
        var files = candidates.ToArray();
        if (files.Length == 0)
        {
            return null;
        }

        var localizedDocumentName = $"{documentName}.{language}.md";
        var defaultDocumentName = $"{documentName}.md";

        return files.FirstOrDefault(file => Path.GetFileName(file).Equals(localizedDocumentName, StringComparison.OrdinalIgnoreCase))
            ?? files.FirstOrDefault(file => Path.GetFileName(file).Equals(defaultDocumentName, StringComparison.OrdinalIgnoreCase))
            ?? files.FirstOrDefault(file => !Path.GetFileName(file).EndsWith(".en.md", StringComparison.OrdinalIgnoreCase));
    }

    [GeneratedRegex("^[a-z][a-z0-9-]{0,63}$", RegexOptions.Compiled)]
    private static partial Regex HelpRouteSegmentRegex();
}
