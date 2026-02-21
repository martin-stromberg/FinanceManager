namespace FinanceManager.Shared.Dtos.Budget;

/// <summary>
/// DTO for Home Monthly Budget KPI.
/// </summary>
public sealed record MonthlyBudgetKpiDto
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
    /// Actual result (actual income minus actual expenses) for the current month.
    /// </summary>
    public decimal ActualResult { get; set; }

    /// <summary>
    /// Target result (planned income minus planned expenses) for the current month.
    /// </summary>
    public decimal PlannedResult { get; set; }
    /// <summary>
    /// Expected income for the current month (planned + unbudgeted actuals).
    /// </summary>
    public decimal ExpectedIncome { get; set; }

    /// <summary>
    /// Expected expenses (absolute, positive value) for the current month (planned + unbudgeted actuals).
    /// </summary>
    public decimal ExpectedExpenseAbs { get; set; }
    /// <summary>
    /// Remaining planned (budgeted) expenses that are not yet covered by actuals (absolute, positive value).
    /// </summary>
    public decimal RemainingPlannedExpenseAbs { get; set; }

    /// <summary>
    /// Remaining planned (budgeted) income that is not yet covered by actuals.
    /// </summary>
    public decimal RemainingPlannedIncome { get; set; }
    /// <summary>
    /// Expected target result for the current month (ExpectedIncome - ExpectedExpenseAbs).
    /// </summary>
    public decimal ExpectedTargetResult { get; set; }
    /// <summary>
    /// Sum of unbudgeted actual income present in the period (income without assigned purpose).
    /// </summary>
    public decimal UnbudgetedIncome { get; set; }

    /// <summary>
    /// Sum of unbudgeted actual expenses (absolute, positive) present in the period (expenses without assigned purpose).
    /// </summary>
    public decimal UnbudgetedExpenseAbs { get; set; }

    /// <summary>
    /// Budgeted realized income: sum of positive budget rules for which there are postings.
    /// </summary>
    public decimal BudgetedRealizedIncome { get; set; }

    /// <summary>
    /// Budgeted realized expenses (absolute, positive): sum of negative budget rules for which there are postings.
    /// Includes both purpose- and category-level negative rules that have associated postings.
    /// </summary>
    public decimal BudgetedRealizedExpenseAbs { get; set; }
}