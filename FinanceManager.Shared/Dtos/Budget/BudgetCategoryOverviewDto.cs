namespace FinanceManager.Shared.Dtos.Budget;

/// <summary>
/// Overview DTO for budget category list pages.
/// </summary>
public sealed record BudgetCategoryOverviewDto(
    Guid Id,
    string Name,
    decimal Budget,
    decimal Actual,
    decimal Delta,
    int PurposeCount);
