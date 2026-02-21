namespace FinanceManager.Shared.Dtos.Budget;

/// <summary>
/// Request payload for creating a budget override.
/// </summary>
public sealed record BudgetOverrideCreateRequest(
    Guid BudgetPurposeId,
    BudgetPeriodKey Period,
    decimal Amount);
