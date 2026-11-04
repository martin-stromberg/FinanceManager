using FinanceManager.Application.Accounts;
using FinanceManager.Application.Contacts;
using FinanceManager.Application.Mcp;
using FinanceManager.Application.Savings;
using FinanceManager.Application.Securities;
using FinanceManager.Application.Statements;
using FinanceManager.Domain.Mcp;
using FinanceManager.Shared.Dtos.Statements;
using System.Globalization;
using System.Text.Json;

namespace FinanceManager.Web.Mcp.Tools;

/// <summary>
/// Descriptor of the write-preparing tool <c>fm_statement_draft_edit</c> (T18): applies
/// explicit corrections to one draft entry per call — overwriting the correctable core
/// fields (<c>bookingDate</c>, <c>valutaDate</c>, <c>amount</c>, <c>subject</c>,
/// <c>recipientName</c>, <c>currencyCode</c>, <c>bookingDescription</c>), adding or
/// removing an entry, and resetting a wrong duplicate flag. Unlike
/// <c>fm_statement_draft_complete</c> (gaps only) this tool is the intended overwrite path
/// and requires the Änderung right; <c>addEntry</c> additionally requires Erstellung.
/// Scope: (Kontoauszugsentwürfe, Änderung).
/// </summary>
public sealed class FmStatementDraftEditDescriptor : McpToolDescriptorBase
{
    /// <summary>Tool name.</summary>
    public const string ToolName = "fm_statement_draft_edit";

    /// <summary>Creates the descriptor.</summary>
    public FmStatementDraftEditDescriptor() : base(
        "{\"type\":\"object\",\"properties\":{\"draftId\":{\"type\":\"string\",\"format\":\"uuid\",\"description\":\"Draft to correct (required).\"},\"operation\":{\"type\":\"string\",\"enum\":[\"updateEntry\",\"addEntry\",\"deleteEntry\",\"resetDuplicate\"],\"description\":\"Correction operation (required).\"},\"entryId\":{\"type\":\"string\",\"format\":\"uuid\",\"description\":\"Target entry (required for updateEntry/deleteEntry/resetDuplicate).\"},\"entry\":{\"type\":\"object\",\"properties\":{\"bookingDate\":{\"type\":\"string\",\"format\":\"date-time\"},\"valutaDate\":{\"type\":\"string\",\"format\":\"date-time\"},\"amount\":{\"type\":\"number\"},\"subject\":{\"type\":\"string\"},\"recipientName\":{\"type\":\"string\"},\"currencyCode\":{\"type\":\"string\"},\"bookingDescription\":{\"type\":\"string\"}},\"additionalProperties\":false,\"description\":\"Core-field values; updateEntry merges provided fields over the current state, addEntry requires bookingDate/amount/subject.\"}},\"required\":[\"draftId\",\"operation\"],\"additionalProperties\":false}")
    { }

    /// <inheritdoc />
    public override string Name => ToolName;

    /// <inheritdoc />
    public override string Description
        => "Applies explicit corrections to a statement draft: overwrite correctable entry core fields (bookingDate, valutaDate, amount, subject, recipientName, currencyCode, bookingDescription), add an entry, delete an entry or reset a duplicate flag. Scopes: (Kontoauszugsentwürfe, Änderung); addEntry additionally (Kontoauszugsentwürfe, Erstellung).";

    /// <inheritdoc />
    public override McpToolSecurityClass SecurityClass => McpToolSecurityClass.Schreibvorbereitend;

    /// <inheritdoc />
    public override IReadOnlyList<McpToolScope> RequiredScopes => new[]
    {
        new McpToolScope(McpDataType.StatementDrafts, McpRightKind.Modify)
    };
}

/// <summary>
/// Handler of <c>fm_statement_draft_edit</c> (F-0703): executes exactly one correction per
/// call through the existing <see cref="IStatementDraftService"/> paths
/// (<c>UpdateEntryCoreAsync</c>, <c>AddEntryAsync</c>, <c>DeleteEntryAsync</c>,
/// <c>ResetDuplicateEntryAsync</c>) — a partial <c>entry</c> object is merged over the
/// current field values so unspecified fields are preserved; fields outside the
/// correctable whitelist are rejected instead of silently dropped. The fachliche
/// Zusatzvalidierung (F-0704) checks amount/date/text plausibility before the write;
/// <c>AlreadyBooked</c> entries accept only <c>resetDuplicate</c> (the batch convention:
/// they are not editable). The required (Kontoauszugsentwürfe, Erstellung) tuple of
/// <c>addEntry</c> is evaluated inside the handler so correction-only accesses stay
/// usable. Each effect commits atomically with its staged <c>DataChange</c> record incl.
/// field deltas (F-0301).
/// </summary>
public sealed class FmStatementDraftEditHandler : IMcpToolHandler
{
    private const string EntityDraft = "StatementDraft";
    private const string EntityEntry = "StatementDraftEntry";

