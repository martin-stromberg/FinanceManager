using FinanceManager.Domain.Postings;
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

    /// <summary>
    /// Reverses all preliminary (provisional) postings for the specified bank account.
    /// The original postings are zeroed and marked as reversed; a marker reversal posting
    /// is created for each group so that the traceability is preserved.
    /// </summary>
    /// <param name="accountId">Identifier of the bank account whose preliminary postings should be reversed.</param>
    /// <param name="ownerUserId">Owner user identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    private async Task ReversePreliminaryPostingsAsync(Guid accountId, Guid ownerUserId, CancellationToken ct)
    {
        var preliminaryBankPostings = await _db.Postings
            .Where(p => p.AccountId == accountId && p.Kind == PostingKind.Bank && p.IsPreliminary && !p.ReversedByPostingId.HasValue)
            .ToListAsync(ct);

        if (preliminaryBankPostings.Count == 0)
        {
            return;
        }

        var groupIds = preliminaryBankPostings.Select(p => p.GroupId).Distinct().ToList();
        var allGroupPostings = await _db.Postings
            .Where(p => groupIds.Contains(p.GroupId) && !p.ReversedByPostingId.HasValue)
            .ToListAsync(ct);

        foreach (var original in allGroupPostings)
        {
            var newGroupId = Guid.NewGuid();
            var reversal = new Posting(
                sourceId: Guid.NewGuid(),
                kind: original.Kind,
                accountId: original.AccountId,
                contactId: original.ContactId,
                savingsPlanId: original.SavingsPlanId,
                securityId: original.SecurityId,
                bookingDate: original.BookingDate,
                valutaDate: original.ValutaDate,
                amount: 0m,
                subject: original.Subject != null ? $"REVERSAL: {original.Subject}" : "REVERSAL",
                recipientName: original.RecipientName,
                description: original.Description,
                securitySubType: original.SecuritySubType,
                quantity: null)
                .SetGroup(newGroupId)
                .SetIsPreliminary(false);

            _db.Postings.Add(reversal);
            original.ZeroAmount();
            original.SetReversedBy(reversal, ownerUserId);
            reversal.SetReversalFor(original);

            await _aggregateService.UpsertForPostingAsync(reversal, ct);
            await _aggregateService.UpsertForPostingAsync(original, ct);
        }
    }
}
