using System;

namespace FinanceManager.Shared.WellKnown;

/// <summary>
/// Validates redirect target URLs for the well-known endpoints.
/// </summary>
public static class WellKnownUrlValidator
{
    /// <summary>Checks whether the value is a local root path or an absolute http/https URL.</summary>
    /// <param name="url">The url.</param>
    /// <returns>The result.</returns>
    public static bool IsValidUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        var trimmed = url.Trim();
        if (trimmed.StartsWith('/'))
        {
            return !trimmed.StartsWith("//", StringComparison.Ordinal);
        }

        return Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }
}