    /// <summary>The correctable core fields of <c>updateEntry</c>/<c>addEntry</c> (toolkatalog T18).</summary>
    private static readonly HashSet<string> CorrectableFields = new(StringComparer.Ordinal)
    {
        "bookingDate", "valutaDate", "amount", "subject",
        "recipientName", "currencyCode", "bookingDescription"
    };

    private readonly IStatementDraftService _drafts;
    private readonly IContactService _contacts;
    private readonly ISavingsPlanService _savingsPlans;
    private readonly ISecurityService _securities;
    private readonly IAccountService _accounts;
    private readonly IMcpScopeEvaluator _scopeEvaluator;
    private readonly IMcpAuditWriter _auditWriter;
    private readonly McpRecordScopeFilter _recordFilter;

    /// <summary>Creates the handler.</summary>
    public FmStatementDraftEditHandler(
        IStatementDraftService drafts,
        IContactService contacts,
        ISavingsPlanService savingsPlans,
        ISecurityService securities,
        IAccountService accounts,
        IMcpScopeEvaluator scopeEvaluator,
        IMcpAuditWriter auditWriter,
        McpRecordScopeFilter recordFilter)
    {
        _drafts = drafts;
        _contacts = contacts;
        _savingsPlans = savingsPlans;
        _securities = securities;
        _accounts = accounts;
        _scopeEvaluator = scopeEvaluator;
        _auditWriter = auditWriter;
        _recordFilter = recordFilter;
    }

    /// <inheritdoc />
    public string ToolName => FmStatementDraftEditDescriptor.ToolName;

    /// <inheritdoc />
    public async Task<object> InvokeAsync(JsonElement arguments, McpToolCallContext context, CancellationToken ct)
    {
        var owner = context.Access.OwnerUserId;
        var draftId = McpToolArguments.RequiredGuid(arguments, "draftId");
        var operation = McpToolArguments.OptionalString(arguments, "operation", 20)
            ?? throw new McpToolException("Err_Mcp_InvalidArguments", "Argument 'operation' is required.");
        var entryId = McpToolArguments.OptionalGuid(arguments, "entryId");

        var header = await _drafts.GetDraftHeaderAsync(draftId, owner, ct);
        if (header is null)
        {
            // 404 semantics — a foreign draft behaves like a missing one.
            return new { draftId, result = (object?)null };
        }
        var scopes = await _recordFilter.GetScopesAsync(
            context.Access, McpDataType.StatementDrafts, McpRightKind.Modify, ct);
        if (!await DraftAllowedAsync(header, scopes, owner, ct))
        {
            throw new McpToolException("Err_Mcp_ScopeDenied",
                "The record-level scope does not permit editing this draft.");
        }
        if (header.Status != StatementDraftStatus.Draft)
        {
            throw new McpToolException("Err_Mcp_InvalidState",
                $"The draft is not editable (status {header.Status}).");
        }

        return operation switch
        {
            "updateEntry" => await UpdateEntryAsync(draftId, header, RequireEntryId(entryId, operation), arguments, scopes, owner, context, ct),
            "addEntry" => await AddEntryAsync(draftId, header, arguments, owner, context, ct),
            "deleteEntry" => await DeleteEntryAsync(draftId, header, RequireEntryId(entryId, operation), scopes, owner, context, ct),
            "resetDuplicate" => await ResetDuplicateAsync(draftId, header, RequireEntryId(entryId, operation), scopes, owner, context, ct),
            _ => throw new McpToolException("Err_Mcp_InvalidArguments",
                "Argument 'operation' must be one of updateEntry|addEntry|deleteEntry|resetDuplicate.")
        };
    }

    private static Guid RequireEntryId(Guid? entryId, string operation)
        => entryId ?? throw new McpToolException("Err_Mcp_InvalidArguments",
            $"Operation '{operation}' requires 'entryId'.");

