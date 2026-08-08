using System;
using FinanceManager.Shared.Dtos.Admin;

namespace FinanceManager.Tests.TestHelpers;

/// <summary>
/// Factory methods for security.txt test fixtures.
/// </summary>
public static class SecurityTxtSettingsTestData
{
    /// <summary>
    /// Returns a fully populated, valid update request.
    /// </summary>
    public static SecurityTxtSettingsUpdateRequest ValidRequest(
        string contact = "mailto:security@example.com",
        DateTimeOffset? expires = null) =>
        new(
            Contact: contact,
            Expires: expires ?? new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            Encryption: "https://example.com/pgp-key.asc",
            Acknowledgments: "https://example.com/thanks",
            PreferredLanguages: "en, de",
            Policy: "https://example.com/security-policy",
            Hiring: "https://example.com/jobs");

    /// <summary>
    /// Returns a minimal update request with no optional fields set.
    /// </summary>
    public static SecurityTxtSettingsUpdateRequest MinimalRequest(
        string contact = "mailto:security@example.com") =>
        new(
            Contact: contact,
            Expires: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            Encryption: null,
            Acknowledgments: null,
            PreferredLanguages: null,
            Policy: null,
            Hiring: null);

    /// <summary>
    /// Returns an update request with an empty Contact — represents unconfigured state.
    /// </summary>
    public static SecurityTxtSettingsUpdateRequest UnconfiguredRequest() =>
        new(
            Contact: string.Empty,
            Expires: DateTimeOffset.MaxValue,
            Encryption: null,
            Acknowledgments: null,
            PreferredLanguages: null,
            Policy: null,
            Hiring: null);
}
