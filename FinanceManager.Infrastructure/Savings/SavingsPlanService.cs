using System.Linq;
using FinanceManager.Application.Aggregates;
using FinanceManager.Application.Savings;
using FinanceManager.Domain.Attachments;
using FinanceManager.Domain.Postings;
using FinanceManager.Domain.Savings;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Savings;

/// <summary>
/// Service providing operations for managing savings plans (CRUD, analysis and attachments).
/// </summary>
public sealed class SavingsPlanService : ISavingsPlanService
{
    private readonly AppDbContext _db;
    private readonly IPostingAggregateService? _aggregateService;

    /// <summary>
    /// Initializes a new instance of the <see cref="SavingsPlanService"/> class.
    /// </summary>
    /// <param name="db">The application's <see cref="AppDbContext"/> used to query and persist savings plans and related entities.</param>
    /// <param name="aggregateService">Optional aggregate service used to feed posting aggregates when the service creates postings itself (actual-balance reports).</param>
    public SavingsPlanService(AppDbContext db, IPostingAggregateService? aggregateService = null) { _db = db; _aggregateService = aggregateService; }

    /// <summary>
    /// Lists savings plans for a user, optionally filtering to only active plans.
    /// </summary>
    /// <param name="ownerUserId">Owner user identifier.</param>
    /// <param name="onlyActive">When <c>true</c> only active plans are returned; otherwise all plans are returned.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of <see cref="SavingsPlanDto"/> matching the criteria (may be empty).</returns>
    public async Task<IReadOnlyList<SavingsPlanDto>> ListAsync(Guid ownerUserId, bool onlyActive, CancellationToken ct)
    {
        var plans = from p in _db.SavingsPlans.AsNoTracking()
                    where p.OwnerUserId == ownerUserId && (!onlyActive || p.IsActive)
                    join c in _db.SavingsPlanCategories.AsNoTracking() on p.CategoryId equals c.Id into pcs
                    from cat in pcs.DefaultIfEmpty()
                    orderby p.Name
                    select new SavingsPlanDto(
                        p.Id,
                        p.Name,
                        p.Type,
                        p.Type == SavingsPlanType.Open ? null : p.TargetAmount,
                        p.TargetDate,
                        p.Interval,
                        p.IsActive,
                        p.CreatedUtc,
                        p.ArchivedUtc,
                        p.CategoryId,
                        p.ContractNumber,
                        p.SymbolAttachmentId ?? (cat != null ? cat.SymbolAttachmentId : null),
                        0m,
                        0m
                    );

        return await plans.ToListAsync(ct);
    }

    /// <summary>
    /// Gets a savings plan by id for the specified owner.
    /// </summary>
    /// <param name="id">Identifier of the savings plan.</param>
    /// <param name="ownerUserId">Owner user identifier for ownership validation.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The <see cref="SavingsPlanDto"/> when found; otherwise <c>null</c>.</returns>
    public async Task<SavingsPlanDto?> GetAsync(Guid id, Guid ownerUserId, CancellationToken ct)
    {
        var plan = await _db.SavingsPlans.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id && p.OwnerUserId == ownerUserId, ct);
        if (plan == null) { return null; }

        var today = DateTime.Today;
        // sum of postings for this savings plan up to today; for plans with linked
        // accounts the sum of the linked account balances is the only source instead
        var accumulated = await SavingsPlanLinkAnalysis.GetCurrentAmountAsync(_db, id, today, ct);

        // "Eigenanteil": the balance built from the plan's own postings, only reported
        // when the plan has linked accounts (the saldo then follows the account balances)
        decimal? ownAmount = null;
        if ((await SavingsPlanLinkAnalysis.GetLinkedAccountIdsAsync(_db, id, ct)).Count > 0)
        {
            ownAmount = await _db.Postings.AsNoTracking()
                .Where(p => p.SavingsPlanId == id && p.Kind == PostingKind.SavingsPlan && p.BookingDate <= today)
                .SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;
        }

        decimal remaining = 0m;
        if (plan.TargetAmount is not null)
        {
            remaining = Math.Max(0m, plan.TargetAmount.Value - accumulated);
        }

