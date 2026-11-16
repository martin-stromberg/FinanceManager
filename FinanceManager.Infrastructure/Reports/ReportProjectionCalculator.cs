namespace FinanceManager.Infrastructure.Reports;

/// <summary>
/// Parameter bundle for one projection run: interval semantics, the analysis anchor and the
/// display window of the calling aggregation path.
/// </summary>
/// <param name="Interval">Report interval used for period mapping.</param>
/// <param name="Analysis">Analysis month anchor (normalized to a month start).</param>
/// <param name="StartMonth">First displayed month of the report window.</param>
/// <param name="EndExclusive">Exclusive end of the current report window.</param>
/// <returns>Parameter bundle for one projection run.</returns>
internal sealed record ReportProjectionContext(ReportInterval Interval, DateTime Analysis, DateTime StartMonth, DateTime EndExclusive);

/// <summary>
/// A recurring posting event of one report entity used for projection pattern detection.
/// </summary>
/// <param name="Kind">Posting kind of the entity.</param>
/// <param name="EntityId">Id of the entity (account, contact, savings plan or security).</param>
/// <param name="Date">Effective event date.</param>
/// <param name="Month">Month bucket of the event date.</param>
/// <param name="Amount">Event amount (netted for dividend groups).</param>
/// <returns>Recurring posting event of one report entity.</returns>
internal sealed record ProjectionEvent(PostingKind Kind, Guid EntityId, DateTime Date, DateTime Month, decimal Amount);

/// <summary>
/// A prior-year event projected one year forward into the current report window.
/// </summary>
/// <param name="Kind">Posting kind of the entity.</param>
/// <param name="EntityId">Id of the entity.</param>
/// <param name="ExpectedDate">Expected date (prior year date + 1 year).</param>
/// <param name="PriorYearDate">Original prior-year event date.</param>
/// <param name="Amount">Expected amount.</param>
/// <returns>Prior-year event projected one year forward.</returns>
internal sealed record ProjectionCandidate(PostingKind Kind, Guid EntityId, DateTime ExpectedDate, DateTime PriorYearDate, decimal Amount);

/// <summary>
/// Recognized recurrence pattern of an entity's prior-year events.
/// </summary>
internal enum ProjectionPattern
{
    /// <summary>At least six regularly spaced monthly events in the reference year.</summary>
    Monthly,
    /// <summary>Two to five events in at least two quarters with 60-125 days spacing.</summary>
    Quarterly,
    /// <summary>Exactly one event in the reference year.</summary>
    Annual,
    /// <summary>Fallback for histories matching neither regular pattern.</summary>
    Irregular
}

