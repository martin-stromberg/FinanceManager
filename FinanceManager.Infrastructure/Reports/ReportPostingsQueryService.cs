using FinanceManager.Application.Reports;
using FinanceManager.Domain.Postings;
using FinanceManager.Shared.Dtos.Budget;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Reports;

/// <summary>
/// Lists the individual postings behind a single aggregated report cell. Reproduces the
/// selection semantics of <see cref="ReportAggregationService"/> on raw <see cref="Posting"/>
/// rows: ownership via the referenced entities, entity/category/sub-type/text filters,
/// comparison-period windows, the linked-account savings plan source and the netted
/// security dividend path.
/// </summary>
public sealed class ReportPostingsQueryService : IReportPostingsQueryService
{
    private const int MaxPostings = 5000;

    private static readonly SecurityPostingSubType[] NettingSubTypes =
    {
        SecurityPostingSubType.Dividend,
        SecurityPostingSubType.Fee,
        SecurityPostingSubType.Tax
    };

    private readonly AppDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportPostingsQueryService"/> class.
    /// </summary>
    /// <param name="db">The application's <see cref="AppDbContext"/> used to query postings and entities.</param>
    public ReportPostingsQueryService(AppDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<ReportPostingsResult> QueryAsync(ReportPostingsQuery query, CancellationToken ct)
    {
        if (!Enum.IsDefined(query.Period))
        {
            throw new ArgumentOutOfRangeException(nameof(query), query.Period, "Unknown report postings period.");
        }
        if (!Enum.IsDefined(query.Interval))
        {
            throw new ArgumentOutOfRangeException(nameof(query), query.Interval, "Unknown report interval.");
        }
        if (!Enum.IsDefined(query.PostingKind))
        {
            throw new ArgumentOutOfRangeException(nameof(query), query.PostingKind, "Unknown posting kind.");
        }
        if (query.PostingKinds is { } multi && multi.Any(k => !Enum.IsDefined(k)))
        {
            throw new ArgumentOutOfRangeException(nameof(query), nameof(query.PostingKinds), "Unknown posting kind in multi selection.");
        }
        if (!Enum.IsDefined(query.SavingsPlanPostingSource))
        {
            throw new ArgumentOutOfRangeException(nameof(query), query.SavingsPlanPostingSource, "Unknown savings plan posting source.");
        }
        if (query.Take < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(query), query.Take, "Take must not be negative.");
        }
        if (!ReportQuerySemantics.TryParseGroupKey(query.GroupKey, out var target))
        {
            throw new ArgumentException($"Invalid report group key '{query.GroupKey}'.", nameof(query));
        }

        var kinds = (query.PostingKinds is { Count: > 0 }
            ? query.PostingKinds.Distinct().ToArray()
            : new[] { query.PostingKind });

        var analysis = (query.AnalysisDate?.Date) ?? DateTime.UtcNow.Date;
        analysis = new DateTime(analysis.Year, analysis.Month, 1);

        var (windowStart, windowEnd) = ReportQuerySemantics.ComputeCellWindow(query.Interval, query.Period, query.PeriodStart, analysis);

        var f = query.Filters;
        var includeDividendRelated = f?.IncludeDividendRelated == true;
        var onlySecurity = kinds.Length == 1 && kinds[0] == PostingKind.Security;
        var dividendSelected = f?.SecuritySubTypes?.Contains((int)SecurityPostingSubType.Dividend) == true;
        var hasContradictingSubTypes = f?.SecuritySubTypes is { Count: > 0 } && !dividendSelected;
        var compareProjection = query.CompareProjection && onlySecurity && query.Interval != ReportInterval.AllHistory && !hasContradictingSubTypes;
        var netPath = onlySecurity && (includeDividendRelated || compareProjection || (query.Interval == ReportInterval.Ytd && dividendSelected));

        var targetKinds = TargetKinds(target, kinds);
        var linkedMode = query.SavingsPlanPostingSource == SavingsPlanPostingSource.LinkedAccountPostings && targetKinds.Contains(PostingKind.SavingsPlan);

        var owned = new Dictionary<PostingKind, Dictionary<Guid, OwnedEntity>>();
        foreach (var kind in targetKinds)
        {
            owned[kind] = await LoadOwnedAsync(kind, query.OwnerUserId, ct);
        }
        if (linkedMode && !owned.ContainsKey(PostingKind.Bank))
        {
            owned[PostingKind.Bank] = await LoadOwnedAsync(PostingKind.Bank, query.OwnerUserId, ct);
        }

        var filters = new FilterContext(f, query.IncludeCategory);

        var selections = new List<PostingSelection>();
        if (netPath)
        {
            if (owned.TryGetValue(PostingKind.Security, out var ownedSecurities))
            {
                selections.Add(await QueryNetDividendPostingsAsync(query, target, ownedSecurities, filters, windowStart, windowEnd, analysis, compareProjection, ct));
            }
        }
        else
        {
            foreach (var kind in targetKinds)
            {
                var entityIds = SelectEntityIds(kind, target, owned[kind], filters);
                if (kind == PostingKind.SavingsPlan && linkedMode)
                {
                    selections.Add(await LoadLinkedSavingsPlanPostingsAsync(query, entityIds, owned[PostingKind.Bank], filters.SavingsPlanTextPatterns, windowStart, windowEnd, ct));
                }
                else
                {
                    var requiredSubType = target.Kind == ReportGroupKeyTargetKind.Entity && target.PostingKind == PostingKind.Security;
                    selections.Add(await LoadPostingsAsync(query, kind, entityIds, filters.TextPatternsFor(kind), windowStart, windowEnd, requiredSubType ? target.SecuritySubType : null, requiredSubType, ct));
                }
            }
        }

        var totalCount = selections.Sum(s => s.TotalCount);
        var totalAmount = selections.Sum(s => s.TotalAmount);
        var page = selections
            .SelectMany(s => s.Items)
            .OrderByDescending(p => EffectiveDate(p, query.UseValutaDate))
            .ThenBy(p => p.Id)
            .Take(MaxPostings)
            .Select(p => Map(p, owned))
            .ToList();

        return new ReportPostingsResult(page, totalCount, totalAmount, totalCount > MaxPostings);
    }

