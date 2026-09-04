using FinanceManager.Domain.Budget.ReportCalculation;
using FinanceManager.Shared.Dtos.Budget;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Infrastructure.Budget.Mapping;

/// <summary>
/// Maps a calculated <see cref="Budgetbericht"/> to the API-facing DTOs (<see cref="BudgetReportRawDataDto"/>,
/// <see cref="MonthlyBudgetKpiDto"/>) that <see cref="FinanceManager.Application.Budget.IBudgetReportService"/> consumers rely on.
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
                    accumulator.Income += direct.Postings.Where(p => p.BudgetedDisplayAmount > 0).Sum(p => p.BudgetedDisplayAmount);
                    accumulator.Expense += direct.Postings.Where(p => p.BudgetedDisplayAmount < 0).Sum(p => p.BudgetedDisplayAmount);
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

        var budgetedIncome = expectationPostings.Where(p => p.BudgetedDisplayAmount > 0).Sum(p => p.BudgetedDisplayAmount);
        var budgetedExpense = expectationPostings.Where(p => p.BudgetedDisplayAmount < 0).Sum(p => p.BudgetedDisplayAmount);

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

    /// <summary>
    /// Builds the period table (<see cref="BudgetReportPeriodDto"/> rows) from a finished
    /// <see cref="Budgetbericht"/>'s <see cref="Budgetbericht.GetCumulativeResult"/>. Assumes the
    /// <see cref="Budgetbericht"/> was constructed with a monthly interval, so each bucket spans exactly
    /// one calendar month.
    /// </summary>
    /// <param name="cumulativeEntries">The interval buckets returned by <see cref="Budgetbericht.GetCumulativeResult"/>.</param>
    /// <returns>One <see cref="BudgetReportPeriodDto"/> per monthly bucket, in chronological order.</returns>
    public static IReadOnlyList<BudgetReportPeriodDto> MapToPeriodDtos(IReadOnlyList<BudgetReportCumulativeEntry> cumulativeEntries)
    {
        ArgumentNullException.ThrowIfNull(cumulativeEntries);

        return cumulativeEntries
            .Select(entry =>
            {
                var monthEnd = new DateOnly(
                    entry.IntervalStartDate.Year,
                    entry.IntervalStartDate.Month,
                    DateTime.DaysInMonth(entry.IntervalStartDate.Year, entry.IntervalStartDate.Month));

                return new BudgetReportPeriodDto(
                    entry.IntervalStartDate,
                    monthEnd,
                    entry.BudgetedAmount,
                    entry.ActualAmount,
                    entry.Deviation,
                    ToDeltaPctFraction(entry.BudgetedAmount, entry.Deviation));
            })
            .ToArray();
    }

    /// <summary>
    /// Builds the category/purpose detail table (<see cref="BudgetReportCategoryDto"/> rows) from the flat
    /// row list returned by <see cref="Budgetbericht.GetCurrentResult"/>, re-nesting the purpose rows
    /// belonging to each category and appending the Unbudgeted, Sub-sum, cost-neutral and grand-total rows
    /// in the order expected by the API consumers of <see cref="BudgetReportDto"/>.
    /// </summary>
    /// <param name="entries">The detail rows returned by <see cref="Budgetbericht.GetCurrentResult"/>.</param>
    /// <param name="purposeInfoById">Lookup of purpose overview data (source type/id), used to enrich purpose rows.</param>
    /// <returns>The category rows for <see cref="BudgetReportDto.Categories"/>.</returns>
    public static IReadOnlyList<BudgetReportCategoryDto> MapToReportCategoryDtos(
        BudgetReportEntry[] entries,
        IReadOnlyDictionary<Guid, BudgetPurposeOverviewDto> purposeInfoById)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(purposeInfoById);

        var (categories, unbudgetedEntry, costNeutralEntry, totalEntry) = BuildDataCategoryRows(entries, purposeInfoById);

        AppendUnbudgetedRow(categories, unbudgetedEntry);
        AppendSubSumRow(categories, totalEntry, costNeutralEntry);
        AppendCostNeutralRow(categories, costNeutralEntry);
        AppendSumRow(categories, totalEntry);

        return categories;
    }

    // First pass over the flat Budgetbericht.GetCurrentResult() rows: re-nests the Category/Purpose/Subtotal
    // rows into one BudgetReportCategoryDto per category (with its purposes nested inside), and picks out
    // the single Unbudgeted/CostNeutral/Total rows for the synthetic summary rows appended afterwards.
    private static (
        List<BudgetReportCategoryDto> Categories,
        BudgetReportEntry? UnbudgetedEntry,
        BudgetReportEntry? CostNeutralEntry,
        BudgetReportEntry? TotalEntry) BuildDataCategoryRows(
        BudgetReportEntry[] entries,
        IReadOnlyDictionary<Guid, BudgetPurposeOverviewDto> purposeInfoById)
    {
        var categories = new List<BudgetReportCategoryDto>();
        var currentPurposes = new List<BudgetReportPurposeDto>();
        var currentCategoryId = Guid.Empty;

        BudgetReportEntry? unbudgetedEntry = null;
        BudgetReportEntry? costNeutralEntry = null;
        BudgetReportEntry? totalEntry = null;

        foreach (var entry in entries)
        {
            switch (entry.RowKind)
            {
                case BudgetReportEntryRowKind.Category:
                    currentCategoryId = entry.BudgetCategoryId ?? Guid.Empty;
                    currentPurposes = new List<BudgetReportPurposeDto>();
                    break;

                case BudgetReportEntryRowKind.Purpose:
                    currentCategoryId = entry.BudgetCategoryId ?? currentCategoryId;

                    // A "Purpose" row without a BudgetPurposeId is a category-level direct budget
                    // expectation (a BudgetRule attached directly to the category, not to one of its
                    // purposes). Its amount is already folded into the category/subtotal totals below, but
                    // - matching the pre-existing BudgetReportDto contract - it is not a real budget
                    // purpose and must not be surfaced as its own row in the category's Purposes list.
                    if (entry.BudgetPurposeId.HasValue)
                    {
                        var purposeId = entry.BudgetPurposeId.Value;
                        purposeInfoById.TryGetValue(purposeId, out var info);
                        currentPurposes.Add(new BudgetReportPurposeDto(
                            purposeId,
                            entry.Name,
                            entry.BudgetedAmount,
                            entry.ActualAmount,
                            entry.Deviation,
                            ToDeltaPctFraction(entry.BudgetedAmount, entry.Deviation),
                            info?.SourceType ?? BudgetSourceType.Contact,
                            info?.SourceId ?? Guid.Empty));
                    }

                    break;

                case BudgetReportEntryRowKind.Subtotal:
                    categories.Add(new BudgetReportCategoryDto(
                        currentCategoryId,
                        entry.Name,
                        BudgetReportCategoryRowKind.Data,
                        entry.BudgetedAmount,
                        entry.ActualAmount,
                        entry.Deviation,
                        ToDeltaPctFraction(entry.BudgetedAmount, entry.Deviation),
                        currentPurposes));
                    currentPurposes = new List<BudgetReportPurposeDto>();
                    currentCategoryId = Guid.Empty;
                    break;

                case BudgetReportEntryRowKind.Unbudgeted:
                    unbudgetedEntry = entry;
                    break;

                case BudgetReportEntryRowKind.CostNeutral:
                    costNeutralEntry = entry;
                    break;

                case BudgetReportEntryRowKind.Total:
                    totalEntry = entry;
                    break;
            }
        }

        return (categories, unbudgetedEntry, costNeutralEntry, totalEntry);
    }

    private static void AppendUnbudgetedRow(List<BudgetReportCategoryDto> categories, BudgetReportEntry? unbudgetedEntry)
    {
        if (unbudgetedEntry != null && unbudgetedEntry.ActualAmount != 0m)
        {
            categories.Add(new BudgetReportCategoryDto(
                Guid.Empty,
                "Unbudgeted",
                BudgetReportCategoryRowKind.Unbudgeted,
                0m,
                unbudgetedEntry.ActualAmount,
                unbudgetedEntry.ActualAmount,
                0m,
                Array.Empty<BudgetReportPurposeDto>()));
        }
    }

    private static void AppendSubSumRow(List<BudgetReportCategoryDto> categories, BudgetReportEntry? totalEntry, BudgetReportEntry? costNeutralEntry)
    {
        // Sub-sum: all Data categories plus the (possibly zero) regular Unbudgeted amount, before the
        // cost-neutral (self-contact mirror) postings are added in. Both the Total row's BudgetedAmount and
        // the cost-neutral sum are already known at this point, so this can be derived without re-summing
        // 'categories' (Unbudgeted/CostNeutral never contribute a budgeted amount).
        var subSumBudget = totalEntry?.BudgetedAmount ?? 0m;
        var subSumActual = (totalEntry?.ActualAmount ?? 0m) - (costNeutralEntry?.ActualAmount ?? 0m);
        if (subSumActual != 0m || subSumBudget != 0m)
        {
            var subSumDelta = subSumActual - subSumBudget;
            categories.Add(new BudgetReportCategoryDto(
                Guid.Empty,
                "Sub-sum",
                BudgetReportCategoryRowKind.UnbudgetedSubSum,
                subSumBudget,
                subSumActual,
                subSumDelta,
                ToDeltaPctFraction(subSumBudget, subSumDelta),
                Array.Empty<BudgetReportPurposeDto>()));
        }
    }

    private static void AppendCostNeutralRow(List<BudgetReportCategoryDto> categories, BudgetReportEntry? costNeutralEntry)
    {
        if (costNeutralEntry != null && costNeutralEntry.ActualAmount != 0m)
        {
            categories.Add(new BudgetReportCategoryDto(
                Guid.Empty,
                "Unbudgeted (Self, cost-neutral)",
                BudgetReportCategoryRowKind.UnbudgetedSelfCostNeutral,
                0m,
                costNeutralEntry.ActualAmount,
                costNeutralEntry.ActualAmount,
                0m,
                Array.Empty<BudgetReportPurposeDto>()));
        }
    }

    private static void AppendSumRow(List<BudgetReportCategoryDto> categories, BudgetReportEntry? totalEntry)
    {
        if (categories.Count > 0 && totalEntry != null)
        {
            categories.Add(new BudgetReportCategoryDto(
                Guid.Empty,
                "Sum",
                BudgetReportCategoryRowKind.Sum,
                totalEntry.BudgetedAmount,
                totalEntry.ActualAmount,
                totalEntry.Deviation,
                ToDeltaPctFraction(totalEntry.BudgetedAmount, totalEntry.Deviation),
                Array.Empty<BudgetReportPurposeDto>()));
        }
    }

    // The Shared DTOs (BudgetReportPeriodDto/BudgetReportCategoryDto/BudgetReportPurposeDto) express the
    // deviation percentage as a fraction of the budgeted amount (e.g. 0.1 for 10%, formatted with "P0" by
    // the Web UI), whereas Budgetbericht.CalculateDeviation (BudgetReportEntry.DeviationPercentage /
    // BudgetReportCumulativeEntry.DeviationPercentage) expresses it as a percentage value (e.g. 10 for 10%).
    // Recomputing the fraction directly from BudgetedAmount/Deviation avoids relying on that x100 scaling.
    private static decimal ToDeltaPctFraction(decimal budgeted, decimal deviation)
        => budgeted == 0m ? 0m : deviation / Math.Abs(budgeted);

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
