using System.ComponentModel.DataAnnotations;

namespace FinanceManager.Shared.Dtos.Budget;

/// <summary>
/// Request payload for creating a budget rule.
/// </summary>
public sealed record BudgetRuleCreateRequest(
    Guid BudgetPurposeId,
    decimal Amount,
    BudgetIntervalType Interval,
    int? CustomIntervalMonths,
    DateOnly StartDate,
    DateOnly? EndDate);