    private static DateTime EffectiveDate(Posting p, bool useValutaDate)
        => useValutaDate && p.ValutaDate != DateTime.MinValue ? p.ValutaDate : p.BookingDate;

    private static IReadOnlyCollection<PostingKind> TargetKinds(ReportGroupKeyTarget target, IReadOnlyCollection<PostingKind> kinds)
        => target.Kind switch
        {
            ReportGroupKeyTargetKind.Total => kinds.ToArray(),
            _ => target.PostingKind.HasValue && kinds.Contains(target.PostingKind.Value)
                ? new[] { target.PostingKind.Value }
                : Array.Empty<PostingKind>()
        };

    private async Task<Dictionary<Guid, OwnedEntity>> LoadOwnedAsync(PostingKind kind, Guid ownerUserId, CancellationToken ct)
    {
        switch (kind)
        {
            case PostingKind.Bank:
                return (await _db.Accounts.AsNoTracking()
                        .Where(a => a.OwnerUserId == ownerUserId)
                        .Select(a => new { a.Id, a.Name })
                        .ToListAsync(ct))
                    .ToDictionary(x => x.Id, x => new OwnedEntity(x.Id, x.Name, null));
            case PostingKind.Contact:
                return (await _db.Contacts.AsNoTracking()
                        .Where(c => c.OwnerUserId == ownerUserId)
                        .Select(c => new { c.Id, c.Name, c.CategoryId })
                        .ToListAsync(ct))
                    .ToDictionary(x => x.Id, x => new OwnedEntity(x.Id, x.Name, x.CategoryId));
            case PostingKind.SavingsPlan:
                return (await _db.SavingsPlans.AsNoTracking()
                        .Where(s => s.OwnerUserId == ownerUserId)
                        .Select(s => new { s.Id, s.Name, s.CategoryId })
                        .ToListAsync(ct))
                    .ToDictionary(x => x.Id, x => new OwnedEntity(x.Id, x.Name, x.CategoryId));
            case PostingKind.Security:
                return (await _db.Securities.AsNoTracking()
                        .Where(s => s.OwnerUserId == ownerUserId)
                        .Select(s => new { s.Id, s.Name, s.CategoryId })
                        .ToListAsync(ct))
                    .ToDictionary(x => x.Id, x => new OwnedEntity(x.Id, x.Name, x.CategoryId));
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported posting kind.");
        }
    }

