using FinanceManager.Application.Accounts;
using FinanceManager.Application.Contacts;
using FinanceManager.Application.Mcp;
using FinanceManager.Application.Savings;
using FinanceManager.Application.Securities;
using FinanceManager.Application.Statements;
using FinanceManager.Domain.Mcp;
using FinanceManager.Shared.Dtos.Accounts;
using FinanceManager.Shared.Dtos.Contacts;
using FinanceManager.Shared.Dtos.Securities;
using FinanceManager.Shared.Dtos.Statements;
using System.Globalization;
using System.Text.Json;

namespace FinanceManager.Web.Mcp.Tools;

/// <summary>
/// Descriptor of the write-preparing tool <c>fm_statement_draft_complete</c> (T17):
/// completes a statement draft by filling <b>gaps only</b> — existing values are never
/// overwritten (corrections belong to <c>fm_statement_draft_edit</c>). Completable fields:
/// entry assignments (contact, savings plan incl. archive flag, security tuple,
/// cost-neutral flag, split draft) plus draft-level detected account and description.
/// Optional <c>dryRun</c> previews the plan without writing. Scopes:
/// (Kontoauszugsentwürfe, Vervollständigung) + read on Kontakte/Sparpläne/Wertpapiere/Konten.
/// </summary>
public sealed class FmStatementDraftCompleteDescriptor : McpToolDescriptorBase
{
    /// <summary>Tool name.</summary>
    public const string ToolName = "fm_statement_draft_complete";

    /// <summary>Creates the descriptor.</summary>
    public FmStatementDraftCompleteDescriptor() : base(
        "{\"type\":\"object\",\"properties\":{\"draftId\":{\"type\":\"string\",\"format\":\"uuid\",\"description\":\"Draft to complete (required).\"},\"accountId\":{\"type\":\"string\",\"format\":\"uuid\",\"description\":\"Fill a missing DetectedAccountId.\"},\"description\":{\"type\":\"string\",\"description\":\"Fill a missing draft description.\"},\"entries\":{\"type\":\"array\",\"maxItems\":100,\"items\":{\"type\":\"object\",\"properties\":{\"entryId\":{\"type\":\"string\",\"format\":\"uuid\"},\"contactId\":{\"type\":\"string\",\"format\":\"uuid\"},\"savingsPlanId\":{\"type\":\"string\",\"format\":\"uuid\"},\"archiveSavingsPlanOnBooking\":{\"type\":\"boolean\"},\"securityId\":{\"type\":\"string\",\"format\":\"uuid\"},\"securityTransactionType\":{\"type\":\"string\",\"enum\":[\"Buy\",\"Sell\",\"Dividend\"]},\"securityQuantity\":{\"type\":\"number\"},\"securityFeeAmount\":{\"type\":\"number\"},\"securityTaxAmount\":{\"type\":\"number\"},\"isCostNeutral\":{\"type\":\"boolean\"},\"splitDraftId\":{\"type\":\"string\",\"format\":\"uuid\"}},\"required\":[\"entryId\"],\"additionalProperties\":false},\"description\":\"Gap-only assignment updates per entry (max 100, all-or-error).\"},\"dryRun\":{\"type\":\"boolean\",\"description\":\"Preview the plan without persisting.\"}},\"required\":[\"draftId\"],\"additionalProperties\":false}")
    { }

    /// <inheritdoc />
    public override string Name => ToolName;

    /// <inheritdoc />
    public override string Description
        => "Completes a statement draft by filling gaps only (never overwrites existing values): entry assignments (contact, savings plan + archive flag, security fields, cost-neutral, split draft) and draft-level account/description. Atomic all-or-error batches, optional dryRun preview. Scopes: (Kontoauszugsentwürfe, Vervollständigung) + read on Kontakte/Sparpläne/Wertpapiere/Konten.";

    /// <inheritdoc />
    public override McpToolSecurityClass SecurityClass => McpToolSecurityClass.Schreibvorbereitend;

    /// <inheritdoc />
    public override IReadOnlyList<McpToolScope> RequiredScopes => new[]
    {
        new McpToolScope(McpDataType.StatementDrafts, McpRightKind.Complete),
        new McpToolScope(McpDataType.Contacts, McpRightKind.Read),
        new McpToolScope(McpDataType.SavingsPlans, McpRightKind.Read),
        new McpToolScope(McpDataType.Securities, McpRightKind.Read),
        new McpToolScope(McpDataType.Accounts, McpRightKind.Read)
    };
}

