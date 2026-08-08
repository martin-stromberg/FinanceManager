using FinanceManager.Shared.Dtos.Budget;

namespace FinanceManager.Domain.Budget.ReportCalculation;

/// <summary>
/// Aggregate root that calculates a budget report for a given period. Drives the calculation through
/// five phases: Initialization (constructor), Planning (<see cref="SetPlanung"/>), Posting Assignment
/// (<see cref="AddPosting"/>), Finish (<see cref="Finish"/>) and Output (<see cref="GetCurrentResult"/>,
/// <see cref="GetCumulativeResult"/>).
/// </summary>
/// <remarks>
/// <see cref="SetPlanung"/> accepts the lightweight <c>FinanceManager.Shared.Dtos.Budget</c> DTOs
/// (<see cref="BudgetCategoryDto"/>, <see cref="BudgetPurposeDto"/>, <see cref="BudgetRuleDto"/>) rather
/// than the <c>FinanceManager.Domain.Budget</c> entities, because those DTOs already carry the stable,
/// persisted identifiers required to correlate the calculation result back to the user's real
/// categories/purposes, and because no repository access is introduced for this calculation.
/// A <see cref="BudgetRule"/> whose period spans multiple months (Quarterly/Yearly/CustomMonths) is
/// attributed, for display and summation purposes, to the month containing its period start only, so
/// that summing months never double-counts a budgeted amount; matching of actual postings against such
/// a rule is independent of this "home month" and instead uses the rule's own period window.
/// <para>
/// The class name <c>Budgetbericht</c> and the constructor parameter names (<c>betrachtungsDatum</c>,
/// <c>anzahlMonate</c>, <c>intervall</c>) are intentionally German: the class is explicitly named
/// <c>Budgetbericht</c> in the customer's original requirement (<c>issue.md</c>, Akzeptanzkriterien),
/// and <see cref="SetPlanung"/> plus these constructor parameter names are an explicit design decision
/// documented in the implementation plan (<c>plan.md</c>, "Designentscheidungen" / "Konstruktor-Parameter
/// für Budgetbericht"). This is a deliberate, reviewed deviation from the codebase's otherwise English
/// naming convention, not an oversight.
/// </para>
/// </remarks>
public sealed class Budgetbericht
{
    private const string UncategorizedCategoryName = "Uncategorized";

    private readonly DateOnly _periodStart;
    private readonly DateOnly _periodEnd;
    private readonly BudgetReportInterval _interval;
    private readonly BudgetReportDateBasis _dateBasis;

    private readonly List<MonthlyBudgetResult> _monthlyResults = new();
    private readonly Dictionary<DateOnly, MonthlyBudgetResult> _monthlyResultsByMonth = new();

    private readonly Dictionary<Guid, BudgetSource> _purposeSources = new();
    private readonly Dictionary<Guid, List<MonthlyBudgetExpectationPosting>> _purposeCandidatePostings = new();
    private readonly Dictionary<Guid, List<BudgetSource>> _categorySources = new();
    private readonly Dictionary<Guid, List<MonthlyBudgetExpectationPosting>> _categoryCandidatePostings = new();

    private bool _planningDone;
    private bool _finished;

    /// <summary>
    /// Initializes a new <see cref="Budgetbericht"/> for the given period, creating one empty
    /// <see cref="MonthlyBudgetResult"/> per month.
    /// </summary>
    /// <param name="betrachtungsDatum">Any date within the first month of the report period.</param>
    /// <param name="anzahlMonate">Number of months to include in the report period. Must be greater than zero.</param>
    /// <param name="intervall">Aggregation interval used by <see cref="GetCumulativeResult"/>.</param>
    /// <param name="dateBasis">Date basis used to determine which month an actual posting belongs to.</param>
    /// <exception cref="BudgetReportCalculationException">Thrown when <paramref name="anzahlMonate"/> is not greater than zero or <paramref name="betrachtungsDatum"/> is not a valid date.</exception>
    public Budgetbericht(DateOnly betrachtungsDatum, int anzahlMonate, BudgetReportInterval intervall, BudgetReportDateBasis dateBasis)
    {
        if (anzahlMonate <= 0)
        {
            throw new BudgetReportCalculationException("AnzahlMonate must be greater than zero.");
        }

        if (betrachtungsDatum == default)
        {
            throw new BudgetReportCalculationException("BetrachtungsDatum must be a valid date.");
        }

        _periodStart = new DateOnly(betrachtungsDatum.Year, betrachtungsDatum.Month, 1);
        _periodEnd = _periodStart.AddMonths(anzahlMonate).AddDays(-1);
        _interval = intervall;
        _dateBasis = dateBasis;

        for (var i = 0; i < anzahlMonate; i++)
        {
            var monthStart = _periodStart.AddMonths(i);
            var result = new MonthlyBudgetResult(monthStart.ToDateTime(TimeOnly.MinValue));
            _monthlyResults.Add(result);
            _monthlyResultsByMonth[monthStart] = result;
        }
    }