    private static HashSet<Guid> SelectEntityIds(PostingKind kind, ReportGroupKeyTarget target, Dictionary<Guid, OwnedEntity> owned, FilterContext filters)
    {
        IEnumerable<OwnedEntity> candidates = target.Kind switch
        {
            ReportGroupKeyTargetKind.Entity => target.PostingKind == kind && target.EntityId.HasValue && owned.TryGetValue(target.EntityId.Value, out var e)
                ? new[] { e }
                : Array.Empty<OwnedEntity>(),
            ReportGroupKeyTargetKind.Category => target.PostingKind == kind
                ? owned.Values.Where(e => e.CategoryId == target.CategoryId)
                : Array.Empty<OwnedEntity>(),
            _ => target.PostingKind == kind || target.Kind == ReportGroupKeyTargetKind.Total
                ? owned.Values
                : Array.Empty<OwnedEntity>()
        };
        return candidates.Where(e => filters.EntityAllowed(kind, e)).Select(e => e.Id).ToHashSet();
    }

    private async Task<PostingSelection> LoadLinkedSavingsPlanPostingsAsync(ReportPostingsQuery query, HashSet<Guid> planIds, Dictionary<Guid, OwnedEntity> ownedAccounts, IReadOnlyCollection<string>? savingsPlanTextPatterns, DateTime? windowStart, DateTime? windowEnd, CancellationToken ct)
    {
        if (planIds.Count == 0)
        {
            return PostingSelection.CreateEmpty();
        }

        var links = await _db.SavingsPlanAccountLinks.AsNoTracking()
            .Where(l => l.OwnerUserId == query.OwnerUserId && planIds.Contains(l.SavingsPlanId))
            .Select(l => new { l.SavingsPlanId, l.AccountId })
            .ToListAsync(ct);
        var linksByPlan = links
            .GroupBy(l => l.SavingsPlanId)
            .ToDictionary(g => g.Key, g => g.Select(l => l.AccountId).Distinct().ToList());

        var selections = new List<PostingSelection>(2);
        var linkedAccountIds = linksByPlan.Values.SelectMany(x => x).Where(ownedAccounts.ContainsKey).Distinct().ToList();
        if (linkedAccountIds.Count > 0)
        {
            // Savings plan text filters apply to the source bank postings of the linked accounts.
            selections.Add(await LoadPostingsAsync(query, PostingKind.Bank, linkedAccountIds, savingsPlanTextPatterns, windowStart, windowEnd, null, false, ct));
        }

        var unlinkedPlanIds = planIds.Where(id => !linksByPlan.ContainsKey(id)).ToList();
        if (unlinkedPlanIds.Count > 0)
        {
            selections.Add(await LoadPostingsAsync(query, PostingKind.SavingsPlan, unlinkedPlanIds, savingsPlanTextPatterns, windowStart, windowEnd, null, false, ct));
        }
        return PostingSelection.Combine(selections);
    }

