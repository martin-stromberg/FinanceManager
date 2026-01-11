using System.ComponentModel.DataAnnotations;

namespace FinanceManager.Shared.Dtos.Budget;

/// <summary>
/// Request payload for updating a budget purpose.
/// </summary>
public sealed record BudgetPurposeUpdateRequest(
    [Required, StringLength(150, MinimumLength = 1)] string Name,
    BudgetSourceType SourceType,
    Guid SourceId,
    [StringLength(500)] string? Description);