    /// <summary>
    /// Gets the calculation results for each month of the report period, in chronological order.
    /// </summary>
    public IReadOnlyList<MonthlyBudgetResult> MonthlyResults => _monthlyResults;

    /// <summary>
    /// Builds the budget expectations for the report period from the given categories, purposes and rules.
    /// Must be called exactly once, before any call to <see cref="AddPosting"/>.
    /// </summary>
    /// <param name="categories">The budget categories owned by the user.</param>
    /// <param name="purposes">The budget purposes owned by the user.</param>
    /// <param name="rules">All budget rules (purpose-level and category-level) owned by the user.</param>
    /// <exception cref="BudgetReportCalculationException">Thrown when planning has already been executed, or a rule has an invalid interval configuration.</exception>
    public void SetPlanung(IReadOnlyList<BudgetCategoryDto> categories, IReadOnlyList<BudgetPurposeDto> purposes, IReadOnlyList<BudgetRuleDto> rules)
    {
        ArgumentNullException.ThrowIfNull(categories);
        ArgumentNullException.ThrowIfNull(purposes);
        ArgumentNullException.ThrowIfNull(rules);

        if (_planningDone)
        {
            throw new BudgetReportCalculationException("SetPlanung has already been executed for this Budgetbericht.");
        }

        foreach (var rule in rules)
        {
            ValidateRule(rule);
        }

        BuildSourceIndexes(categories, purposes);

        var (purposeExpectationPostingsByHomeMonth, categoryExpectationPostingsByHomeMonth) =
            ExpandRulesToExpectationPostings(rules, purposes);

        BuildMonthlyExpectationGroups(categories, purposes, purposeExpectationPostingsByHomeMonth, categoryExpectationPostingsByHomeMonth);

        _planningDone = true;
    }

    /// <summary>
    /// Assigns a single actual posting to the matching budget expectation, or records it as unbudgeted
    /// (or cost-neutral, when it carries a mirror <c>GroupId</c>) when no expectation matches.
    /// </summary>
    /// <param name="posting">The posting to assign.</param>
    /// <param name="dateBasis">Date basis used to determine which month the posting belongs to.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="posting"/> is <c>null</c>.</exception>
    /// <exception cref="BudgetReportCalculationException">Thrown when <see cref="Finish"/> has already been called.</exception>
    public void AddPosting(MonthlyBudgetRealization posting, BudgetReportDateBasis dateBasis)
    {
        ArgumentNullException.ThrowIfNull(posting);

        if (_finished)
        {
            throw new BudgetReportCalculationException("Cannot add postings after Finish() has been called.");
        }

        var postingDate = GetPostingDate(posting, dateBasis);
        var postingMonth = new DateOnly(postingDate.Year, postingDate.Month, 1);

        if (!_monthlyResultsByMonth.TryGetValue(postingMonth, out var monthResult))
        {
            return;
        }

        var candidates = FindCandidateExpectationPostings(posting, postingDate);
        if (candidates.Count == 0)
        {
            RecordUnvaluedMatches(posting, postingDate);
            RouteUnmatchedPosting(monthResult, posting);
            return;
        }

        AssignSequentially(candidates, posting, monthResult);
    }

    /// <summary>
    /// Finalizes the calculation. For budget purposes with several competing occurrences (several
    /// <see cref="BudgetRule"/> entries, e.g. several total budgets for one purpose), re-assigns the
    /// already collected postings in posting-date order, so that priority is resolved chronologically
    /// rather than by the order <see cref="AddPosting"/> happened to be called in.
    /// </summary>
    /// <exception cref="BudgetReportCalculationException">Thrown when <see cref="Finish"/> has already been called.</exception>
    public void Finish()
    {
        if (_finished)
        {
            throw new BudgetReportCalculationException("Finish() has already been called for this Budgetbericht.");
        }

        foreach (var monthResult in _monthlyResults)
        {
            foreach (var group in monthResult.ExpectationGroups)
            {
                foreach (var expectation in group.DirectExpectations.Concat(group.Purposes))
                {
                    ReconcileMultiOccurrenceExpectation(monthResult, expectation);
                }
            }
        }

        _finished = true;
    }

