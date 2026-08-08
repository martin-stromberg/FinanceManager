using System.ComponentModel.DataAnnotations;
using System;

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
    [MaxLength(2048)] string? Hiring);
