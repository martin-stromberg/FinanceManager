using FinanceManager.Application.Savings;
using FinanceManager.Domain.Savings;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Savings;

/// <summary>
/// Service implementing owner-scoped management of the explicit many-to-many links between
/// savings plans and bank accounts (see <see cref="ISavingsPlanAccountLinkService"/>).
/// </summary>
public sealed class SavingsPlanAccountLinkService : ISavingsPlanAccountLinkService
{
    private readonly AppDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="SavingsPlanAccountLinkService"/> class.
    /// </summary>
    /// <param name="db">The application's <see cref="AppDbContext"/> used to query and persist savings plan account links.</param>
    public SavingsPlanAccountLinkService(AppDbContext db) { _db = db; }

    /// <summary>
    /// Creates a link between a savings plan and an account of the same owner. Both entities are
    /// validated against <paramref name="ownerUserId"/>, so links between plan and account of
    /// different owners are rejected without revealing existence of foreign data.
    /// </summary>
    /// <param name="savingsPlanId">Identifier of the savings plan to link.</param>
    /// <param name="accountId">Identifier of the account to link.</param>
    /// <param name="ownerUserId">Identifier of the owner performing the operation.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the link has been persisted.</returns>
    /// <exception cref="ArgumentException">Thrown when the savings plan or the account does not exist or is not owned by <paramref name="ownerUserId"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the link already exists.</exception>
    public async Task AddLinkAsync(Guid savingsPlanId, Guid accountId, Guid ownerUserId, CancellationToken ct)
    {
        var planExists = await _db.SavingsPlans.AsNoTracking()
            .AnyAsync(p => p.Id == savingsPlanId && p.OwnerUserId == ownerUserId, ct);
        if (!planExists) { throw new ArgumentException("Savings plan not found", nameof(savingsPlanId)); }

        var accountType = await _db.Accounts.AsNoTracking()
            .Where(a => a.Id == accountId && a.OwnerUserId == ownerUserId)
            .Select(a => (FinanceManager.Shared.Dtos.Accounts.AccountType?)a.Type)
            .FirstOrDefaultAsync(ct);
        if (accountType is null) { throw new ArgumentException("Account not found", nameof(accountId)); }
        if (accountType != FinanceManager.Shared.Dtos.Accounts.AccountType.Savings)
        {
            throw new ArgumentException("Only savings accounts can be linked to a savings plan", nameof(accountId));
        }

        var duplicate = await _db.SavingsPlanAccountLinks.AsNoTracking()
            .AnyAsync(l => l.SavingsPlanId == savingsPlanId && l.AccountId == accountId, ct);
        if (duplicate) { throw new InvalidOperationException("Account already linked to this savings plan"); }

        _db.SavingsPlanAccountLinks.Add(new SavingsPlanAccountLink(savingsPlanId, accountId, ownerUserId));
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Removes the link between a savings plan and an account.
    /// </summary>
    /// <param name="savingsPlanId">Identifier of the savings plan.</param>
    /// <param name="accountId">Identifier of the account.</param>
    /// <param name="ownerUserId">Identifier of the owner performing the operation.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><c>true</c> when the link existed and was removed; otherwise <c>false</c> — including
    /// the case that the savings plan does not exist or does not belong to the owner.</returns>
    public async Task<bool> RemoveLinkAsync(Guid savingsPlanId, Guid accountId, Guid ownerUserId, CancellationToken ct)
    {
        var planExists = await _db.SavingsPlans.AsNoTracking()
            .AnyAsync(p => p.Id == savingsPlanId && p.OwnerUserId == ownerUserId, ct);
        if (!planExists) { return false; }

        var link = await _db.SavingsPlanAccountLinks
            .FirstOrDefaultAsync(l => l.SavingsPlanId == savingsPlanId && l.AccountId == accountId, ct);
        if (link == null) { return false; }

        _db.SavingsPlanAccountLinks.Remove(link);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>
    /// Lists the identifiers of all accounts linked to the specified savings plan.
    /// </summary>
    /// <param name="savingsPlanId">Identifier of the savings plan.</param>
    /// <param name="ownerUserId">Identifier of the owner performing the query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The linked account identifiers (possibly empty), or <c>null</c> when the savings
    /// plan does not exist or does not belong to <paramref name="ownerUserId"/>.</returns>
    public async Task<IReadOnlyList<Guid>?> GetLinkedAccountIdsAsync(Guid savingsPlanId, Guid ownerUserId, CancellationToken ct)
    {
        var planExists = await _db.SavingsPlans.AsNoTracking()
            .AnyAsync(p => p.Id == savingsPlanId && p.OwnerUserId == ownerUserId, ct);
        if (!planExists) { return null; }

        return await _db.SavingsPlanAccountLinks.AsNoTracking()
            .Where(l => l.SavingsPlanId == savingsPlanId)
            .Select(l => l.AccountId)
            .ToListAsync(ct);
    }

    /// <summary>
    /// Lists the identifiers of all savings plans linked to the specified account.
    /// </summary>
    /// <param name="accountId">Identifier of the account.</param>
    /// <param name="ownerUserId">Identifier of the owner performing the query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The linked savings plan identifiers (possibly empty), or <c>null</c> when the
    /// account does not exist or does not belong to <paramref name="ownerUserId"/>.</returns>
    public async Task<IReadOnlyList<Guid>?> GetLinkedSavingsPlanIdsAsync(Guid accountId, Guid ownerUserId, CancellationToken ct)
    {
        var accountExists = await _db.Accounts.AsNoTracking()
            .AnyAsync(a => a.Id == accountId && a.OwnerUserId == ownerUserId, ct);
        if (!accountExists) { return null; }

        return await _db.SavingsPlanAccountLinks.AsNoTracking()
            .Where(l => l.AccountId == accountId)
            .Select(l => l.SavingsPlanId)
            .ToListAsync(ct);
    }
}
