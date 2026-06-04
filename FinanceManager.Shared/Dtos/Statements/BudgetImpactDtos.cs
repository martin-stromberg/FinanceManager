namespace FinanceManager.Shared.Dtos.Statements;

/// <summary>
/// Per-purpose budget impact hint for statement draft entry updates.
/// </summary>
public sealed record BudgetImpactHintDto(
    Guid? BudgetPurposeId,
    string? BudgetPurposeName,
    string BudgetPeriod,
    BudgetImpactHintType HintType,
    decimal TargetValue,
    decimal ActualBefore,
    decimal ActualAfter,
    decimal FulfillmentRateBefore,
    decimal FulfillmentRateAfter,
    decimal Delta,
    string Reason);

/// <summary>
/// Budget impact result for a single entry interaction.
/// </summary>
public sealed record BudgetImpactEvaluationDto(
    Guid EntryId,
    DateTime EvaluatedAtUtc,
    string EvaluationFingerprint,
    IReadOnlyList<BudgetImpactHintDto> Hints);

/// <summary>
/// Single summary row for final booking impact output.
/// </summary>
public sealed record BookingImpactSummaryItemDto(
    Guid? BudgetPurposeId,
    string? BudgetPurposeName,
    string BudgetPeriod,
    BudgetImpactHintType HintType,
    decimal TargetValue,
    decimal ActualBefore,
    decimal ActualAfter,
    decimal FulfillmentRateBefore,
    decimal FulfillmentRateAfter,
    decimal Delta,
    string Reason);

/// <summary>
/// Final summary of budget impact for entry or full draft booking.
/// </summary>
public sealed record BookingImpactSummaryDto(
    Guid DraftId,
    Guid? EntryId,
    DateTime EvaluatedAtUtc,
    string EvaluationFingerprint,
    BudgetImpactHintType HighestSeverity,
    IReadOnlyList<BookingImpactSummaryItemDto> Items);
