using FinanceManager.Domain.Contacts;
using FinanceManager.Domain.Savings;
using FinanceManager.Domain.Statements;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace FinanceManager.Infrastructure.Statements;

public sealed partial class StatementDraftService
{
    /// <summary>
    /// Normalizes German umlauts in the provided text to ASCII-friendly replacements.
    /// Example: "ä" -> "ae", "ß" -> "ss".
    /// </summary>
    /// <param name="text">Input text to normalize. May be <c>null</c> or empty.</param>
    /// <returns>A new string with umlauts replaced, or an empty string when <paramref name="text"/> is null or empty.</returns>
    private static string NormalizeUmlauts(string text)
    {
        if (string.IsNullOrEmpty(text)) { return string.Empty; }
        return text
            .Replace("ä", "ae", StringComparison.OrdinalIgnoreCase)
            .Replace("ö", "oe", StringComparison.OrdinalIgnoreCase)
            .Replace("ü", "ue", StringComparison.OrdinalIgnoreCase)
            .Replace("ß", "ss", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Attempts to auto-assign a savings plan to the provided draft entry when the entry originates from the user's own account.
    /// The method compares normalized subject text and contract numbers against available user plans.
    /// </summary>
    /// <param name="entry">The draft entry to attempt assignment for. Must not be <c>null</c>.</param>
    /// <param name="userPlans">Collection of the user's active savings plans to match against. Must not be <c>null</c>.</param>
    /// <param name="selfContact">The owner's self contact used to detect owner postings. Must not be <c>null</c>.</param>
    /// <remarks>
    /// When a single matching plan is found the entry will be assigned to that plan. If multiple matches are found
    /// the entry will be flagged for manual review via <see cref="StatementDraftEntry.MarkNeedsCheck"/>.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="entry"/>, <paramref name="userPlans"/> or <paramref name="selfContact"/> is <c>null</c>.</exception>
    public void TryAutoAssignSavingsPlan(StatementDraftEntry entry, IEnumerable<SavingsPlan> userPlans, Contact selfContact)
    {
        if (entry == null) throw new ArgumentNullException(nameof(entry));
        if (userPlans == null) throw new ArgumentNullException(nameof(userPlans));
        if (selfContact == null) throw new ArgumentNullException(nameof(selfContact));

        if (entry.ContactId is null) { return; }
        if (entry.ContactId != selfContact.Id) { return; }

        string Clean(string s) => Regex.Replace(s ?? string.Empty, "\\s+", string.Empty);

        var normalizedSubject = NormalizeUmlauts(entry.Subject).ToLowerInvariant();
        var normalizedSubjectNoSpaces = Clean(normalizedSubject);

        var matchingPlans = userPlans.Where(plan =>
        {
            if (string.IsNullOrWhiteSpace(plan.Name)) { return false; }
            var normalizedPlanName = Clean(NormalizeUmlauts(plan.Name).ToLowerInvariant());

            bool nameMatches = normalizedSubjectNoSpaces.Contains(normalizedPlanName);
            bool contractMatches = false;

            if (!nameMatches && !string.IsNullOrWhiteSpace(plan.ContractNumber))
            {
                var cn = plan.ContractNumber.Trim();
                var subjectForContract = Regex.Replace(entry.Subject ?? string.Empty, "[\\s-]", string.Empty, RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
                var cnNormalized = Regex.Replace(cn, "[\\s-]", string.Empty, RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
                contractMatches = subjectForContract.Contains(cnNormalized, StringComparison.OrdinalIgnoreCase);
            }
            return (nameMatches || contractMatches);
        }).ToList();

        if (matchingPlans.FirstOrDefault() is SavingsPlan plan)
            entry.AssignSavingsPlan(plan.Id);
        if (matchingPlans.Count > 1)
            entry.MarkNeedsCheck();
    }

    /// <summary>
    /// Re-evaluates the status of a parent draft entry that was previously split into a child draft.
    /// Ensures the parent entry's accounted status matches the sum of the assigned split entries.
    /// </summary>
    /// <param name="ownerUserId">The owner user identifier used to scope drafts.</param>
    /// <param name="splitDraftId">The draft id of the split/assigned draft whose parent should be re-evaluated.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the re-evaluation and any changes have been persisted.</returns>
    private async Task ReevaluateParentEntryStatusAsync(Guid ownerUserId, Guid splitDraftId, CancellationToken ct)
    {
        var parentEntry = await _db.StatementDraftEntries.FirstOrDefaultAsync(e => e.SplitDraftId == splitDraftId, ct);
        if (parentEntry == null) { return; }
        var parentDraft = await _db.StatementDrafts.Include(d => d.Entries).FirstOrDefaultAsync(d => d.Id == parentEntry.DraftId && d.OwnerUserId == ownerUserId, ct);
        if (parentDraft == null) { return; }
        var assignedDraft = await _db.StatementDrafts.FirstOrDefaultAsync(d => d.Id == splitDraftId && d.OwnerUserId == ownerUserId, ct);
        if (parentDraft == null) { return; }
        var assignedDrafts = await _db.StatementDrafts.Where(d => (assignedDraft.UploadGroupId != null &&  d.UploadGroupId == assignedDraft.UploadGroupId) || (d.Id == assignedDraft.Id)).Select(d => d.Id).ToListAsync(ct);
        var total = await _db.StatementDraftEntries.Where(e => assignedDrafts.Contains(e.DraftId)).SumAsync(e => e.Amount, ct);
        if (total == parentEntry.Amount && parentEntry.ContactId != null && parentEntry.Status != StatementDraftEntryStatus.Accounted)
        {
            parentEntry.MarkAccounted(parentEntry.ContactId.Value);
        }
        else if (total != parentEntry.Amount && parentEntry.Status == StatementDraftEntryStatus.Accounted)
        {
            parentEntry.ResetOpen();
            if (parentEntry.ContactId != null)
            {
                parentEntry.AssignContactWithoutAccounting(parentEntry.ContactId.Value);
            }
        }
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Classifies header information and entries for a draft. This includes auto-assignment of contacts, savings plans and securities
    /// based on heuristics and existing data in the database.
    /// </summary>
    /// <param name="draft">The draft to classify. Must not be <c>null</c>.</param>
    /// <param name="entryId">Optional specific entry id to limit classification to a single entry.</param>
    /// <param name="ownerUserId">Owner user identifier used to scope lookups.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when classification has finished and changes are persisted.</returns>
    private async Task ClassifyInternalAsync(StatementDraft draft, Guid? entryId, Guid ownerUserId, CancellationToken ct)
    {
        await ClassifyHeader(draft, ownerUserId, ct);

        var entries = await _db.StatementDraftEntries.Where(e => e.DraftId == draft.Id && (entryId == null || e.Id == entryId)).ToListAsync(ct);

        var contacts = await _db.Contacts.AsNoTracking()
            .Where(c => c.OwnerUserId == ownerUserId)
            .ToListAsync(ct);
        var selfContact = contacts.First(c => c.Type == ContactType.Self);
        var aliasNames = await _db.AliasNames.AsNoTracking()
            .Where(a => contacts.Select(c => c.Id).Contains(a.ContactId))
            .ToListAsync(ct);
        var aliasLookup = aliasNames
            .GroupBy(a => a.ContactId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Pattern).ToList());
        var savingPlans = await _db.SavingsPlans.AsNoTracking()
            .Where(sp => sp.OwnerUserId == ownerUserId && sp.IsActive)
            .ToListAsync(ct);

        // Securities für Auto-Matching laden (nur aktive)
        var securities = await _db.Securities.AsNoTracking()
            .Where(s => s.OwnerUserId == ownerUserId && s.IsActive)
            .OrderBy(s => s.Name)
            .ToListAsync(ct);

        // Duplicate detection: consider existing Bank postings and historical StatementEntries
        List<(DateTime BookingDate, decimal Amount, string Subject)> existing = new();
        DateTime? oldest = await _db.StatementDraftEntries.AsNoTracking()
                .Where(e => e.DraftId == draft.Id)
                .MinAsync(e => (DateTime?)e.BookingDate, ct);
        if (oldest.HasValue)
        {
            var since = oldest.Value.Date;
            if (draft.DetectedAccountId != null)
            {
                var bankPosts = await _db.Postings.AsNoTracking()
                    .Where(p => p.Kind == PostingKind.Bank)
                    .Where(p => p.AccountId == draft.DetectedAccountId)
                    .Where(p => p.BookingDate >= since)
                    .Select(p => new { p.BookingDate, p.Amount, p.Subject })
                    .ToListAsync(ct);
                existing.AddRange(bankPosts.Select(x => (x.BookingDate.Date, x.Amount, x.Subject)));
            }

            // Also check StatementEntries (regardless of account), as they represent already imported statements
            var histEntries = await _db.StatementEntries.AsNoTracking()
                .Where(se => se.BookingDate >= since)
                .Select(se => new { se.BookingDate, se.Amount, se.Subject })
                .ToListAsync(ct);
            existing.AddRange(histEntries.Select(x => (x.BookingDate.Date, x.Amount, x.Subject)));
        }


        Domain.Accounts.Account? bankAccount = null;
        Guid? bankContactId = null;
        if (draft.DetectedAccountId != null)
        {
            bankAccount = await _db.Accounts
                .FirstOrDefaultAsync(a => a.Id == draft.DetectedAccountId, ct);
            bankContactId = bankAccount.BankContactId;
        }

        foreach (var entry in entries)
        {
            if (entry.Status != StatementDraftEntryStatus.AlreadyBooked)
            {
                entry.ResetOpen();
            }

            if (existing.Any(x => x.BookingDate == entry.BookingDate.Date && x.Amount == entry.Amount && string.Equals(x.Subject, entry.Subject, StringComparison.OrdinalIgnoreCase)))
            {
                entry.MarkAlreadyBooked();
                continue;
            }

            if (entry.IsAnnounced)
            {
                // keep announced unless fully accounted
                continue;
            }

            TryAutoAssignContact(contacts, aliasLookup, bankContactId, selfContact, entry);
            if (bankAccount is not null && bankAccount.SavingsPlanExpectation != SavingsPlanExpectation.None)
                TryAutoAssignSavingsPlan(entry, savingPlans, selfContact);
            TryAutoAssignSecurity(securities, contacts, bankContactId, entry);
        }

        static void TryAutoAssignSecurity(IEnumerable<Domain.Securities.Security> securities, List<Contact> contacts, Guid? bankContactId, StatementDraftEntry entry)
        {
            if (entry.ContactId is not null && entry.ContactId != bankContactId)
            {
                return;
            }

            // Helper zur Normalisierung (nur A-Z/0-9, Großschreibung, Umlaute vereinheitlichen)
            static string NormalizeForSecurityMatch(string? s)
            {
                var baseText = NormalizeUmlauts(s ?? string.Empty).ToUpperInvariant();
                return Regex.Replace(baseText, "[^A-Z0-9]", string.Empty, RegexOptions.CultureInvariant | RegexOptions.Compiled);
            }

            // Automatisierte Wertpapierzuordnung:
            // - Nur wenn bisher kein Wertpapier gesetzt ist
            // - Match anhand Identifier, AlphaVantageCode oder Name, die im Betreff / Beschreibung / Empfänger vorkommen
            if (!securities.Any())
            {
                return;
            }
            var rawText = $"{entry.Subject} {entry.BookingDescription} {entry.RecipientName}";
            var haystack = NormalizeForSecurityMatch(rawText);

            bool Matches(string? probe)
            {
                var p = NormalizeForSecurityMatch(probe);
                if (string.IsNullOrEmpty(p)) { return false; }
                return haystack.Contains(p, StringComparison.Ordinal);
            }

            var matched = securities
                .Where(s =>
                    Matches(s.Identifier) ||
                    Matches(s.Name))
                .ToList();

            if (matched.Count == 1)
            {
                entry.SetSecurity(matched[0].Id, entry.SecurityTransactionType, entry.SecurityQuantity, entry.SecurityFeeAmount, entry.SecurityTaxAmount);
            }
            else if (matched.Count > 1)
            {
                var first = matched.First();
                entry.SetSecurity(first.Id, entry.SecurityTransactionType, entry.SecurityQuantity, entry.SecurityFeeAmount, entry.SecurityTaxAmount);
                entry.ResetOpen();
            }
            else
            {
                entry.SetSecurity(null, null, null, null, null);
            }
        }
    }

    /// <summary>
    /// Attempts to match and assign a contact to the provided draft entry based on name, aliases and heuristics.
    /// If a match is found the entry is marked as accounted or assigned accordingly.
    /// </summary>
    /// <param name="contacts">List of contacts belonging to the owner to search in.</param>
    /// <param name="aliasLookup">Precomputed dictionary of alias patterns per contact id.</param>
    /// <param name="bankContactId">Optional bank contact id used for internal bank postings.</param>
    /// <param name="selfContact">The owner's self contact used for special rules.</param>
    /// <param name="entry">The draft entry that should be assigned. Must not be <c>null</c>.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="contacts"/>, <paramref name="aliasLookup"/>, or <paramref name="entry"/> is <c>null</c>.</exception>
    private static void TryAutoAssignContact(List<Contact> contacts, Dictionary<Guid, List<string>> aliasLookup, Guid? bankContactId, Contact selfContact, StatementDraftEntry entry)
    {
        if (contacts == null) throw new ArgumentNullException(nameof(contacts));
        if (aliasLookup == null) throw new ArgumentNullException(nameof(aliasLookup));
        if (entry == null) throw new ArgumentNullException(nameof(entry));

        var normalizedRecipient = NormalizeUmlauts((entry.RecipientName ?? string.Empty).ToLowerInvariant().TrimEnd());
        Guid? matchedContactId = AssignContact(contacts, aliasLookup, bankContactId, entry, normalizedRecipient);
        var matchedContact = contacts.FirstOrDefault(c => c.Id == matchedContactId);
        if (matchedContact != null && matchedContact.IsPaymentIntermediary)
        {
            var normalizedSubject = (entry.Subject ?? string.Empty).ToLowerInvariant().TrimEnd();
            matchedContactId = AssignContact(contacts, aliasLookup, bankContactId, entry, normalizedSubject);
        }
        else if (matchedContact != null && matchedContact.Type == ContactType.Bank && bankContactId != null && matchedContact.Id != bankContactId)
        {
            entry.MarkCostNeutral(true);
            entry.MarkAccounted(selfContact.Id);
        }
        else if (matchedContact != null)
        {
            if (matchedContact.Id == selfContact.Id)
            {
                entry.MarkCostNeutral(true);
            }
            entry.MarkAccounted(matchedContact.Id);
        }
    }

    /// <summary>
    /// Attempts to find a matching contact id for the provided search text using exact/contains matches and alias patterns.
    /// When a matching contact id is determined the entry may be marked/accounted by the caller.
    /// </summary>
    /// <param name="contacts">List of contacts to search in.</param>
    /// <param name="aliasLookup">Dictionary mapping contact ids to alias patterns.</param>
    /// <param name="bankContactId">Optional bank contact id used to automatically account bank-related postings when recipient is empty.</param>
    /// <param name="entry">The draft entry used to decide default behaviors when matches are found.</param>
    /// <param name="searchText">Normalized search text used for matching.</param>
    /// <returns>The matched contact id when found; otherwise <c>null</c>.</returns>
    private static Guid? AssignContact(
        List<Contact> contacts,
        Dictionary<Guid, List<string>> aliasLookup,
        Guid? bankContactId,
        StatementDraftEntry entry,
        string searchText)
    {
        Guid? matchedContactId = contacts
            .Where(c => string.Equals(NormalizeUmlauts(c.Name), searchText, StringComparison.OrdinalIgnoreCase))
            .Select(c => c.Id)
            .FirstOrDefault();
        Guid? secondaryContactId = matchedContactId = contacts
                .Where(c => searchText.Contains(NormalizeUmlauts(c.Name), StringComparison.OrdinalIgnoreCase))
                .Select(c => c.Id)
                .FirstOrDefault();

        for (int idxMode = 0; idxMode < 2; idxMode++)
        {
            if (matchedContactId != Guid.Empty) { break; }

            if (idxMode == 1)
            {
                searchText = Regex.Replace(searchText, "\\s+", string.Empty);
            }

            foreach (var kvp in aliasLookup)
            {
                foreach (var pattern in kvp.Value.Select(val => val.ToLowerInvariant()))
                {
                    if (string.IsNullOrWhiteSpace(pattern)) { continue; }
                    var regexPattern = "^" + Regex.Escape(pattern)
                        .Replace("\\*", ".*")
                        .Replace("\\?", ".") + "$";
                    if (Regex.IsMatch(searchText, regexPattern, RegexOptions.IgnoreCase))
                    {
                        matchedContactId = kvp.Key;
                        break;
                    }
                }
                if (matchedContactId != Guid.Empty) { break; }
            }
        }
        if (matchedContactId == null || matchedContactId == Guid.Empty) { matchedContactId = secondaryContactId; }

        var matchedContact = contacts.FirstOrDefault(c => c.Id == matchedContactId);
        if (string.IsNullOrWhiteSpace(entry.RecipientName) && bankContactId != null && bankContactId != Guid.Empty)
        {
            entry.MarkAccounted(bankContactId.Value);
        }
        else if (matchedContactId != null && matchedContactId != Guid.Empty)
        {
            if (matchedContact != null && matchedContact.IsPaymentIntermediary)
            {
                entry.AssignContactWithoutAccounting(matchedContact.Id);
            }
            else
            {
                entry.MarkAccounted(matchedContactId.Value);
            }
        }

        return matchedContactId;
    }

    /// <summary>
    /// Classifies the draft header to detect the account id when possible (IBAN or single account scenarios).
    /// </summary>
    /// <param name="draft">The draft to classify.</param>
    /// <param name="ownerUserId">Owner user identifier used to scope account lookups.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when header classification has finished.</returns>
    private async Task ClassifyHeader(StatementDraft draft, Guid ownerUserId, CancellationToken ct)
    {
        if ((draft.DetectedAccountId == null) && (!string.IsNullOrWhiteSpace(draft.AccountName)))
        {
            var account = await _db.Accounts.AsNoTracking()
                .Where(a => a.OwnerUserId == ownerUserId && (a.Iban == draft.AccountName))
                .Select(a => new { a.Id })
                .FirstOrDefaultAsync(ct);
            if (account is null)
            {
                var simAccounts = await _db.Accounts.AsNoTracking()
                    .Where(a => a.OwnerUserId == ownerUserId && (a.Iban.EndsWith(draft.AccountName)))
                    .Select(a => new { a.Id })
                    .ToListAsync(ct);
                account = simAccounts.Count == 1 ? simAccounts.First() : null;
            }
            if (account != null)
            {
                draft.SetDetectedAccount(account.Id);
            }
        }
        if (draft.DetectedAccountId == null && string.IsNullOrWhiteSpace(draft.AccountName))
        {
            var singleAccountId = await _db.Accounts.AsNoTracking()
                .Where(a => a.OwnerUserId == ownerUserId)
                .Select(a => a.Id)
                .ToListAsync(ct);
            if (singleAccountId.Count == 1)
            {
                draft.SetDetectedAccount(singleAccountId[0]);
            }
        }
    }
}