    private async Task<PostingSelection> LoadPostingsAsync(ReportPostingsQuery query, PostingKind kind, IReadOnlyCollection<Guid> entityIds, IReadOnlyCollection<string>? textPatterns, DateTime? windowStart, DateTime? windowEnd, SecurityPostingSubType? requiredSubType, bool hasSubTypeConstraint, CancellationToken ct)
    {
        if (entityIds.Count == 0)
        {
            return PostingSelection.CreateEmpty();
        }

        IQueryable<Posting> q = _db.Postings.AsNoTracking().Where(p => p.Kind == kind && p.Amount != 0m);
        q = kind switch
        {
            PostingKind.Bank => q.Where(p => p.AccountId != null && entityIds.Contains(p.AccountId.Value)),
            PostingKind.Contact => q.Where(p => p.ContactId != null && entityIds.Contains(p.ContactId.Value)),
            PostingKind.SavingsPlan => q.Where(p => p.SavingsPlanId != null && entityIds.Contains(p.SavingsPlanId.Value)),
            PostingKind.Security => q.Where(p => p.SecurityId != null && entityIds.Contains(p.SecurityId.Value)),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported posting kind.")
        };
        q = ReportQuerySemantics.ApplyDateWindow(q, query.UseValutaDate, windowStart, windowEnd);
        if (kind == PostingKind.Security)
        {
            if (hasSubTypeConstraint)
            {
                q = q.Where(p => p.SecuritySubType == requiredSubType);
            }
            if (query.Filters?.SecuritySubTypes is { Count: > 0 } subTypes)
            {
                q = q.Where(p => p.SecuritySubType != null && subTypes.Contains((int)p.SecuritySubType.Value));
            }
        }

        if (textPatterns is { Count: > 0 })
        {
            // Text matching cannot be translated to SQL; the candidate set has to be
            // materialized before the pattern filter and the sorted top-N selection apply.
            var filtered = (await q.ToListAsync(ct))
                .Where(p => textPatterns.Any(pat => BudgetRulePatternMatcher.MatchesPosting(p.Subject, p.Description, pat, useRegex: false)))
                .ToList();
            return new PostingSelection(
                filtered
                    .OrderByDescending(p => EffectiveDate(p, query.UseValutaDate))
                    .ThenBy(p => p.Id)
                    .Take(MaxPostings)
                    .ToList(),
                filtered.Count,
                filtered.Sum(p => p.Amount));
        }

        // Without in-memory filters the exact count, the exact total and the sorted
        // top-N selection are all produced database-side.
        var totalCount = await q.CountAsync(ct);
        var totalAmount = await q.SumAsync(p => p.Amount, ct);
        var items = await OrderPostings(q, query.UseValutaDate)
            .Take(MaxPostings)
            .ToListAsync(ct);
        return new PostingSelection(items, totalCount, totalAmount);
    }

    private async Task<PostingSelection> QueryNetDividendPostingsAsync(ReportPostingsQuery query, ReportGroupKeyTarget target, Dictionary<Guid, OwnedEntity> ownedSecurities, FilterContext filters, DateTime? windowStart, DateTime? windowEnd, DateTime analysis, bool compareProjection, CancellationToken ct)
    {
        // Netted dividend cells are keyed "Security:{id}"; a sub-typed entity key has no
        // counterpart cell in this mode, so it is rejected instead of silently ignoring the
        // sub type (the caller maps the ArgumentException to a 400 response).
        if (target.SecuritySubType.HasValue)
        {
            throw new ArgumentException("Sub-typed security group keys are not available for netted dividend cells.", nameof(query));
        }

        var securityIds = SelectEntityIds(PostingKind.Security, target, ownedSecurities, filters);
        if (securityIds.Count == 0)
        {
            return PostingSelection.CreateEmpty();
        }

        // Reproduce the load bounds of ReportAggregationService.QuerySecurityDividendsNetAsync:
        // postings (and therefore anchors and group members) are restricted to
        // [loadStartMonth, endExclusive), where loadStartMonth widens for the year-ago and
        // projection comparisons. Only members inside this window contribute to the cell value.
        var monthsBack = query.Take > 0 ? query.Take - 1 : 0;
        var startMonth = query.Interval == ReportInterval.Ytd
            ? new DateTime(analysis.Year - monthsBack, 1, 1)
            : analysis.AddMonths(-monthsBack);
        var endExclusive = analysis.AddMonths(1);
        var loadStartMonth = compareProjection ? startMonth.AddYears(-2) : query.CompareYear ? startMonth.AddYears(-1) : startMonth;

        // Anchors are the dividend postings inside the cell window intersected with the
        // loaded aggregation window; their groups carry the fee/tax members that the
        // netted cell value is composed of.
        var anchorStart = windowStart.HasValue && windowStart.Value > loadStartMonth ? windowStart.Value : loadStartMonth;
        var anchorEnd = windowEnd.HasValue && windowEnd.Value < endExclusive ? windowEnd.Value : endExclusive;
        if (anchorEnd <= anchorStart)
        {
            return PostingSelection.CreateEmpty();
        }

        var anchors = await _db.Postings.AsNoTracking()
            .Where(p => p.Kind == PostingKind.Security
                && p.SecuritySubType == SecurityPostingSubType.Dividend
                && p.SecurityId != null
                && securityIds.Contains(p.SecurityId.Value)
                && (query.UseValutaDate ? ((DateTime?)p.ValutaDate ?? p.BookingDate) : p.BookingDate) >= anchorStart
                && (query.UseValutaDate ? ((DateTime?)p.ValutaDate ?? p.BookingDate) : p.BookingDate) < anchorEnd)
            .Select(p => new { p.GroupId, p.Subject, p.Description })
            .ToListAsync(ct);

        var patterns = filters.SecurityTextPatterns;
        var groupIds = anchors
            .Where(a => patterns is not { Count: > 0 } || patterns.Any(pat => BudgetRulePatternMatcher.MatchesPosting(a.Subject, a.Description, pat, useRegex: false)))
            .Select(a => a.GroupId)
            .ToHashSet();
        if (groupIds.Count == 0)
        {
            return PostingSelection.CreateEmpty();
        }

        // Group members are listed independently of their own date but limited to the loaded
        // aggregation window so the overlay sum matches the netted cell value exactly.
        var members = _db.Postings.AsNoTracking()
            .Where(p => p.Kind == PostingKind.Security
                && p.SecurityId != null
                && securityIds.Contains(p.SecurityId.Value)
                && p.SecuritySubType != null
                && NettingSubTypes.Contains(p.SecuritySubType.Value)
                && groupIds.Contains(p.GroupId)
                && (query.UseValutaDate ? ((DateTime?)p.ValutaDate ?? p.BookingDate) : p.BookingDate) >= loadStartMonth
                && (query.UseValutaDate ? ((DateTime?)p.ValutaDate ?? p.BookingDate) : p.BookingDate) < endExclusive);

        var totalCount = await members.CountAsync(ct);
        var totalAmount = await members.SumAsync(p => p.Amount, ct);
        var items = await OrderPostings(members, query.UseValutaDate)
            .Take(MaxPostings)
            .ToListAsync(ct);
        return new PostingSelection(items, totalCount, totalAmount);
    }

