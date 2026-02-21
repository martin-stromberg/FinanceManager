namespace FinanceManager.Shared.Dtos.Budget;

/// <summary>
/// Data transfer object for budget categories.
/// </summary>
public sealed record BudgetCategoryDto(Guid Id, Guid OwnerUserId, string Name);
