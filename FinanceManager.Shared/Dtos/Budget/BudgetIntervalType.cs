namespace FinanceManager.Shared.Dtos.Budget;

/// <summary>
/// Defines how often a budget rule produces an expected amount.
/// </summary>
public enum BudgetIntervalType
{
    /// <summary>
    /// Once per month.
    /// </summary>
    Monthly = 0,

    /// <summary>
    /// Once per quarter.
    /// </summary>
    Quarterly = 1,

    /// <summary>
    /// Once per year.
    /// </summary>
    Yearly = 2,

    /// <summary>
    /// A custom interval in months.
    /// </summary>
    CustomMonths = 3
}