    /// <summary>
    /// Returns the detail rows for the report. When <paramref name="month"/> is given, only that month
    /// is included; otherwise all months of the report period are aggregated into a single set of rows.
    /// </summary>
    /// <param name="month">Optional month to restrict the result to.</param>
    /// <returns>The report rows, including category, purpose, subtotal, unbudgeted, cost-neutral and total rows.</returns>
    public BudgetReportEntry[] GetCurrentResult(DateOnly? month = null)
    {
        var monthsToInclude = month.HasValue
            ? _monthlyResults.Where(m => DateOnly.FromDateTime(m.Month) == new DateOnly(month.Value.Year, month.Value.Month, 1)).ToList()
            : _monthlyResults;

        var allGroups = monthsToInclude.SelectMany(m => m.ExpectationGroups).ToList();
        var categoryIds = allGroups.Select(g => g.BudgetCategoryId).Distinct().ToList();
        var showCategoryRows = categoryIds.Count > 1 || categoryIds.Any(id => id != Guid.Empty);

        var entries = new List<BudgetReportEntry>();
        decimal totalBudgeted = 0m;
        decimal totalActual = 0m;

        foreach (var categoryId in categoryIds)
        {
            var groupsForCategory = allGroups.Where(g => g.BudgetCategoryId == categoryId).ToList();
            var categoryName = groupsForCategory[0].CategoryName;
            var directExpectations = groupsForCategory.SelectMany(g => g.DirectExpectations).ToList();
            var purposeExpectations = groupsForCategory.SelectMany(g => g.Purposes).ToList();

            var categoryBudgeted = directExpectations.Sum(e => e.SumExpectedAmount) + purposeExpectations.Sum(e => e.SumExpectedAmount);
            var categoryActual = directExpectations.Sum(e => e.SumActualAmount) + purposeExpectations.Sum(e => e.SumActualAmount);

            if (showCategoryRows)
            {
                entries.Add(CreateEntry(BudgetReportEntryRowKind.Category, categoryName, categoryBudgeted, categoryActual, Array.Empty<MonthlyBudgetRealization>()));
            }

            foreach (var direct in directExpectations)
            {
                entries.Add(CreateEntry(BudgetReportEntryRowKind.Purpose, direct.Name, direct.SumExpectedAmount, direct.SumActualAmount, CollectAssigned(direct)));
            }

            foreach (var purpose in purposeExpectations)
            {
                entries.Add(CreateEntry(BudgetReportEntryRowKind.Purpose, purpose.Name, purpose.SumExpectedAmount, purpose.SumActualAmount, CollectAssigned(purpose)));
            }

            entries.Add(CreateEntry(BudgetReportEntryRowKind.Subtotal, categoryName, categoryBudgeted, categoryActual, Array.Empty<MonthlyBudgetRealization>()));

            totalBudgeted += categoryBudgeted;
            totalActual += categoryActual;
        }

        var unbudgeted = monthsToInclude.SelectMany(m => m.UnbudgetedPostings).ToArray();
        var costNeutral = monthsToInclude.SelectMany(m => m.CostNeutralPostings).ToArray();
        var unbudgetedSum = unbudgeted.Sum(p => p.Amount);
        var costNeutralSum = costNeutral.Sum(p => p.Amount);

        entries.Add(CreateEntry(BudgetReportEntryRowKind.Unbudgeted, "Unbudgeted", 0m, unbudgetedSum, unbudgeted));
        entries.Add(CreateEntry(BudgetReportEntryRowKind.CostNeutral, "CostNeutral", 0m, costNeutralSum, costNeutral));

        totalActual += unbudgetedSum + costNeutralSum;
        entries.Add(CreateEntry(BudgetReportEntryRowKind.Total, "Total", totalBudgeted, totalActual, Array.Empty<MonthlyBudgetRealization>()));

        return entries.ToArray();
    }

