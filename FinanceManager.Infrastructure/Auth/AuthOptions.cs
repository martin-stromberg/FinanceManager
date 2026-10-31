namespace FinanceManager.Infrastructure.Auth;

/// <summary>
/// Options controlling authentication and registration behavior.
/// </summary>
public sealed class AuthOptions
{
    /// <summary>Configuration section path for authentication options.</summary>
    public const string SectionName = "Auth";

    /// <summary>
    /// When <c>true</c>, the public registration endpoint accepts new accounts for users other
    /// than the very first one. When <c>false</c> (default), only the first user may register
    /// themselves; additional accounts are created through the admin user-management API.
    /// </summary>
    public bool AllowSelfRegistration { get; set; }
}
