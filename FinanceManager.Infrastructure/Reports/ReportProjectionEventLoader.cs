using FinanceManager.Domain.Postings;
using FinanceManager.Shared.Dtos.Budget;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Reports;

/// <summary>
/// Loads the recurring posting events used by <see cref="ReportProjectionCalculator"/> for all
/// posting kinds. Events are loaded per owner entity (account, contact, savings plan or
/// security); entity ownership and filter allow-lists are resolved by the caller.
/// </summary>
internal sealed class ReportProjectionEventLoader
{
    private readonly AppDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportProjectionEventLoader"/> class.
    /// </summary>
    /// <param name="db">Database context.</param>
    public ReportProjectionEventLoader(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Loads projection events for entity-based posting kinds (bank accounts, contacts and
    /// savings plans). Every non-zero posting inside the window counts as an event; storno,
    /// split and linked-posting relations are not resolved. The effective date follows
    /// <see cref="ReportQuerySemantics.ApplyDateWindow"/> semantics (booking date or valuta
    /// date with the <see cref="DateTime.MinValue"/> fallback to the booking date).
    /// </summary>
    /// <param name="kind">Posting kind to load (<see cref="PostingKind.Bank"/>, <see cref="PostingKind.Contact"/> or <see cref="PostingKind.SavingsPlan"/>).</param>
    /// <param name="entityIds">Owned entity ids of the kind.</param>
    /// <param name="entityIdFilter">Optional allow-list further restricting <paramref name="entityIds"/>.</param>
    /// <param name="textPatterns">Optional normalized text patterns; every pattern is evaluated against subject and description.</param>
    /// <param name="windowStart">Inclusive window start.</param>
    /// <param name="windowEnd">Exclusive window end.</param>
    /// <param name="useValutaDate">Whether the valuta date determines the effective event date.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Projection events ordered by the caller.</returns>
    internal async Task<List<ProjectionEvent>> LoadEntityEventsAsync(
        PostingKind kind,
        IReadOnlyCollection<Guid> entityIds,
        IReadOnlyCollection<Guid>? entityIdFilter,
        IReadOnlyCollection<string>? textPatterns,
        DateTime windowStart,
        DateTime windowEnd,
        bool useValutaDate,
        CancellationToken ct)
    {
        var allowedIds = entityIdFilter is null ? entityIds.ToList() : entityIds.Where(entityIdFilter.Contains).ToList();
        if (allowedIds.Count == 0)
        {
            return new List<ProjectionEvent>();
        }

        var source = kind switch
        {
            PostingKind.Bank => _db.Postings.AsNoTracking()
                .Where(p => p.Kind == PostingKind.Bank && p.AccountId != null && allowedIds.Contains(p.AccountId.Value)),
            PostingKind.Contact => _db.Postings.AsNoTracking()
                .Where(p => p.Kind == PostingKind.Contact && p.ContactId != null && allowedIds.Contains(p.ContactId.Value)),
            PostingKind.SavingsPlan => _db.Postings.AsNoTracking()
                .Where(p => p.Kind == PostingKind.SavingsPlan && p.SavingsPlanId != null && allowedIds.Contains(p.SavingsPlanId.Value)),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported posting kind for entity event loading.")
        };

        var rows = await ReportQuerySemantics.ApplyDateWindow(source, useValutaDate, windowStart, windowEnd)
            .Select(p => new { p.Id, p.AccountId, p.ContactId, p.SavingsPlanId, p.BookingDate, p.ValutaDate, p.Amount, p.Subject, p.Description })
            .ToListAsync(ct);

        return rows
            .Where(r => r.Amount != 0m)
            .Where(r => textPatterns is null || textPatterns.Any(pattern => BudgetRulePatternMatcher.MatchesPosting(r.Subject, r.Description, pattern, useRegex: false)))
            .Select(r =>
            {
                var entityId = kind switch
                {
                    PostingKind.Bank => r.AccountId!.Value,
                    PostingKind.Contact => r.ContactId!.Value,
                    _ => r.SavingsPlanId!.Value
                };
                var effective = useValutaDate && r.ValutaDate != DateTime.MinValue ? r.ValutaDate : r.BookingDate;
                return new ProjectionEvent(kind, entityId, effective, new DateTime(effective.Year, effective.Month, 1), r.Amount, r.Id);
            })
            .ToList();
    }

    /// <summary>
    /// Loads the savings-plan → bank-account links of an owner.
    /// </summary>
    /// <param name="ownerUserId">Owner whose links are loaded.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Mapping of plan id to the linked account ids.</returns>
    internal async Task<Dictionary<Guid, List<Guid>>> LoadSavingsPlanAccountLinksAsync(Guid ownerUserId, CancellationToken ct)
    {
        var links = await _db.SavingsPlanAccountLinks.AsNoTracking()
            .Where(l => l.OwnerUserId == ownerUserId)
            .Select(l => new { l.SavingsPlanId, l.AccountId })
            .ToListAsync(ct);
        return links
            .GroupBy(l => l.SavingsPlanId)
            .ToDictionary(g => g.Key, g => g.Select(l => l.AccountId).Distinct().ToList());
    }

    /// <summary>
    /// Loads savings plan events for the <see cref="SavingsPlanPostingSource.LinkedAccountPostings"/>
    /// source: linked plans derive their events from the bank postings of their linked accounts,
    /// unlinked plans keep their own plan postings.
    /// </summary>
    /// <param name="planIds">Owned savings plan ids.</param>
    /// <param name="planFilter">Optional allow-list further restricting <paramref name="planIds"/>.</param>
    /// <param name="textPatterns">Optional normalized text patterns applied to the source postings.</param>
    /// <param name="linksByPlan">Owner's savings-plan → account links.</param>
    /// <param name="windowStart">Inclusive window start.</param>
    /// <param name="windowEnd">Exclusive window end.</param>
    /// <param name="useValutaDate">Whether the valuta date determines the effective event date.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Projection events carrying <see cref="PostingKind.SavingsPlan"/> and the plan id.</returns>
    internal async Task<List<ProjectionEvent>> LoadSavingsPlanEventsAsync(
        IReadOnlyCollection<Guid> planIds,
        IReadOnlyCollection<Guid>? planFilter,
        IReadOnlyCollection<string>? textPatterns,
        IReadOnlyDictionary<Guid, List<Guid>> linksByPlan,
        DateTime windowStart,
        DateTime windowEnd,
        bool useValutaDate,
        CancellationToken ct)
    {
        var allowedIds = planFilter is null ? planIds.ToList() : planIds.Where(planFilter.Contains).ToList();
        var linked = linksByPlan.Where(kv => allowedIds.Contains(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value);
        var unlinked = allowedIds.Where(id => !linksByPlan.ContainsKey(id)).ToList();

        var events = new List<ProjectionEvent>();
        if (unlinked.Count > 0)
        {
            events.AddRange(await LoadEntityEventsAsync(PostingKind.SavingsPlan, unlinked, null, textPatterns, windowStart, windowEnd, useValutaDate, ct));
        }
        if (linked.Count == 0)
        {
            return events;
        }

        var linkedAccountIds = linked.Values.SelectMany(x => x).Distinct().ToList();
        var rows = await ReportQuerySemantics.ApplyDateWindow(
                _db.Postings.AsNoTracking()
                    .Where(p => p.Kind == PostingKind.Bank && p.AccountId != null && linkedAccountIds.Contains(p.AccountId.Value)),
                useValutaDate, windowStart, windowEnd)
            .Select(p => new { p.Id, p.AccountId, p.BookingDate, p.ValutaDate, p.Amount, p.Subject, p.Description })
            .ToListAsync(ct);

        var rowsByAccount = rows
            .Where(r => r.Amount != 0m)
            .Where(r => textPatterns is null || textPatterns.Any(pattern => BudgetRulePatternMatcher.MatchesPosting(r.Subject, r.Description, pattern, useRegex: false)))
            .GroupBy(r => r.AccountId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var (planId, accountIds) in linked)
        {
            foreach (var accountId in accountIds)
            {
                if (!rowsByAccount.TryGetValue(accountId, out var accountRows))
                {
                    continue;
                }
                foreach (var row in accountRows)
                {
                    var effective = useValutaDate && row.ValutaDate != DateTime.MinValue ? row.ValutaDate : row.BookingDate;
                    events.Add(new ProjectionEvent(PostingKind.SavingsPlan, planId, effective, new DateTime(effective.Year, effective.Month, 1), row.Amount, row.Id));
                }
            }
        }

        return events;
    }

    /// <summary>
    /// Loads netted security dividend events: the postings of every (security, dividend group)
    /// pair are netted (dividend + fee + tax) into a single event dated like the dividend
    /// posting. Groups containing dividend-related security postings that reference a dividend
    /// group via <see cref="Posting.GroupId"/> or by linking to a dividend posting are included;
    /// postings without a group (<see cref="Guid.Empty"/>) net per security, so ungrouped
    /// postings of different securities never merge into one event. The effective date follows
    /// <see cref="ReportQuerySemantics.ApplyDateWindow"/> semantics (booking date or valuta date
    /// with the <see cref="DateTime.MinValue"/> fallback).
    /// </summary>
    /// <param name="securityIds">Owned security ids to consider.</param>
    /// <param name="entityIdFilter">Optional allow-list further restricting <paramref name="securityIds"/>.</param>
    /// <param name="textPatterns">Optional normalized text patterns applied to the group's member postings.</param>
    /// <param name="windowStart">Inclusive window start.</param>
    /// <param name="windowEnd">Exclusive window end.</param>
    /// <param name="useValutaDate">Whether the valuta date determines the effective event date.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>One net event per (security, dividend group) pair.</returns>
    internal async Task<List<ProjectionEvent>> LoadSecurityDividendEventsAsync(
        IReadOnlyCollection<Guid> securityIds,
        IReadOnlyCollection<Guid>? entityIdFilter,
        IReadOnlyCollection<string>? textPatterns,
        DateTime windowStart,
        DateTime windowEnd,
        bool useValutaDate,
        CancellationToken ct)
    {
        var allowedIds = entityIdFilter is null ? securityIds.ToList() : securityIds.Where(entityIdFilter.Contains).ToList();
        if (allowedIds.Count == 0)
        {
            return new List<ProjectionEvent>();
        }

        var postings = ReportQuerySemantics.ApplyDateWindow(
                _db.Postings.AsNoTracking()
                    .Where(p => p.Kind == PostingKind.Security)
                    .Where(p => p.SecurityId != null && allowedIds.Contains(p.SecurityId.Value)),
                useValutaDate, windowStart, windowEnd)
            .Where(p => p.SecuritySubType != null);

        // Text filters select dividend events via their anchor posting (subtype Dividend);
        // a matching anchor keeps the whole group including fee/tax netting.
        var anchors = await postings
            .Where(p => p.SecuritySubType == SecurityPostingSubType.Dividend)
            .Select(p => new { p.Id, p.GroupId, p.SecurityId, p.Subject, p.Description })
            .ToListAsync(ct);
        var matchingAnchors = anchors
            .Where(a => textPatterns is not { Count: > 0 } || textPatterns.Any(pattern => BudgetRulePatternMatcher.MatchesPosting(a.Subject, a.Description, pattern, useRegex: false)))
            .ToList();

        // Postings that never joined a group keep GroupId.Empty. The net is still computed per
        // (security, group) pair so ungrouped dividend/fee/tax postings of one security net
        // together (same semantics as the postings overlay) without merging unrelated
        // securities into a single event.
        var dividendGroupKeys = matchingAnchors
            .Select(a => (SecurityId: a.SecurityId!.Value, a.GroupId))
            .ToHashSet();
        var dividendGroupIds = dividendGroupKeys
            .Select(k => k.GroupId)
            .ToHashSet();

        // For each dividend group, compute net = sum(dividend + fee + tax) and take the dividend's booking date as anchor
        var groupNets = await postings
            .Where(p => dividendGroupIds.Contains(p.GroupId))
            .GroupBy(p => new { p.SecurityId, p.GroupId })
            .Select(g => new
            {
                g.Key.SecurityId,
                g.Key.GroupId,
                Net = g.Where(x => x.SecuritySubType == SecurityPostingSubType.Dividend
                    || x.SecuritySubType == SecurityPostingSubType.Fee
                    || x.SecuritySubType == SecurityPostingSubType.Tax).Sum(x => x.Amount),
                DividendDate = g.Where(x => x.SecuritySubType == SecurityPostingSubType.Dividend)
                    .Select(x => useValutaDate ? (x.ValutaDate == DateTime.MinValue ? x.BookingDate : x.ValutaDate) : x.BookingDate)
                    .FirstOrDefault(),
                DividendPostingId = g.Where(x => x.SecuritySubType == SecurityPostingSubType.Dividend)
                    .Select(x => (Guid?)x.Id)
                    .FirstOrDefault()
            })
            .ToListAsync(ct);

        return groupNets
            .Where(g => g.SecurityId.HasValue
                && g.DividendDate != default(DateTime)
                && dividendGroupKeys.Contains((g.SecurityId.Value, g.GroupId)))
            .Select(g => new ProjectionEvent(PostingKind.Security, g.SecurityId!.Value, g.DividendDate, new DateTime(g.DividendDate.Year, g.DividendDate.Month, 1), g.Net, g.DividendPostingId))
            .ToList();
    }

    /// <summary>
    /// Loads the buy/sell security postings up to a projection end used for the
    /// positive-holding check of security expectations.
    /// </summary>
    /// <param name="securityIds">Owned security ids to consider.</param>
    /// <param name="endInclusive">Inclusive load end.</param>
    /// <param name="useValutaDate">Whether the valuta date determines the effective posting date.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Holding transactions as tuples (security id, effective date, sub type, quantity, reversal flag).</returns>
    internal async Task<List<(Guid SecurityId, DateTime Date, SecurityPostingSubType SubType, decimal Quantity, bool IsReversal)>> LoadSecurityHoldingTransactionsAsync(
        IReadOnlyCollection<Guid> securityIds,
        DateTime endInclusive,
        bool useValutaDate,
        CancellationToken ct)
    {
        if (securityIds.Count == 0)
        {
            return new List<(Guid, DateTime, SecurityPostingSubType, decimal, bool)>();
        }

        return await _db.Postings.AsNoTracking()
            .Where(p => p.Kind == PostingKind.Security)
            .Where(p => p.SecurityId != null && securityIds.Contains(p.SecurityId.Value))
            .Where(p => p.SecuritySubType == SecurityPostingSubType.Buy || p.SecuritySubType == SecurityPostingSubType.Sell)
            .Where(p => p.Quantity != null)
            .Where(p => (useValutaDate ? (p.ValutaDate == DateTime.MinValue ? p.BookingDate : p.ValutaDate) : p.BookingDate) <= endInclusive)
            .Select(p => new ValueTuple<Guid, DateTime, SecurityPostingSubType, decimal, bool>(
                p.SecurityId!.Value,
                useValutaDate ? (p.ValutaDate == DateTime.MinValue ? p.BookingDate : p.ValutaDate) : p.BookingDate,
                p.SecuritySubType!.Value,
                p.Quantity!.Value,
                p.ReversalForPostingId.HasValue))
            .ToListAsync(ct);
    }
}