    // ------------------------------------------------------------------
    // updateEntry — merge provided core fields over the current values
    // ------------------------------------------------------------------

    private async Task<object> UpdateEntryAsync(
        Guid draftId, StatementDraftDto header, Guid entryId, JsonElement arguments,
        IReadOnlyList<McpAccessScope> scopes, Guid owner, McpToolCallContext context, CancellationToken ct)
    {
        var entry = await _drafts.GetDraftEntryAsync(draftId, entryId, ct)
            ?? throw new McpToolException("Err_Mcp_InvalidArguments",
                $"Entry '{entryId}' does not belong to this draft.");
        if (!await EntryAllowedAsync(draftId, entry, header.DetectedAccountId, scopes, owner, ct))
        {
            throw new McpToolException("Err_Mcp_ScopeDenied",
                $"The record-level scope does not permit editing entry '{entryId}'.");
        }
        if (entry.Status == StatementDraftEntryStatus.AlreadyBooked)
        {
            throw new McpToolException("Err_Mcp_InvalidState",
                "The entry is flagged as already booked and not editable — reset the duplicate flag first (resetDuplicate).");
        }

        var patch = ParseEntryPatch(arguments, out var providedFields);
        if (providedFields.Count == 0)
        {
            throw new McpToolException("Err_Mcp_InvalidArguments",
                "Operation 'updateEntry' requires 'entry' with at least one correctable field.");
        }

        // Merge: provided fields overwrite, the rest keeps the current values.
        var effBookingDate = patch.BookingDate ?? entry.BookingDate;
        var effValuta = providedFields.Contains("valutaDate") ? patch.ValutaDate : entry.ValutaDate;
        var effAmount = patch.Amount ?? entry.Amount;
        var effSubject = providedFields.Contains("subject") ? patch.Subject! : entry.Subject;
        var effRecipient = providedFields.Contains("recipientName") ? patch.RecipientName : entry.RecipientName;
        var effCurrency = providedFields.Contains("currencyCode") ? patch.CurrencyCode : entry.CurrencyCode;
        var effDescription = providedFields.Contains("bookingDescription") ? patch.BookingDescription : entry.BookingDescription;

        // F-0704 plausibility before any effect.
        McpStatementDraftGuards.ValidateEntryCore(
            effBookingDate, effValuta, effAmount, effSubject, effRecipient, effCurrency, effDescription);

        var changes = new List<McpAuditFieldChange>();
        AddChange(changes, entryId, "BookingDate", entry.BookingDate, effBookingDate);
        AddChange(changes, entryId, "ValutaDate", entry.ValutaDate, effValuta);
        AddChange(changes, entryId, "Amount", entry.Amount, effAmount);
        AddChange(changes, entryId, "Subject", entry.Subject, effSubject);
        AddChange(changes, entryId, "RecipientName", entry.RecipientName, effRecipient);
        AddChange(changes, entryId, "CurrencyCode", entry.CurrencyCode, effCurrency);
        AddChange(changes, entryId, "BookingDescription", entry.BookingDescription, effDescription);
        if (changes.Count == 0)
        {
            return new { draftId, operation = "updateEntry", applied = false, reason = "unchanged" };
        }

        var staged = await McpWriteAudit.StageDataChangeAsync(
            _auditWriter, context, "DraftEdited", ToolName, changes, 1,
            JsonSerializer.Serialize(new { draftId, operation = "updateEntry", entryId }), ct);
        StatementDraftEntryDto? updated;
        try
        {
            updated = await _drafts.UpdateEntryCoreAsync(draftId, entryId, owner,
                effBookingDate, effValuta, effAmount, effSubject, effRecipient, effCurrency, effDescription, ct);
        }
        catch
        {
            McpWriteAudit.DiscardStaged(context, staged);
            throw;
        }
        if (updated is null)
        {
            McpWriteAudit.DiscardStaged(context, staged);
            throw new McpToolException("Err_Mcp_InvalidState", "The entry could not be updated.");
        }
        return new
        {
            draftId,
            operation = "updateEntry",
            applied = true,
            changedFields = changes.Select(c => c.Field).ToList(),
            entry = EntryResult(updated)
        };
    }

    // ------------------------------------------------------------------
    // addEntry — requires (Kontoauszugsentwürfe, Erstellung) additionally
    // ------------------------------------------------------------------

