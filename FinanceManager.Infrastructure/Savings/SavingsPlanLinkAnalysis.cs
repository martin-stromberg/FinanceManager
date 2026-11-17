using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Savings;

/// <summary>
/// Shared evaluation helpers for savings plans with account links
/// (<see cref="FinanceManager.Domain.Savings.SavingsPlanAccountLink"/>): the unified "current
/// amount" source selection (linked account balances vs. plan postings) and the derivation of
/// informal savings plan postings.
/// <para>
/// A savings plan posting is <b>informal</b> when its plan has at least one linked account and the
/// posting's group contains a <see cref="PostingKind.Bank"/> posting on an account that is NOT
/// linked to the plan - i.e. the posting only documents the funding source while the money flow
/// itself already lives on the linked contract account. Plan postings without a bank counterpart
/// posting (e.g. disbursements or actual-balance reports) and postings whose bank counterpart sits
/// on a linked contract account are never informal.
/// </para>
/// </summary>
public static class SavingsPlanLinkAnalysis
{
    /// <summary>
    /// Minimal posting reference used for the informal derivation.
    /// </summary>
    /// <param name="PostingId">Identifier of the savings plan posting.</param>
    /// <param name="SavingsPlanId">Identifier of the plan the posting belongs to.</param>
    /// <param name="GroupId">Posting group id (<see cref="Guid.Empty"/> when ungrouped).</param>
    /// <returns>The result.</returns>
    public sealed record PlanPostingRef(Guid PostingId, Guid SavingsPlanId, Guid GroupId);

    /// <summary>
    /// Returns the identifiers of all accounts linked to the given savings plan (empty when none).
    /// </summary>
    /// <param name="db">Database context.</param>
    /// <param name="savingsPlanId">Savings plan identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Linked account ids (possibly empty).</returns>
    public static async Task<IReadOnlyList<Guid>> GetLinkedAccountIdsAsync(AppDbContext db, Guid savingsPlanId, CancellationToken ct)
    {
        return await db.SavingsPlanAccountLinks.AsNoTracking()
            .Where(l => l.SavingsPlanId == savingsPlanId)
            .Select(l => l.AccountId)
            .ToListAsync(ct);
    }

    /// <summary>
    /// Computes the plan's "current" amount using the unified source selection: when the plan has
    /// linked accounts the sum of their <c>CurrentBalance</c> values is the only truth (additive,
    /// no splitting for collection accounts); otherwise the sum of plan postings is used.
    /// </summary>
    /// <param name="db">Database context.</param>
    /// <param name="savingsPlanId">Savings plan identifier.</param>
    /// <param name="postingsUpTo">
    /// Optional inclusive booking-date bound for the posting-based fallback
    /// (<c>null</c> sums all plan postings).
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The plan's current amount.</returns>
    public static async Task<decimal> GetCurrentAmountAsync(AppDbContext db, Guid savingsPlanId, DateTime? postingsUpTo, CancellationToken ct)
    {
        var linkedAccountIds = await GetLinkedAccountIdsAsync(db, savingsPlanId, ct);
        if (linkedAccountIds.Count > 0)
        {
            return await db.Accounts.AsNoTracking()
                .Where(a => linkedAccountIds.Contains(a.Id))
                .SumAsync(a => (decimal?)a.CurrentBalance, ct) ?? 0m;
        }

        var query = db.Postings.AsNoTracking()
            .Where(p => p.SavingsPlanId == savingsPlanId && p.Kind == PostingKind.SavingsPlan);
        if (postingsUpTo.HasValue)
        {
            var bound = postingsUpTo.Value.Date;
            query = query.Where(p => p.BookingDate <= bound);
        }
        return await query.SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;
    }

    /// <summary>
    /// Loads the linked account ids for a set of savings plans in one query.
    /// </summary>
    /// <param name="db">Database context.</param>
    /// <param name="savingsPlanIds">Savings plan identifiers.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Map from plan id to the set of linked account ids (only plans with links are present).</returns>
    public static async Task<Dictionary<Guid, HashSet<Guid>>> GetLinkedAccountsByPlanAsync(AppDbContext db, IReadOnlyCollection<Guid> savingsPlanIds, CancellationToken ct)
    {
        var map = new Dictionary<Guid, HashSet<Guid>>();
        if (savingsPlanIds.Count == 0) { return map; }
        var links = await db.SavingsPlanAccountLinks.AsNoTracking()
            .Where(l => savingsPlanIds.Contains(l.SavingsPlanId))
            .Select(l => new { l.SavingsPlanId, l.AccountId })
            .ToListAsync(ct);
        foreach (var l in links)
        {
            if (!map.TryGetValue(l.SavingsPlanId, out var set))
            {
                set = new HashSet<Guid>();
                map[l.SavingsPlanId] = set;
            }
            set.Add(l.AccountId);
        }
        return map;
    }

