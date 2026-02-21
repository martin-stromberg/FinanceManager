using System.ComponentModel.DataAnnotations;

namespace FinanceManager.Shared.Dtos.Budget;

/// <summary>
/// Request for creating a budget purpose.
/// </summary>
public sealed record BudgetPurposeCreateRequest(
    [Required, MinLength(2), MaxLength(150)]string Name,
    BudgetSourceType SourceType,
    Guid SourceId,
    [MaxLength(500)] string? Description,
    Guid? BudgetCategoryId);
