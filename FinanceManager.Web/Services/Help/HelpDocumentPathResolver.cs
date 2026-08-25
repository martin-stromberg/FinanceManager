using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;

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
        var bundledPath = Path.GetFullPath(Path.Combine(environment.ContentRootPath, "Docs", "help"));
        if (Directory.Exists(bundledPath))
        {
            return bundledPath;
        }

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
        return HelpContentCatalog.TryResolveDocument(docsPath, language, helpPath, out _, out _, out var markdownPath)
            ? markdownPath
            : null;
    }

    [GeneratedRegex("^[a-z][a-z0-9-]{0,63}$", RegexOptions.Compiled)]
    private static partial Regex HelpRouteSegmentRegex();
}
