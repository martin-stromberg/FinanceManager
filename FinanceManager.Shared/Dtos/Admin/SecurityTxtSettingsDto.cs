using System;

namespace FinanceManager.Shared.Dtos.Admin;

/// <summary>
/// DTO representing security.txt settings.
/// </summary>
public sealed class SecurityTxtSettingsDto
{
    /// <summary>Contact directive.</summary>
    public string Contact { get; set; } = string.Empty;
    /// <summary>Expires directive.</summary>
    public DateTimeOffset Expires { get; set; }
    /// <summary>Encryption directive.</summary>
    public string? Encryption { get; set; }
    /// <summary>Acknowledgments directive.</summary>
    public string? Acknowledgments { get; set; }
    /// <summary>Preferred-Languages directive.</summary>
    public string? PreferredLanguages { get; set; }
    /// <summary>Policy directive.</summary>
    public string? Policy { get; set; }
    /// <summary>Hiring directive.</summary>
    public string? Hiring { get; set; }
}
