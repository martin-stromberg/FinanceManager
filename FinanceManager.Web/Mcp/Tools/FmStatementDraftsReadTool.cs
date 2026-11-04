using FinanceManager.Application.Accounts;
using FinanceManager.Application.Contacts;
using FinanceManager.Application.Mcp;
using FinanceManager.Application.Savings;
using FinanceManager.Application.Securities;
using FinanceManager.Application.Statements;
using FinanceManager.Domain.Mcp;
using FinanceManager.Shared.Dtos.Statements;
using System.Text.Json;

namespace FinanceManager.Web.Mcp.Tools;

/// <summary>
/// Descriptor of the read tool <c>fm_statement_drafts_read</c> (T13): lists open statement
/// drafts (paged) and reads a single draft incl. all entries, split-group sums and
/// upload-group navigation — the same shape as the UI view.
/// Scope: (Kontoauszugsentwürfe, Lesen).
/// </summary>
public sealed class FmStatementDraftsReadDescriptor : McpToolDescriptorBase
{
    /// <summary>Tool name.</summary>
    public const string ToolName = "fm_statement_drafts_read";

    /// <summary>Creates the descriptor.</summary>
    public FmStatementDraftsReadDescriptor() : base(
        "{\"type\":\"object\",\"properties\":{\"draftId\":{\"type\":\"string\",\"format\":\"uuid\",\"description\":\"Optional single draft incl. entries; omitted = paged list of open drafts.\"},\"entryId\":{\"type\":\"string\",\"format\":\"uuid\",\"description\":\"Optional single entry id (requires draftId).\"},\"includeEntries\":{\"type\":\"boolean\",\"description\":\"Include draft entries (default: only single lookups).\"},\"skip\":{\"type\":\"integer\",\"minimum\":0},\"take\":{\"type\":\"integer\",\"minimum\":1,\"maximum\":200},\"entrySkip\":{\"type\":\"integer\",\"minimum\":0},\"entryTake\":{\"type\":\"integer\",\"minimum\":1,\"maximum\":200}},\"additionalProperties\":false}")
    { }

    /// <inheritdoc />
    public override string Name => ToolName;

    /// <inheritdoc />
    public override string Description
        => "Lists open statement drafts or reads one draft incl. entries, split sums and upload-group neighbors. Scope: (Kontoauszugsentwürfe, Lesen).";

    /// <inheritdoc />
    public override McpToolSecurityClass SecurityClass => McpToolSecurityClass.Lesend;

    /// <inheritdoc />
    public override IReadOnlyList<McpToolScope> RequiredScopes => new[]
    {
        new McpToolScope(McpDataType.StatementDrafts, McpRightKind.Read)
    };
}

/// <summary>
/// Handler of <c>fm_statement_drafts_read</c>: adapts the read paths of
/// <see cref="IStatementDraftService"/> (<c>GetOpenDraftsAsync</c>, <c>GetDraftAsync</c>,
/// <c>GetDraftEntriesAsync</c>, <c>GetDraftEntryAsync</c>, <c>GetSplitGroupSumAsync</c>,
/// <c>GetUploadGroupNeighborsAsync</c>). File names, descriptions and imported booking
/// texts are untrusted content (<c>untrusted_data</c>). Record-level filters apply per the
/// derivation table: Kontotyp via <c>DetectedAccountId</c>→<c>Account.Type</c> and the
/// linked contact/savings-plan/security categories on entries, sofern gesetzt.
/// </summary>
public sealed class FmStatementDraftsReadHandler : IMcpToolHandler
{
    private readonly IStatementDraftService _drafts;
    private readonly IAccountService _accounts;
    private readonly IContactService _contacts;
    private readonly ISavingsPlanService _savingsPlans;
    private readonly ISecurityService _securities;
    private readonly McpRecordScopeFilter _recordFilter;

    /// <summary>Creates the handler.</summary>
    public FmStatementDraftsReadHandler(
        IStatementDraftService drafts,
        IAccountService accounts,
        IContactService contacts,
        ISavingsPlanService savingsPlans,
        ISecurityService securities,
        McpRecordScopeFilter recordFilter)
    {
        _drafts = drafts;
        _accounts = accounts;
        _contacts = contacts;
        _savingsPlans = savingsPlans;
        _securities = securities;
        _recordFilter = recordFilter;
    }

    /// <inheritdoc />
    public string ToolName => FmStatementDraftsReadDescriptor.ToolName;