/// <summary>
/// Handler of <c>fm_statement_draft_complete</c> (F-0702): plans gap-only completions per
/// entry and draft level, applies the fachliche Zusatzvalidierung (F-0704) — owner-scoped
/// reference resolution incl. record-scope on the referenced targets, savings-plan/
/// security side conditions of the pipeline — before any write, then applies per-entry via
/// <see cref="IStatementDraftService.SaveEntryAllAsync"/>/
/// <see cref="IStatementDraftService.SetEntrySplitDraftAsync"/> and draft-level via
/// <see cref="IStatementDraftService.SetAccountAsync"/>/
/// <see cref="IStatementDraftService.SetDescriptionAsync"/>. Draft-level changes run first
/// and trigger pipeline classification; entry gaps are re-evaluated on the refreshed state
/// so classification results are respected (a value filled in between is reported
/// <c>alreadySet</c>, never overwritten). <c>dryRun</c> returns the same plan without
/// writes or audit. Every effect commits atomically with its staged
/// <c>DataChange</c> record (F-0301); a rejected call writes nothing.
/// </summary>
public sealed class FmStatementDraftCompleteHandler : IMcpToolHandler
{
    /// <summary>Maximum entry updates per call (toolkatalog §4.6 batch bound).</summary>
    internal const int MaxEntryUpdates = 100;

    private const string EntityDraft = "StatementDraft";
    private const string EntityEntry = "StatementDraftEntry";

    private readonly IStatementDraftService _drafts;
    private readonly IContactService _contacts;
    private readonly ISavingsPlanService _savingsPlans;
    private readonly ISecurityService _securities;
    private readonly IAccountService _accounts;
    private readonly IMcpAuditWriter _auditWriter;
    private readonly McpRecordScopeFilter _recordFilter;

    /// <summary>Creates the handler.</summary>
    public FmStatementDraftCompleteHandler(
        IStatementDraftService drafts,
        IContactService contacts,
        ISavingsPlanService savingsPlans,
        ISecurityService securities,
        IAccountService accounts,
        IMcpAuditWriter auditWriter,
        McpRecordScopeFilter recordFilter)
    {
        _drafts = drafts;
        _contacts = contacts;
        _savingsPlans = savingsPlans;
        _securities = securities;
        _accounts = accounts;
        _auditWriter = auditWriter;
        _recordFilter = recordFilter;
    }

    /// <inheritdoc />
    public string ToolName => FmStatementDraftCompleteDescriptor.ToolName;