    /// <summary>
    /// Returns the report aggregated per interval bucket (month, quarter or year, depending on the
    /// interval given to the constructor).
    /// </summary>
    /// <remarks>
    /// This method is part of the customer-mandated public API of <see cref="Budgetbericht"/> (see
    /// <c>issue.md</c>, Akzeptanzkriterien: "GetCummulativeResult liefert korrekte Intervall-Zusammenfassungen
    /// für Monat, Quartal und Jahr."). It is intentionally not called from <c>BudgetReportService</c> or
    /// <c>BudgetReportsController</c>: both already have their own, independently tested period/interval
    /// aggregation (<c>BudgetReportsController.GetAsync</c>'s per-period loop and
    /// <c>BudgetReportViewModel.BuildPeriodBoundaries</c>), and the plan for this refactor explicitly keeps
    /// those call sites unchanged ("Keine Änderungen" to <c>BudgetReportRawDataDto</c> and its consumers).
    /// Wiring this method into either of them would be a behavioral change outside the scope of this task.
    /// It is covered directly by unit tests instead.
    /// </remarks>
    /// <returns>One <see cref="BudgetReportCumulativeEntry"/> per interval bucket, in chronological order.</returns>
    public BudgetReportCumulativeEntry[] GetCumulativeResult()
    {
        var buckets = new List<(DateOnly Start, string Label, List<MonthlyBudgetResult> Months)>();

        foreach (var monthResult in _monthlyResults)
        {
            var monthStart = DateOnly.FromDateTime(monthResult.Month);
            var bucketStart = GetBucketStart(monthStart);

            var bucketIndex = buckets.FindIndex(b => b.Start == bucketStart);
            if (bucketIndex < 0)
            {
                buckets.Add((bucketStart, GetBucketLabel(bucketStart), new List<MonthlyBudgetResult>()));
                bucketIndex = buckets.Count - 1;
            }

            buckets[bucketIndex].Months.Add(monthResult);
        }

        var result = new List<BudgetReportCumulativeEntry>();
        foreach (var bucket in buckets.OrderBy(b => b.Start))
        {
            decimal budgeted = 0m;
            decimal actual = 0m;

            foreach (var monthResult in bucket.Months)
            {
                foreach (var expectation in monthResult.ExpectationGroups.SelectMany(g => g.DirectExpectations.Concat(g.Purposes)))
                {
                    budgeted += expectation.SumExpectedAmount;
                    actual += expectation.SumActualAmount;
                }

                actual += monthResult.UnbudgetedPostings.Sum(p => p.Amount);
                actual += monthResult.CostNeutralPostings.Sum(p => p.Amount);
            }

            var (deviation, deviationPct) = CalculateDeviation(budgeted, actual);

            result.Add(new BudgetReportCumulativeEntry
            {
                IntervalStartDate = bucket.Start,
                IntervalLabel = bucket.Label,
                BudgetedAmount = budgeted,
                ActualAmount = actual,
                Deviation = deviation,
                DeviationPercentage = deviationPct
            });
        }

        return result.ToArray();
    }

    private DateOnly GetBucketStart(DateOnly monthStart) => _interval switch
    {
        BudgetReportInterval.Quarter => new DateOnly(monthStart.Year, ((monthStart.Month - 1) / 3 * 3) + 1, 1),
        BudgetReportInterval.Year => new DateOnly(monthStart.Year, 1, 1),
        _ => monthStart
    };

    private string GetBucketLabel(DateOnly bucketStart) => _interval switch
    {
        BudgetReportInterval.Quarter => $"Q{((bucketStart.Month - 1) / 3) + 1}/{bucketStart.Year}",
        BudgetReportInterval.Year => bucketStart.Year.ToString(),
        _ => $"{bucketStart.Month:D2}/{bucketStart.Year}"
    };

    // Populates _purposeSources/_categorySources (which Contact/ContactGroup/SavingsPlan a purpose or
    // category matches against) and initializes the corresponding candidate-posting lists.
    private void BuildSourceIndexes(IReadOnlyList<BudgetCategoryDto> categories, IReadOnlyList<BudgetPurposeDto> purposes)
    {
        foreach (var purpose in purposes)
        {
            _purposeSources[purpose.Id] = new BudgetSource(purpose.SourceType, purpose.SourceId);
            _purposeCandidatePostings[purpose.Id] = new List<MonthlyBudgetExpectationPosting>();
        }

        foreach (var category in categories)
        {
            var sources = purposes
                .Where(p => p.BudgetCategoryId == category.Id)
                .Select(p => new BudgetSource(p.SourceType, p.SourceId))
                .Distinct()
                .ToList();
            _categorySources[category.Id] = sources;
            _categoryCandidatePostings[category.Id] = new List<MonthlyBudgetExpectationPosting>();
        }
    }

