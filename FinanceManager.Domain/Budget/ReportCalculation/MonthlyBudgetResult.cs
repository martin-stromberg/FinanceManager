using FinanceManager.Shared.Dtos.Budget;

namespace FinanceManager.Domain.Budget.ReportCalculation;

/// <summary>
/// Represents the calculation result for a single month within a <see cref="Budgetbericht"/>: the
/// budget expectations grouped by category, and the actual postings that could not be matched to
/// any expectation (unbudgeted) or that represent cost-neutral mirror transfers.
/// </summary>
public sealed class MonthlyBudgetResult
{
    private readonly List<MonthlyBudgetExpectationGroup> _expectationGroups = new();
    private readonly List<MonthlyBudgetRealization> _unbudgetedPostings = new();
    private readonly List<MonthlyBudgetRealization> _costNeutralPostings = new();

    internal MonthlyBudgetResult(DateTime month)
    {
        Month = month;
    }

    /// <summary>
    /// Gets the first day of the month this result represents.
    /// </summary>
    public DateTime Month { get; }

    /// <summary>
    /// Gets the budget expectations for this month, grouped by category.
    /// </summary>
    public IReadOnlyList<MonthlyBudgetExpectationGroup> ExpectationGroups => _expectationGroups;

    /// <summary>
    /// Gets the postings for this month that could not be matched to any budget expectation.
    /// </summary>
    public IReadOnlyList<MonthlyBudgetRealization> UnbudgetedPostings => _unbudgetedPostings;

    /// <summary>
    /// Gets the cost-neutral mirror postings for this month (postings with a <c>GroupId</c> that did
    /// not match any budget expectation).
    /// </summary>
    public IReadOnlyList<MonthlyBudgetRealization> CostNeutralPostings => _costNeutralPostings;

    internal void AddExpectationGroup(MonthlyBudgetExpectationGroup group) => _expectationGroups.Add(group);

    internal void AddUnbudgetedPosting(MonthlyBudgetRealization posting) => _unbudgetedPostings.Add(posting);

    internal void AddCostNeutralPosting(MonthlyBudgetRealization posting) => _costNeutralPostings.Add(posting);
}
