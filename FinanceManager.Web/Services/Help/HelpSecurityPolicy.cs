namespace FinanceManager.Web.Services.Help;

/// <summary>
/// Provides path checks and headers for the help security boundary.
/// </summary>
public static class HelpSecurityPolicy
{
    /// <summary>
    /// Content Security Policy applied to help UI, help assets and help API responses.
    /// </summary>
    public const string ContentSecurityPolicy =
        "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; connect-src 'self' ws: wss:; object-src 'none'; base-uri 'self'; frame-ancestors 'self'; form-action 'self'";

    /// <summary>
    /// Determines whether the request path belongs to the help surface.
    /// </summary>
    /// <param name="path">The request path.</param>
    /// <returns><c>true</c> for help UI, assets and API routes.</returns>
    public static bool IsHelpPath(PathString path)
    {
        return path.StartsWithSegments("/help", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/api/help", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Determines whether the request targets a static help asset protected by the manifest.
    /// </summary>
    /// <param name="path">The request path.</param>
    /// <returns><c>true</c> for static help files that are delivered directly from wwwroot.</returns>
    public static bool IsStaticHelpAssetPath(PathString path)
    {
        if (!path.StartsWithSegments("/help", out var remaining))
        {
            return false;
        }

        var extension = Path.GetExtension(remaining.Value ?? string.Empty);
        return !string.IsNullOrEmpty(extension);
    }
}
