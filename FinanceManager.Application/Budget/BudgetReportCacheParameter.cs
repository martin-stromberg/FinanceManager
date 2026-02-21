using FinanceManager.Shared.Dtos.Budget;

namespace FinanceManager.Application.Budget;

/// <summary>
/// Parameter describing a cached budget report raw data range.
/// </summary>
public sealed record BudgetReportCacheParameter(DateOnly From, DateOnly To, FinanceManager.Shared.Dtos.Budget.BudgetReportDateBasis DateBasis);
