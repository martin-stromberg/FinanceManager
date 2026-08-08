using FinanceManager.Domain.Budget.ReportCalculation;
using FinanceManager.Shared.Dtos.Budget;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Infrastructure.Budget.Mapping;

/// <summary>
/// Maps a calculated <see cref="Budgetbericht"/> to the API-facing DTOs (<see cref="BudgetReportRawDataDto"/>,
/// <see cref="MonthlyBudgetKpiDto"/>) that <see cref="IBudgetReportService"/> consumers rely on.
/// </summary>
public static class BudgetberichtMapper
{
    /// <summary>
    /// Builds a <see cref="BudgetReportRawDataDto"/> from a finished <see cref="Budgetbericht"/>.
    /// </summary>
    /// <param name="budgetbericht">The finished budget report calculation.</param>
    /// <param name="from">Inclusive start of the report period, used for the DTO's period fields.</param>
    /// <param name="to">Inclusive end of the report period, used for the DTO's period fields.</param>
    /// <param name="purposeInfoById">Lookup of purpose overview data (source name, valuation type) by purpose id, used to enrich purpose rows.</param>
    /// <param name="logger">Optional logger used to record data inconsistencies (e.g. a purpose missing from <paramref name="purposeInfoById"/>) instead of silently falling back to default values.</param>
    /// <returns>The raw data DTO with categorized purposes, uncategorized purposes and unbudgeted postings.</returns>
    public static BudgetReportRawDataDto MapToRawDataDto(
        Budgetbericht budgetbericht,
        DateOnly from,
        DateOnly to,
        IReadOnlyDictionary<Guid, BudgetPurposeOverviewDto> purposeInfoById,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(budgetbericht);
        ArgumentNullException.ThrowIfNull(purposeInfoById);

        var uncategorizedExpectationsByPurpose = new Dictionary<Guid, List<MonthlyBudgetExpectation>>();
        var unbudgetedDtos = new List<BudgetReportPostingRawDataDto>();
        var categoryAccumulator = new Dictionary<Guid, CategoryAccumulator>();

        // Every MonthlyBudgetExpectation is scoped to a single month, so a purpose spanning several
        // months of the report period produces one expectation per month. These are accumulated per
        // purpose id here and merged into a single row per purpose (spanning the whole period) below,
        // to avoid duplicating the same purpose once per month in the output.
        foreach (var monthResult in budgetbericht.MonthlyResults)
        {
            foreach (var group in monthResult.ExpectationGroups)
            {
                if (group.BudgetCategoryId == Guid.Empty)
                {
                    foreach (var expectation in group.Purposes)
                    {
                        AddExpectation(uncategorizedExpectationsByPurpose, expectation);
                    }

                    continue;
                }

                if (!categoryAccumulator.TryGetValue(group.BudgetCategoryId, out var accumulator))
                {
                    accumulator = new CategoryAccumulator(group.CategoryName);
                    categoryAccumulator[group.BudgetCategoryId] = accumulator;
                }

                foreach (var direct in group.DirectExpectations)
                {
                    accumulator.Income += direct.Postings.Where(p => p.Amount > 0).Sum(p => p.Amount);
                    accumulator.Expense += direct.Postings.Where(p => p.Amount < 0).Sum(p => p.Amount);
                }

                foreach (var expectation in group.Purposes)
                {
                    AddExpectation(accumulator.ExpectationsByPurpose, expectation);
                }
            }

            foreach (var posting in monthResult.UnbudgetedPostings)
            {
                unbudgetedDtos.Add(MapUnbudgetedPosting(posting));
            }

            foreach (var posting in monthResult.CostNeutralPostings)
            {
                unbudgetedDtos.Add(MapUnbudgetedPosting(posting));
            }
        }

        var categoryDtos = categoryAccumulator
            .Select(entry => new BudgetReportCategoryRawDataDto
            {
                CategoryId = entry.Key,
                CategoryName = entry.Value.Name,
                BudgetedIncome = entry.Value.Income,
                BudgetedExpense = entry.Value.Expense,
                BudgetedTarget = entry.Value.Income + entry.Value.Expense,
                Purposes = entry.Value.ExpectationsByPurpose.Values
                    .Select(expectations => MapPurpose(expectations, purposeInfoById, logger))
                    .OrderBy(p => p.PurposeName)
                    .ToArray()
            })
            .OrderBy(c => c.CategoryName)
            .ToArray();

        var uncategorizedDtos = uncategorizedExpectationsByPurpose.Values
            .Select(expectations => MapPurpose(expectations, purposeInfoById, logger))
            .OrderBy(p => p.PurposeName)
            .ToArray();

        return new BudgetReportRawDataDto
        {
            PeriodStart = from.ToDateTime(TimeOnly.MinValue),
            PeriodEnd = to.ToDateTime(TimeOnly.MaxValue),
            Categories = categoryDtos,
            UncategorizedPurposes = uncategorizedDtos,
            UnbudgetedPostings = unbudgetedDtos.ToArray()
        };
    }

