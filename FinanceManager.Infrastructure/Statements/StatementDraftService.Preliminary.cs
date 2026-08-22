using FinanceManager.Domain.Statements;
using FinanceManager.Shared.Dtos.Statements;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace FinanceManager.Infrastructure.Statements;

public sealed partial class StatementDraftService
{
    /// <summary>
    /// Creates a new preliminary (provisional) statement draft for the specified account.
    /// </summary>
    /// <param name="ownerUserId">Owner user identifier.</param>
    /// <param name="accountId">Identifier of the bank account for which the preliminary draft is created.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created <see cref="StatementDraftDto"/>; otherwise <c>null</c> when the account does not exist or is not owned.</returns>
    public async Task<StatementDraftDto?> CreatePreliminaryDraftAsync(Guid ownerUserId, Guid accountId, CancellationToken ct = default)
    {
        var account = await _db.Accounts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == accountId && a.OwnerUserId == ownerUserId, ct);
        if (account == null)
        {
            return null;
        }

        var dateText = DateTime.Today.ToString("d", new CultureInfo("de-DE"));
        var draft = new StatementDraft(
            ownerUserId,
            "Preliminary",
            null,
            $"Vorl. Buchungen vom {dateText}");

        draft.SetDetectedAccount(accountId);
        draft.MarkAsPreliminary();

        draft.AddEntry(
            DateTime.Today,
            0m,
            string.Empty,
            null,
            DateTime.Today,
            null,
            null,
            false,
            false);

        _db.StatementDrafts.Add(draft);
        await _db.SaveChangesAsync(ct);

        return Map(draft);
    }
}