/// <summary>
/// Kind-independent projection calculator shared by the standard aggregation path and the
/// netted security dividend path. Detects recurrence patterns per entity, projects prior-year
/// events into the current window, subtracts already confirmed events and assigns the
/// expected amounts plus details to leaf, category and type rows.
/// </summary>
internal static class ReportProjectionCalculator
{
    /// <summary>
    /// Applies projection amounts to the supplied report points in place.
    /// </summary>
    /// <param name="targetPoints">Report points to enrich (mutated).</param>
    /// <param name="events">Recurring posting events per entity, loaded for the reference window.</param>
    /// <param name="context">Interval and window context of the calling path.</param>
    /// <param name="nameResolver">Resolves the display name of an entity by kind and id.</param>
    /// <param name="hasActiveSecurity">
    /// Optional activity check applied to <see cref="PostingKind.Security"/> candidates only
    /// (the positive-holding rule); <c>null</c> admits every candidate.
    /// </param>
    /// <param name="securityDividendLeafOnly">
    /// When <c>true</c>, security expectations are only assigned to entity rows whose group key
    /// carries the <see cref="SecurityPostingSubType.Dividend"/> suffix (sub-type split rows of
    /// the standard path). When <c>false</c>, every <c>Security:{id}</c> entity row qualifies
    /// (netted dividend path, where plain keys denote net dividends).
    /// </param>
    internal static void ApplyProjectionAmounts(
        List<ReportAggregatePointDto> targetPoints,
        IReadOnlyList<ProjectionEvent> events,
        ReportProjectionContext context,
        Func<PostingKind, Guid, string> nameResolver,
        Func<Guid, DateTime, bool>? hasActiveSecurity = null,
        bool securityDividendLeafOnly = false)
    {
        if (targetPoints.Count == 0 || events.Count == 0)
        {
            return;
        }

        DateTime MapProjectionPeriod(DateTime date)
        {
            return context.Interval switch
            {
                ReportInterval.Month => new DateTime(date.Year, date.Month, 1),
                ReportInterval.Quarter => new DateTime(date.Year, ((date.Month - 1) / 3) * 3 + 1, 1),
                ReportInterval.HalfYear => new DateTime(date.Year, ((date.Month - 1) / 6) * 6 + 1, 1),
                ReportInterval.Year => new DateTime(date.Year, 1, 1),
                ReportInterval.Ytd => date.Month <= context.Analysis.Month ? new DateTime(date.Year, 1, 1) : DateTime.MinValue,
                _ => DateTime.MinValue
            };
        }

        var projectionStart = context.Interval == ReportInterval.Year
            ? targetPoints.Min(p => p.PeriodStart)
            : context.StartMonth;
        var projectionEnd = context.Interval == ReportInterval.Year
            ? targetPoints.Max(p => GetProjectionPeriodEnd(context.Interval, p.PeriodStart, context.EndExclusive))
            : context.EndExclusive;
        var currentYearStart = new DateTime(context.Analysis.Year, 1, 1);
        var expectedDetails = events
            .GroupBy(e => (e.Kind, e.EntityId))
            .SelectMany(g =>
            {
                var entityEvents = g.OrderBy(e => e.Date).ToList();
                var currentEvents = entityEvents
                    .Where(e => e.Date >= currentYearStart && e.Date < context.EndExclusive)
                    .OrderBy(e => e.Date)
                    .ToList();
                var pattern = DetectPattern(entityEvents, context.Analysis.Year - 1);
                var candidates = entityEvents
                    .Where(e => e.Date >= projectionStart.AddYears(-1) && e.Date < projectionEnd.AddYears(-1))
                    .OrderBy(e => e.Date)
                    .Select(e => new ProjectionCandidate(e.Kind, e.EntityId, e.Date.AddYears(1), e.Date, e.Amount))
                    .Where(e => MapProjectionPeriod(e.ExpectedDate) != DateTime.MinValue)
                    .Where(e => e.Kind != PostingKind.Security || hasActiveSecurity is null || hasActiveSecurity(e.EntityId, e.ExpectedDate))
                    .ToList();

                return FindExpectedItems(pattern, candidates, currentEvents, context.Analysis, g.Key.Kind)
                    .Select(x => new ReportProjectionExpectedItemDto(
                        x.Kind,
                        x.EntityId,
                        nameResolver(x.Kind, x.EntityId),
                        x.ExpectedDate,
                        x.PriorYearDate,
                        x.Amount));
            })
            .ToList();

        var expectedByEntityPeriod = expectedDetails
            .GroupBy(e => (e.Kind, e.EntityId, Period: MapProjectionPeriod(e.ExpectedDate)))
            .Select(g => new
            {
                g.Key,
                Expected = g.Sum(x => x.Amount),
                Details = (IReadOnlyList<ReportProjectionExpectedItemDto>)g
                    .OrderBy(x => x.ExpectedDate)
                    .ThenBy(x => x.EntityName)
                    .ToList()
            })
            .Where(e => e.Expected != 0m)
            .ToDictionary(e => e.Key, e => (e.Expected, e.Details));

        // Leaf assignment: entity rows only; security expectations are restricted to the
        // dividend sub-type rows when the calling path splits security points per sub type.
        for (var i = 0; i < targetPoints.Count; i++)
        {
            var point = targetPoints[i];
            if (!ReportQuerySemantics.TryParseGroupKey(point.GroupKey, out var target)
                || target.Kind != ReportGroupKeyTargetKind.Entity
                || target.PostingKind is not { } leafKind
                || target.EntityId is not { } entityId)
            {
                continue;
            }
            if (leafKind == PostingKind.Security && securityDividendLeafOnly && target.SecuritySubType != SecurityPostingSubType.Dividend)
            {
                continue;
            }

            var expected = expectedByEntityPeriod.TryGetValue((leafKind, entityId, point.PeriodStart), out var value) ? value.Expected : 0m;
            var details = expectedByEntityPeriod.TryGetValue((leafKind, entityId, point.PeriodStart), out var detailValue) && detailValue.Details.Count > 0
                ? detailValue.Details
                : null;
            targetPoints[i] = point with { ProjectionAmount = point.Amount + expected, ProjectionExpectedItems = details };
        }

        // Category roll-up: a category row carries the summed projection of its children,
        // independent of whether it is a top-level row (single kind) or sits under a type row.
        var childProjectionByParent = targetPoints
            .Where(p => p.ParentGroupKey != null && p.ProjectionAmount.HasValue)
            .GroupBy(p => (p.ParentGroupKey!, p.PeriodStart))
            .ToDictionary(g => g.Key, g => (decimal?)g.Sum(x => x.ProjectionAmount!.Value));
        var childDetailsByParent = targetPoints
            .Where(p => p.ParentGroupKey != null && p.ProjectionExpectedItems is { Count: > 0 })
            .GroupBy(p => (p.ParentGroupKey!, p.PeriodStart))
            .ToDictionary(g => g.Key, g => (IReadOnlyList<ReportProjectionExpectedItemDto>)g
                .SelectMany(x => x.ProjectionExpectedItems!)
                .OrderBy(x => x.ExpectedDate)
                .ThenBy(x => x.EntityName)
                .ToList());

        for (var i = 0; i < targetPoints.Count; i++)
        {
            var point = targetPoints[i];
            if (point.GroupKey.StartsWith("Category:", StringComparison.Ordinal)
                && childProjectionByParent.TryGetValue((point.GroupKey, point.PeriodStart), out var projection))
            {
                var details = childDetailsByParent.TryGetValue((point.GroupKey, point.PeriodStart), out var detailList)
                    ? detailList
                    : null;
                targetPoints[i] = point with { ProjectionAmount = projection, ProjectionExpectedItems = details };
            }
        }

        // Type roll-up: a "Type:{K}" row carries the projection of all rows of kind K that are
        // either parent-less (single-kind flat list, single-kind category tree) or direct
        // children of the type row (multi-kind leafs and category rows). Entity leafs under a
        // "Category:" row are only counted via the category roll-up, never twice.
        var typeChildren = targetPoints
            .Where(p => !p.GroupKey.StartsWith("Type:", StringComparison.Ordinal))
            .Select(p => new { Point = p, Kind = ReportQuerySemantics.ParseKindFromKey(p.GroupKey) })
            .Where(x => x.Kind.HasValue && (x.Point.ParentGroupKey == null || x.Point.ParentGroupKey == $"Type:{x.Kind.Value}"))
            .ToList();
        var typeProjectionByKey = typeChildren
            .Where(x => x.Point.ProjectionAmount.HasValue)
            .GroupBy(x => ($"Type:{x.Kind!.Value}", x.Point.PeriodStart))
            .ToDictionary(g => g.Key, g => (decimal?)g.Sum(x => x.Point.ProjectionAmount!.Value));
        var typeDetailsByKey = typeChildren
            .Where(x => x.Point.ProjectionExpectedItems is { Count: > 0 })
            .GroupBy(x => ($"Type:{x.Kind!.Value}", x.Point.PeriodStart))
            .ToDictionary(g => g.Key, g => (IReadOnlyList<ReportProjectionExpectedItemDto>)g
                .SelectMany(x => x.Point.ProjectionExpectedItems!)
                .OrderBy(x => x.ExpectedDate)
                .ThenBy(x => x.EntityName)
                .ToList());

        for (var i = 0; i < targetPoints.Count; i++)
        {
            var point = targetPoints[i];
            if (!point.GroupKey.StartsWith("Type:", StringComparison.Ordinal))
            {
                continue;
            }

            if (typeProjectionByKey.TryGetValue((point.GroupKey, point.PeriodStart), out var typeProjection))
            {
                var details = typeDetailsByKey.TryGetValue((point.GroupKey, point.PeriodStart), out var detailList)
                    ? detailList
                    : null;
                targetPoints[i] = point with { ProjectionAmount = typeProjection, ProjectionExpectedItems = details };
            }
        }
    }