    /// <inheritdoc />
    public async Task<object> InvokeAsync(JsonElement arguments, McpToolCallContext context, CancellationToken ct)
    {
        var owner = context.Access.OwnerUserId;
        var draftId = McpToolArguments.RequiredGuid(arguments, "draftId");
        var accountId = McpToolArguments.OptionalGuid(arguments, "accountId");
        var description = McpToolArguments.OptionalString(arguments, "description", McpStatementDraftGuards.MaxTextLength);
        var dryRun = McpToolArguments.OptionalBool(arguments, "dryRun") ?? false;
        var entryRequests = ParseEntryRequests(arguments);

        if (accountId is null && description is null && entryRequests.Count == 0)
        {
            throw new McpToolException("Err_Mcp_InvalidArguments",
                "Nothing to complete: provide 'accountId', 'description' or at least one entry update.");
        }

        var header = await _drafts.GetDraftHeaderAsync(draftId, owner, ct);
        if (header is null)
        {
            // 404 semantics — a foreign draft behaves like a missing one.
            return new { draftId, result = (object?)null };
        }
        var scopes = await _recordFilter.GetScopesAsync(
            context.Access, McpDataType.StatementDrafts, McpRightKind.Complete, ct);
        if (!await DraftAllowedAsync(header, scopes, owner, context, ct))
        {
            throw new McpToolException("Err_Mcp_ScopeDenied",
                "The record-level scope does not permit completing this draft.");
        }
        if (header.Status != StatementDraftStatus.Draft)
        {
            throw new McpToolException("Err_Mcp_InvalidState",
                $"The draft is not editable (status {header.Status}).");
        }

        var allEntries = (await _drafts.GetDraftEntriesAsync(draftId, ct)).ToList();
        var byId = allEntries.ToDictionary(e => e.Id);
        foreach (var req in entryRequests)
        {
            if (!byId.TryGetValue(req.EntryId, out var target))
            {
                throw new McpToolException("Err_Mcp_InvalidArguments",
                    $"Entry '{req.EntryId}' does not belong to this draft.");
            }
            if (!await EntryAllowedAsync(draftId, target, header.DetectedAccountId, scopes, owner, context, ct))
            {
                throw new McpToolException("Err_Mcp_ScopeDenied",
                    $"The record-level scope does not permit completing entry '{req.EntryId}'.");
            }
        }

        var effectiveAccountId = header.DetectedAccountId;
        var draftLevel = new Dictionary<string, object>(StringComparer.Ordinal);
        var auditDraftLevel = new List<McpAuditFieldChange>();

        // --- Draft-level plan (gaps only) ---
        AccountDto? effectiveAccount = null;
        var fillAccount = false;
        if (accountId.HasValue)
        {
            if (header.DetectedAccountId is null)
            {
                // F-0704: owner-scoped reference + record scope on the target.
                effectiveAccount = await ScopedAccountAsync(accountId.Value, owner, context, ct);
                effectiveAccountId = accountId.Value;
                fillAccount = true;
                draftLevel["accountId"] = new { action = "fill", value = accountId.Value };
                auditDraftLevel.Add(new McpAuditFieldChange(EntityDraft, draftId, "DetectedAccountId", null, accountId.Value.ToString()));
            }
            else if (header.DetectedAccountId == accountId.Value)
            {
                draftLevel["accountId"] = new { action = "unchanged" };
            }
            else
            {
                draftLevel["accountId"] = new { action = "skipped", reason = "alreadySet" };
            }
        }
        if (header.DetectedAccountId.HasValue)
        {
            // Needed for the entry side conditions below.
            effectiveAccount ??= await _accounts.GetAsync(header.DetectedAccountId.Value, owner, ct);
        }

        var fillDescription = false;
        if (description is not null)
        {
            if (string.IsNullOrWhiteSpace(header.Description))
            {
                fillDescription = true;
                draftLevel["description"] = new { action = "fill" };
                auditDraftLevel.Add(new McpAuditFieldChange(EntityDraft, draftId, "Description", null, description));
            }
            else if (string.Equals(header.Description, description, StringComparison.Ordinal))
            {
                draftLevel["description"] = new { action = "unchanged" };
            }
            else
            {
                draftLevel["description"] = new { action = "skipped", reason = "alreadySet" };
            }
        }

        // --- Entry-level plan (gaps only) + F-0704 validation of the effective state ---
        var plans = new List<EntryPlan>(entryRequests.Count);
        foreach (var req in entryRequests)
        {
            plans.Add(await PlanEntryAsync(draftId, header, byId[req.EntryId], req,
                effectiveAccountId, effectiveAccount, owner, context, ct));
        }

        if (dryRun)
        {
            return new
            {
                draftId,
                dryRun = true,
                result = (object?)new
                {
                    draftLevel,
                    entries = plans.Select(p => p.Result).ToList(),
                    plannedFieldCount = plans.Sum(p => p.Fills.Count)
                }
            };
        }

        // --- Apply phase 1: draft-level fills (each Set* triggers classification) ---
        if (auditDraftLevel.Count > 0)
        {
            var staged = await McpWriteAudit.StageDataChangeAsync(
                _auditWriter, context, "DraftCompleted", ToolName, auditDraftLevel,
                auditDraftLevel.Count, Payload(draftId, accountId, description), ct);
            try
            {
                if (fillAccount && await _drafts.SetAccountAsync(draftId, owner, accountId!.Value, ct) is null)
                {
                    throw new McpToolException("Err_Mcp_InvalidState", "The detected account could not be set.");
                }
                if (fillDescription && await _drafts.SetDescriptionAsync(draftId, owner, description, ct) is null)
                {
                    throw new McpToolException("Err_Mcp_InvalidState", "The description could not be set.");
                }
            }
            catch
            {
                McpWriteAudit.DiscardStaged(context, staged);
                throw;
            }

            // Classification may have filled entry gaps meanwhile — re-evaluate the plan on
            // the refreshed state; a meanwhile-set value is reported alreadySet, never
            // overwritten ("nur Lücken", fachkonzept.md §4.2).
            allEntries = (await _drafts.GetDraftEntriesAsync(draftId, ct)).ToList();
            byId = allEntries.ToDictionary(e => e.Id);
            header = (await _drafts.GetDraftHeaderAsync(draftId, owner, ct))!;
            effectiveAccountId = header.DetectedAccountId;
            effectiveAccount = effectiveAccountId.HasValue
                ? await _accounts.GetAsync(effectiveAccountId.Value, owner, ct)
                : null;
            var refreshed = new List<EntryPlan>(plans.Count);
            foreach (var req in entryRequests)
            {
                refreshed.Add(await PlanEntryAsync(draftId, header, byId[req.EntryId], req,
                    effectiveAccountId, effectiveAccount, owner, context, ct));
            }
            plans = refreshed;
        }

        // --- Apply phase 2: entry-level fills (all-or-error via pre-validation) ---
        var auditEntryLevel = new List<McpAuditFieldChange>();
        foreach (var plan in plans)
        {
            auditEntryLevel.AddRange(plan.Fills.Select(f =>
                new McpAuditFieldChange(EntityEntry, plan.EntryId, f.Field, f.OldValue, f.NewValue)));
        }
        if (auditEntryLevel.Count > 0)
        {
            var staged = await McpWriteAudit.StageDataChangeAsync(
                _auditWriter, context, "DraftCompleted", ToolName, auditEntryLevel,
                auditEntryLevel.Count, null, ct);
            try
            {
                foreach (var plan in plans.Where(p => p.Fills.Count > 0))
                {
                    await ApplyEntryPlanAsync(plan, owner, ct);
                }
            }
            catch (Exception ex) when (ex is not McpToolException)
            {
                McpWriteAudit.DiscardStaged(context, staged);
                throw new McpToolException("Err_Mcp_InvalidState",
                    $"The completion failed while applying: {ex.Message}");
            }
            catch
            {
                McpWriteAudit.DiscardStaged(context, staged);
                throw;
            }
        }

        return new
        {
            draftId,
            dryRun = false,
            result = (object?)new
            {
                draftLevel,
                entries = plans.Select(p => p.Result).ToList(),
                appliedFieldCount = auditEntryLevel.Count
            }
        };
    }

