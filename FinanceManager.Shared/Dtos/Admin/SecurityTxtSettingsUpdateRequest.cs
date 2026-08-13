using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Net;

namespace FinanceManager.Shared.Dtos.Admin;

/// <summary>
/// Request payload for updating security.txt settings.
/// </summary>
public sealed record SecurityTxtSettingsUpdateRequest(
    /// <summary>Contact directive.</summary>
    [Required, MaxLength(2048)] string Contact,
    /// <summary>Expires directive.</summary>
    DateTimeOffset Expires,
    /// <summary>Encryption directive.</summary>
    [MaxLength(2048)] string? Encryption,
    /// <summary>Acknowledgments directive.</summary>
    [MaxLength(2048)] string? Acknowledgments,
    /// <summary>Preferred-Languages directive.</summary>
    [MaxLength(2048)] string? PreferredLanguages,
    /// <summary>Policy directive.</summary>
    [MaxLength(2048)] string? Policy,
    /// <summary>Hiring directive.</summary>
    [MaxLength(2048)] string? Hiring,
    /// <summary>Canonical directive.</summary>
    [MaxLength(2048)] string? Canonical) : IValidatableObject
{
    /// <inheritdoc />
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Expires <= DateTimeOffset.UtcNow)
        {
            yield return new ValidationResult("Expires must be in the future.", [nameof(Expires)]);
        }

        if (string.IsNullOrWhiteSpace(Canonical))
        {
            yield break;
        }

        if (!Uri.TryCreate(Canonical, UriKind.Absolute, out var canonicalUri) || canonicalUri.Scheme != Uri.UriSchemeHttps)
        {
            yield return new ValidationResult("Canonical must be an absolute HTTPS URL.", [nameof(Canonical)]);
            yield break;
        }

        if (!string.IsNullOrEmpty(canonicalUri.Query))
        {
            yield return new ValidationResult("Canonical must not contain a query string.", [nameof(Canonical)]);
        }

        if (!string.IsNullOrEmpty(canonicalUri.Fragment))
        {
            yield return new ValidationResult("Canonical must not contain a fragment.", [nameof(Canonical)]);
        }

        var host = canonicalUri.Host;
        if (canonicalUri.IsLoopback || string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) || IPAddress.TryParse(host, out var address) && IPAddress.IsLoopback(address))
        {
            yield return new ValidationResult("Canonical host must not be localhost or a loopback address.", [nameof(Canonical)]);
        }
    }
}
