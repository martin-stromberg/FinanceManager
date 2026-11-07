using FinanceManager.Application.Accounts;
using FinanceManager.Application.Contacts;
using FinanceManager.Application.Mcp;
using FinanceManager.Application.Savings;
using FinanceManager.Application.Securities;
using FinanceManager.Application.Statements;
using FinanceManager.Shared.Dtos.Accounts;
using FinanceManager.Shared.Dtos.Contacts;
using FinanceManager.Shared.Dtos.SavingsPlans;
using FinanceManager.Shared.Dtos.Securities;
using FinanceManager.Shared.Dtos.Statements;

namespace FinanceManager.Web.Mcp;

/// <summary>
/// One fachlicher Befund of the §4.4 Zusatzregeln as emitted by the read tool
/// <c>fm_statement_draft_validate</c> (T14): same rule family as the throwing
/// <see cref="McpStatementDraftGuards"/> write guards, but collected as data instead
/// of aborting the call. <see cref="Code"/> uses the <c>MCP_</c> prefix so tool-level
/// findings stay distinguishable from pipeline codes of <c>ValidateAsync</c>;
/// <see cref="Severity"/> reuses the pipeline vocabulary (<c>Error</c>/<c>Warning</c>).
/// </summary>
/// <param name="Code">Stable rule code (<c>MCP_*</c>).</param>
/// <param name="Severity"><c>Error</c> or <c>Warning</c>.</param>
/// <param name="Message">System-generated finding text.</param>
internal sealed record McpRuleFinding(string Code, string Severity, string Message);

/// <summary>
/// MCP-specific fachliche Zusatzvalidierung of the statement-draft write tools (F-0704,
/// scope-und-toolkatalog.md §4.4): reference checks against owner-scoped master-data
/// contracts (a foreign id behaves like a missing record and is rejected), plausibility
/// rules for amounts/dates/text lengths and the assignment side conditions the import
/// pipeline enforces inside <c>SaveEntryAllAsync</c>/<c>SetEntrySplitDraftAsync</c>. All
/// checks run <b>before</b> any service effect so a rejected call leaves no half state;
/// violations surface as <c>Err_Mcp_InvalidArguments</c>/<c>Err_Mcp_InvalidState</c>.
/// </summary>
internal static class McpStatementDraftGuards
{
    /// <summary>Maximum accepted raw upload size for one statement file (Kestrel limit, 20 MB).</summary>
    internal const long MaxUploadBytes = 20L * 1024L * 1024L;

    /// <summary>Maximum accepted file-name length.</summary>
    internal const int MaxFileNameLength = 250;

    /// <summary>Maximum subject/booking-description length — MCP-side defense cap, the
    /// domain has no such limit (batch-update convention).</summary>
    internal const int MaxTextLength = 1000;

    /// <summary>Maximum recipient-name length — MCP-side defense cap, the domain has no
    /// such limit (batch-update convention).</summary>
    internal const int MaxRecipientNameLength = 250;

    /// <summary>Tolerated backwards distance between valuta date and booking date —
    /// the MCP-side plausibility convention covered by toolkatalog §4.4 Datumslogik;
    /// the domain itself does not relate the two dates.</summary>
    internal static readonly TimeSpan ValutaTolerance = TimeSpan.FromDays(7);

    /// <summary>
    /// Resolves a contact of the owner or rejects the call. A contact of another user is
    /// indistinguishable from a missing one — the reference is invalid either way (F-0704).
    /// </summary>
    /// <param name="contacts">Contact service contract.</param>
    /// <param name="contactId">Referenced contact id.</param>
    /// <param name="owner">Owner user id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The resolved contact.</returns>
    /// <exception cref="McpToolException">The reference cannot be resolved for the owner.</exception>
    public static async Task<ContactDto> RequireContactAsync(
        IContactService contacts, Guid contactId, Guid owner, CancellationToken ct)
        => await contacts.GetAsync(contactId, owner, ct)
           ?? throw new McpToolException("Err_Mcp_InvalidArguments",
               "The referenced contact does not exist or is not owned by the caller.");

