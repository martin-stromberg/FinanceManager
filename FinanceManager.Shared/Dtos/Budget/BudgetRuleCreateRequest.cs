using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace FinanceManager.Shared.Dtos.Budget;

/// <summary>
/// Request payload for creating a budget rule.
/// Exactly one of <see cref="BudgetPurposeId"/> or <see cref="BudgetCategoryId"/> must be provided.
/// </summary>
public sealed record BudgetRuleCreateRequest(
    Guid? BudgetPurposeId,
    Guid? BudgetCategoryId,
    decimal Amount,
    BudgetIntervalType Interval,
    int? CustomIntervalMonths,
    DateOnly StartDate,
    DateOnly? EndDate);
