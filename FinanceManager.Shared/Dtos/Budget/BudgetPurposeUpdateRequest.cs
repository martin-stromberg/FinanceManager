using System.ComponentModel.DataAnnotations;

namespace FinanceManager.Shared.Dtos.Budget;

/// <summary>
/// Request for updating a budget purpose.
/// </summary>
public sealed record BudgetPurposeUpdateRequest(
    [Required, MinLength(2), MaxLength(150)] string Name,
    BudgetSourceType SourceType,
    Guid SourceId,
    [MaxLength(500)] string? Description,
    Guid? BudgetCategoryId);
