namespace FinanceManager.Infrastructure.Auth;

/// <summary>
/// Strongly typed configuration for JWT issuing and validation.
/// </summary>
public sealed class JwtOptions
{
    /// <summary>
    /// Configuration section name containing JWT settings.
    /// </summary>
    public const string SectionName = "Jwt";

    /// <summary>
    /// Symmetric signing key used for HMAC JWT signatures.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Expected issuer for issued and validated tokens.
    /// </summary>
    public string Issuer { get; set; } = "financemanager";

    /// <summary>
    /// Expected audience for issued and validated tokens.
    /// </summary>
    public string Audience { get; set; } = "financemanager";

    /// <summary>
    /// Token lifetime in minutes.
    /// </summary>
    public int LifetimeMinutes { get; set; } = 30;

    /// <summary>Minimum renewal window in minutes; defaults to five minutes for regular deployments.</summary>
    public int MinimumRenewalWindowMinutes { get; set; } = 5;

    /// <summary>Gets the shared renewal window: at least the configured minimum, otherwise half the token lifetime.</summary>
    public TimeSpan RenewalWindow
    {
        get => TimeSpan.FromMinutes(Math.Max(MinimumRenewalWindowMinutes, LifetimeMinutes / 2.0));
    }
}