    /// <summary>
    /// Loads the account ids of all <see cref="PostingKind.Bank"/> postings contained in the given
    /// posting groups in one query.
    /// </summary>
    /// <param name="db">Database context.</param>
    /// <param name="groupIds">Posting group ids.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Map from group id to the bank posting account ids of that group.</returns>
    public static async Task<Dictionary<Guid, List<Guid>>> GetGroupBankAccountIdsAsync(AppDbContext db, IReadOnlyCollection<Guid> groupIds, CancellationToken ct)
    {
        var map = new Dictionary<Guid, List<Guid>>();
        if (groupIds.Count == 0) { return map; }
        var rows = await db.Postings.AsNoTracking()
            .Where(p => p.Kind == PostingKind.Bank && p.AccountId != null && groupIds.Contains(p.GroupId))
            .Select(p => new { p.GroupId, AccountId = p.AccountId!.Value })
            .ToListAsync(ct);
        foreach (var r in rows)
        {
            if (!map.TryGetValue(r.GroupId, out var list))
            {
                list = new List<Guid>();
                map[r.GroupId] = list;
            }
            list.Add(r.AccountId);
        }
        return map;
    }

    /// <summary>
    /// Derives whether a single savings plan posting is informal: the plan must have at least one
    /// linked account and the posting's group must contain a bank posting on an account that is
    /// not linked to the plan.
    /// </summary>
    /// <param name="savingsPlanId">Plan the posting belongs to.</param>
    /// <param name="groupId">Posting group id.</param>
    /// <param name="linkedAccountsByPlan">Link map from <see cref="GetLinkedAccountsByPlanAsync"/>.</param>
    /// <param name="groupBankAccountIds">Group map from <see cref="GetGroupBankAccountIdsAsync"/>.</param>
    /// <returns><c>true</c> when the posting is an informal funding posting.</returns>
    public static bool IsInformalPosting(
        Guid savingsPlanId,
        Guid groupId,
        IReadOnlyDictionary<Guid, HashSet<Guid>> linkedAccountsByPlan,
        IReadOnlyDictionary<Guid, List<Guid>> groupBankAccountIds)
    {
        if (!linkedAccountsByPlan.TryGetValue(savingsPlanId, out var linked) || linked.Count == 0)
        {
            return false;
        }
        if (groupId == Guid.Empty || !groupBankAccountIds.TryGetValue(groupId, out var bankAccountIds))
        {
            return false;
        }
        return bankAccountIds.Any(a => !linked.Contains(a));
    }

    /// <summary>
    /// Determines the ids of the informal postings among the given savings plan posting candidates
    /// (kind <see cref="PostingKind.SavingsPlan"/>). Two lookups regardless of candidate count.
    /// </summary>
    /// <param name="db">Database context.</param>
    /// <param name="postings">Candidate savings plan postings.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Set of posting ids that are informal.</returns>
    public static async Task<HashSet<Guid>> GetInformalPostingIdsAsync(AppDbContext db, IReadOnlyCollection<PlanPostingRef> postings, CancellationToken ct)
    {
        var result = new HashSet<Guid>();
        if (postings.Count == 0) { return result; }

        var planIds = postings.Select(p => p.SavingsPlanId).Distinct().ToList();
        var linkedByPlan = await GetLinkedAccountsByPlanAsync(db, planIds, ct);
        if (linkedByPlan.Count == 0) { return result; }

        var groupIds = postings
            .Where(p => linkedByPlan.ContainsKey(p.SavingsPlanId) && p.GroupId != Guid.Empty)
            .Select(p => p.GroupId)
            .Distinct()
            .ToList();
        if (groupIds.Count == 0) { return result; }

        var groupBankAccounts = await GetGroupBankAccountIdsAsync(db, groupIds, ct);
        foreach (var p in postings)
        {
            if (IsInformalPosting(p.SavingsPlanId, p.GroupId, linkedByPlan, groupBankAccounts))
            {
                result.Add(p.PostingId);
            }
        }
        return result;
    }
}
