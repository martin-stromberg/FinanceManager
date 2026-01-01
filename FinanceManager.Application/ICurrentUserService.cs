namespace FinanceManager.Application;

/// <summary>
/// Provides information about the current authenticated user and their context.
/// Implementations typically read this from the request principal or test doubles.
/// </summary>
public interface ICurrentUserService
{
    /// <summary>
    /// Identifier of the current user.
    /// </summary>
    Guid UserId { get; }

    /// <summary>
    /// Preferred language/culture of the current user (e.g. "en", "de").
    /// May be null when not available.
    /// </summary>
    string? PreferredLanguage { get; }

    /// <summary>
    /// Indicates whether the current request is authenticated.
    /// </summary>
    bool IsAuthenticated { get; }

    /// <summary>
    /// Indicates whether the current user has administrative privileges.
    /// </summary>
    bool IsAdmin { get; }
}