    // ------------------------------------------------------------------
    // Planning (nur Lücken) + fachliche Zusatzvalidierung (F-0704)
    // ------------------------------------------------------------------

    private sealed record Fill(string Field, string? OldValue, string? NewValue);

    private sealed record EntryPlan(
        Guid DraftId,
        Guid EntryId,
        List<Fill> Fills,
        Guid? EffContactId,
        Guid? EffSavingsPlanId,
        bool EffArchiveOnBooking,
        Guid? EffSecurityId,
        SecurityTransactionType? EffTxType,
        decimal? EffQuantity,
        decimal? EffFee,
        decimal? EffTax,
        bool EffCostNeutral,
        Guid? EffSplitDraftId,
        List<object> Skipped,
        List<string> Unchanged)
    {
        /// <summary>Serializable per-entry result projection.</summary>
        public object Result => new
        {
            entryId = EntryId,
            filled = Fills.Select(f => f.Field).ToList(),
            skipped = Skipped,
            unchanged = Unchanged
        };
    }

    private async Task<EntryPlan> PlanEntryAsync(
        Guid draftId, StatementDraftDto header, StatementDraftEntryDto entry, EntryUpdateRequest req,
        Guid? effectiveAccountId, AccountDto? effectiveAccount,
        Guid owner, McpToolCallContext context, CancellationToken ct)
    {
        var fills = new List<Fill>();
        var skipped = new List<object>();
        var unchanged = new List<string>();

        Guid? effContact = Gap(entry.ContactId, req.ContactId, req.ContactSet, "ContactId",
            v => v?.ToString(), fills, skipped, unchanged);
        Guid? effSavingsPlan = Gap(entry.SavingsPlanId, req.SavingsPlanId, req.SavingsPlanSet, "SavingsPlanId",
            v => v?.ToString(), fills, skipped, unchanged);
        var effArchive = Gap(entry.ArchiveSavingsPlanOnBooking, req.ArchiveOnBooking, req.ArchiveSet, "ArchiveSavingsPlanOnBooking",
            v => v?.ToString(CultureInfo.InvariantCulture), fills, skipped, unchanged);
        Guid? effSecurity = Gap(entry.SecurityId, req.SecurityId, req.SecuritySet, "SecurityId",
            v => v?.ToString(), fills, skipped, unchanged);
        var effTxType = Gap<SecurityTransactionType>(entry.SecurityTransactionType, req.TxType, req.TxTypeSet, "SecurityTransactionType",
            v => v?.ToString(), fills, skipped, unchanged);
        var effQuantity = Gap(entry.SecurityQuantity, req.Quantity, req.QuantitySet, "SecurityQuantity",
            v => v?.ToString(CultureInfo.InvariantCulture), fills, skipped, unchanged);
        var effFee = Gap(entry.SecurityFeeAmount, req.Fee, req.FeeSet, "SecurityFeeAmount",
            v => v?.ToString(CultureInfo.InvariantCulture), fills, skipped, unchanged);
        var effTax = Gap(entry.SecurityTaxAmount, req.Tax, req.TaxSet, "SecurityTaxAmount",
            v => v?.ToString(CultureInfo.InvariantCulture), fills, skipped, unchanged);
        var effCostNeutral = Gap(entry.IsCostNeutral, req.CostNeutral, req.CostNeutralSet, "IsCostNeutral",
            v => v?.ToString(CultureInfo.InvariantCulture), fills, skipped, unchanged);
        Guid? effSplit = Gap(entry.SplitDraftId, req.SplitDraftId, req.SplitSet, "SplitDraftId",
            v => v?.ToString(), fills, skipped, unchanged);

        // ---- F-0704: resolve every planned reference owner-scoped (+ record scope on the
        // target) and enforce the pipeline side conditions on the EFFECTIVE state ----
        ContactDto? effContactDto = null;
        if (req.ContactSet && fills.Any(f => f.Field == "ContactId"))
        {
            effContactDto = await ScopedContactAsync(req.ContactId!.Value, owner, context, ct);
        }
        else if (effContact.HasValue)
        {
            effContactDto = await _contacts.GetAsync(effContact.Value, owner, ct);
        }

        var savingsPlanFill = fills.Any(f => f.Field is "SavingsPlanId" or "ArchiveSavingsPlanOnBooking");
        if (fills.Any(f => f.Field == "SavingsPlanId"))
        {
            await ScopedSavingsPlanAsync(req.SavingsPlanId!.Value, owner, context, ct);
        }
        if (effArchive && effSavingsPlan is null)
        {
            throw new McpToolException("Err_Mcp_InvalidArguments",
                "ArchiveSavingsPlanOnBooking requires an assigned savings plan.");
        }
        if (effSavingsPlan.HasValue && savingsPlanFill)
        {
            McpStatementDraftGuards.ValidateSavingsPlanAssignment(effContactDto, effectiveAccount);
        }

        var securityFill = fills.Any(f => f.Field is "SecurityId" or "SecurityTransactionType"
            or "SecurityQuantity" or "SecurityFeeAmount" or "SecurityTaxAmount");
        if (securityFill)
        {
            if (effSecurity is null && effTxType is null && effQuantity is null && effFee is null && effTax is null)
            {
                throw new McpToolException("Err_Mcp_InvalidArguments",
                    "Security fields produce no effective security assignment.");
            }
            if (fills.Any(f => f.Field == "SecurityId"))
            {
                await ScopedSecurityAsync(req.SecurityId!.Value, owner, context, ct);
            }
            McpStatementDraftGuards.ValidateSecurityAssignment(effContactDto, effectiveAccount);
            if (effSecurity.HasValue && effTxType is null)
            {
                throw new McpToolException("Err_Mcp_InvalidArguments",
                    "SecurityTransactionType is required when a security is assigned (Zuordnungsvollständigkeit).");
            }
            if (effQuantity is <= 0m)
            {
                throw new McpToolException("Err_Mcp_InvalidArguments", "SecurityQuantity must be positive.");
            }
            if (effFee is < 0m || effTax is < 0m)
            {
                throw new McpToolException("Err_Mcp_InvalidArguments", "Security fee/tax amounts must not be negative.");
            }
        }

        if (fills.Any(f => f.Field == "SplitDraftId"))
        {
            var splitHeader = await _drafts.GetDraftHeaderAsync(req.SplitDraftId!.Value, owner, ct)
                ?? throw new McpToolException("Err_Mcp_InvalidArguments",
                    "The referenced split draft does not exist or is not owned by the caller.");
            if (splitHeader.DetectedAccountId is not null)
            {
                throw new McpToolException("Err_Mcp_InvalidArguments",
                    "The referenced split draft must not have an account assigned.");
            }
            if (header.UploadGroupId is not null && splitHeader.UploadGroupId == header.UploadGroupId)
            {
                throw new McpToolException("Err_Mcp_InvalidArguments",
                    "Split drafts must not originate from the same upload group.");
            }
            if (effContactDto is null)
            {
                throw new McpToolException("Err_Mcp_InvalidArguments",
                    "A split assignment requires an entry contact.");
            }
            if (!effContactDto.IsPaymentIntermediary)
            {
                throw new McpToolException("Err_Mcp_InvalidArguments",
                    "A split assignment requires the entry contact to be a payment intermediary.");
            }
        }

        return new EntryPlan(draftId, entry.Id, fills, effContact, effSavingsPlan, effArchive,
            effSecurity, effTxType, effQuantity, effFee, effTax, effCostNeutral, effSplit,
            skipped, unchanged);
    }

