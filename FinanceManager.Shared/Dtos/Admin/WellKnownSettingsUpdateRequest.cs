using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using FinanceManager.Shared.WellKnown;

namespace FinanceManager.Shared.Dtos.Admin;

/// <summary>
/// Request payload for updating well-known endpoint settings.
/// </summary>
/// <param name="ChangePasswordUrl">Target URL that the change-password well-known endpoint redirects to.</param>
/// <returns>The result.</returns>
public sealed record WellKnownSettingsUpdateRequest(
    [Required, MaxLength(2048)] string ChangePasswordUrl) : IValidatableObject
{
    /// <inheritdoc />
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!WellKnownUrlValidator.IsValidUrl(ChangePasswordUrl))
        {
            yield return new ValidationResult("ChangePasswordUrl must be a local root path or an absolute http/https URL.", [nameof(ChangePasswordUrl)]);
        }
    }
}