    private static IOrderedQueryable<Posting> OrderPostings(IQueryable<Posting> source, bool useValutaDate)
        => useValutaDate
            ? source.OrderByDescending(p => p.ValutaDate == DateTime.MinValue ? p.BookingDate : p.ValutaDate).ThenBy(p => p.Id)
            : source.OrderByDescending(p => p.BookingDate).ThenBy(p => p.Id);

    /// <summary>
    /// One child selection of a postings query: the materialized top-N items plus the exact
    /// count and amount sum of the whole (untruncated) selection.
    /// </summary>
    /// <param name="Items">Materialized postings, at most <c>MaxPostings</c>, sorted by effective date descending.</param>
    /// <param name="TotalCount">Exact number of matching postings before truncation.</param>
    /// <param name="TotalAmount">Exact amount sum of all matching postings.</param>
    /// <returns>A selection result with materialized items and exact untruncated totals.</returns>
    private sealed record PostingSelection(IReadOnlyList<Posting> Items, int TotalCount, decimal TotalAmount)
    {
        /// <summary>Returns the empty selection.</summary>
        /// <returns>The empty selection.</returns>
        internal static PostingSelection CreateEmpty() => new([], 0, 0m);

        /// <summary>Merges child selections (e.g. linked bank postings plus own plan postings).</summary>
        /// <param name="selections">Selections to merge.</param>
        /// <returns>The merged selection.</returns>
        internal static PostingSelection Combine(IEnumerable<PostingSelection> selections)
        {
            var list = selections.ToList();
            return new PostingSelection(
                list.SelectMany(s => s.Items).ToList(),
                list.Sum(s => s.TotalCount),
                list.Sum(s => s.TotalAmount));
        }
    }

