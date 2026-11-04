using FinanceManager.Application.Accounts;
using FinanceManager.Application.Contacts;
using FinanceManager.Application.Mcp;
using FinanceManager.Application.Statements;
using FinanceManager.Domain.Mcp;
using FinanceManager.Shared.Dtos.Statements;
using System.Text.Json;

namespace FinanceManager.Web.Mcp.Tools;

/// <summary>
/// Descriptor of the suggestion tool <c>fm_statement_draft_prepare_booking</c> (T19):
/// builds the booking plan of a statement draft — bookable vs. blocked entries (pipeline
/// status, validation findings, missing assignments), the expected posting-kind
/// distribution and balance effect, plus the <c>forceWarnings</c> recommendation — without
/// booking anything. Scopes: (Kontoauszugsentwürfe, Vorschlag), (Buchungen, Lesen).
/// </summary>
public sealed class FmStatementDraftPrepareBookingDescriptor : McpToolDescriptorBase
{
    /// <summary>Tool name.</summary>
    public const string ToolName = "fm_statement_draft_prepare_booking";

    /// <summary>Creates the descriptor.</summary>
    public FmStatementDraftPrepareBookingDescriptor() : base(
        "{\"type\":\"object\",\"properties\":{\"draftId\":{\"type\":\"string\",\"format\":\"uuid\",\"description\":\"Draft to plan the booking for (required).\"},\"entryIds\":{\"type\":\"array\",\"items\":{\"type\":\"string\",\"format\":\"uuid\"},\"description\":\"Optional restriction to specific entries.\"}},\"required\":[\"draftId\"],\"additionalProperties\":false}")
    { }

    /// <inheritdoc />
    public override string Name => ToolName;

    /// <inheritdoc />
    public override string Description
        => "Booking plan for a statement draft (no booking): bookable vs. blocked entries with reasons, expected PostingKind distribution and balance effect, forceWarnings recommendation. Scopes: (Kontoauszugsentwürfe, Vorschlag), (Buchungen, Lesen).";

    /// <inheritdoc />
    public override McpToolSecurityClass SecurityClass => McpToolSecurityClass.Vorschlagend;

    /// <inheritdoc />
    public override IReadOnlyList<McpToolScope> RequiredScopes => new[]
    {
        new McpToolScope(McpDataType.StatementDrafts, McpRightKind.Suggest),
        new McpToolScope(McpDataType.Postings, McpRightKind.Read)
    };
}

/// <summary>
/// Handler of <c>fm_statement_draft_prepare_booking</c>: mirrors the gate logic of
/// <c>StatementDraftService.BookCoreAsync</c> as a pure preview — draft-level blockers
/// (already committed/expired, child/split draft, no detected account, validation errors),
/// entry-level blocks (<c>AlreadyBooked</c>, <c>Announced</c>, missing contact, entry
/// validation errors) and the expected effect (posting kinds per bookable entry, bank
/// amount delta, budget impact, <c>forceWarnings</c> recommendation). Contact/account
/// detail lookups run only when the matching read scopes are granted (degraded otherwise).
/// Emits the plan as data — the booking itself stays the approval-bound tool T20.
/// </summary>
public sealed class FmStatementDraftPrepareBookingHandler : IMcpToolHandler
{
    private readonly IStatementDraftService _drafts;
    private readonly IContactService _contacts;
    private readonly IAccountService _accounts;
    private readonly IMcpScopeEvaluator _scopeEvaluator;
    private readonly McpRecordScopeFilter _recordFilter;

    /// <summary>Creates the handler.</summary>
    public FmStatementDraftPrepareBookingHandler(
        IStatementDraftService drafts,
        IContactService contacts,
        IAccountService accounts,
        IMcpScopeEvaluator scopeEvaluator,
        McpRecordScopeFilter recordFilter)
    {
        _drafts = drafts;
        _contacts = contacts;
        _accounts = accounts;
        _scopeEvaluator = scopeEvaluator;
        _recordFilter = recordFilter;
    }

    /// <inheritdoc />
    public string ToolName => FmStatementDraftPrepareBookingDescriptor.ToolName;

