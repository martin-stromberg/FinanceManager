namespace FinanceManager.Shared.Dtos.Budget;

/// <summary>
/// Request payload for updating a budget rule.
/// Target association (purpose/category) is not updatable.
/// </summary>
public sealed record BudgetRuleUpdateRequest(
    decimal Amount,
    BudgetIntervalType Interval,
    int? CustomIntervalMonths,
    DateOnly StartDate,
    DateOnly? EndDate);
