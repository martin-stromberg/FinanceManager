namespace FinanceManager.Web.Services;

/// <summary>
/// Provides the global security response headers applied to every request.
/// </summary>
public static class SecurityHeadersPolicy
{
    /// <summary>
    /// Global Content Security Policy. Permits inline scripts and styles because the Blazor
    /// import map and component markup require them; deliberately omits
    /// <c>upgrade-insecure-requests</c> so plain-HTTP access in local networks keeps working.
    /// </summary>
    public const string ContentSecurityPolicy =
        "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; font-src 'self' data:; connect-src 'self' ws: wss:; object-src 'none'; base-uri 'self'; frame-ancestors 'self'; form-action 'self'";

    /// <summary>
    /// Restrictive Permissions-Policy disabling browser features the application does not use.
    /// </summary>
    /// <returns>The Permissions-Policy header value.</returns>
    public static string GetPermissionsPolicy() =>
        string.Join(", ", DisabledFeatures.Select(name => name + "=()"));

    private static readonly string[] DisabledFeatures =
    {
        "accelerometer", "camera", "geolocation", "gyroscope", "magnetometer", "microphone", "payment", "usb"
    };

    /// <summary>
    /// Applies the global security headers to the response.
    /// </summary>
    /// <param name="response">The response to annotate.</param>
    public static void Apply(HttpResponse response)
    {
        var headers = response.Headers;
        headers["X-Content-Type-Options"] = "nosniff";
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        headers["X-Frame-Options"] = "SAMEORIGIN";
        headers["Permissions-Policy"] = GetPermissionsPolicy();
        headers["Content-Security-Policy"] = ContentSecurityPolicy;
    }
}