    /// <inheritdoc />
    public async Task<object> InvokeAsync(JsonElement arguments, McpToolCallContext context, CancellationToken ct)
    {
        var owner = context.Access.OwnerUserId;
        var draftId = McpToolArguments.RequiredGuid(arguments, "draftId");
        var entryIds = McpToolArguments.OptionalGuidList(arguments, "entryIds");

        var header = await _drafts.GetDraftHeaderAsync(draftId, owner, ct);
        if (header is null)
        {
            return new { draftId, plan = (object?)null };
        }
        var draftScopes = await _recordFilter.GetScopesAsync(
            context.Access, McpDataType.StatementDrafts, McpRightKind.Suggest, ct);
        if (McpRecordScopeFilter.RequiresRecordFilter(draftScopes)
            && !await DraftVisibleAsync(header, draftScopes, owner, ct))
        {
            return new { draftId, plan = (object?)null };
        }

        var access = context.Access.Access;
        var contactsAllowed = access is not null
            && await _scopeEvaluator.IsAllowedAsync(access, McpDataType.Contacts, McpRightKind.Read, ct);
        var accountsAllowed = access is not null
            && await _scopeEvaluator.IsAllowedAsync(access, McpDataType.Accounts, McpRightKind.Read, ct);

        // Pipeline validation — the same gate BookCoreAsync applies first.
        var validation = await _drafts.ValidateAsync(draftId, null, owner, ct);
        var entryErrors = validation.Messages
            .Where(m => m.EntryId.HasValue
                        && string.Equals(m.Severity, "Error", StringComparison.OrdinalIgnoreCase))
            .GroupBy(m => m.EntryId!.Value)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<DraftValidationMessageDto>)g.ToList());
        var warningCount = validation.Messages.Count(m =>
            string.Equals(m.Severity, "Warning", StringComparison.OrdinalIgnoreCase));
        var errorCount = validation.Messages.Count(m =>
            string.Equals(m.Severity, "Error", StringComparison.OrdinalIgnoreCase));

        var draftBlockers = new List<object>();
        if (header.Status == StatementDraftStatus.Committed)
        {
            draftBlockers.Add(Blocker("alreadyCommitted", "The draft has already been booked."));
        }
        else if (header.Status == StatementDraftStatus.Expired)
        {
            draftBlockers.Add(Blocker("draftExpired", "The draft is expired and cannot be booked."));
        }
        if (header.IsSplitDraft)
        {
            draftBlockers.Add(Blocker("childDraft",
                "The draft is a split child draft; it is booked through its parent entry."));
        }
        if (header.DetectedAccountId is null)
        {
            draftBlockers.Add(Blocker("noAccount",
                "No detected account — booking requires SetDetectedAccountId first."));
        }
        if (errorCount > 0)
        {
            draftBlockers.Add(Blocker("validationErrors",
                "The draft validation reports errors — booking fails until they are resolved."));
        }

        var allEntries = (await _drafts.GetDraftEntriesAsync(draftId, ct)).ToList();
        var planEntries = entryIds is null
            ? allEntries
            : allEntries.Where(e => entryIds.Contains(e.Id)).ToList();
        var excludedCount = allEntries.Count - planEntries.Count;

        var bookable = new List<object>();
        var blockedEntries = new List<object>();
        var bankDelta = 0m;
        var postingKindCounts = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["bank"] = 0, ["contact"] = 0, ["security"] = 0, ["savingsPlan"] = 0
        };

        var selfContactId = contactsAllowed
            ? (await _contacts.ListAsync(owner, 0, McpToolArguments.MaxTake, ContactType.Self, null, ct))
                .FirstOrDefault()?.Id
            : (Guid?)null;

        foreach (var e in planEntries)
        {
            var blocks = new List<object>();
            if (e.Status == StatementDraftEntryStatus.AlreadyBooked)
            {
                blocks.Add(Blocker("alreadyBooked",
                    "Pipeline duplicate flag — never booked silently (reset via draft edit)."));
            }
            else if (e.Status == StatementDraftEntryStatus.Announced)
            {
                blocks.Add(Blocker("announced", "Announced entries are skipped by booking."));
            }
            if (e.ContactId is null)
            {
                blocks.Add(Blocker("missingContact",
                    "Booking skips entries without an assigned contact."));
            }
            else if (contactsAllowed && await _contacts.GetAsync(e.ContactId.Value, owner, ct) is null)
            {
                blocks.Add(Blocker("contactMissing",
                    "The assigned contact no longer exists — booking would skip this entry."));
            }
            if (entryErrors.TryGetValue(e.Id, out var messages))
            {
                foreach (var m in messages)
                {
                    blocks.Add(new
                    {
                        code = "validationError",
                        validationCode = m.Code,
                        untrusted_data = new { message = m.Message }
                    });
                }
            }

            if (blocks.Count > 0)
            {
                blockedEntries.Add(new { entryId = e.Id, entryNumber = e.EntryNumber, blocks });
                continue;
            }

            // Expected posting kinds per bookable entry (mirrors BookCoreAsync):
            // bank + contact always; savings plan when linked (and the contact is self);
            // security trade + optional fee/tax when a security is assigned.
            var securityPostings = e.SecurityId is null
                ? 0
                : 1 + (e.SecurityFeeAmount is not null and not 0m ? 1 : 0)
                  + (e.SecurityTaxAmount is not null and not 0m ? 1 : 0);
            // The savings-plan posting is only created when the entry contact is the
            // owner's self contact — unverifiable without the contacts read scope.
            var savingsPlanPostings = e.SavingsPlanId is null ? 0 : 1;
            bool? savingsPlanConditional = null;
            if (e.SavingsPlanId is not null)
            {
                if (selfContactId is null)
                {
                    savingsPlanConditional = true;
                }
                else if (e.ContactId != selfContactId)
                {
                    savingsPlanPostings = 0;
                }
            }
            var zeroAmountParent = false;
            if (e.SplitDraftId is not null && contactsAllowed)
            {
                var contact = e.ContactId.HasValue
                    ? await _contacts.GetAsync(e.ContactId.Value, owner, ct)
                    : null;
                zeroAmountParent = contact?.IsPaymentIntermediary == true;
            }

            bankDelta += zeroAmountParent ? 0m : e.Amount;
            postingKindCounts["bank"] += 1;
            postingKindCounts["contact"] += 1;
            postingKindCounts["security"] += securityPostings;
            postingKindCounts["savingsPlan"] += savingsPlanPostings;
            bookable.Add(new
            {
                entryId = e.Id,
                entryNumber = e.EntryNumber,
                amount = e.Amount,
                postings = new
                {
                    bank = 1,
                    contact = 1,
                    security = securityPostings,
                    savingsPlan = savingsPlanPostings
                },
                savingsPlanPostingConditional = savingsPlanConditional,
                zeroAmountParent = zeroAmountParent ? true : (bool?)null
            });
        }

        decimal? expectedBalance = null;
        if (accountsAllowed && header.DetectedAccountId.HasValue)
        {
            var account = await _accounts.GetAsync(header.DetectedAccountId.Value, owner, ct);
            if (account is not null)
            {
                expectedBalance = account.CurrentBalance + bankDelta;
            }
        }

        return new
        {
            draftId,
            plan = (object)new
            {
                status = header.Status.ToString(),
                detectedAccountId = header.DetectedAccountId,
                canBook = draftBlockers.Count == 0 && bookable.Count > 0,
                forceWarningsRecommended = errorCount == 0 && warningCount > 0,
                draftBlockers,
                bookableEntries = bookable,
                blockedEntries,
                excludedEntryCount = excludedCount,
                expectedEffect = new
                {
                    postingKinds = postingKindCounts,
                    bankAmountDelta = bankDelta,
                    expectedBalance
                },
                validation = new
                {
                    isValid = validation.IsValid,
                    errorCount,
                    warningCount,
                    messages = validation.Messages.Select(m => new
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
                budgetImpact = validation.BudgetImpact is null ? null : new
                {
                    highestSeverity = validation.BudgetImpact.HighestSeverity.ToString(),
                    evaluatedAtUtc = validation.BudgetImpact.EvaluatedAtUtc,
                    items = validation.BudgetImpact.Items.Select(i => new
                    {
                        budgetPurposeId = i.BudgetPurposeId,
                        budgetPeriod = i.BudgetPeriod,
                        hintType = i.HintType.ToString(),
                        targetValue = i.TargetValue,
                        actualBefore = i.ActualBefore,
                        actualAfter = i.ActualAfter,
                        delta = i.Delta,
                        untrusted_data = new { budgetPurposeName = i.BudgetPurposeName, reason = i.Reason }
                    }).ToList()
                },
                applyHint = new
                {
                    tool = "fm_statement_draft_book",
                    securityClass = "genehmigungspflichtig",
                    requiredArguments = new[] { "draftId", "accountId", "importFormat", "forceWarnings" }
                },
                degraded = new
                {
                    contactChecks = !contactsAllowed,
                    balanceEffect = !accountsAllowed
                }
            }
        };
    }

    private static object Blocker(string code, string message)
        => new { code, untrusted_data = new { message } };

    private async Task<bool> DraftVisibleAsync(
        StatementDraftDto draft, IReadOnlyList<McpAccessScope> scopes, Guid owner, CancellationToken ct)
    {
        var record = new McpRecordContext
        {
            AccountTypeValue = draft.DetectedAccountId.HasValue
                ? (short?)(await _accounts.GetAsync(draft.DetectedAccountId.Value, owner, ct))?.Type
                : null
        };
        return McpRecordScopeFilter.IsAllowed(scopes, record);
    }
}