    /// <summary>
    /// Returns the exclusive period end of a period anchor under the given interval.
    /// </summary>
    /// <param name="interval">Report interval.</param>
    /// <param name="periodStart">Period anchor.</param>
    /// <param name="endExclusive">Fallback end for year-to-date and unknown intervals.</param>
    /// <returns>Exclusive end of the period.</returns>
    internal static DateTime GetProjectionPeriodEnd(ReportInterval interval, DateTime periodStart, DateTime endExclusive)
    {
        return interval switch
        {
            ReportInterval.Month => periodStart.AddMonths(1),
            ReportInterval.Quarter => periodStart.AddMonths(3),
            ReportInterval.HalfYear => periodStart.AddMonths(6),
            ReportInterval.Year => periodStart.AddYears(1),
            _ => endExclusive
        };
    }

    /// <summary>
    /// Detects the recurrence pattern of an entity's events in the reference year.
    /// </summary>
    /// <param name="entityEvents">All loaded events of the entity, ordered by date.</param>
    /// <param name="referenceYear">Calendar year used as the pattern reference.</param>
    /// <returns>The detected <see cref="ProjectionPattern"/>.</returns>
    internal static ProjectionPattern DetectPattern(IReadOnlyList<ProjectionEvent> entityEvents, int referenceYear)
    {
        var referenceEvents = entityEvents
            .Where(e => e.Date.Year == referenceYear)
            .OrderBy(e => e.Date)
            .ToList();
        if (referenceEvents.Count == 1)
        {
            return ProjectionPattern.Annual;
        }

        var monthlyReferenceEvents = referenceEvents
            .GroupBy(e => e.Month)
            .Select(g => new ProjectionEvent(g.First().Kind, g.First().EntityId, g.Key, g.Key, g.Sum(e => e.Amount)))
            .Where(e => e.Amount != 0m)
            .OrderBy(e => e.Date)
            .ToList();
        if (monthlyReferenceEvents.Count >= 6 && HasRegularMonthlySpacing(monthlyReferenceEvents))
        {
            return ProjectionPattern.Monthly;
        }

        var distinctQuarters = referenceEvents.Select(e => (e.Date.Month - 1) / 3).Distinct().Count();
        if (referenceEvents.Count is >= 2 and <= 5 && distinctQuarters >= 2 && HasRegularSpacing(referenceEvents, 60, 125))
        {
            return ProjectionPattern.Quarterly;
        }

        return ProjectionPattern.Irregular;
    }

