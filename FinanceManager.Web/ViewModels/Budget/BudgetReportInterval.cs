namespace FinanceManager.Web.ViewModels.Budget;

/// <summary>
/// Interval of the aggregated budget report.
/// </summary>
public enum BudgetReportInterval
{
    /// <summary>
    /// Monthly periods.
    /// </summary>
    Month = 0,

    /// <summary>
    /// Quarterly periods.
    /// </summary>
    Quarter = 1,

    /// <summary>
    /// Yearly periods.
    /// </summary>
    Year = 2
}
