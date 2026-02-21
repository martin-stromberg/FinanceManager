namespace FinanceManager.Shared.Dtos.Budget;

/// <summary>
/// Represents a budget rule which can apply either to a specific budget purpose or to a whole budget category.
/// </summary>
public sealed record BudgetRuleDto(
    Guid Id,
    Guid OwnerUserId,
    Guid? BudgetPurposeId,
    Guid? BudgetCategoryId,
    decimal Amount,
    BudgetIntervalType Interval,
    int? CustomIntervalMonths,
    DateOnly StartDate,
    DateOnly? EndDate);
