namespace FinanceManager.Shared.Dtos.Budget;

/// <summary>
/// DTO for Home Monthly Budget KPI.
/// </summary>
public sealed class MonthlyBudgetKpiDto
{
    /// <summary>
    /// Planned income for the current month.
    /// </summary>
    public decimal PlannedIncome { get; set; }

    /// <summary>
    /// Planned expenses (absolute, positive value) for the current month.
    /// </summary>
    public decimal PlannedExpenseAbs { get; set; }

    /// <summary>
    /// Actual income for the current month.
    /// </summary>
    public decimal ActualIncome { get; set; }

    /// <summary>
    /// Actual expenses (absolute, positive value) for the current month.
    /// </summary>
    public decimal ActualExpenseAbs { get; set; }

    /// <summary>
    /// Target result (planned income minus planned expenses) for the current month.
    /// </summary>
    public decimal TargetResult { get; set; }
    /// <summary>
    /// Expected income for the current month (planned + unbudgeted actuals).
    /// </summary>
    public decimal ExpectedIncome { get; set; }

    /// <summary>
    /// Expected expenses (absolute, positive value) for the current month (planned + unbudgeted actuals).
    /// </summary>
    public decimal ExpectedExpenseAbs { get; set; }
    /// <summary>
    /// Sum of unbudgeted actual income present in the period (income without assigned purpose).
    /// </summary>
    public decimal UnbudgetedIncome { get; set; }

    /// <summary>
    /// Sum of unbudgeted actual expenses (absolute, positive) present in the period (expenses without assigned purpose).
    /// </summary>
    public decimal UnbudgetedExpenseAbs { get; set; }
}