    private static void AddExpectation(Dictionary<Guid, List<MonthlyBudgetExpectation>> expectationsByPurpose, MonthlyBudgetExpectation expectation)
    {
        var key = expectation.BudgetPurposeId ?? Guid.Empty;
        if (!expectationsByPurpose.TryGetValue(key, out var list))
        {
            list = new List<MonthlyBudgetExpectation>();
            expectationsByPurpose[key] = list;
        }

        list.Add(expectation);
    }

    /// <summary>
    /// Builds a <see cref="MonthlyBudgetKpiDto"/> from the detail rows of a single-month <see cref="Budgetbericht"/>.
    /// </summary>
    /// <param name="entries">The detail rows returned by <see cref="Budgetbericht.GetCurrentResult"/>.</param>
    /// <returns>The KPI DTO with planned, actual and expected income/expense figures.</returns>
    public static MonthlyBudgetKpiDto MapToMonthlyKpiDto(BudgetReportEntry[] entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var purposeRows = entries.Where(e => e.RowKind == BudgetReportEntryRowKind.Purpose).ToArray();

        var plannedIncome = purposeRows.Where(e => e.BudgetedAmount > 0).Sum(e => e.BudgetedAmount);
        var plannedExpenseAbs = Math.Abs(purposeRows.Where(e => e.BudgetedAmount < 0).Sum(e => e.BudgetedAmount));

        var budgetedRealizedIncome = purposeRows.SelectMany(e => e.Postings).Where(p => p.Amount > 0).Sum(p => p.Amount);
        var budgetedRealizedExpenseAbs = Math.Abs(purposeRows.SelectMany(e => e.Postings).Where(p => p.Amount < 0).Sum(p => p.Amount));

        var unbudgetedPostings = entries.FirstOrDefault(e => e.RowKind == BudgetReportEntryRowKind.Unbudgeted)?.Postings
            ?? Array.Empty<MonthlyBudgetRealization>();
        var unbudgetedIncome = unbudgetedPostings.Where(p => p.Amount > 0).Sum(p => p.Amount);
        var unbudgetedExpenseAbs = Math.Abs(unbudgetedPostings.Where(p => p.Amount < 0).Sum(p => p.Amount));

        // Cost-neutral postings (self-contact mirror transfers, see Budgetbericht.AddPosting) are not
        // "unbudgeted spending" in the UnbudgetedIncome/UnbudgetedExpenseAbs sense, but they are still part
        // of what actually happened in the period, so - like the "Endsumme" row of GetCurrentResult() -
        // ActualIncome/ActualExpenseAbs include them.
        var costNeutralPostings = entries.FirstOrDefault(e => e.RowKind == BudgetReportEntryRowKind.CostNeutral)?.Postings
            ?? Array.Empty<MonthlyBudgetRealization>();
        var costNeutralIncome = costNeutralPostings.Where(p => p.Amount > 0).Sum(p => p.Amount);
        var costNeutralExpenseAbs = Math.Abs(costNeutralPostings.Where(p => p.Amount < 0).Sum(p => p.Amount));

        var actualIncome = budgetedRealizedIncome + unbudgetedIncome + costNeutralIncome;
        var actualExpenseAbs = budgetedRealizedExpenseAbs + unbudgetedExpenseAbs + costNeutralExpenseAbs;
        var remainingPlannedIncome = Math.Max(0, plannedIncome - budgetedRealizedIncome);
        var remainingPlannedExpenseAbs = Math.Max(0, plannedExpenseAbs - budgetedRealizedExpenseAbs);

        return new MonthlyBudgetKpiDto
        {
            PlannedIncome = plannedIncome,
            PlannedExpenseAbs = plannedExpenseAbs,
            PlannedResult = plannedIncome - plannedExpenseAbs,
            UnbudgetedIncome = unbudgetedIncome,
            UnbudgetedExpenseAbs = unbudgetedExpenseAbs,
            BudgetedRealizedIncome = budgetedRealizedIncome,
            BudgetedRealizedExpenseAbs = budgetedRealizedExpenseAbs,
            ActualIncome = actualIncome,
            ActualExpenseAbs = actualExpenseAbs,
            ActualResult = actualIncome - actualExpenseAbs,
            ExpectedIncome = actualIncome + remainingPlannedIncome,
            ExpectedExpenseAbs = actualExpenseAbs + remainingPlannedExpenseAbs,
            RemainingPlannedExpenseAbs = remainingPlannedExpenseAbs,
            RemainingPlannedIncome = remainingPlannedIncome,
            ExpectedTargetResult = (actualIncome + remainingPlannedIncome) - (actualExpenseAbs + remainingPlannedExpenseAbs)
        };
    }

