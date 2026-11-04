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

    /// <summary>Maximum subject/booking-description length (batch-update convention).</summary>
    internal const int MaxTextLength = 1000;

    /// <summary>Maximum recipient-name length (batch-update convention).</summary>
    internal const int MaxRecipientNameLength = 250;

    /// <summary>Tolerated backwards distance between valuta date and booking date (§4.4 Datumslogik).</summary>
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
    /// before <c>UpdateEntryCoreAsync</c>/<c>AddEntryAsync</c>: non-zero amount, non-empty
    /// subject, valuta-date plausibility and the API text-length limits.
    /// </summary>
    /// <param name="bookingDate">Effective booking date.</param>
    /// <param name="valutaDate">Effective valuta date (may be <see langword="null"/>).</param>
    /// <param name="amount">Effective amount.</param>
    /// <param name="subject">Effective subject.</param>
    /// <param name="recipientName">Effective recipient name.</param>
    /// <param name="currencyCode">Effective currency code (may be <see langword="null"/>).</param>
    /// <param name="bookingDescription">Effective booking description.</param>
    /// <exception cref="McpToolException">A plausibility rule is violated.</exception>
    public static void ValidateEntryCore(
        DateTime bookingDate, DateTime? valutaDate, decimal amount, string subject,
        string? recipientName, string? currencyCode, string? bookingDescription)
    {
        if (amount == 0m)
        {
            throw new McpToolException("Err_Mcp_InvalidArguments", "Amount must not be zero.");
        }
        if (string.IsNullOrWhiteSpace(subject))
        {
            throw new McpToolException("Err_Mcp_InvalidArguments", "Subject is required.");
        }
        if (subject.Length > MaxTextLength)
        {
            throw new McpToolException("Err_Mcp_InvalidArguments", $"Subject must not exceed {MaxTextLength} characters.");
        }
        if (bookingDescription is { Length: > MaxTextLength })
        {
            throw new McpToolException("Err_Mcp_InvalidArguments", $"BookingDescription must not exceed {MaxTextLength} characters.");
        }
        if (recipientName is { Length: > MaxRecipientNameLength })
        {
            throw new McpToolException("Err_Mcp_InvalidArguments", $"RecipientName must not exceed {MaxRecipientNameLength} characters.");
        }
        if (currencyCode is not null
            && (currencyCode.Length != 3 || !currencyCode.All(char.IsLetter)))
        {
            throw new McpToolException("Err_Mcp_InvalidArguments", "CurrencyCode must be a 3-letter ISO-4217 code.");
        }
        // Datumslogik (§4.4): valuta date must not precede the booking date beyond the
        // tolerated booking-week distance.
        if (valutaDate.HasValue && valutaDate.Value.Date < bookingDate.Date - ValutaTolerance)
        {
            throw new McpToolException("Err_Mcp_InvalidArguments",
                "ValutaDate must not precede BookingDate by more than the tolerated booking week.");
        }
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
