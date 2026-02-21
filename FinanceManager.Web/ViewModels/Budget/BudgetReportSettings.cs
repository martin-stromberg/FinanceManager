namespace FinanceManager.Web.ViewModels.Budget;

/// <summary>
/// Determines which date field is used for postings aggregation.
/// </summary>
public enum BudgetReportDateBasis
{
    /// <summary>
    /// Use the booking date.
    /// </summary>
    BookingDate = 0,

    /// <summary>
    /// Use the valuta/value date.
    /// </summary>
    ValutaDate = 1
}

/// <summary>
/// Settings controlling the budget report rendering.
/// </summary>
public sealed record BudgetReportSettings(
    DateOnly AsOfDate,
    int Months,
    BudgetReportInterval Interval,
    bool ShowTitle,
    bool ShowLineChart,
    bool ShowMonthlyTable,
    bool ShowDetailsTable,
    bool ShowPurposeRows,
    bool ShowPeriodSumRow,
    bool ShowCategorySumRow,
    BudgetReportValueScope CategoryValueScope,
    BudgetReportDateBasis DateBasis)
{
    /// <summary>
    /// Default settings: 12 months ending at end of current month.
    /// </summary>
    public static BudgetReportSettings Default { get; } = new(
        AsOfDate: new DateOnly(DateTime.Today.Year, DateTime.Today.Month, DateTime.DaysInMonth(DateTime.Today.Year, DateTime.Today.Month)),
        Months: 12,
        Interval: BudgetReportInterval.Month,
        ShowTitle: true,
        ShowLineChart: true,
        ShowMonthlyTable: true,
        ShowDetailsTable: true,
        ShowPurposeRows: true,
        ShowPeriodSumRow: true,
        ShowCategorySumRow: true,
        CategoryValueScope: BudgetReportValueScope.LastInterval,
        DateBasis: BudgetReportDateBasis.BookingDate);
}