    private async Task<object> AddEntryAsync(
        Guid draftId, StatementDraftDto header, JsonElement arguments,
        Guid owner, McpToolCallContext context, CancellationToken ct)
    {
        var access = context.Access.Access;
        if (access is not null
            && !await _scopeEvaluator.IsAllowedAsync(access, McpDataType.StatementDrafts, McpRightKind.Create, ct))
        {
            throw new McpToolException("Err_Mcp_ScopeDenied",
                "Operation 'addEntry' additionally requires the scope (Kontoauszugsentwürfe, Erstellung).");
        }

        var patch = ParseEntryPatch(arguments, out var providedFields);
        if (patch.BookingDate is null || patch.Amount is null || string.IsNullOrWhiteSpace(patch.Subject))
        {
            throw new McpToolException("Err_Mcp_InvalidArguments",
                "Operation 'addEntry' requires 'entry' with bookingDate, amount and subject.");
        }
        // F-0704 plausibility before any effect.
        McpStatementDraftGuards.ValidateEntryCore(
            patch.BookingDate.Value, patch.ValutaDate, patch.Amount.Value,
            patch.Subject!, patch.RecipientName, patch.CurrencyCode, patch.BookingDescription);

        var changes = new List<McpAuditFieldChange>
        {
            new(EntityDraft, draftId, "EntryAdded", null,
                $"{patch.BookingDate.Value:yyyy-MM-dd}|{patch.Amount.Value.ToString(CultureInfo.InvariantCulture)}")
        };
        var staged = await McpWriteAudit.StageDataChangeAsync(
            _auditWriter, context, "DraftEdited", ToolName, changes, 1,
            JsonSerializer.Serialize(new { draftId, operation = "addEntry" }), ct);
        StatementDraftDto? draft;
        try
        {
            draft = await _drafts.AddEntryAsync(draftId, owner, patch.BookingDate.Value, patch.Amount.Value, patch.Subject!, ct);
        }
        catch
        {
            McpWriteAudit.DiscardStaged(context, staged);
            throw;
        }
        if (draft is null)
        {
            McpWriteAudit.DiscardStaged(context, staged);
            throw new McpToolException("Err_Mcp_InvalidState", "The entry could not be added.");
        }
        var newEntry = draft.Entries.OrderBy(e => e.EntryNumber).LastOrDefault();
        return new
        {
            draftId,
            operation = "addEntry",
            applied = true,
            entry = newEntry is null ? null : EntryResult(newEntry)
        };
    }

    // ------------------------------------------------------------------
    // deleteEntry — part of the draft correction (Änderung right)
    // ------------------------------------------------------------------

    private async Task<object> DeleteEntryAsync(
        Guid draftId, StatementDraftDto header, Guid entryId,
        IReadOnlyList<McpAccessScope> scopes, Guid owner, McpToolCallContext context, CancellationToken ct)
    {
        var entry = await _drafts.GetDraftEntryAsync(draftId, entryId, ct)
            ?? throw new McpToolException("Err_Mcp_InvalidArguments",
                $"Entry '{entryId}' does not belong to this draft.");
        if (!await EntryAllowedAsync(draftId, entry, header.DetectedAccountId, scopes, owner, ct))
        {
            throw new McpToolException("Err_Mcp_ScopeDenied",
                $"The record-level scope does not permit deleting entry '{entryId}'.");
        }
        if (entry.Status == StatementDraftEntryStatus.AlreadyBooked)
        {
            throw new McpToolException("Err_Mcp_InvalidState",
                "An already-booked-flagged entry cannot be deleted — reset the duplicate flag first (resetDuplicate).");
        }

        var changes = new List<McpAuditFieldChange>
        {
            new(EntityEntry, entryId, "Deleted",
                $"{entry.BookingDate:yyyy-MM-dd}|{entry.Amount.ToString(CultureInfo.InvariantCulture)}|no.{entry.EntryNumber}", null)
        };
        var staged = await McpWriteAudit.StageDataChangeAsync(
            _auditWriter, context, "DraftEdited", ToolName, changes, 1,
            JsonSerializer.Serialize(new { draftId, operation = "deleteEntry", entryId }), ct);
        bool deleted;
        try
        {
            deleted = await _drafts.DeleteEntryAsync(draftId, entryId, owner, ct);
        }
        catch
        {
            McpWriteAudit.DiscardStaged(context, staged);
            throw;
        }
        if (!deleted)
        {
            McpWriteAudit.DiscardStaged(context, staged);
            throw new McpToolException("Err_Mcp_InvalidState", "The entry could not be deleted.");
        }
        return new { draftId, operation = "deleteEntry", applied = true, entryId };
    }