    private static BudgetReportPurposeRawDataDto MapPurpose(
        IReadOnlyList<MonthlyBudgetExpectation> expectations,
        IReadOnlyDictionary<Guid, BudgetPurposeOverviewDto> purposeInfoById,
        ILogger? logger = null)
    {
        var first = expectations[0];
        var purposeId = first.BudgetPurposeId ?? Guid.Empty;

        if (!purposeInfoById.TryGetValue(purposeId, out var info))
        {
            logger?.LogWarning(
                "Budget report: no overview data found for purpose {PurposeId} ({PurposeName}); falling back to default source/valuation info for this row.",
                purposeId,
                first.Name);
        }

        var expectationPostings = expectations.SelectMany(e => e.Postings).ToList();

        var budgetedIncome = expectationPostings.Where(p => p.Amount > 0).Sum(p => p.Amount);
        var budgetedExpense = expectationPostings.Where(p => p.Amount < 0).Sum(p => p.Amount);

        var valuedPostings = expectationPostings.SelectMany(p => p.AssignedPostings).Select(MapPosting);
        var unvaluedPostings = expectationPostings.SelectMany(p => p.UnvaluedMatchedPostings)
            .Select(p => MapPosting(p) with { IsValuedForBudgetPurpose = false });

        var postings = valuedPostings
            .Concat(unvaluedPostings)
            .OrderBy(p => p.BookingDate)
            .ToArray();

        return new BudgetReportPurposeRawDataDto
        {
            PurposeId = purposeId,
            PurposeName = first.Name,
            BudgetedIncome = budgetedIncome,
            BudgetedExpense = budgetedExpense,
            BudgetedTarget = budgetedIncome + budgetedExpense,
            BudgetSourceType = info?.SourceType ?? BudgetSourceType.Contact,
            SourceId = info?.SourceId ?? Guid.Empty,
            SourceName = info?.SourceName ?? string.Empty,
            ValuationType = info?.ValuationType ?? BudgetValuationType.ExactPostings,
            Postings = postings
        };
    }

    private static BudgetReportPostingRawDataDto MapUnbudgetedPosting(MonthlyBudgetRealization posting) =>
        MapPosting(posting) with { IsValuedForBudgetPurpose = false, BudgetCategoryId = null, BudgetPurposeId = null };

    private static BudgetReportPostingRawDataDto MapPosting(MonthlyBudgetRealization posting) => new()
    {
        PostingId = posting.PostingId,
        BookingDate = posting.BookingDate,
        ValutaDate = posting.ValutaDate,
        Amount = posting.Amount,
        PostingKind = posting.PostingKind,
        Description = posting.Description ?? string.Empty,
        Subject = posting.Purpose ?? string.Empty,
        AccountId = posting.AccountId,
        AccountName = posting.AccountName,
        ContactId = posting.ContactId,
        ContactName = posting.ContactName,
        SavingsPlanId = posting.SavingsPlanId,
        SavingsPlanName = posting.SavingsPlanName,
        SecurityId = posting.SecurityId,
        SecurityName = posting.SecurityName,
        IsValuedForBudgetPurpose = true,
        GroupId = posting.GroupId
    };

    private sealed class CategoryAccumulator
    {
        public CategoryAccumulator(string name)
        {
            Name = name;
        }

        public string Name { get; }
        public decimal Income { get; set; }
        public decimal Expense { get; set; }
        public Dictionary<Guid, List<MonthlyBudgetExpectation>> ExpectationsByPurpose { get; } = new();
    }
}
