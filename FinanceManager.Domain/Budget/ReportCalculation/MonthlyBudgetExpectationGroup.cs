namespace FinanceManager.Domain.Budget.ReportCalculation;

/// <summary>
/// Groups the budget expectations of a single month by budget category, distinguishing between
/// expectations declared directly on the category and expectations declared on the purposes assigned
/// to it. Uses <see cref="Guid.Empty"/> as the category id for the virtual "Uncategorized" group.
/// </summary>
public sealed class MonthlyBudgetExpectationGroup
{
    private readonly List<MonthlyBudgetExpectation> _directExpectations = new();
    private readonly List<MonthlyBudgetExpectation> _purposes = new();

    internal MonthlyBudgetExpectationGroup(Guid budgetCategoryId, string categoryName)
    {
        BudgetCategoryId = budgetCategoryId;
        CategoryName = categoryName;
    }

    /// <summary>
    /// Gets the budget category id, or <see cref="Guid.Empty"/> for the virtual "Uncategorized" group.
    /// </summary>
    public Guid BudgetCategoryId { get; }

    /// <summary>
    /// Gets the display name of the category.
    /// </summary>
    public string CategoryName { get; }

    /// <summary>
    /// Gets the expectations declared directly on the category (from <see cref="BudgetRule.BudgetCategoryId"/> rules).
    /// </summary>
    public IReadOnlyList<MonthlyBudgetExpectation> DirectExpectations => _directExpectations;

    /// <summary>
    /// Gets the expectations of the budget purposes assigned to this category.
    /// </summary>
    public IReadOnlyList<MonthlyBudgetExpectation> Purposes => _purposes;

    internal void AddDirectExpectation(MonthlyBudgetExpectation expectation) => _directExpectations.Add(expectation);

    internal void AddPurposeExpectation(MonthlyBudgetExpectation expectation) => _purposes.Add(expectation);
}
