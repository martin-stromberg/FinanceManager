using FinanceManager.Application.Accounts;
using FinanceManager.Application.Contacts;
using FinanceManager.Application.Mcp;
using FinanceManager.Application.Savings;
using FinanceManager.Application.Securities;
using FinanceManager.Application.Statements;
using FinanceManager.Domain.Mcp;
using FinanceManager.Shared.Dtos.Accounts;
using FinanceManager.Shared.Dtos.Statements;
using System.Text.Json;

namespace FinanceManager.Web.Mcp.Tools;

/// <summary>
/// Descriptor of the read tool <c>fm_statement_draft_validate</c> (T14): plausibility check
/// of a statement draft or a single entry — returns the pipeline
/// <c>DraftValidationResultDto</c> (warnings/errors) plus the fachliche Zusatzregeln of
/// toolkatalog §4.4 as a findings list. Scope: (Kontoauszugsentwürfe, Lesen).
/// </summary>
public sealed class FmStatementDraftValidateDescriptor : McpToolDescriptorBase
{
    /// <summary>Tool name.</summary>
    public const string ToolName = "fm_statement_draft_validate";

    /// <summary>Creates the descriptor.</summary>
    public FmStatementDraftValidateDescriptor() : base(
        "{\"type\":\"object\",\"properties\":{\"draftId\":{\"type\":\"string\",\"format\":\"uuid\",\"description\":\"Draft to validate (required).\"},\"entryId\":{\"type\":\"string\",\"format\":\"uuid\",\"description\":\"Optional single entry restriction.\"}},\"required\":[\"draftId\"],\"additionalProperties\":false}")
    { }

    /// <inheritdoc />
    public override string Name => ToolName;

    /// <inheritdoc />
    public override string Description
        => "Validates a statement draft (or one entry): pipeline validation result with warnings/errors plus the §4.4 additional findings (amount/sign plausibility, period overlap with booked imports, split-group sums, unresolved references, locked entries). Scope: (Kontoauszugsentwürfe, Lesen).";

    /// <inheritdoc />
    public override McpToolSecurityClass SecurityClass => McpToolSecurityClass.Lesend;

    /// <inheritdoc />
    public override IReadOnlyList<McpToolScope> RequiredScopes => new[]
    {
        new McpToolScope(McpDataType.StatementDrafts, McpRightKind.Read)
    };
}

/// <summary>
/// Handler of <c>fm_statement_draft_validate</c>: adapts
/// <see cref="IStatementDraftService.ValidateAsync"/> and appends the §4.4
/// Zusatzregeln (F-0704) as a Befundliste — the same rule family the write tools
/// enforce via <see cref="McpStatementDraftGuards"/>, collected here as data instead
/// of aborting: core-field plausibility, amount/sign consistency of the assignment
/// (savings-plan deposit vs. payout, security buy/sell/dividend), fachlich gesperrte
/// entries (<c>AlreadyBooked</c>/<c>Announced</c>), unresolved or unusable references
/// (foreign contact, archived savings plan, inactive security, missing split draft),
/// split-group sum consistency (<see cref="IStatementDraftService.GetSplitGroupSumAsync"/>)
/// and the Zeitraum-Überlappung against already committed statement-import periods of
/// the detected account (<see cref="IStatementImportQueryService"/>). Reference and
/// overlap checks run only when the matching read scopes are granted (reported under
/// <c>degraded</c> otherwise) — the declared tool scope stays
/// (Kontoauszugsentwürfe, Lesen). Validation messages and finding texts are
/// system-generated; they can embed record fragments and are therefore emitted inside
/// the marked <c>untrusted_data</c> wrapper. A foreign or filtered draft yields a null
/// result (no forbidden/not-found distinction).
/// </summary>
public sealed class FmStatementDraftValidateHandler : IMcpToolHandler
{
    private readonly IStatementDraftService _drafts;
    private readonly IContactService _contacts;
    private readonly ISavingsPlanService _savingsPlans;
    private readonly ISecurityService _securities;
    private readonly IAccountService _accounts;
    private readonly IStatementImportQueryService _imports;
    private readonly IMcpScopeEvaluator _scopeEvaluator;
    private readonly McpRecordScopeFilter _recordFilter;

