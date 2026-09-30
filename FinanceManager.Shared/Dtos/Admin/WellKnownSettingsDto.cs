namespace FinanceManager.Shared.Dtos.Admin;

/// <summary>
/// DTO representing well-known endpoint settings.
/// </summary>
public sealed class WellKnownSettingsDto
{
    /// <summary>Target URL that the change-password well-known endpoint redirects to.</summary>
    public string ChangePasswordUrl { get; set; } = string.Empty;
}
