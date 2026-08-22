namespace FinanceManager.Web.Infrastructure.Auth;

/// <summary>
/// Validates and builds internal return URLs used by the login redirect flow.
/// </summary>
public static class AuthReturnUrl
{
    /// <summary>
    /// Converts a navigation URI into a validated relative internal return URL.
    /// </summary>
    /// <param name="baseUri">The application's absolute base URI.</param>
    /// <param name="currentUri">The current navigation URI.</param>
    /// <returns>A safe relative URL including query string and fragment, or <c>null</c> when rejected.</returns>
    public static string? FromNavigationUri(string baseUri, string currentUri)
    {
        if (!Uri.TryCreate(baseUri, UriKind.Absolute, out var baseAbsolute))
        {
            return null;
        }

        if (!Uri.TryCreate(currentUri, UriKind.Absolute, out var currentAbsolute)
            && !Uri.TryCreate(baseAbsolute, currentUri, out currentAbsolute))
        {
            return null;
        }

        if (!SameOrigin(baseAbsolute, currentAbsolute))
        {
            return null;
        }

        return Validate(currentAbsolute.PathAndQuery + currentAbsolute.Fragment);
    }

    /// <summary>
    /// Validates a return URL and accepts only internal, non-public application routes.
    /// </summary>
    /// <param name="returnUrl">The return URL candidate.</param>
    /// <returns>The validated relative URL when valid; otherwise <c>null</c>.</returns>
    public static string? Validate(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            return null;
        }

        var originalValue = returnUrl.Trim();
        if (!TryDecodeOnce(originalValue, out var decodedValue))
        {
            return null;
        }

        if (decodedValue.Contains('\\', StringComparison.Ordinal)
            || decodedValue.Any(char.IsControl)
            || Uri.TryCreate(decodedValue, UriKind.Absolute, out _)
            || decodedValue.StartsWith("//", StringComparison.Ordinal)
            || !decodedValue.StartsWith("/", StringComparison.Ordinal))
        {
            return null;
        }

        var path = GetPath(decodedValue);
        if (PathContainsNestedEncoding(path))
        {
            return null;
        }

        var safeValue = originalValue.StartsWith("/", StringComparison.Ordinal)
            ? originalValue
            : decodedValue;

        return IsBlockedPath(path) ? null : safeValue;
    }

    /// <summary>
    /// Builds the login URL and appends the provided return URL only when it is valid.
    /// </summary>
    /// <param name="returnUrl">The return URL candidate.</param>
    /// <returns>The login URL, optionally with an encoded <c>returnUrl</c> query parameter.</returns>
    public static string BuildLoginUrl(string? returnUrl)
    {
        var safeReturnUrl = Validate(returnUrl);
        return safeReturnUrl is null
            ? "/login"
            : $"/login?returnUrl={Uri.EscapeDataString(safeReturnUrl)}";
    }

    /// <summary>
    /// Determines whether a path can be reached without an authenticated user.
    /// </summary>
    /// <param name="path">The absolute application path without scheme or host.</param>
    /// <returns><c>true</c> when the path is public; otherwise <c>false</c>.</returns>
    public static bool IsPublicPath(string path)
    {
        if (string.IsNullOrEmpty(path) || path == "/")
        {
            return false;
        }

        return path.Equals("/login", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/register", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/api/auth", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/error", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryDecodeOnce(string value, out string decoded)
    {
        try
        {
            decoded = Uri.UnescapeDataString(value);
            return true;
        }
        catch
        {
            decoded = string.Empty;
            return false;
        }
    }

    private static string GetPath(string value)
    {
        var queryIndex = value.IndexOf('?', StringComparison.Ordinal);
        var fragmentIndex = value.IndexOf('#', StringComparison.Ordinal);

        var pathEnd = queryIndex >= 0 && fragmentIndex >= 0
            ? Math.Min(queryIndex, fragmentIndex)
            : Math.Max(queryIndex, fragmentIndex);

        return pathEnd >= 0 ? value[..pathEnd] : value;
    }

    private static bool PathContainsNestedEncoding(string path)
    {
        if (!path.Contains('%', StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            return !string.Equals(path, Uri.UnescapeDataString(path), StringComparison.Ordinal);
        }
        catch
        {
            return true;
        }
    }

    private static bool IsBlockedPath(string path)
    {
        return path.Equals("/login", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/login/", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/register", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/register/", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/error", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/error/", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool SameOrigin(Uri left, Uri right)
    {
        return string.Equals(left.Scheme, right.Scheme, StringComparison.OrdinalIgnoreCase)
            && string.Equals(left.Host, right.Host, StringComparison.OrdinalIgnoreCase)
            && left.Port == right.Port;
    }
}