    // Expands every rule's occurrences within the report period into MonthlyBudgetExpectationPosting
    // instances, registers them as candidates for posting assignment, and groups them by "home month"
    // (the month containing each occurrence's period start) for later use when building the monthly
    // expectation groups.
    private (
        Dictionary<(Guid PurposeId, DateOnly Month), List<MonthlyBudgetExpectationPosting>> PurposeExpectationPostingsByHomeMonth,
        Dictionary<(Guid CategoryId, DateOnly Month), List<MonthlyBudgetExpectationPosting>> CategoryExpectationPostingsByHomeMonth
        ) ExpandRulesToExpectationPostings(IReadOnlyList<BudgetRuleDto> rules, IReadOnlyList<BudgetPurposeDto> purposes)
    {
        var purposeExpectationPostingsByHomeMonth = new Dictionary<(Guid PurposeId, DateOnly Month), List<MonthlyBudgetExpectationPosting>>();
        var categoryExpectationPostingsByHomeMonth = new Dictionary<(Guid CategoryId, DateOnly Month), List<MonthlyBudgetExpectationPosting>>();

        var creationOrder = 0;
        foreach (var rule in rules.OrderBy(r => r.StartDate))
        {
            var valuationType = ResolveValuationType(rule, purposes);

            foreach (var (periodStart, periodEnd) in ExpandRuleOccurrences(rule, _periodStart, _periodEnd))
            {
                var homeMonth = new DateOnly(periodStart.Year, periodStart.Month, 1);
                if (!_monthlyResultsByMonth.ContainsKey(homeMonth))
                {
                    continue;
                }

                var expectationPosting = new MonthlyBudgetExpectationPosting(
                    rule.Amount,
                    valuationType,
                    rule.StartDate,
                    creationOrder++,
                    new RuleOccurrencePeriod(periodStart, periodEnd),
                    new PurposeMatchPattern(rule.PurposePattern, rule.UseRegex));

                if (rule.BudgetPurposeId.HasValue)
                {
                    AddToHomeMonth(purposeExpectationPostingsByHomeMonth, (rule.BudgetPurposeId.Value, homeMonth), expectationPosting);
                    if (_purposeCandidatePostings.TryGetValue(rule.BudgetPurposeId.Value, out var purposeCandidates))
                    {
                        purposeCandidates.Add(expectationPosting);
                    }
                }
                else if (rule.BudgetCategoryId.HasValue)
                {
                    AddToHomeMonth(categoryExpectationPostingsByHomeMonth, (rule.BudgetCategoryId.Value, homeMonth), expectationPosting);
                    if (_categoryCandidatePostings.TryGetValue(rule.BudgetCategoryId.Value, out var categoryCandidates))
                    {
                        categoryCandidates.Add(expectationPosting);
                    }
                }
            }
        }

        return (purposeExpectationPostingsByHomeMonth, categoryExpectationPostingsByHomeMonth);
    }

    private static BudgetValuationType ResolveValuationType(BudgetRuleDto rule, IReadOnlyList<BudgetPurposeDto> purposes)
    {
        if (!rule.BudgetPurposeId.HasValue)
        {
            return BudgetValuationType.ExactPostings;
        }

        var purpose = purposes.FirstOrDefault(p => p.Id == rule.BudgetPurposeId.Value);
        return purpose?.ValuationType ?? BudgetValuationType.ExactPostings;
    }

