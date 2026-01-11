namespace FinanceManager.Shared.Dtos.Budget;

/// <summary>
/// Request payload for updating a budget override.
/// </summary>
public sealed record BudgetOverrideUpdateRequest(
    BudgetPeriodKey Period,
    decimal Amount);