    /// <summary>
    /// Gap-only decision for one field: fills when the current value is unset (for booleans:
    /// only the false→true direction counts as a fill — clearing is a correction belonging
    /// to <c>fm_statement_draft_edit</c>), reports <c>unchanged</c> on equal values and
    /// <c>skipped(alreadySet|requiresEdit)</c> otherwise. Never silently overwrites.
    /// </summary>
    private static T? Gap<T>(
        T? current, T? requested, bool requestedSet, string field,
        Func<T?, string?> render,
        List<Fill> fills, List<object> skipped, List<string> unchanged) where T : struct
    {
        if (!requestedSet)
        {
            return current;
        }
        if (!current.HasValue)
        {
            fills.Add(new Fill(field, render(null), render(requested)));
            return requested;
        }
        return EqualityComparer<T>.Default.Equals(current.Value, requested!.Value)
            ? ReturnUnchanged(current, field, unchanged)
            : ReturnSkipped(current, field, "alreadySet", skipped);
    }

    private static bool Gap(
        bool current, bool? requested, bool requestedSet, string field,
        Func<bool?, string?> render,
        List<Fill> fills, List<object> skipped, List<string> unchanged)
    {
        if (!requestedSet)
        {
            return current;
        }
        if (requested == current)
        {
            unchanged.Add(field);
            return current;
        }
        if (requested == true)
        {
            fills.Add(new Fill(field, render(current), render(requested)));
            return true;
        }
        // Clearing a boolean flag is a correction, not a completion.
        skipped.Add(new { field, reason = "requiresEdit" });
        return current;
    }