    // Builds the per-month MonthlyBudgetExpectationGroup tree (one group per real category, plus the
    // virtual "Uncategorized" group) from the expectation postings produced by ExpandRulesToExpectationPostings.
    private void BuildMonthlyExpectationGroups(
        IReadOnlyList<BudgetCategoryDto> categories,
        IReadOnlyList<BudgetPurposeDto> purposes,
        Dictionary<(Guid PurposeId, DateOnly Month), List<MonthlyBudgetExpectationPosting>> purposeExpectationPostingsByHomeMonth,
        Dictionary<(Guid CategoryId, DateOnly Month), List<MonthlyBudgetExpectationPosting>> categoryExpectationPostingsByHomeMonth)
    {
        var uncategorizedPurposes = purposes.Where(p => !p.BudgetCategoryId.HasValue).OrderBy(p => p.Name).ToList();

        foreach (var monthStart in _monthlyResultsByMonth.Keys.OrderBy(m => m))
        {
            var monthResult = _monthlyResultsByMonth[monthStart];
            var groupsByCategory = new List<MonthlyBudgetExpectationGroup>();

            foreach (var category in categories.OrderBy(c => c.Name))
            {
                var group = new MonthlyBudgetExpectationGroup(category.Id, category.Name);

                if (categoryExpectationPostingsByHomeMonth.TryGetValue((category.Id, monthStart), out var directPostings))
                {
                    var directExpectation = new MonthlyBudgetExpectation(null, category.Name);
                    foreach (var directPosting in directPostings)
                    {
                        directExpectation.AddPosting(directPosting);
                    }

                    group.AddDirectExpectation(directExpectation);
                }

                foreach (var purpose in purposes.Where(p => p.BudgetCategoryId == category.Id).OrderBy(p => p.Name))
                {
                    group.AddPurposeExpectation(BuildPurposeExpectation(purpose, monthStart, purposeExpectationPostingsByHomeMonth));
                }

                groupsByCategory.Add(group);
            }

            if (uncategorizedPurposes.Count > 0)
            {
                var uncategorizedGroup = new MonthlyBudgetExpectationGroup(Guid.Empty, UncategorizedCategoryName);
                foreach (var purpose in uncategorizedPurposes)
                {
                    uncategorizedGroup.AddPurposeExpectation(BuildPurposeExpectation(purpose, monthStart, purposeExpectationPostingsByHomeMonth));
                }

                groupsByCategory.Add(uncategorizedGroup);
            }

            foreach (var group in groupsByCategory)
            {
                monthResult.AddExpectationGroup(group);
            }
        }
    }

    private MonthlyBudgetExpectation BuildPurposeExpectation(
        BudgetPurposeDto purpose,
        DateOnly monthStart,
        Dictionary<(Guid PurposeId, DateOnly Month), List<MonthlyBudgetExpectationPosting>> purposeExpectationPostingsByHomeMonth)
    {
        var expectation = new MonthlyBudgetExpectation(purpose.Id, purpose.Name);
        if (purposeExpectationPostingsByHomeMonth.TryGetValue((purpose.Id, monthStart), out var postings))
        {
            foreach (var posting in postings)
            {
                expectation.AddPosting(posting);
            }
        }

        return expectation;
    }

    private static void AddToHomeMonth<TKey>(Dictionary<TKey, List<MonthlyBudgetExpectationPosting>> map, TKey key, MonthlyBudgetExpectationPosting posting) where TKey : notnull
    {
        if (!map.TryGetValue(key, out var list))
        {
            list = new List<MonthlyBudgetExpectationPosting>();
            map[key] = list;
        }

        list.Add(posting);
    }

    private void AssignSequentially(List<MonthlyBudgetExpectationPosting> candidates, MonthlyBudgetRealization posting, MonthlyBudgetResult monthResult)
    {
        var remaining = posting;
        foreach (var candidate in candidates)
        {
            if (remaining.Amount == 0m)
            {
                break;
            }

            var leftover = candidate.Assign(remaining);
            remaining = remaining with { Amount = leftover };
        }

        if (remaining.Amount != 0m)
        {
            monthResult.AddUnbudgetedPosting(remaining);
        }
    }

    private void ReconcileMultiOccurrenceExpectation(MonthlyBudgetResult monthResult, MonthlyBudgetExpectation expectation)
    {
        if (expectation.Postings.Count <= 1)
        {
            return;
        }

        var allAssigned = expectation.Postings.SelectMany(p => p.AssignedPostings).ToList();
        if (allAssigned.Count == 0)
        {
            return;
        }

        foreach (var occurrence in expectation.Postings)
        {
            occurrence.Reset();
        }

        var orderedOccurrences = expectation.Postings
            .OrderBy(p => p.StartDate)
            .ThenBy(p => p.CreationOrder)
            .ToList();

        var sortedAssigned = allAssigned
            .OrderBy(a => GetPostingDate(a, _dateBasis))
            .ToList();

        foreach (var assigned in sortedAssigned)
        {
            AssignSequentially(orderedOccurrences, assigned, monthResult);
        }
    }