    /// <summary>Creates the handler.</summary>
    public FmStatementDraftValidateHandler(
        IStatementDraftService drafts,
        IContactService contacts,
        ISavingsPlanService savingsPlans,
        ISecurityService securities,
        IAccountService accounts,
        IStatementImportQueryService imports,
        IMcpScopeEvaluator scopeEvaluator,
        McpRecordScopeFilter recordFilter)
    {
        _drafts = drafts;
        _contacts = contacts;
        _savingsPlans = savingsPlans;
        _securities = securities;
        _accounts = accounts;
        _imports = imports;
        _scopeEvaluator = scopeEvaluator;
        _recordFilter = recordFilter;
    }

    /// <inheritdoc />
    public string ToolName => FmStatementDraftValidateDescriptor.ToolName;

    /// <inheritdoc />
    public async Task<object> InvokeAsync(JsonElement arguments, McpToolCallContext context, CancellationToken ct)
    {
        var owner = context.Access.OwnerUserId;
        var draftId = McpToolArguments.RequiredGuid(arguments, "draftId");
        var entryId = McpToolArguments.OptionalGuid(arguments, "entryId");

        // Record-level check first: a filtered/non-owned draft is invisible.
        var header = await _drafts.GetDraftHeaderAsync(draftId, owner, ct);
        if (header is null || !await _recordFilter.IsVisibleAsync(
                context.Access, McpDataType.StatementDrafts,
                new McpRecordContext { DraftId = draftId }, ct))
        {
            return new { draftId, validation = (object?)null, additionalFindings = (object?)null };
        }

        var result = await _drafts.ValidateAsync(draftId, entryId, owner, ct);

        // --- §4.4 Zusatzregeln (F-0704) as findings list ---
        var access = context.Access.Access;
        var contactsAllowed = access is not null
            && await _scopeEvaluator.IsAllowedAsync(access, McpDataType.Contacts, McpRightKind.Read, ct);
        var savingsPlansAllowed = access is not null
            && await _scopeEvaluator.IsAllowedAsync(access, McpDataType.SavingsPlans, McpRightKind.Read, ct);
        var securitiesAllowed = access is not null
            && await _scopeEvaluator.IsAllowedAsync(access, McpDataType.Securities, McpRightKind.Read, ct);
        var importsAllowed = access is not null
            && await _scopeEvaluator.IsAllowedAsync(access, McpDataType.StatementImports, McpRightKind.Read, ct);
        var accountsAllowed = access is not null
            && await _scopeEvaluator.IsAllowedAsync(access, McpDataType.Accounts, McpRightKind.Read, ct);

        var scopes = await _recordFilter.GetReadScopesAsync(context.Access, McpDataType.StatementDrafts, ct);
        var entriesFiltered = McpRecordScopeFilter.RequiresRecordFilter(scopes);

        var allEntries = (await _drafts.GetDraftEntriesAsync(draftId, ct)).ToList();
        var considered = entryId is null
            ? allEntries
            : allEntries.Where(e => e.Id == entryId.Value).ToList();

        var findings = new List<Finding>();
        var visible = new List<StatementDraftEntryDto>(considered.Count);
        foreach (var e in considered)
        {
            // Same datensatzscharf entry filter as fm_statement_drafts_read (§6
            // Ableitungstabelle): account type via DetectedAccountId plus the linked
            // contact/savings-plan/security categories.
            if (entriesFiltered
                && !await EntryVisibleAsync(e, header.DetectedAccountId, scopes, owner, ct))
            {
                continue;
            }
            visible.Add(e);

            foreach (var f in McpStatementDraftGuards.CollectEntryCoreFindings(
                         e.BookingDate, e.ValutaDate, e.Amount, e.Subject,
                         e.RecipientName, e.CurrencyCode, e.BookingDescription))
            {
                findings.Add(new Finding(f.Code, f.Severity, e.Id, null, null, f.Message));
            }
            foreach (var f in McpStatementDraftGuards.CollectEntryFindings(e))
            {
                findings.Add(new Finding(f.Code, f.Severity, e.Id, null, null, f.Message));
            }

            // Fremd-Referenzen (§4.4): an id that does not resolve owner-scoped is a
            // finding here (the write guards reject the same case with an error).
            if (e.ContactId is { } contactId && contactsAllowed
                && await _contacts.GetAsync(contactId, owner, ct) is null)
            {
                findings.Add(new Finding("MCP_CONTACT_UNRESOLVED", "Error", e.Id, "contacts", contactId,
                    "The assigned contact does not exist or is not owned by the caller."));
            }
            if (e.SavingsPlanId is { } planId && savingsPlansAllowed)
            {
                var plan = await _savingsPlans.GetAsync(planId, owner, ct);
                if (plan is null)
                {
                    findings.Add(new Finding("MCP_SAVINGSPLAN_UNRESOLVED", "Error", e.Id, "savings-plans", planId,
                        "The assigned savings plan does not exist or is not owned by the caller."));
                }
                else if (plan.ArchivedUtc is not null)
                {
                    findings.Add(new Finding("MCP_SAVINGSPLAN_ARCHIVED", "Error", e.Id, "savings-plans", planId,
                        "The assigned savings plan is archived and cannot be booked."));
                }
            }
            if (e.SecurityId is { } securityId && securitiesAllowed)
            {
                var security = await _securities.GetAsync(securityId, owner, ct);
                if (security is null)
                {
                    findings.Add(new Finding("MCP_SECURITY_UNRESOLVED", "Error", e.Id, "securities", securityId,
                        "The assigned security does not exist or is not owned by the caller."));
                }
                else if (!security.IsActive)
                {
                    findings.Add(new Finding("MCP_SECURITY_INACTIVE", "Error", e.Id, "securities", securityId,
                        "The assigned security is inactive and cannot be booked."));
                }
            }
        }

        // Split-Konsistenz (§4.4): the split-group sum must cover the entry amount —
        // mirrors the pipeline rule SPLIT_AMOUNT_MISMATCH; an unresolvable split draft
        // is a foreign/missing reference.
        foreach (var splitId in visible.Where(e => e.SplitDraftId is not null)
                     .Select(e => e.SplitDraftId!.Value).Distinct())
        {
            var sum = await _drafts.GetSplitGroupSumAsync(splitId, owner, ct);
            foreach (var e in visible.Where(x => x.SplitDraftId == splitId))
            {
                if (sum is null)
                {
                    findings.Add(new Finding("MCP_SPLIT_UNRESOLVED", "Error", e.Id, "statement-drafts", splitId,
                        "The assigned split draft does not exist or is not owned by the caller."));
                }
                else if (sum.Value != e.Amount)
                {
                    findings.Add(new Finding("MCP_SPLIT_SUM_MISMATCH", "Error", e.Id, "statement-drafts", splitId,
                        $"The split-group sum {sum.Value} does not cover the entry amount {e.Amount}."));
                }
            }
        }

        // Credit-card rule: a draft bound to a CreditCard account may only be booked
        // indirectly as a split of a parent statement entry (the debiting giro line) —
        // without a parent link standalone booking fails (mirrors the pipeline rule
        // CARD_REQUIRES_PARENT). Account-type resolution needs the accounts read scope.
        if (accountsAllowed && !header.IsSplitDraft && header.DetectedAccountId is { } detectedAccountForCardCheck)
        {
            var detectedAccount = await _accounts.GetAsync(detectedAccountForCardCheck, owner, ct);
            if (detectedAccount?.Type == AccountType.CreditCard)
            {
                findings.Add(new Finding("MCP_CARD_REQUIRES_PARENT", "Error", null,
                    "accounts", detectedAccountForCardCheck,
                    "The detected account is a credit card — card statement drafts must be assigned as a split of a parent statement entry (the debiting giro statement line) and can never be booked standalone."));
            }
        }

        // Datumslogik (§4.4): Zeitraum-Überlappung zu bereits verbuchten
        // StatementImport-Perioden desselben Kontos — heuristic on the first page of
        // each import's entries (booking-date ordered), Warning severity.
        if (importsAllowed && header.DetectedAccountId is { } detectedAccountId && visible.Count > 0)
        {
            var draftFrom = visible.Min(e => e.BookingDate.Date);
            var draftTo = visible.Max(e => e.BookingDate.Date);
            var imports = await _imports.ListImportsAsync(
                owner, detectedAccountId, null, null, 0, McpToolArguments.MaxTake, ct);
            foreach (var import in imports)
            {
                var importEntries = await _imports.ListEntriesAsync(
                    owner, import.Id, 0, McpToolArguments.MaxTake, ct);
                if (importEntries.Count == 0)
                {
                    continue;
                }
                var importFrom = importEntries.Min(x => x.BookingDate.Date);
                var importTo = importEntries.Max(x => x.BookingDate.Date);
                if (importFrom <= draftTo && draftFrom <= importTo)
                {
                    findings.Add(new Finding("MCP_PERIOD_OVERLAP", "Warning", null,
                        "statement-imports", import.Id,
                        $"The draft period {draftFrom:yyyy-MM-dd} to {draftTo:yyyy-MM-dd} overlaps the already booked import period {importFrom:yyyy-MM-dd} to {importTo:yyyy-MM-dd} of the same account — possible duplicate coverage."));
                }
            }
        }

        return new
        {
            draftId = result.DraftId,
            validation = new
            {
                isValid = result.IsValid,
                budgetImpact = result.BudgetImpact,
                messages = result.Messages.Select(m => new
                {
                    code = m.Code,
                    severity = m.Severity,
                    draftId = m.DraftId,
                    entryId = m.EntryId,
                    relatedRecordKind = m.RelatedRecordKind,
                    relatedRecordId = m.RelatedRecordId,
                    untrusted_data = new { message = m.Message }
                }).ToList()
            },
            additionalFindings = findings.Select(f => new
            {
                code = f.Code,
                severity = f.Severity,
                entryId = f.EntryId,
                relatedRecordKind = f.RelatedRecordKind,
                relatedRecordId = f.RelatedRecordId,
                untrusted_data = new { message = f.Message }
            }).ToList(),
            degraded = new
            {
                contactReferences = !contactsAllowed,
                savingsPlanReferences = !savingsPlansAllowed,
                securityReferences = !securitiesAllowed,
                periodOverlap = !importsAllowed,
                accountTypeChecks = !accountsAllowed
            }
        };
    }

