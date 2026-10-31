namespace FinanceManager.Web.Infrastructure;

/// <summary>
/// Names of the rate-limiting policies applied to sensitive endpoints.
/// </summary>
public static class RateLimitPolicies
{
    /// <summary>
    /// Fixed-window per-source-address policy for anonymous authentication-related endpoints
    /// (registration and user-existence probes).
    /// </summary>
    public const string AuthSensitive = "auth-sensitive";
}
