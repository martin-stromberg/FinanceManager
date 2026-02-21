namespace FinanceManager.Shared.Dtos.Budget;

/// <summary>
/// Request for exporting all postings of a budget report across the full report range.
/// </summary>
public sealed record BudgetReportExportRequest(
    DateOnly AsOfDate,
    int Months,
    BudgetReportDateBasis DateBasis
);