        return new SavingsPlanDto(plan.Id, plan.Name, plan.Type, plan.TargetAmount, plan.TargetDate, plan.Interval, plan.IsActive, plan.CreatedUtc, plan.ArchivedUtc, plan.CategoryId, plan.ContractNumber, plan.SymbolAttachmentId, remaining, accumulated, ownAmount);
    }

    /// <summary>
    /// Creates a new savings plan for the specified owner.
    /// </summary>
    /// <param name="ownerUserId">Owner user identifier.</param>
    /// <param name="name">Name of the savings plan. Must be unique per user when provided.</param>
    /// <param name="type">Type of savings plan.</param>
    /// <param name="targetAmount">Optional target amount for closed plans.</param>
    /// <param name="targetDate">Optional target date for reaching the target amount.</param>
    /// <param name="interval">Optional contribution interval.</param>
    /// <param name="categoryId">Optional category id.</param>
    /// <param name="contractNumber">Optional contract number.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created <see cref="SavingsPlanDto"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when a plan with the same name already exists for the user.</exception>
    public async Task<SavingsPlanDto> CreateAsync(Guid ownerUserId, string name, SavingsPlanType type, decimal? targetAmount, DateTime? targetDate, SavingsPlanInterval? interval, Guid? categoryId, string? contractNumber, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            var exists = await _db.SavingsPlans.AnyAsync(p => p.OwnerUserId == ownerUserId && p.Name == name, ct);
            if (exists) { throw new ArgumentException("Savings plan name must be unique per user", nameof(name)); }
        }
        var plan = new SavingsPlan(ownerUserId, name, type, targetAmount, targetDate, interval, categoryId);
        plan.SetContractNumber(contractNumber);
        _db.SavingsPlans.Add(plan);
        await _db.SaveChangesAsync(ct);
        return new SavingsPlanDto(plan.Id, plan.Name, plan.Type, plan.TargetAmount, plan.TargetDate, plan.Interval, plan.IsActive, plan.CreatedUtc, plan.ArchivedUtc, plan.CategoryId, plan.ContractNumber, plan.SymbolAttachmentId);
    }

    /// <summary>
    /// Updates an existing savings plan.
    /// </summary>
    /// <param name="id">Identifier of the savings plan to update.</param>
    /// <param name="ownerUserId">Owner user identifier for ownership validation.</param>
    /// <param name="name">New name of the plan.</param>
    /// <param name="type">New type.</param>
    /// <param name="targetAmount">New target amount.</param>
    /// <param name="targetDate">New target date.</param>
    /// <param name="interval">New interval.</param>
    /// <param name="categoryId">New category id.</param>
    /// <param name="contractNumber">New contract number.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated <see cref="SavingsPlanDto"/>, or <c>null</c> when not found.</returns>
    /// <exception cref="ArgumentException">Thrown when the new name conflicts with another plan of the same user.</exception>
    public async Task<SavingsPlanDto?> UpdateAsync(Guid id, Guid ownerUserId, string name, SavingsPlanType type, decimal? targetAmount, DateTime? targetDate, SavingsPlanInterval? interval, Guid? categoryId, string? contractNumber, CancellationToken ct)
    {
        var plan = await _db.SavingsPlans.FirstOrDefaultAsync(p => p.Id == id && p.OwnerUserId == ownerUserId, ct);
        if (plan == null) { return null; }
        if (!string.Equals(plan.Name, name, StringComparison.OrdinalIgnoreCase))
        {
            var exists = await _db.SavingsPlans.AnyAsync(p => p.OwnerUserId == ownerUserId && p.Name == name && p.Id != id, ct);
            if (exists) { throw new ArgumentException("Savings plan name must be unique per user", nameof(name)); }
        }
        plan.Rename(name);
        plan.ChangeType(type);
        plan.SetTarget(targetAmount, targetDate);
        plan.SetInterval(interval);
        plan.SetCategory(categoryId);
        plan.SetContractNumber(contractNumber);
        await _db.SaveChangesAsync(ct);
        return new SavingsPlanDto(plan.Id, plan.Name, plan.Type, plan.TargetAmount, plan.TargetDate, plan.Interval, plan.IsActive, plan.CreatedUtc, plan.ArchivedUtc, plan.CategoryId, plan.ContractNumber, plan.SymbolAttachmentId);
    }

    /// <summary>
    /// Archives a savings plan (marks it as inactive).
    /// </summary>
    /// <param name="id">Identifier of the plan to archive.</param>
    /// <param name="ownerUserId">Owner user identifier for ownership validation.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><c>true</c> when the plan was found and archived; otherwise <c>false</c> when not found.</returns>
    /// <exception cref="ArgumentException">Thrown when the plan is already archived.</exception>
    public async Task<bool> ArchiveAsync(Guid id, Guid ownerUserId, CancellationToken ct)
    {
        var plan = await _db.SavingsPlans.FirstOrDefaultAsync(p => p.Id == id && p.OwnerUserId == ownerUserId, ct);
        if (plan == null) { return false; }
        if (!plan.IsActive) { throw new ArgumentException("Savings plan is already archived", "ArchiveState"); }
        plan.Archive();
        await _db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>
    /// Deletes a savings plan if business rules allow it (archived and not referenced).
    /// </summary>
    /// <param name="id">Identifier of the plan to delete.</param>
    /// <param name="ownerUserId">Owner user identifier for ownership validation.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><c>true</c> when the plan existed and was deleted; otherwise <c>false</c> when not found.</returns>
    /// <exception cref="ArgumentException">Thrown when deletion is blocked by business rules (active, referenced by entries/postings/drafts).</exception>
    public async Task<bool> DeleteAsync(Guid id, Guid ownerUserId, CancellationToken ct)
    {
        var plan = await _db.SavingsPlans.FirstOrDefaultAsync(p => p.Id == id && p.OwnerUserId == ownerUserId, ct);
        if (plan == null) { return false; }

        // Business rules:
        // 1. Only archived plans can be deleted (avoid accidental data loss of active plans).
        if (plan.IsActive) { throw new ArgumentException("Savings plan must be archived before it can be deleted", "IsActive"); }

        // 2. Prevent deletion if referenced by committed statement entries.
        bool hasStatementEntries = await _db.StatementEntries.AsNoTracking().AnyAsync(e => e.SavingsPlanId == id, ct);
        if (hasStatementEntries) { throw new ArgumentException("Savings plan is referenced by committed statement entries and cannot be deleted", "StatementEntries"); }

        // 3. Prevent deletion if referenced by draft entries (user must remove assignments first).
        bool hasDraftEntries = await _db.StatementDraftEntries.AsNoTracking().AnyAsync(e => e.SavingsPlanId == id, ct);
        if (hasDraftEntries) { throw new ArgumentException("Savings plan is referenced by draft entries and cannot be deleted", "DraftEntries"); }

        // 4. Prevent deletion if postings reference the plan (future feature; safety for already created postings).
        bool hasPostings = await _db.Postings.AsNoTracking().AnyAsync(p => p.SavingsPlanId == id, ct);
        if (hasPostings) { throw new ArgumentException("Savings plan is referenced by postings and cannot be deleted", "Postings"); }

        // Delete attachments of this savings plan
        await _db.Attachments
            .Where(a => a.OwnerUserId == ownerUserId && a.EntityKind == AttachmentEntityKind.SavingsPlan && a.EntityId == plan.Id)
            .ExecuteDeleteAsync(ct);

        _db.SavingsPlans.Remove(plan);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>
    /// Computes an analysis for the savings plan (accumulated contributions, forecast, required monthly
    /// amount and the schedule deficit against the observed contribution rhythm).
    /// </summary>
    /// <param name="id">Identifier of the plan to analyze.</param>
    /// <param name="ownerUserId">Owner user identifier for ownership validation.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="SavingsPlanAnalysisDto"/> containing the analysis results. When the plan is not found a default result with Exists=false is returned.</returns>
    public async Task<SavingsPlanAnalysisDto> AnalyzeAsync(Guid id, Guid ownerUserId, CancellationToken ct)
    {
        var plan = await _db.SavingsPlans.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id && p.OwnerUserId == ownerUserId, ct);
        if (plan == null)
        {
            return new SavingsPlanAnalysisDto(id, false, null, null, 0m, 0m, 0);
        }

        var today = DateTime.Today;

        var linkedAccountIds = await SavingsPlanLinkAnalysis.GetLinkedAccountIdsAsync(_db, id, ct);

        decimal accumulated;
        IReadOnlyList<PostingObservation> history;
        if (linkedAccountIds.Count > 0)
        {
            // Account-linked plan: the contract account balances are the single source of truth
            // for the accumulated amount (additive over all linked accounts; a collection account
            // contributes its full balance to every linked plan - no split).
            accumulated = await _db.Accounts.AsNoTracking()
                .Where(a => linkedAccountIds.Contains(a.Id))
                .SumAsync(a => (decimal?)a.CurrentBalance, ct) ?? 0m;

            // The contribution rhythm follows the inflow on the linked accounts (own debits,
            // employer-funded VL, interest and any other positive postings), not the
            // giro-side funding outflow.
            var accountInflow = await _db.Postings.AsNoTracking()
                .Where(p => p.Kind == PostingKind.Bank
                    && p.AccountId != null
                    && linkedAccountIds.Contains(p.AccountId.Value)
                    && p.Amount > 0m
                    && p.BookingDate <= today)
                .Select(p => new PostingObservation(p.BookingDate, p.Amount, p.ReversedByPostingId != null, p.ReversalForPostingId != null))
                .ToListAsync(ct);

            var planPostings = await _db.Postings.AsNoTracking()
                .Where(p => p.SavingsPlanId == id && p.Kind == PostingKind.SavingsPlan && p.BookingDate <= today)
                .Select(p => new
                {
                    p.Id,
                    p.GroupId,
                    p.BookingDate,
                    p.Amount,
                    IsReversed = p.ReversedByPostingId != null,
                    IsReversal = p.ReversalForPostingId != null
                })
                .ToListAsync(ct);

            var groupIds = planPostings.Where(x => x.GroupId != Guid.Empty).Select(x => x.GroupId).Distinct().ToList();
            var groupBankAccounts = await SavingsPlanLinkAnalysis.GetGroupBankAccountIdsAsync(_db, groupIds, ct);
            var linkedSet = linkedAccountIds.ToHashSet();

            var planObservations = new List<PostingObservation>(planPostings.Count);
            foreach (var x in planPostings)
            {
                var bankAccounts = x.GroupId != Guid.Empty && groupBankAccounts.TryGetValue(x.GroupId, out var b) ? b : null;
                var hasForeignBankCounterpart = bankAccounts?.Any(a => !linkedSet.Contains(a)) == true;
                var hasLinkedBankCounterpart = bankAccounts?.Any(a => linkedSet.Contains(a)) == true;
                if (hasForeignBankCounterpart)
                {
                    // Informal funding posting: the same money flow already counts via the
                    // linked account balance/inflow - skip to avoid double counting.
                    continue;
                }
                if (x.Amount > 0m && hasLinkedBankCounterpart)
                {
                    // Positive plan posting whose bank counterpart sits on a linked account:
                    // the inflow is already part of the account observations above.
                    continue;
                }
                // Non-informal plan postings needed for cycle/disbursement detection - in
                // particular negative disbursements without a bank counterpart.
                planObservations.Add(new PostingObservation(x.BookingDate, x.Amount, x.IsReversed, x.IsReversal));
            }

            history = accountInflow.Concat(planObservations).ToList();
        }
        else
        {
            // Past contributions up to today for this plan (always compute accumulated amount)
            history = await _db.Postings.AsNoTracking()
                .Where(p => p.SavingsPlanId == id && p.Kind == PostingKind.SavingsPlan && p.BookingDate <= today)
                .Select(p => new PostingObservation(p.BookingDate, p.Amount, p.ReversedByPostingId != null, p.ReversalForPostingId != null))
                .ToListAsync(ct);

            accumulated = history.Sum(x => x.Amount);
        }
        var schedule = ComputeSchedule(history, plan.Interval, plan.TargetAmount, plan.TargetDate, today);

        // If no target defined, return accumulated amount and no forecast
        if (plan.TargetAmount is null || plan.TargetDate is null)
        {
            return new SavingsPlanAnalysisDto(id, true, plan.TargetAmount, plan.TargetDate, accumulated, 0m, 0,
                schedule.ScheduleDeficit, schedule.ExpectedToDate, schedule.ReceivedToDate, schedule.ObservedMonthlyRate, schedule.Disbursements);
        }

        var endDate = plan.TargetDate.Value.Date;
        var monthsRemaining = Math.Max(0, ((endDate.Year - today.Year) * 12 + endDate.Month - today.Month));

        var target = plan.TargetAmount.Value;

        if (monthsRemaining <= 0)
        {
            var reached = accumulated >= target;
            return new SavingsPlanAnalysisDto(id, reached, target, endDate, accumulated, 0m, 0,
                schedule.ScheduleDeficit, schedule.ExpectedToDate, schedule.ReceivedToDate, schedule.ObservedMonthlyRate, schedule.Disbursements);
        }

        // Average monthly saving based on history grouped by month
        var monthlyTotals = history
            .GroupBy(x => new { x.BookingDate.Year, x.BookingDate.Month })
            .Select(g => g.Sum(x => x.Amount))
            .ToList();
        var averagePerMonth = monthlyTotals.Count > 0 ? monthlyTotals.Average() : 0m;

        var forecast = averagePerMonth * monthsRemaining;
        var totalExpected = accumulated + forecast;
        var reachable = totalExpected >= target;

        var remaining = Math.Max(0m, target - accumulated);
        var requiredMonthly = monthsRemaining > 0 ? remaining / monthsRemaining : 0m;

        return new SavingsPlanAnalysisDto(id, reachable, target, endDate, accumulated, requiredMonthly, monthsRemaining,
            schedule.ScheduleDeficit, schedule.ExpectedToDate, schedule.ReceivedToDate, schedule.ObservedMonthlyRate, schedule.Disbursements);
    }

    /// <summary>
    /// Derives the plan's observed contribution rhythm and the backlog against it within the
    /// current financing cycle. Effective postings are those neither reversed themselves nor a
    /// reversal of another posting, so a reversed deposit and its cancellation drop out of both
    /// sides. Due plans run in financing cycles, not as a continuous history: deposits fund the
    /// next due amount, then a disbursement of roughly the target amount pays it out and opens a
    /// new cycle. The backlog therefore only counts the current cycle - deposits received before
    /// the last payout are already spent and can never be "missing". The cycle starts at the
    /// booking date of the last due disbursement (the newest negative posting within 20 % of the
    /// target amount, else the largest withdrawal), else at the last reached
    /// <paramref name="targetDate"/> when no usable disbursement exists, else at the first
    /// deposit. Positive effective postings since then feed <c>ReceivedToDate</c>, while
    /// <c>Disbursements</c> always reports the whole-history total of negative effective
    /// postings. The positive cycle postings cluster into amount classes (equal amounts
    /// within max(0.50, 5 %) tolerance) and the dominant class - the most frequent one,
    /// ties by larger amount - stands for the recurring contribution. A most-frequent
    /// class below half of the largest class amount is micro noise (e.g. interest
    /// credits): it can never dominate when a comparably frequent larger class exists
    /// (at least half the members), and its postings never enter
    /// <c>ObservedMonthlyRate</c>, <c>ExpectedToDate</c> or <c>ReceivedToDate</c>. The
    /// deposit cadence is read from the bookings themselves: the median month gap
    /// between consecutive booking months of the dominant class (1 = monthly,
    /// 3 = quarterly ...). The plan interval only fills in when no cadence is
    /// derivable - fewer than two booking months, or a single ambiguous gap larger
    /// than one month that cannot be told apart from missed monthly deposits - as it
    /// marks the due rhythm of the financed expense (e.g. an annual insurance
    /// premium), not how often money flows in. The
    /// observed rate is the dominant class amount divided by the cadence months, and
    /// expected periods are the fully elapsed cadence windows since the cycle start
    /// (floor of elapsed months / cadence, so the current month is not due yet).
    /// When the in-cycle disbursements beyond the cycle-opening
    /// payout roughly balance the received deposits the plan revolves (e.g. a
    /// credit-card settlement reserve) and <c>ScheduleDeficit</c> stays
    /// <see langword="null"/> - "cyclically balanced" is more honest than zero. Without
    /// an explicit interval a rhythm is only assumed when dominant-class deposits cover
    /// at least half of the elapsed months in at least two distinct months - irregular
    /// one-off top-ups then yield <see langword="null"/> instead of a bogus deficit, and
    /// so does a history in which every deposit amount occurred at most once (no
    /// detectable dominant class).
    /// <param name="history">Observed postings (booking date, amount and reversal flags) feeding the analysis.</param>
    /// <param name="interval">Optional contribution interval of the plan - used only as cadence fallback.</param>
    /// <param name="targetAmount">Optional target amount - qualifies due disbursements for the cycle boundary.</param>
    /// <param name="targetDate">Optional target date - bounds the current cycle when reached.</param>
    /// <param name="today">Reference date of the evaluation.</param>
    /// <returns>The computed contribution schedule.</returns>
    /// </summary>
    private static ContributionSchedule ComputeSchedule(
        IReadOnlyList<PostingObservation> history, SavingsPlanInterval? interval, decimal? targetAmount, DateTime? targetDate, DateTime today)
    {
        if (history.Count == 0)
        {
            return ContributionSchedule.Empty;
        }

        var effective = history.Where(h => h is { IsReversed: false, IsReversal: false }).ToList();
        var disbursements = effective.Where(h => h.Amount < 0m).Sum(h => h.Amount);

        // Cycle boundary: the last due disbursement (|amount| ≈ targetAmount ±20 %, else the
        // largest withdrawal). Without a target amount no withdrawal can qualify as "due", so a
        // reached target date bounds the cycle instead; without either the whole history since
        // the first deposit forms the cycle (previous behaviour).
        var withdrawals = effective.Where(h => h.Amount < 0m).ToList();
        PostingObservation? cycleBoundary = null;
        if (targetAmount is { } target && withdrawals.Count > 0)
        {
            var absTarget = Math.Abs(target);
            cycleBoundary = withdrawals
                .Where(w => Math.Abs(w.Amount) >= absTarget * 0.8m && Math.Abs(w.Amount) <= absTarget * 1.2m)
                .OrderByDescending(w => w.BookingDate)
                .FirstOrDefault()
                ?? withdrawals.OrderByDescending(w => Math.Abs(w.Amount)).ThenByDescending(w => w.BookingDate).First();
        }

        var cycleStart = cycleBoundary?.BookingDate.Date
            ?? (targetDate is { } due && due.Date <= today ? due.Date : (DateTime?)null)
            ?? effective.Where(h => h.Amount > 0m).Select(h => (DateTime?)h.BookingDate.Date).Min();

        var inCycle = cycleStart is { } start
            ? effective.Where(h => h.BookingDate.Date >= start).ToList()
            : new List<PostingObservation>(0);

        var received = inCycle.Where(h => h.Amount > 0m).Sum(h => h.Amount);
        // In-cycle disbursements beyond the cycle-opening payout itself: the boundary withdrawal
        // marks the completed previous cycle and must not weigh against the new cycle's deposits.
        var cycleDisbursements = inCycle.Where(h => h.Amount < 0m && !ReferenceEquals(h, cycleBoundary)).Sum(h => h.Amount);

        // Amount classes: ascending positive postings join the previous class while they
        // stay within max(0.50, 5 % of the last member) - enough slack for cent-level
        // wobble of a fixed contribution, tight enough to keep interest credits and
        // distinct contribution rates apart.
        var positive = inCycle.Where(h => h.Amount > 0m).OrderBy(h => h.Amount).ToList();
        if (positive.Count == 0)
        {
            return new ContributionSchedule(received, disbursements, null, null, null);
        }

        var amountClasses = new List<List<PostingObservation>>();
        foreach (var p in positive)
        {
            var current = amountClasses.Count > 0 ? amountClasses[^1] : null;
            if (current is { Count: > 0 })
            {
                var lastAmount = current[^1].Amount;
                if (p.Amount - lastAmount <= Math.Max(0.5m, lastAmount * 0.05m))
                {
                    current.Add(p);
                    continue;
                }
            }
            amountClasses.Add(new List<PostingObservation> { p });
        }

        // Dominant class = the most frequent amount class; ties go to the larger
        // amount. Exception: when the most frequent class is a micro class (below
        // 50 % of the largest class amount) a comparably frequent larger class wins
        // instead - a swarm of small interest credits must not out-vote the recurring
        // deposit it accompanies by sheer count.
        var dominant = amountClasses
            .OrderByDescending(c => c.Count)
            .ThenByDescending(c => c.Average(x => x.Amount))
            .First();
        var largestClassAmount = amountClasses.Max(c => c.Average(x => x.Amount));
        if (dominant.Average(x => x.Amount) < largestClassAmount * 0.5m)
        {
            dominant = amountClasses
                .Where(c => c.Count * 2 >= dominant.Count)
                .OrderByDescending(c => c.Average(x => x.Amount))
                .First();
        }

        // No detectable dominant class: when every deposit amount occurred at most
        // once there is no recurring contribution to derive a rhythm from.
        if (dominant.Count < 2 && amountClasses.Count > 1)
        {
            return new ContributionSchedule(received, disbursements, null, null, null);
        }

        var dominantAmount = dominant.Average(x => x.Amount);
        // Received/expected/deficit only count the dominant class: micro postings
        // (below ~50 % of the dominant class amount, e.g. interest credits) and
        // non-recurring amounts of other classes never enter the rhythm fields.
        var receivedDominant = dominant.Sum(x => x.Amount);

        // At least one deposit exists here, so cycleStart is set.
        var elapsedMonths = ((today.Year - cycleStart!.Value.Year) * 12) + today.Month - cycleStart.Value.Month;

        var intervalMonths = interval switch
        {
            SavingsPlanInterval.Monthly => 1,
            SavingsPlanInterval.BiMonthly => 2,
            SavingsPlanInterval.Quarterly => 3,
            SavingsPlanInterval.SemiAnnually => 6,
            SavingsPlanInterval.Annually => 12,
            _ => 1
        };

        // The deposit cadence comes from the bookings themselves - the median month
        // gap between consecutive booking months of the dominant class (1 = monthly,
        // 3 = quarterly ...). The plan interval only marks the due rhythm of the
        // expense the plan finances (e.g. an annual membership fee), not how often
        // money flows in, so it fills in when the cadence is not derivable: fewer
        // than two booking months, or a single ambiguous gap larger than one month.
        var dominantMonths = dominant
            .Select(x => (x.BookingDate.Year * 12) + x.BookingDate.Month)
            .Distinct()
            .OrderBy(m => m)
            .ToList();
        var cadenceMonths = intervalMonths;
        if (dominantMonths.Count >= 2)
        {
            var gaps = dominantMonths
                .Skip(1)
                .Select((m, i) => m - dominantMonths[i])
                .OrderBy(g => g)
                .ToList();
            if (gaps.Count == 1)
            {
                // A single observed gap is ambiguous: a gap of 1 proves a monthly
                // flow, but a larger gap cannot be told apart from missed monthly
                // deposits - the plan interval stays the tiebreaker.
                if (gaps[0] == 1)
                {
                    cadenceMonths = 1;
                }
            }
            else
            {
                var median = gaps.Count % 2 == 1
                    ? gaps[gaps.Count / 2]
                    : (int)Math.Round((gaps[(gaps.Count / 2) - 1] + gaps[gaps.Count / 2]) / 2m, MidpointRounding.AwayFromZero);
                cadenceMonths = Math.Max(1, median);
            }
        }

        var rhythmDetected = interval.HasValue
            || (elapsedMonths >= 1 && dominantMonths.Count >= 2 && dominantMonths.Count * 2 >= elapsedMonths);
        if (!rhythmDetected)
        {
            return new ContributionSchedule(received, disbursements, null, null, null);
        }

        // Only fully elapsed cadence windows count as due - a window ending in the
        // current month is not due yet.
        var expectedPeriods = elapsedMonths / cadenceMonths;
        var observedMonthlyRate = dominantAmount / cadenceMonths;
        var expectedToDate = dominantAmount * expectedPeriods;

        // A revolving plan (in-cycle disbursements roughly balancing the deposits, e.g. a
        // credit-card settlement reserve) carries no backlog: null states "cyclically balanced".
        decimal? scheduleDeficit = Math.Max(0m, expectedToDate - receivedDominant);
        if (receivedDominant > 0m && cycleDisbursements < 0m
            && Math.Abs(receivedDominant + cycleDisbursements) <= 0.2m * Math.Max(receivedDominant, Math.Abs(cycleDisbursements)))
        {
            scheduleDeficit = null;
        }

        return new ContributionSchedule(receivedDominant, disbursements, observedMonthlyRate, expectedToDate, scheduleDeficit);
    }

    private sealed record PostingObservation(DateTime BookingDate, decimal Amount, bool IsReversed, bool IsReversal);

    private sealed record ContributionSchedule(
        decimal? ReceivedToDate,
        decimal? Disbursements,
        decimal? ObservedMonthlyRate,
        decimal? ExpectedToDate,
        decimal? ScheduleDeficit)
    {
        public static readonly ContributionSchedule Empty = new(null, null, null, null, null);
    }

    /// <summary>
    /// Counts savings plans for the owner, optionally filtering to active plans.
    /// </summary>
    /// <param name="ownerUserId">Owner user identifier.</param>
    /// <param name="onlyActive">When <c>true</c> only active plans are counted.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The number of matching savings plans.</returns>
    public Task<int> CountAsync(Guid ownerUserId, bool onlyActive, CancellationToken ct)
    {
        var q = _db.SavingsPlans.AsNoTracking().Where(p => p.OwnerUserId == ownerUserId);
        if (onlyActive) { q = q.Where(p => p.IsActive); }
        return q.CountAsync(ct);
    }

    /// <summary>
    /// Sets or clears a symbol attachment reference for the savings plan.
    /// </summary>
    /// <param name="id">Identifier of the savings plan.</param>
    /// <param name="ownerUserId">Owner user identifier for ownership validation.</param>
    /// <param name="attachmentId">Attachment identifier to set, or <c>null</c> to clear.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the change has been persisted.</returns>
    /// <exception cref="ArgumentException">Thrown when the savings plan is not found or not owned by the user.</exception>
    public async Task SetSymbolAttachmentAsync(Guid id, Guid ownerUserId, Guid? attachmentId, CancellationToken ct)
    {
        var plan = await _db.SavingsPlans.FirstOrDefaultAsync(p => p.Id == id && p.OwnerUserId == ownerUserId, ct);
        if (plan == null) throw new ArgumentException("Savings plan not found", nameof(id));
        plan.SetSymbolAttachment(attachmentId);
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Captures an actual-balance report ("Istmeldung") for a savings plan without linked accounts:
    /// creates a plan posting for the difference between the reported balance and the current
    /// accumulated amount - marked with <see cref="SavingsPlanPostingSubType.ActualReport"/> and
    /// without an account id, so no account movement is produced. For plans with linked accounts
    /// the account balance is the single source of truth, so reports are rejected.
    /// </summary>
    /// <param name="id">Identifier of the savings plan.</param>
    /// <param name="ownerUserId">Owner user identifier for ownership validation.</param>
    /// <param name="reportedBalance">The real balance reported by the user.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="SavingsPlanActualReportResponse"/> describing the created correction posting.</returns>
    /// <exception cref="ArgumentException">Thrown when the savings plan is not found or not owned by the user.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the plan has linked accounts.</exception>
    public async Task<SavingsPlanActualReportResponse> ReportActualBalanceAsync(Guid id, Guid ownerUserId, decimal reportedBalance, CancellationToken ct)
    {
        var plan = await _db.SavingsPlans.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id && p.OwnerUserId == ownerUserId, ct);
        if (plan == null) { throw new ArgumentException("Savings plan not found", nameof(id)); }

        var hasLinkedAccounts = await _db.SavingsPlanAccountLinks.AsNoTracking()
            .AnyAsync(l => l.SavingsPlanId == id, ct);
        if (hasLinkedAccounts)
        {
            throw new InvalidOperationException("Actual-balance reports are only allowed for savings plans without linked accounts");
        }

        var today = DateTime.Today;
        var accumulated = await _db.Postings.AsNoTracking()
            .Where(p => p.SavingsPlanId == id && p.Kind == PostingKind.SavingsPlan && p.BookingDate <= today)
            .SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;
        var difference = reportedBalance - accumulated;

        // Booked at today's date so the correction is effective in the "current" view (which
        // includes postings with BookingDate <= today) immediately.
        var now = DateTime.UtcNow.Date;
        var posting = new Posting(
            id, // source: the plan itself is the origin of the correction
            PostingKind.SavingsPlan,
            null,
            null,
            id,
            null,
            now,
            now,
            difference,
            "Istmeldung",
            null,
            null,
            null,
            null)
            .SetSavingsPlanSubType(SavingsPlanPostingSubType.ActualReport);
        _db.Postings.Add(posting);
        await _db.SaveChangesAsync(ct);

        if (_aggregateService != null)
        {
            await _aggregateService.UpsertForPostingAsync(posting, ct);
            await _db.SaveChangesAsync(ct);
        }

        return new SavingsPlanActualReportResponse(posting.Id, difference, accumulated + difference);
    }
}