    /// <summary>One §4.4 Befund including the entry/related-record anchors.</summary>
    private sealed record Finding(
        string Code, string Severity, Guid? EntryId,
        string? RelatedRecordKind, Guid? RelatedRecordId, string Message);

    private async Task<bool> EntryVisibleAsync(
        StatementDraftEntryDto entry, Guid? detectedAccountId,
        IReadOnlyList<McpAccessScope> scopes, Guid owner, CancellationToken ct)
    {
        // Same derivation as fm_statement_drafts_read/fm_statement_draft_analyze:
        // Kontotyp via DetectedAccountId→Account.Type plus the categories of the linked
        // contact/savings plan/security, sofern gesetzt (Ableitungstabelle §6).
        var record = new McpRecordContext
        {
            AccountTypeValue = detectedAccountId.HasValue
                ? (short?)(await _accounts.GetAsync(detectedAccountId.Value, owner, ct))?.Type
                : null,
            ContactCategoryId = entry.ContactId.HasValue
                ? (await _contacts.GetAsync(entry.ContactId.Value, owner, ct))?.CategoryId
                : null,
            SavingsPlanCategoryId = entry.SavingsPlanId.HasValue
                ? (await _savingsPlans.GetAsync(entry.SavingsPlanId.Value, owner, ct))?.CategoryId
                : null,
            SecurityCategoryId = entry.SecurityId.HasValue
                ? (await _securities.GetAsync(entry.SecurityId.Value, owner, ct))?.CategoryId
                : null
        };
        return McpRecordScopeFilter.IsAllowed(scopes, record);
    }
}