    private List<MonthlyBudgetExpectationPosting> FindCandidateExpectationPostings(MonthlyBudgetRealization posting, DateOnly postingDate)
    {
        var candidates = new List<MonthlyBudgetExpectationPosting>();

        foreach (var (purposeId, source) in _purposeSources)
        {
            if (!MatchesSource(source, posting))
            {
                continue;
            }

            foreach (var candidate in _purposeCandidatePostings[purposeId])
            {
                if (IsEligible(candidate, posting, postingDate))
                {
                    candidates.Add(candidate);
                }
            }
        }

        foreach (var (categoryId, sources) in _categorySources)
        {
            if (!sources.Any(s => MatchesSource(s, posting)))
            {
                continue;
            }

            foreach (var candidate in _categoryCandidatePostings[categoryId])
            {
                if (IsEligible(candidate, posting, postingDate))
                {
                    candidates.Add(candidate);
                }
            }
        }

        return candidates
            .OrderBy(c => c.StartDate)
            .ThenBy(c => c.CreationOrder)
            .ToList();
    }

    // Records, for every purpose whose source matches the posting, any ExactPostings-valued expectation
    // occurrence whose period and purpose pattern match the (otherwise fully unmatched) posting but whose
    // sign does not — so it can still be shown against the purpose (with IsValuedForBudgetPurpose = false
    // at the DTO layer) even though it is not counted toward the purpose's actual amount.
    private void RecordUnvaluedMatches(MonthlyBudgetRealization posting, DateOnly postingDate)
    {
        foreach (var (purposeId, source) in _purposeSources)
        {
            if (!MatchesSource(source, posting))
            {
                continue;
            }

            if (!_purposeCandidatePostings.TryGetValue(purposeId, out var candidates))
            {
                continue;
            }

            foreach (var candidate in candidates)
            {
                if (candidate.BudgetType != BudgetValuationType.ExactPostings)
                {
                    continue;
                }

                if (postingDate < candidate.PeriodStart || postingDate > candidate.PeriodEnd)
                {
                    continue;
                }

                if (!BudgetRulePatternMatcher.MatchesPosting(posting.Purpose, posting.Description, candidate.PurposePattern, candidate.PurposePatternIsRegex))
                {
                    continue;
                }

                candidate.AddUnvaluedMatch(posting);
            }
        }
    }

    private static bool IsEligible(MonthlyBudgetExpectationPosting candidate, MonthlyBudgetRealization posting, DateOnly postingDate)
    {
        if (postingDate < candidate.PeriodStart || postingDate > candidate.PeriodEnd)
        {
            return false;
        }

        if (!BudgetRulePatternMatcher.MatchesPosting(posting.Purpose, posting.Description, candidate.PurposePattern, candidate.PurposePatternIsRegex))
        {
            return false;
        }

        if (candidate.BudgetType == BudgetValuationType.ExactPostings && Math.Sign(posting.Amount) != Math.Sign(candidate.Amount))
        {
            return false;
        }

        return true;
    }

    private static bool MatchesSource(BudgetSource source, MonthlyBudgetRealization posting) => source.SourceType switch
    {
        BudgetSourceType.Contact => posting.ContactId.HasValue && posting.ContactId.Value == source.SourceId,
        BudgetSourceType.ContactGroup => posting.ContactGroupId.HasValue && posting.ContactGroupId.Value == source.SourceId,
        BudgetSourceType.SavingsPlan => posting.SavingsPlanId.HasValue && posting.SavingsPlanId.Value == source.SourceId,
        _ => false
    };

    private static void RouteUnmatchedPosting(MonthlyBudgetResult monthResult, MonthlyBudgetRealization posting)
    {
        if (posting.GroupId.HasValue)
        {
            monthResult.AddCostNeutralPosting(posting);
        }
        else
        {
            monthResult.AddUnbudgetedPosting(posting);
        }
    }

    private static DateOnly GetPostingDate(MonthlyBudgetRealization posting, BudgetReportDateBasis dateBasis)
    {
        if (dateBasis == BudgetReportDateBasis.ValutaDate && posting.ValutaDate.HasValue)
        {
            return DateOnly.FromDateTime(posting.ValutaDate.Value);
        }

        return DateOnly.FromDateTime(posting.BookingDate);
    }