    /// <summary>
    /// Resolves an active savings plan of the owner or rejects the call (F-0704).
    /// </summary>
    /// <param name="plans">Savings-plan service contract.</param>
    /// <param name="savingsPlanId">Referenced savings plan id.</param>
    /// <param name="owner">Owner user id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The resolved savings plan.</returns>
    /// <exception cref="McpToolException">Missing/foreign reference or archived plan.</exception>
    public static async Task<SavingsPlanDto> RequireSavingsPlanAsync(
        ISavingsPlanService plans, Guid savingsPlanId, Guid owner, CancellationToken ct)
    {
        var plan = await plans.GetAsync(savingsPlanId, owner, ct)
            ?? throw new McpToolException("Err_Mcp_InvalidArguments",
                "The referenced savings plan does not exist or is not owned by the caller.");
        if (plan.ArchivedUtc is not null)
        {
            throw new McpToolException("Err_Mcp_InvalidArguments",
                "The referenced savings plan is archived and cannot be assigned.");
        }
        return plan;
    }

    /// <summary>
    /// Resolves an active security of the owner or rejects the call (F-0704).
    /// </summary>
    /// <param name="securities">Security service contract.</param>
    /// <param name="securityId">Referenced security id.</param>
    /// <param name="owner">Owner user id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The resolved security.</returns>
    /// <exception cref="McpToolException">Missing/foreign reference or inactive security.</exception>
    public static async Task<SecurityDto> RequireSecurityAsync(
        ISecurityService securities, Guid securityId, Guid owner, CancellationToken ct)
    {
        var security = await securities.GetAsync(securityId, owner, ct)
            ?? throw new McpToolException("Err_Mcp_InvalidArguments",
                "The referenced security does not exist or is not owned by the caller.");
        if (!security.IsActive)
        {
            throw new McpToolException("Err_Mcp_InvalidArguments",
                "The referenced security is inactive and cannot be assigned.");
        }
        return security;
    }

    /// <summary>
    /// Resolves an account of the owner or rejects the call (F-0704).
    /// </summary>
    /// <param name="accounts">Account service contract.</param>
    /// <param name="accountId">Referenced account id.</param>
    /// <param name="owner">Owner user id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The resolved account.</returns>
    /// <exception cref="McpToolException">The reference cannot be resolved for the owner.</exception>
    public static async Task<AccountDto> RequireAccountAsync(
        IAccountService accounts, Guid accountId, Guid owner, CancellationToken ct)
        => await accounts.GetAsync(accountId, owner, ct)
           ?? throw new McpToolException("Err_Mcp_InvalidArguments",
               "The referenced account does not exist or is not owned by the caller.");

    /// <summary>
    /// Validates the correctable core fields of a draft entry (toolkatalog T18 field list)
    /// before <c>UpdateEntryCoreAsync</c>/<c>AddEntryAsync</c>. Only transport/defense
    /// checks run here: the MCP-side length caps, the currency-code shape convention and
    /// the §4.4 valuta-date plausibility. Value ranges stay with the domain — a zero
    /// amount is explicitly legal (<c>StatementEntry</c>: "Zero is allowed for neutral
    /// items") and an empty subject is persistable (<c>StatementDraftEntry.UpdateCore</c>);
    /// both are only surfaced as findings by the read tool's Befundliste.
    /// </summary>
    /// <param name="bookingDate">Effective booking date.</param>
    /// <param name="valutaDate">Effective valuta date (may be <see langword="null"/>).</param>
    /// <param name="amount">Effective amount.</param>
    /// <param name="subject">Effective subject.</param>
    /// <param name="recipientName">Effective recipient name.</param>
    /// <param name="currencyCode">Effective currency code (may be <see langword="null"/>).</param>
    /// <param name="bookingDescription">Effective booking description.</param>
    /// <exception cref="McpToolException">A transport/defense check is violated.</exception>
    public static void ValidateEntryCore(
        DateTime bookingDate, DateTime? valutaDate, decimal amount, string subject,
        string? recipientName, string? currencyCode, string? bookingDescription)
    {
        var findings = CollectEntryCoreFindings(
            bookingDate, valutaDate, amount, subject, recipientName, currencyCode,
            bookingDescription, forWrite: true);
        if (findings.Count > 0)
        {
            throw new McpToolException("Err_Mcp_InvalidArguments", findings[0].Message);
        }
    }

