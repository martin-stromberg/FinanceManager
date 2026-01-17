namespace FinanceManager.Web.ViewModels.Budget;

/// <summary>
/// Determines which time scope is used for the category table values.
/// </summary>
public enum BudgetReportValueScope
{
    /// <summary>
    /// Use values for the whole selected report range.
    /// </summary>
    TotalRange = 0,

    /// <summary>
    /// Use values for the last calculated interval bucket.
    /// </summary>
    LastInterval = 1
}