    private static T? ReturnUnchanged<T>(T? current, string field, List<string> unchanged) where T : struct
    {
        unchanged.Add(field);
        return current;
    }

    private static T? ReturnSkipped<T>(T? current, string field, string reason, List<object> skipped) where T : struct
    {
        skipped.Add(new { field, reason });
        return current;
    }

    // ------------------------------------------------------------------
    // Apply
    // ------------------------------------------------------------------

    private async Task ApplyEntryPlanAsync(EntryPlan plan, Guid owner, CancellationToken ct)
    {
        var coveredBySaveAll = plan.Fills.Any(f => f.Field is "ContactId" or "IsCostNeutral"
            or "SavingsPlanId" or "ArchiveSavingsPlanOnBooking" or "SecurityId"
            or "SecurityTransactionType" or "SecurityQuantity" or "SecurityFeeAmount"
            or "SecurityTaxAmount");
        if (coveredBySaveAll)
        {
            // SaveEntryAllAsync contract note: the declared interface parameter names
            // (splitDraftId, contactId, savingsPlanId, price, fee) do not match the
            // implementation binding — the effective positional contract is
            // (contactId, isCostNeutral, savingsPlanId, archiveOnBooking, securityId,
            // transactionType, quantity, feeAmount, taxAmount). Null clears, so every
            // argument carries the EFFECTIVE value (requested fill or current state).
            var updated = await _drafts.SaveEntryAllAsync(
                plan.DraftId, plan.EntryId, owner,
                plan.EffContactId, plan.EffCostNeutral, plan.EffSavingsPlanId, plan.EffArchiveOnBooking,
                plan.EffSecurityId, plan.EffTxType, plan.EffQuantity, plan.EffFee, plan.EffTax, ct);
            if (updated is null)
            {
                throw new McpToolException("Err_Mcp_InvalidState",
                    $"Entry '{plan.EntryId}' could not be completed (not editable or no longer present).");
            }
        }
        if (plan.Fills.Any(f => f.Field == "SplitDraftId"))
        {
            var updated = await _drafts.SetEntrySplitDraftAsync(
                plan.DraftId, plan.EntryId, plan.EffSplitDraftId, owner, ct);
            if (updated is null)
            {
                throw new McpToolException("Err_Mcp_InvalidState",
                    $"The split assignment of entry '{plan.EntryId}' could not be set.");
            }
        }
    }