    /// <summary>
    /// Non-throwing variant of <see cref="ValidateEntryCore"/> for the T14 Befundliste
    /// (F-0704, §4.4): evaluates the same rules in the same order and returns one
    /// <see cref="McpRuleFinding"/> per violation instead of aborting at the first.
    /// Additionally reports the downstream-booking finding <c>MCP_SUBJECT_MISSING</c> —
    /// an empty subject is persistable but fails at booking time.
    /// </summary>
    /// <param name="bookingDate">Effective booking date.</param>
    /// <param name="valutaDate">Effective valuta date (may be <see langword="null"/>).</param>
    /// <param name="amount">Effective amount.</param>
    /// <param name="subject">Effective subject.</param>
    /// <param name="recipientName">Effective recipient name.</param>
    /// <param name="currencyCode">Effective currency code (may be <see langword="null"/>).</param>
    /// <param name="bookingDescription">Effective booking description.</param>
    /// <returns>The violated core-field rules (empty when the entry is plausible).</returns>
    public static IReadOnlyList<McpRuleFinding> CollectEntryCoreFindings(
        DateTime bookingDate, DateTime? valutaDate, decimal amount, string subject,
        string? recipientName, string? currencyCode, string? bookingDescription)
        => CollectEntryCoreFindings(
            bookingDate, valutaDate, amount, subject, recipientName, currencyCode,
            bookingDescription, forWrite: false);

    private static IReadOnlyList<McpRuleFinding> CollectEntryCoreFindings(
        DateTime bookingDate, DateTime? valutaDate, decimal amount, string subject,
        string? recipientName, string? currencyCode, string? bookingDescription, bool forWrite)
    {
        // No amount check at all: zero is a legal domain value (StatementEntry —
        // "Zero is allowed for neutral items"), the domain owns the value range.
        _ = amount;
        var findings = new List<McpRuleFinding>();
        if (!forWrite && string.IsNullOrWhiteSpace(subject))
        {
            // Persistable (UpdateCore stores an empty subject) but booking would fail —
            // the finding belongs to the validate Befundliste, not the write guard.
            findings.Add(new("MCP_SUBJECT_MISSING", "Error",
                "Subject is empty — the entry is persistable but booking will fail."));
        }
        // MCP-side defense caps, the domain has no such limits (batch-update convention).
        else if (subject is { Length: > MaxTextLength })
        {
            findings.Add(new("MCP_SUBJECT_TOO_LONG", "Error", $"Subject must not exceed {MaxTextLength} characters."));
        }
        if (bookingDescription is { Length: > MaxTextLength })
        {
            findings.Add(new("MCP_DESCRIPTION_TOO_LONG", "Error", $"BookingDescription must not exceed {MaxTextLength} characters."));
        }
        if (recipientName is { Length: > MaxRecipientNameLength })
        {
            findings.Add(new("MCP_RECIPIENT_TOO_LONG", "Error", $"RecipientName must not exceed {MaxRecipientNameLength} characters."));
        }
        // MCP-side convention: a present currency code must carry the ISO-4217 shape;
        // the domain accepts any string (or defaults to EUR).
        if (currencyCode is not null
            && (currencyCode.Length != 3 || !currencyCode.All(char.IsLetter)))
        {
            findings.Add(new("MCP_CURRENCY_INVALID", "Error", "CurrencyCode must be a 3-letter ISO-4217 code."));
        }
        // Datumslogik (§4.4): valuta date must not precede the booking date beyond the
        // tolerated booking-week distance.
        if (valutaDate.HasValue && valutaDate.Value.Date < bookingDate.Date - ValutaTolerance)
        {
            findings.Add(new("MCP_VALUTA_TOO_EARLY", "Error",
                "ValutaDate must not precede BookingDate by more than the tolerated booking week."));
        }
        return findings;
    }

