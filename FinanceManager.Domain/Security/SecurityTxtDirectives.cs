namespace FinanceManager.Domain.Security;

/// <summary>
/// Value object containing all configurable security.txt directives.
/// </summary>
public sealed record SecurityTxtDirectives(
    string Contact,
    DateTimeOffset Expires,
    string? Encryption,
    string? Acknowledgments,
    string? PreferredLanguages,
    string? Policy,
    string? Hiring,
    string? Canonical);
