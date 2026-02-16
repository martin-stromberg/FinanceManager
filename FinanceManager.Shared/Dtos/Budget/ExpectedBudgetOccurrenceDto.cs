using System;

namespace FinanceManager.Shared.Dtos.Budget;

/// <summary>
/// Represents a single expected budget occurrence derived from a budget rule.
/// Contains the originating rule id, optional purpose/category, amount and occurrence date.
/// </summary>
public sealed record ExpectedBudgetOccurrenceDto(
    Guid RuleId,
    Guid? PurposeId,
    Guid? CategoryId,
    decimal Amount,
    BudgetIntervalType Interval,
    DateOnly OccurrenceDate,
    BudgetSourceType? SourceType,
    Guid? SourceId,
    string SourceName,
    string PurposeName,
    string CategoryName);