    private static bool HasRegularMonthlySpacing(IReadOnlyList<ProjectionEvent> orderedMonthlyEvents)
    {
        if (orderedMonthlyEvents.Count < 2)
        {
            return false;
        }

        for (var i = 1; i < orderedMonthlyEvents.Count; i++)
        {
            var months = (orderedMonthlyEvents[i].Date.Year - orderedMonthlyEvents[i - 1].Date.Year) * 12
                + orderedMonthlyEvents[i].Date.Month
                - orderedMonthlyEvents[i - 1].Date.Month;
            if (months != 1)
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasRegularSpacing(IReadOnlyList<ProjectionEvent> orderedEvents, int minimumDays, int maximumDays)
    {
        if (orderedEvents.Count < 2)
        {
            return false;
        }

        for (var i = 1; i < orderedEvents.Count; i++)
        {
            var days = (orderedEvents[i].Date - orderedEvents[i - 1].Date).TotalDays;
            if (days < minimumDays || days > maximumDays)
            {
                return false;
            }
        }

        return true;
    }

    private static IEnumerable<ProjectionCandidate> FindExpectedItems(
        ProjectionPattern pattern,
        IReadOnlyList<ProjectionCandidate> candidates,
        IReadOnlyList<ProjectionEvent> currentEvents,
        DateTime analysis,
        PostingKind kind)
    {
        if (candidates.Count == 0)
        {
            return Array.Empty<ProjectionCandidate>();
        }

        // Securities keep the adopted dividend semantics: a security typically pays once a year,
        // so any current-year event confirms the year's expectation. Other kinds can carry
        // several independent recurring series on one entity (e.g. interest plus rent on the
        // same account), so each candidate must be confirmed individually instead — an
        // unmatched candidate stays expected.
        var strictWholeYearConfirmation = kind == PostingKind.Security;

        return pattern switch
        {
            ProjectionPattern.Monthly => candidates.Where(candidate =>
                !HasCurrentMatch(currentEvents, current => current.Date.Month == candidate.ExpectedDate.Month) &&
                candidate.ExpectedDate.Month >= analysis.Month),
            ProjectionPattern.Quarterly => candidates.Where(candidate =>
                !HasCurrentMatch(currentEvents, current => GetQuarter(current.Date) == GetQuarter(candidate.ExpectedDate)) &&
                GetQuarter(candidate.ExpectedDate) >= GetQuarter(analysis)),
            ProjectionPattern.Annual => strictWholeYearConfirmation
                ? (currentEvents.Count == 0 ? candidates : Array.Empty<ProjectionCandidate>())
                : candidates.Where(candidate =>
                    !HasCurrentMatch(currentEvents, current => current.Date.Month == candidate.ExpectedDate.Month)),
            _ => strictWholeYearConfirmation && currentEvents.Count > 0
                ? Array.Empty<ProjectionCandidate>()
                : FindIrregularExpected(candidates, currentEvents)
        };
    }

    private static IEnumerable<ProjectionCandidate> FindIrregularExpected(
        IReadOnlyList<ProjectionCandidate> candidates,
        IReadOnlyList<ProjectionEvent> currentEvents)
    {
        var usedCurrentIndexes = new HashSet<int>();
        var unmatched = new List<ProjectionCandidate>();
        foreach (var candidate in candidates)
        {
            var matchIndex = -1;
            for (var i = 0; i < currentEvents.Count; i++)
            {
                if (usedCurrentIndexes.Contains(i))
                {
                    continue;
                }

                if (Math.Abs((currentEvents[i].Date - candidate.ExpectedDate).TotalDays) <= 30)
                {
                    matchIndex = i;
                    break;
                }
            }

            if (matchIndex >= 0)
            {
                usedCurrentIndexes.Add(matchIndex);
            }
            else
            {
                unmatched.Add(candidate);
            }
        }

        return unmatched;
    }

    private static bool HasCurrentMatch(IReadOnlyList<ProjectionEvent> currentEvents, Func<ProjectionEvent, bool> predicate)
    {
        return currentEvents.Any(predicate);
    }

    private static int GetQuarter(DateTime date)
    {
        return (date.Month - 1) / 3;
    }
}
