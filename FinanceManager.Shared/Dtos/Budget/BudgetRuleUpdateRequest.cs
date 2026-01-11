namespace FinanceManager.Shared.Dtos.Budget;

/// <summary>
/// Request payload for updating a budget rule.
/// </summary>
public sealed record BudgetRuleUpdateRequest(
    decimal Amount,
    BudgetIntervalType Interval,
    int? CustomIntervalMonths,
    DateOnly StartDate,
    DateOnly? EndDate);