    /// <summary>
    /// Collects the entry-level §4.4 Zusatzregeln that need no external lookup for the
    /// T14 Befundliste: fachlich gesperrte Entries (pipeline status
    /// <see cref="StatementDraftEntryStatus.AlreadyBooked"/>/<see cref="StatementDraftEntryStatus.Announced"/>),
    /// the Betrags-/Vorzeichen-Plausibilität of the assignment (a savings-plan
    /// contribution and a security <c>Buy</c> are outflows, <c>Sell</c>/<c>Dividend</c>
    /// are inflows on the detected account) and the archive-flag side condition
    /// (<c>ArchiveSavingsPlanOnBooking</c> requires an assigned savings plan).
    /// </summary>
    /// <param name="entry">The draft entry to evaluate.</param>
    /// <returns>The violated rules (empty when the entry raises no finding).</returns>
    public static IReadOnlyList<McpRuleFinding> CollectEntryFindings(StatementDraftEntryDto entry)
    {
        var findings = new List<McpRuleFinding>();

        // Dublettenreste/fachliche Sperren (§4.4): gesperrte Entries werden im Befund
        // ausgewiesen — nie stillschweigend mitgebucht.
        if (entry.Status == StatementDraftEntryStatus.AlreadyBooked)
        {
            findings.Add(new("MCP_ENTRY_ALREADY_BOOKED", "Error",
                "Entry is flagged AlreadyBooked by the pipeline duplicate detection — it is blocked for booking (reset via fm_statement_draft_edit)."));
        }
        else if (entry.Status == StatementDraftEntryStatus.Announced)
        {
            findings.Add(new("MCP_ENTRY_ANNOUNCED", "Warning",
                "Entry is announced; booking skips announced entries."));
        }

        // Betrags-/Vorzeichenkonsistenz (§4.4): a savings-plan contribution is an
        // outflow from the detected account (Einzahlung); a positive amount reads as
        // a plan payout. Security Buy is an outflow, Sell/Dividend an inflow.
        if (entry.SavingsPlanId is not null && entry.Amount > 0m)
        {
            findings.Add(new("MCP_SIGN_SAVINGSPLAN", "Warning",
                "Savings-plan assignments normally book as an outflow (negative amount); this positive amount looks like a plan payout — verify the assignment."));
        }
        if (entry.SecurityTransactionType == SecurityTransactionType.Buy && entry.Amount > 0m)
        {
            findings.Add(new("MCP_SIGN_SECURITY", "Warning",
                "A security Buy is expected as an outflow (negative amount) on the detected account."));
        }
        else if (entry.SecurityTransactionType is SecurityTransactionType.Sell or SecurityTransactionType.Dividend
                 && entry.Amount < 0m)
        {
            findings.Add(new("MCP_SIGN_SECURITY", "Warning",
                "A security Sell/Dividend is expected as an inflow (positive amount) on the detected account."));
        }

        // Nebenbedingung (§4.4): the archive flag only makes sense on an assigned plan.
        if (entry.ArchiveSavingsPlanOnBooking && entry.SavingsPlanId is null)
        {
            findings.Add(new("MCP_ARCHIVE_WITHOUT_PLAN", "Warning",
                "ArchiveSavingsPlanOnBooking is set but no savings plan is assigned — the flag has no effect."));
        }

        return findings;
    }

    /// <summary>
    /// Enforces the savings-plan assignment side conditions of the pipeline
    /// (<c>SaveEntryAllAsync</c>): a savings plan requires the effective contact to be the
    /// owner's self contact and a detected account that accepts savings plans.
    /// </summary>
    /// <param name="effectiveContact">Effective entry contact (may be <see langword="null"/>).</param>
    /// <param name="account">Effective detected account (may be <see langword="null"/>).</param>
    /// <exception cref="McpToolException">A side condition is violated.</exception>
    public static void ValidateSavingsPlanAssignment(ContactDto? effectiveContact, AccountDto? account)
    {
        if (effectiveContact is null)
        {
            throw new McpToolException("Err_Mcp_InvalidArguments",
                "A savings plan can only be assigned when the entry has a contact.");
        }
        if (effectiveContact.Type != ContactType.Self)
        {
            throw new McpToolException("Err_Mcp_InvalidArguments",
                "A savings plan can only be assigned when the entry contact is the own (Self) contact.");
        }
        if (account is null)
        {
            throw new McpToolException("Err_Mcp_InvalidArguments",
                "A savings plan can only be assigned when the draft has a detected account.");
        }
        if (account.SavingsPlanExpectation == SavingsPlanExpectation.None)
        {
            throw new McpToolException("Err_Mcp_InvalidArguments",
                "The detected account does not allow savings-plan assignments.");
        }
    }

    /// <summary>
    /// Enforces the security assignment side conditions of the pipeline
    /// (<c>SaveEntryAllAsync</c>): security fields require the effective contact to equal
    /// the bank contact of a security-enabled detected account.
    /// </summary>
    /// <param name="effectiveContact">Effective entry contact (may be <see langword="null"/>).</param>
    /// <param name="account">Effective detected account (may be <see langword="null"/>).</param>
    /// <exception cref="McpToolException">A side condition is violated.</exception>
    public static void ValidateSecurityAssignment(ContactDto? effectiveContact, AccountDto? account)
    {
        if (account is null)
        {
            throw new McpToolException("Err_Mcp_InvalidArguments",
                "Security fields can only be assigned when the draft has a detected account.");
        }
        if (!account.SecurityProcessingEnabled)
        {
            throw new McpToolException("Err_Mcp_InvalidArguments",
                "The detected account does not allow security processing.");
        }
        if (effectiveContact is null || effectiveContact.Id != account.BankContactId)
        {
            throw new McpToolException("Err_Mcp_InvalidArguments",
                "Security fields can only be assigned when the entry contact equals the bank contact of the detected account.");
        }
    }
}