    // ------------------------------------------------------------------
    // Reference resolution incl. record scope on the target (F-0702/F-0704)
    // ------------------------------------------------------------------

    private async Task<ContactDto> ScopedContactAsync(Guid id, Guid owner, McpToolCallContext context, CancellationToken ct)
    {
        var contact = await McpStatementDraftGuards.RequireContactAsync(_contacts, id, owner, ct);
        await RequireTargetScopeAsync(context, McpDataType.Contacts,
            new McpRecordContext { ContactId = id, ContactCategoryId = contact.CategoryId }, ct);
        return contact;
    }

    private async Task<FinanceManager.Shared.Dtos.SavingsPlans.SavingsPlanDto> ScopedSavingsPlanAsync(Guid id, Guid owner, McpToolCallContext context, CancellationToken ct)
    {
        var plan = await McpStatementDraftGuards.RequireSavingsPlanAsync(_savingsPlans, id, owner, ct);
        await RequireTargetScopeAsync(context, McpDataType.SavingsPlans,
            new McpRecordContext { SavingsPlanId = id, SavingsPlanCategoryId = plan.CategoryId }, ct);
        return plan;
    }

    private async Task<SecurityDto> ScopedSecurityAsync(Guid id, Guid owner, McpToolCallContext context, CancellationToken ct)
    {
        var security = await McpStatementDraftGuards.RequireSecurityAsync(_securities, id, owner, ct);
        await RequireTargetScopeAsync(context, McpDataType.Securities,
            new McpRecordContext { SecurityId = id, SecurityCategoryId = security.CategoryId }, ct);
        return security;
    }

    private async Task<AccountDto> ScopedAccountAsync(Guid id, Guid owner, McpToolCallContext context, CancellationToken ct)
    {
        var account = await McpStatementDraftGuards.RequireAccountAsync(_accounts, id, owner, ct);
        await RequireTargetScopeAsync(context, McpDataType.Accounts,
            new McpRecordContext { AccountId = id, AccountTypeValue = (short)account.Type }, ct);
        return account;
    }

    private async Task RequireTargetScopeAsync(
        McpToolCallContext context, McpDataType dataType, McpRecordContext record, CancellationToken ct)
    {
        var scopes = await _recordFilter.GetScopesAsync(context.Access, dataType, McpRightKind.Read, ct);
        if (McpRecordScopeFilter.RequiresRecordFilter(scopes)
            && !McpRecordScopeFilter.IsAllowed(scopes, record))
        {
            throw new McpToolException("Err_Mcp_ScopeDenied",
                $"The record-level scope does not permit referencing this {dataType} record.");
        }
    }

    // ------------------------------------------------------------------
    // Record-scope checks on draft + entries (stage-2 filters of the Complete rows)
    // ------------------------------------------------------------------