    /// <inheritdoc />
    public async Task<object> InvokeAsync(JsonElement arguments, McpToolCallContext context, CancellationToken ct)
    {
        var owner = context.Access.OwnerUserId;
        var draftId = McpToolArguments.OptionalGuid(arguments, "draftId");
        var entryId = McpToolArguments.OptionalGuid(arguments, "entryId");
        // A single-entry lookup only exists inside a draft — reject instead of silently
        // falling into the list path (schema: "requires draftId").
        if (entryId.HasValue && !draftId.HasValue)
        {
            throw new McpToolException("Err_Mcp_InvalidArguments",
                "Argument 'entryId' requires 'draftId'.");
        }
        var includeEntries = McpToolArguments.OptionalBool(arguments, "includeEntries");
        var skip = McpToolArguments.Skip(arguments);
        var take = McpToolArguments.Take(arguments);
        var entrySkip = Math.Max(0, McpToolArguments.OptionalInt(arguments, "entrySkip") ?? 0);
        var entryTake = Math.Clamp(McpToolArguments.OptionalInt(arguments, "entryTake") ?? 200, 1, McpToolArguments.MaxTake);

        var scopes = await _recordFilter.GetReadScopesAsync(context.Access, McpDataType.StatementDrafts, ct);
        var filtered = McpRecordScopeFilter.RequiresRecordFilter(scopes);

        if (draftId.HasValue)
        {
            var draft = await _drafts.GetDraftAsync(draftId.Value, owner, ct);
            if (draft is null || !await DraftVisibleAsync(draft, scopes, filtered, owner, ct))
            {
                return new { draft = (object?)null };
            }

            IReadOnlyList<StatementDraftEntryDto> entries;
            if (entryId.HasValue)
            {
                var entry = await _drafts.GetDraftEntryAsync(draftId.Value, entryId.Value, ct);
                entries = entry is null ? Array.Empty<StatementDraftEntryDto>() : new[] { entry };
            }
            else
            {
                includeEntries ??= true;
                entries = includeEntries.Value
                    ? (await _drafts.GetDraftEntriesAsync(draftId.Value, ct))
                        .Skip(entrySkip).Take(entryTake).ToList()
                    : Array.Empty<StatementDraftEntryDto>();
            }

            var entryOut = new List<object>(entries.Count);
            foreach (var e in entries)
            {
                if (filtered && !await EntryVisibleAsync(e, draft.DetectedAccountId, scopes, owner, ct))
                {
                    continue;
                }
                entryOut.Add(MapEntry(e));
            }

            var neighbors = await _drafts.GetUploadGroupNeighborsAsync(draftId.Value, owner, ct);
            decimal? splitGroupSum = null;
            if (draft.IsSplitDraft)
            {
                splitGroupSum = await _drafts.GetSplitGroupSumAsync(draftId.Value, owner, ct);
            }

            return new
            {
                draft = (object)new
                {
                    id = draft.DraftId,
                    status = draft.Status.ToString(),
                    detectedAccountId = draft.DetectedAccountId,
                    accountBankContactId = draft.AccountBankContactId,
                    attachmentSymbolId = draft.AttachmentSymbolId,
                    totalAmount = draft.TotalAmount,
                    isSplitDraft = draft.IsSplitDraft,
                    parentDraftId = draft.ParentDraftId,
                    parentEntryId = draft.ParentEntryId,
                    parentEntryAmount = draft.ParentEntryAmount,
                    uploadGroupId = draft.UploadGroupId,
                    isPreliminary = draft.IsPreliminary,
                    splitGroupSum,
                    uploadGroupNeighbors = new { prevDraftId = neighbors.prevId, nextDraftId = neighbors.nextId },
                    entries = entryOut,
                    untrusted_data = new
                    {
                        originalFileName = draft.OriginalFileName,
                        description = draft.Description
                    }
                }
            };
        }

        var open = await _drafts.GetOpenDraftsAsync(owner, skip, take, ct);
        var openTotal = await _drafts.GetOpenDraftsCountAsync(owner, ct);
        var visible = new List<StatementDraftDto>(open.Count);
        foreach (var d in open)
        {
            if (await DraftVisibleAsync(d, scopes, filtered, owner, ct))
            {
                visible.Add(d);
            }
        }

        return new
        {
            total = openTotal,
            drafts = visible.Select(d => (object)new
            {
                id = d.DraftId,
                status = d.Status.ToString(),
                detectedAccountId = d.DetectedAccountId,
                totalAmount = d.TotalAmount,
                isSplitDraft = d.IsSplitDraft,
                parentDraftId = d.ParentDraftId,
                uploadGroupId = d.UploadGroupId,
                isPreliminary = d.IsPreliminary,
                entryCount = d.Entries?.Count ?? 0,
                untrusted_data = new
                {
                    originalFileName = d.OriginalFileName,
                    description = d.Description
                }
            }).ToList()
        };
    }

    private async Task<bool> DraftVisibleAsync(
        StatementDraftDto draft, IReadOnlyList<McpAccessScope> scopes, bool filtered, Guid owner, CancellationToken ct)
    {
        if (!filtered)
        {
            return true;
        }
        var record = new McpRecordContext
        {
            AccountTypeValue = draft.DetectedAccountId.HasValue
                ? (short?)(await _accounts.GetAsync(draft.DetectedAccountId.Value, owner, ct))?.Type
                : null
        };
        return McpRecordScopeFilter.IsAllowed(scopes, record);
    }

    private async Task<bool> EntryVisibleAsync(
        StatementDraftEntryDto entry, Guid? detectedAccountId, IReadOnlyList<McpAccessScope> scopes, Guid owner, CancellationToken ct)
    {
        // Filters on entries act through DetectedAccountId→Account.Type and the linked
        // contact/savings-plan/security categories, sofern gesetzt (derivation table §6).
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

    private static object MapEntry(StatementDraftEntryDto e)
        => new
        {
            id = e.Id,
            entryNumber = e.EntryNumber,
            bookingDate = e.BookingDate,
            valutaDate = e.ValutaDate,
            amount = e.Amount,
            currencyCode = e.CurrencyCode,
            isAnnounced = e.IsAnnounced,
            isCostNeutral = e.IsCostNeutral,
            status = e.Status.ToString(),
            contactId = e.ContactId,
            savingsPlanId = e.SavingsPlanId,
            archiveSavingsPlanOnBooking = e.ArchiveSavingsPlanOnBooking,
            splitDraftId = e.SplitDraftId,
            securityId = e.SecurityId,
            securityTransactionType = e.SecurityTransactionType?.ToString(),
            securityQuantity = e.SecurityQuantity,
            securityFeeAmount = e.SecurityFeeAmount,
            securityTaxAmount = e.SecurityTaxAmount,
            // Imported booking texts are untrusted content (sicherheitskonzept.md §5.2).
            untrusted_data = new
            {
                subject = e.Subject,
                recipientName = e.RecipientName,
                bookingDescription = e.BookingDescription
            }
        };
}
