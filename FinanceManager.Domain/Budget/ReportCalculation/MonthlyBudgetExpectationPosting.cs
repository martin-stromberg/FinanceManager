using FinanceManager.Shared.Dtos.Budget;

namespace FinanceManager.Domain.Budget.ReportCalculation;

/// <summary>
/// Represents a single expected occurrence of a <see cref="BudgetRule"/> (one budgeted amount for a
/// specific period), together with the actual postings that were assigned to it.
/// </summary>
public sealed class MonthlyBudgetExpectationPosting
{
    private readonly List<MonthlyBudgetRealization> _assignedPostings = new();
    private readonly List<MonthlyBudgetRealization> _unvaluedMatchedPostings = new();

    internal MonthlyBudgetExpectationPosting(
        decimal amount,
        BudgetValuationType budgetType,
        DateOnly startDate,
        int creationOrder,
        RuleOccurrencePeriod period,
        PurposeMatchPattern purposeMatchPattern)
    {
        Amount = amount;
        BudgetType = budgetType;
        StartDate = startDate;
        CreationOrder = creationOrder;
        PeriodStart = period.PeriodStart;
        PeriodEnd = period.PeriodEnd;
        PurposePattern = purposeMatchPattern.Pattern;
        PurposePatternIsRegex = purposeMatchPattern.IsRegex;
    }

    /// <summary>
    /// Gets the expected amount for this occurrence (positive or negative).
    /// </summary>
    public decimal Amount { get; }

    /// <summary>
    /// Gets how matching postings are valued for this occurrence.
    /// </summary>
    public BudgetValuationType BudgetType { get; }

    /// <summary>
    /// Gets the start date of the originating <see cref="BudgetRule"/>, used for priority sorting.
    /// </summary>
    public DateOnly StartDate { get; }

    // Sequential creation order, used as a tie-breaker when several occurrences share the same StartDate.
    internal int CreationOrder { get; }

    /// <summary>
    /// Gets the inclusive start date of the period this occurrence covers.
    /// </summary>
    public DateOnly PeriodStart { get; }

    /// <summary>
    /// Gets the inclusive end date of the period this occurrence covers.
    /// </summary>
    public DateOnly PeriodEnd { get; }

    /// <summary>
    /// Gets the optional purpose pattern used to match postings against this occurrence.
    /// </summary>
    public string? PurposePattern { get; }

    /// <summary>
    /// Gets whether <see cref="PurposePattern"/> should be treated as a regular expression.
    /// </summary>
    public bool PurposePatternIsRegex { get; }

    /// <summary>
    /// Gets the postings (or partial posting amounts) that have been assigned to this occurrence.
    /// </summary>
    public IReadOnlyList<MonthlyBudgetRealization> AssignedPostings => _assignedPostings;

    /// <summary>
    /// Gets postings that matched this occurrence's source and pattern but were not assigned to it
    /// (e.g. excluded by an <see cref="BudgetValuationType.ExactPostings"/> sign mismatch). These are
    /// reported alongside <see cref="AssignedPostings"/> for visibility, but are not counted in
    /// <see cref="SumAssignedAmount"/>.
    /// </summary>
    public IReadOnlyList<MonthlyBudgetRealization> UnvaluedMatchedPostings => _unvaluedMatchedPostings;

    /// <summary>
    /// Gets the sum of the amounts currently assigned to this occurrence.
    /// </summary>
    public decimal SumAssignedAmount => _assignedPostings.Sum(p => p.Amount);

    /// <summary>
    /// Gets the remaining capacity (absolute value) of this occurrence before it is considered exhausted.
    /// </summary>
    public decimal RemainingCapacity => Math.Max(0m, Math.Abs(Amount) - Math.Abs(SumAssignedAmount));

    // Clears all currently assigned postings, allowing re-assignment (used by the finish phase when
    // several occurrences for the same purpose need to be re-assigned in posting-date order).
    internal void Reset() => _assignedPostings.Clear();

    // Records a posting that matched this occurrence's source/period/pattern but could not be assigned
    // to it (see UnvaluedMatchedPostings).
    internal void AddUnvaluedMatch(MonthlyBudgetRealization posting) => _unvaluedMatchedPostings.Add(posting);

    // Assigns as much of 'posting' as fits into RemainingCapacity. Returns the leftover amount
    // (same sign as posting's amount) that could not be absorbed.
    internal decimal Assign(MonthlyBudgetRealization posting)
    {
        if (posting.Amount == 0m)
        {
            return 0m;
        }

        var remainingCapacity = RemainingCapacity;
        if (remainingCapacity <= 0m)
        {
            return posting.Amount;
        }

        var postingMagnitude = Math.Abs(posting.Amount);
        if (postingMagnitude <= remainingCapacity)
        {
            _assignedPostings.Add(posting);
            return 0m;
        }

        var sign = Math.Sign(posting.Amount);
        var assignedAmount = remainingCapacity * sign;
        _assignedPostings.Add(posting with { Amount = assignedAmount });
        return posting.Amount - assignedAmount;
    }
}

/// <summary>
/// Groups the inclusive start/end date of a single <see cref="BudgetRule"/> occurrence, as produced by
/// interval expansion (Monthly/Quarterly/Yearly/CustomMonths).
/// </summary>
/// <param name="PeriodStart">Inclusive start date of the occurrence's period.</param>
/// <param name="PeriodEnd">Inclusive end date of the occurrence's period.</param>
public readonly record struct RuleOccurrencePeriod(DateOnly PeriodStart, DateOnly PeriodEnd);

/// <summary>
/// Groups the optional purpose-pattern used to match postings against a <see cref="BudgetRule"/> occurrence.
/// </summary>
/// <param name="Pattern">The pattern (plain substring or regular expression), or <c>null</c> when the rule does not restrict matching by pattern.</param>
/// <param name="IsRegex">Whether <paramref name="Pattern"/> should be treated as a regular expression rather than a plain substring.</param>
public readonly record struct PurposeMatchPattern(string? Pattern, bool IsRegex);