    // ------------------------------------------------------------------
    // resetDuplicate — protokollierte Dubletten-Rücksetzung
    // ------------------------------------------------------------------

    private async Task<object> ResetDuplicateAsync(
        Guid draftId, StatementDraftDto header, Guid entryId,
        IReadOnlyList<McpAccessScope> scopes, Guid owner, McpToolCallContext context, CancellationToken ct)
    {
        var entry = await _drafts.GetDraftEntryAsync(draftId, entryId, ct)
            ?? throw new McpToolException("Err_Mcp_InvalidArguments",
                $"Entry '{entryId}' does not belong to this draft.");
        if (!await EntryAllowedAsync(draftId, entry, header.DetectedAccountId, scopes, owner, ct))
        {
            throw new McpToolException("Err_Mcp_ScopeDenied",
                $"The record-level scope does not permit resetting entry '{entryId}'.");
        }
        if (entry.Status != StatementDraftEntryStatus.AlreadyBooked)
        {
            return new { draftId, operation = "resetDuplicate", applied = false, reason = "notFlagged" };
        }

        var changes = new List<McpAuditFieldChange>
        {
            new(EntityEntry, entryId, "Status",
                StatementDraftEntryStatus.AlreadyBooked.ToString(),
                StatementDraftEntryStatus.Open.ToString()),
            new(EntityEntry, entryId, "DuplicateReset", null, "via fm_statement_draft_edit")
        };
        var staged = await McpWriteAudit.StageDataChangeAsync(
            _auditWriter, context, "DraftEdited", ToolName, changes, 1,
            JsonSerializer.Serialize(new { draftId, operation = "resetDuplicate", entryId }), ct);
        StatementDraftEntryDto? updated;
        try
        {
            updated = await _drafts.ResetDuplicateEntryAsync(draftId, entryId, owner, ct);
        }
        catch
        {
            McpWriteAudit.DiscardStaged(context, staged);
            throw;
        }
        if (updated is null)
        {
            McpWriteAudit.DiscardStaged(context, staged);
            throw new McpToolException("Err_Mcp_InvalidState", "The duplicate flag could not be reset.");
        }
        return new
        {
            draftId,
            operation = "resetDuplicate",
            applied = true,
            entry = EntryResult(updated)
        };
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private sealed record EntryPatch(
        DateTime? BookingDate, bool BookingDateSet,
        DateTime? ValutaDate,
        decimal? Amount,
        string? Subject,
        string? RecipientName,
        string? CurrencyCode,
        string? BookingDescription);

    private static EntryPatch ParseEntryPatch(JsonElement arguments, out HashSet<string> providedFields)
    {
        providedFields = new HashSet<string>(StringComparer.Ordinal);
        if (!McpToolArguments.HasNonNull(arguments, "entry"))
        {
            return new EntryPatch(null, false, null, null, null, null, null, null);
        }
        var el = arguments.GetProperty("entry");
        if (el.ValueKind != JsonValueKind.Object)
        {
            throw new McpToolException("Err_Mcp_InvalidArguments", "Argument 'entry' must be an object.");
        }
        foreach (var prop in el.EnumerateObject())
        {
            if (!CorrectableFields.Contains(prop.Name))
            {
                throw new McpToolException("Err_Mcp_InvalidArguments",
                    $"Field '{prop.Name}' is not a correctable entry field (allowed: bookingDate, valutaDate, amount, subject, recipientName, currencyCode, bookingDescription).");
            }
            if (prop.Value.ValueKind != JsonValueKind.Null)
            {
                providedFields.Add(prop.Name);
            }
            else
            {
                // Explicit null is only meaningful for nullable fields — it is a provided
                // correction for valutaDate/recipientName/currencyCode/bookingDescription.
                if (prop.Name is "bookingDate" or "amount" or "subject")
                {
                    throw new McpToolException("Err_Mcp_InvalidArguments",
                        $"Field '{prop.Name}' must not be null.");
                }
                providedFields.Add(prop.Name);
            }
        }
        return new EntryPatch(
            McpToolArguments.OptionalDateTime(el, "bookingDate"), providedFields.Contains("bookingDate"),
            providedFields.Contains("valutaDate") ? McpToolArguments.OptionalDateTime(el, "valutaDate") : null,
            OptionalDecimal(el, "amount"),
            McpToolArguments.OptionalString(el, "subject", McpStatementDraftGuards.MaxTextLength),
            McpToolArguments.OptionalString(el, "recipientName", McpStatementDraftGuards.MaxRecipientNameLength),
            McpToolArguments.OptionalString(el, "currencyCode", 10),
            McpToolArguments.OptionalString(el, "bookingDescription", McpStatementDraftGuards.MaxTextLength));
    }

    private static decimal? OptionalDecimal(JsonElement args, string name)
    {
        if (!McpToolArguments.HasNonNull(args, name))
        {
            return null;
        }
        var el = args.GetProperty(name);
        if (el.ValueKind == JsonValueKind.Number && el.TryGetDecimal(out var value))
        {
            return value;
        }
        throw new McpToolException("Err_Mcp_InvalidArguments", $"Argument 'entry.{name}' must be a number.");
    }

    private async Task<bool> DraftAllowedAsync(
        StatementDraftDto draft, IReadOnlyList<McpAccessScope> scopes, Guid owner, CancellationToken ct)
    {
        if (!McpRecordScopeFilter.RequiresRecordFilter(scopes))
        {
            return true;
        }
        var record = new McpRecordContext
        {
            DraftId = draft.DraftId,
            AccountTypeValue = draft.DetectedAccountId.HasValue
                ? (short?)(await _accounts.GetAsync(draft.DetectedAccountId.Value, owner, ct))?.Type
                : null
        };
        return McpRecordScopeFilter.IsAllowed(scopes, record);
    }

    private async Task<bool> EntryAllowedAsync(
        Guid draftId, StatementDraftEntryDto entry, Guid? detectedAccountId, IReadOnlyList<McpAccessScope> scopes,
        Guid owner, CancellationToken ct)
    {
        if (!McpRecordScopeFilter.RequiresRecordFilter(scopes))
        {
            return true;
        }
        var record = new McpRecordContext
        {
            DraftId = draftId,
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

    private static void AddChange(List<McpAuditFieldChange> changes, Guid entityId,
        string field, DateTime? oldValue, DateTime? newValue)
    {
        if (oldValue != newValue)
        {
            changes.Add(new McpAuditFieldChange(EntityEntry, entityId, field,
                oldValue?.ToString("O", CultureInfo.InvariantCulture),
                newValue?.ToString("O", CultureInfo.InvariantCulture)));
        }
    }

    private static void AddChange(List<McpAuditFieldChange> changes, Guid entityId,
        string field, decimal oldValue, decimal newValue)
    {
        if (oldValue != newValue)
        {
            changes.Add(new McpAuditFieldChange(EntityEntry, entityId, field,
                oldValue.ToString(CultureInfo.InvariantCulture),
                newValue.ToString(CultureInfo.InvariantCulture)));
        }
    }

    private static void AddChange(List<McpAuditFieldChange> changes, Guid entityId,
        string field, string? oldValue, string? newValue)
    {
        if (!string.Equals(oldValue, newValue, StringComparison.Ordinal))
        {
            changes.Add(new McpAuditFieldChange(EntityEntry, entityId, field, oldValue, newValue));
        }
    }

    private static object EntryResult(StatementDraftEntryDto entry)
        => new
        {
            entryId = entry.Id,
            entryNumber = entry.EntryNumber,
            bookingDate = entry.BookingDate,
            valutaDate = entry.ValutaDate,
            amount = entry.Amount,
            currencyCode = entry.CurrencyCode,
            status = entry.Status.ToString(),
            isAnnounced = entry.IsAnnounced,
            isCostNeutral = entry.IsCostNeutral,
            contactId = entry.ContactId,
            savingsPlanId = entry.SavingsPlanId,
            archiveSavingsPlanOnBooking = entry.ArchiveSavingsPlanOnBooking,
            splitDraftId = entry.SplitDraftId,
            securityId = entry.SecurityId,
            securityTransactionType = entry.SecurityTransactionType?.ToString(),
            securityQuantity = entry.SecurityQuantity,
            securityFeeAmount = entry.SecurityFeeAmount,
            securityTaxAmount = entry.SecurityTaxAmount,
            untrusted_data = new
            {
                subject = entry.Subject,
                recipientName = entry.RecipientName,
                bookingDescription = entry.BookingDescription
            }
        };
}