    private async Task<bool> DraftAllowedAsync(
        StatementDraftDto draft, IReadOnlyList<McpAccessScope> scopes,
        Guid owner, McpToolCallContext context, CancellationToken ct)
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
        Guid owner, McpToolCallContext context, CancellationToken ct)
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

    // ------------------------------------------------------------------
    // Argument parsing
    // ------------------------------------------------------------------

    private sealed record EntryUpdateRequest(
        Guid EntryId,
        Guid? ContactId, bool ContactSet,
        Guid? SavingsPlanId, bool SavingsPlanSet,
        bool? ArchiveOnBooking, bool ArchiveSet,
        Guid? SecurityId, bool SecuritySet,
        SecurityTransactionType? TxType, bool TxTypeSet,
        decimal? Quantity, bool QuantitySet,
        decimal? Fee, bool FeeSet,
        decimal? Tax, bool TaxSet,
        bool? CostNeutral, bool CostNeutralSet,
        Guid? SplitDraftId, bool SplitSet);

    private static List<EntryUpdateRequest> ParseEntryRequests(JsonElement arguments)
    {
        var list = new List<EntryUpdateRequest>();
        if (!McpToolArguments.Has(arguments, "entries"))
        {
            return list;
        }
        var el = arguments.GetProperty("entries");
        if (el.ValueKind != JsonValueKind.Array || el.GetArrayLength() > MaxEntryUpdates)
        {
            throw new McpToolException("Err_Mcp_InvalidArguments",
                $"Argument 'entries' must be an array of at most {MaxEntryUpdates} updates.");
        }
        var seen = new HashSet<Guid>();
        foreach (var item in el.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                throw new McpToolException("Err_Mcp_InvalidArguments", "Each entry update must be an object.");
            }
            var entryId = McpToolArguments.RequiredGuid(item, "entryId");
            if (!seen.Add(entryId))
            {
                throw new McpToolException("Err_Mcp_InvalidArguments",
                    $"Duplicate update for entry '{entryId}'.");
            }
            var contactId = McpToolArguments.OptionalGuid(item, "contactId");
            var savingsPlanId = McpToolArguments.OptionalGuid(item, "savingsPlanId");
            var archive = McpToolArguments.OptionalBool(item, "archiveSavingsPlanOnBooking");
            var securityId = McpToolArguments.OptionalGuid(item, "securityId");
            var txType = McpToolArguments.OptionalEnum<SecurityTransactionType>(item, "securityTransactionType");
            var quantity = OptionalDecimal(item, "securityQuantity");
            var fee = OptionalDecimal(item, "securityFeeAmount");
            var tax = OptionalDecimal(item, "securityTaxAmount");
            var costNeutral = McpToolArguments.OptionalBool(item, "isCostNeutral");
            var splitDraftId = McpToolArguments.OptionalGuid(item, "splitDraftId");
            if (contactId is null && savingsPlanId is null && archive is null && securityId is null
                && txType is null && quantity is null && fee is null && tax is null
                && costNeutral is null && splitDraftId is null)
            {
                throw new McpToolException("Err_Mcp_InvalidArguments",
                    $"The update for entry '{entryId}' contains no completable field.");
            }
            list.Add(new EntryUpdateRequest(
                entryId,
                contactId, contactId.HasValue,
                savingsPlanId, savingsPlanId.HasValue,
                archive, archive.HasValue,
                securityId, securityId.HasValue,
                txType, txType.HasValue,
                quantity, quantity.HasValue,
                fee, fee.HasValue,
                tax, tax.HasValue,
                costNeutral, costNeutral.HasValue,
                splitDraftId, splitDraftId.HasValue));
        }
        return list;
    }

    private static decimal? OptionalDecimal(JsonElement args, string name)
    {
        if (!McpToolArguments.Has(args, name))
        {
            return null;
        }
        var el = args.GetProperty(name);
        if (el.ValueKind == JsonValueKind.Number && el.TryGetDecimal(out var value))
        {
            return value;
        }
        throw new McpToolException("Err_Mcp_InvalidArguments", $"Argument '{name}' must be a number.");
    }

    private static string Payload(Guid draftId, Guid? accountId, string? description)
        => JsonSerializer.Serialize(new { draftId, accountId, description });
}