    private static IEnumerable<(DateOnly PeriodStart, DateOnly PeriodEnd)> ExpandRuleOccurrences(BudgetRuleDto rule, DateOnly from, DateOnly to)
    {
        var stepMonths = rule.Interval switch
        {
            BudgetIntervalType.Monthly => 1,
            BudgetIntervalType.Quarterly => 3,
            BudgetIntervalType.Yearly => 12,
            BudgetIntervalType.CustomMonths => Math.Max(1, rule.CustomIntervalMonths ?? 1),
            _ => 1
        };

        var ruleEnd = rule.EndDate ?? to;

        if (rule.Interval == BudgetIntervalType.Yearly)
        {
            var normalizedStart = new DateOnly(rule.StartDate.Year, rule.StartDate.Month, 1);
            var normalizedFrom = new DateOnly(from.Year, from.Month, 1);
            var normalizedTo = new DateOnly(to.Year, to.Month, 1);
            var normalizedRuleEnd = new DateOnly(ruleEnd.Year, ruleEnd.Month, 1);

            while (normalizedStart < normalizedFrom)
            {
                normalizedStart = normalizedStart.AddMonths(stepMonths);
                if (normalizedStart > normalizedRuleEnd)
                {
                    yield break;
                }
            }

            while (normalizedStart <= normalizedTo && normalizedStart <= normalizedRuleEnd)
            {
                var next = normalizedStart.AddMonths(stepMonths);
                var periodEnd = next.AddDays(-1);
                if (periodEnd > ruleEnd)
                {
                    periodEnd = ruleEnd;
                }

                if (periodEnd > to)
                {
                    periodEnd = to;
                }

                yield return (normalizedStart, periodEnd);
                normalizedStart = next;
            }

            yield break;
        }

        var current = rule.StartDate;
        while (current < from)
        {
            current = current.AddMonths(stepMonths);
            if (current > ruleEnd)
            {
                yield break;
            }
        }

        while (current <= to && current <= ruleEnd)
        {
            var next = current.AddMonths(stepMonths);
            var periodEnd = next.AddDays(-1);
            if (periodEnd > ruleEnd)
            {
                periodEnd = ruleEnd;
            }

            if (periodEnd > to)
            {
                periodEnd = to;
            }

            yield return (current, periodEnd);
            current = next;
        }
    }

    private static void ValidateRule(BudgetRuleDto rule)
    {
        if (!Enum.IsDefined(rule.Interval))
        {
            throw new BudgetReportCalculationException($"Budget rule {rule.Id} has an invalid interval.");
        }

        if (rule.Interval == BudgetIntervalType.CustomMonths && !rule.CustomIntervalMonths.HasValue)
        {
            throw new BudgetReportCalculationException($"Budget rule {rule.Id} uses a custom interval but has no CustomIntervalMonths configured.");
        }
    }

    private static BudgetReportEntry CreateEntry(BudgetReportEntryRowKind kind, string name, decimal budgeted, decimal actual, IReadOnlyList<MonthlyBudgetRealization> postings)
    {
        var (deviation, deviationPct) = CalculateDeviation(budgeted, actual);

        return new BudgetReportEntry
        {
            RowKind = kind,
            Name = name,
            BudgetedAmount = budgeted,
            ActualAmount = actual,
            Deviation = deviation,
            DeviationPercentage = deviationPct,
            Postings = postings.ToArray()
        };
    }

    // Shared deviation calculation used by both GetCurrentResult (via CreateEntry) and GetCumulativeResult.
    private static (decimal Deviation, decimal DeviationPercentage) CalculateDeviation(decimal budgeted, decimal actual)
    {
        var deviation = actual - budgeted;
        var deviationPct = budgeted != 0m ? deviation / Math.Abs(budgeted) * 100m : 0m;
        return (deviation, deviationPct);
    }

    private static IReadOnlyList<MonthlyBudgetRealization> CollectAssigned(MonthlyBudgetExpectation expectation) =>
        expectation.Postings.SelectMany(p => p.AssignedPostings).ToArray();

    // Small value type replacing the previous (BudgetSourceType, Guid) tuple used to identify what a
    // budget purpose or category matches actual postings against (a Contact, ContactGroup or SavingsPlan).
    private readonly record struct BudgetSource(BudgetSourceType SourceType, Guid SourceId);
}