    private static ReportPostingDto Map(Posting p, IReadOnlyDictionary<PostingKind, Dictionary<Guid, OwnedEntity>> owned)
    {
        var entityId = p.Kind switch
        {
            PostingKind.Bank => p.AccountId,
            PostingKind.Contact => p.ContactId,
            PostingKind.SavingsPlan => p.SavingsPlanId,
            PostingKind.Security => p.SecurityId,
            _ => null
        };
        var entityName = entityId.HasValue && owned.TryGetValue(p.Kind, out var entities) && entities.TryGetValue(entityId.Value, out var entity)
            ? entity.Name
            : entityId.HasValue ? entityId.Value.ToString("N")[..6] : string.Empty;
        return new ReportPostingDto(p.Id, p.BookingDate, p.ValutaDate, p.Amount, p.Kind, p.Subject, p.RecipientName, p.Description, p.AccountId, p.ContactId, p.SavingsPlanId, p.SecurityId, p.SecuritySubType, entityName);
    }

    private sealed record OwnedEntity(Guid Id, string Name, Guid? CategoryId);

    private sealed class FilterContext
    {
        private readonly bool _includeCategory;
        private readonly HashSet<Guid>? _accounts;
        private readonly HashSet<Guid>? _contacts;
        private readonly HashSet<Guid>? _savingsPlans;
        private readonly HashSet<Guid>? _securities;
        private readonly HashSet<Guid>? _contactCategories;
        private readonly HashSet<Guid>? _savingsPlanCategories;
        private readonly HashSet<Guid>? _securityCategories;

        internal FilterContext(ReportAggregationFilters? filters, bool includeCategory)
        {
            _includeCategory = includeCategory;
            _accounts = ToSet(filters?.AccountIds);
            _contacts = ToSet(filters?.ContactIds);
            _savingsPlans = ToSet(filters?.SavingsPlanIds);
            _securities = ToSet(filters?.SecurityIds);
            _contactCategories = ToSet(filters?.ContactCategoryIds);
            _savingsPlanCategories = ToSet(filters?.SavingsPlanCategoryIds);
            _securityCategories = ToSet(filters?.SecurityCategoryIds);
            BankTextPatterns = TextFilterList.Normalize(filters?.BankTextFilters);
            ContactTextPatterns = TextFilterList.Normalize(filters?.ContactTextFilters);
            SavingsPlanTextPatterns = TextFilterList.Normalize(filters?.SavingsPlanTextFilters);
            SecurityTextPatterns = TextFilterList.Normalize(filters?.SecurityTextFilters);
        }

        internal IReadOnlyCollection<string>? BankTextPatterns { get; }
        internal IReadOnlyCollection<string>? ContactTextPatterns { get; }
        internal IReadOnlyCollection<string>? SavingsPlanTextPatterns { get; }
        internal IReadOnlyCollection<string>? SecurityTextPatterns { get; }

        internal IReadOnlyCollection<string>? TextPatternsFor(PostingKind kind)
            => kind switch
            {
                PostingKind.Bank => BankTextPatterns,
                PostingKind.Contact => ContactTextPatterns,
                PostingKind.SavingsPlan => SavingsPlanTextPatterns,
                PostingKind.Security => SecurityTextPatterns,
                _ => null
            };

        internal bool EntityAllowed(PostingKind kind, OwnedEntity entity)
            => kind switch
            {
                PostingKind.Bank => _accounts == null || _accounts.Contains(entity.Id),
                PostingKind.Contact => _includeCategory && _contactCategories != null
                    ? entity.CategoryId.HasValue && _contactCategories.Contains(entity.CategoryId.Value)
                    : _contacts == null || _contacts.Contains(entity.Id),
                PostingKind.SavingsPlan => _includeCategory && _savingsPlanCategories != null
                    ? entity.CategoryId.HasValue && _savingsPlanCategories.Contains(entity.CategoryId.Value)
                    : _savingsPlans == null || _savingsPlans.Contains(entity.Id),
                PostingKind.Security => _includeCategory && _securityCategories != null
                    ? entity.CategoryId.HasValue && _securityCategories.Contains(entity.CategoryId.Value)
                    : _securities == null || _securities.Contains(entity.Id),
                _ => true
            };

        private static HashSet<Guid>? ToSet(IReadOnlyCollection<Guid>? src)
            => src is { Count: > 0 } ? src.ToHashSet() : null;
    }
}
