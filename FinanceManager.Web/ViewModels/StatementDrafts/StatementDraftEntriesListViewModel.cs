using FinanceManager.Shared;
using Microsoft.Extensions.Localization;

namespace FinanceManager.Web.ViewModels.StatementDrafts;

// Embedded list view model for statement draft entries (non-persistent, constructed from already-loaded draft)
internal sealed class StatementDraftEntriesListViewModel : BaseListViewModel<StatementDraftEntryItem>
{
    // Quick-edit state: original snapshot and current edited values per entry id
    private readonly Dictionary<Guid, IDictionary<string, object?>> _editValues = new();
    private readonly Dictionary<Guid, IDictionary<string, object?>> _originalValues = new();
    private readonly Guid _draftId;
    private List<StatementDraftEntryDto> _allEntries = new();
    private int _skip;
    private readonly int _take = 50;
    // API client and maps for symbols/names (per-instance)
    private readonly IApiClient _api;
    private Dictionary<Guid, Guid?> _contactSymbols = new();
    private Dictionary<Guid, string?> _contactNames = new();
    private Dictionary<Guid, Guid?> _savingsPlanSymbols = new();
    private Dictionary<Guid, string?> _savingsPlanNames = new();
    private Dictionary<Guid, Guid?> _securitySymbols = new();
    private Dictionary<Guid, string?> _securityNames = new();
    private Guid? _accountBankContactId;
    private Guid? _selfContactId;
    // map of entry id -> hint text
    private readonly Dictionary<Guid, string> _entryHints = new();
    // flag to request UI focus on first invalid entry after validation
    private bool _focusFirstInvalidRequested;

    public StatementDraftEntriesListViewModel(IServiceProvider sp, Guid draftId)
        : base(sp)
    {
        _draftId = draftId;
        _api = sp.GetRequiredService<IApiClient>();
    }

    /// <summary>
    /// Fields that are editable in quick-edit mode for entries.
    /// </summary>
    // Editable fields for quick-edit mode. Order is not important here but must include all keys the UI may edit.
    public override IReadOnlyList<string> EditableFields => new[] { "BookingDate", "ValutaDate", "Amount", "BookingDescription", "RecipientName", "Subject" };

    /// <summary>
    /// Returns whether the specified row/item is editable in quick-edit mode.
    /// Entries with status AlreadyBooked are not editable.
    /// </summary>
    /// <param name="item">Row item instance.</param>
    public override bool IsRowEditable(object item)
    {
        if (item is StatementDraftEntryItem sdi)
            return sdi.Status != StatementDraftEntryStatus.AlreadyBooked;
        return false;
    }

    /// <summary>
    /// Begins quick-edit session by preparing original and edit snapshots for currently loaded items.
    /// </summary>
    public override Task BeginQuickEditAsync()
    {
        _editValues.Clear();
        _originalValues.Clear();
        foreach (var it in Items)
        {
            var dict = new Dictionary<string, object?>
            {
                ["BookingDate"] = it.BookingDate,
                ["ValutaDate"] = it.ValutaDate,
                ["RecipientName"] = it.RecipientName,
                ["Subject"] = it.Subject,
                ["Amount"] = it.Amount,
                ["BookingDescription"] = it.BookingDescription,
                ["Status"] = it.Status
            };
            _originalValues[it.Id] = new Dictionary<string, object?>(dict);
            _editValues[it.Id] = new Dictionary<string, object?>(dict);
        }
        return base.BeginQuickEditAsync();
    }

    /// <summary>
    /// Ends quick-edit session. Default implementation clears edit snapshots.
    /// </summary>
    public override Task EndQuickEditAsync()
    {
        _editValues.Clear();
        _originalValues.Clear();
        return base.EndQuickEditAsync();
    }

    /// <summary>
    /// Returns the current edited value for the given entry id and field key.
    /// </summary>
    public object? GetEditValue(Guid entryId, string field)
    {
        if (_editValues.TryGetValue(entryId, out var map) && map.TryGetValue(field, out var v))
            return v;
        return null;
    }

    /// <summary>
    /// Sets an edited value for the given entry id and field key.
    /// Raises state changed so UI can re-render.
    /// </summary>
    public void SetEditValue(Guid entryId, string field, object? value)
    {
        if (!_editValues.TryGetValue(entryId, out var map)) return;
        map[field] = value;
        RaiseStateChanged();
    }

    /// <summary>
    /// Resets the edited values for a given entry to the original snapshot.
    /// </summary>
    public void ResetRow(Guid entryId)
    {
        if (_originalValues.TryGetValue(entryId, out var orig))
        {
            _editValues[entryId] = new Dictionary<string, object?>(orig);
            // Also restore visible values on the lightweight item so UI shows restored values
            var item = Items.FirstOrDefault(i => i.Id == entryId);
            if (item != null)
            {
                if (orig.TryGetValue("BookingDate", out var bd) && bd is DateTime bdt) item.BookingDate = bdt;
                if (orig.TryGetValue("ValutaDate", out var vd))
                {
                    if (vd is DateTime vdt) item.ValutaDate = vdt;
                    else item.ValutaDate = null;
                }
                if (orig.TryGetValue("Amount", out var am) && am is decimal d) item.Amount = d;
                if (orig.TryGetValue("RecipientName", out var rn)) item.RecipientName = rn as string;
                if (orig.TryGetValue("Subject", out var s)) item.Subject = s as string;
                if (orig.TryGetValue("BookingDescription", out var bdsc)) item.BookingDescription = bdsc as string;
                if (orig.TryGetValue("Status", out var st) && st is StatementDraftEntryStatus ss) item.Status = ss;
            }
            RaiseStateChanged();
        }
    }

    /// <summary>
    /// Collects changed rows as a mapping EntryId -> (field -> newValue).
    /// Only fields that differ from the original snapshot are returned.
    /// </summary>
    public override IReadOnlyDictionary<Guid, IDictionary<string, object?>> CollectChangedRows()
    {
        var result = new Dictionary<Guid, IDictionary<string, object?>>();
        foreach (var kv in _editValues)
        {
            if (!_originalValues.TryGetValue(kv.Key, out var orig)) continue;
            var diffs = new Dictionary<string, object?>();
            foreach (var f in kv.Value.Keys)
            {
                var newV = kv.Value[f];
                orig.TryGetValue(f, out var oldV);
                if (!object.Equals(newV, oldV))
                    diffs[f] = newV;
            }
            // Additionally, allow status changes applied to the lightweight item (e.g., ResetDup) to be included
            if (Items.FirstOrDefault(i => i.Id == kv.Key) is var lightweight && lightweight != null)
            {
                if (lightweight.Status != null)
                {
                    // if original snapshot did not include status change, include it
                    if (!orig.TryGetValue("Status", out var origStatus) || !object.Equals(origStatus, lightweight.Status))
                    {
                        diffs["Status"] = lightweight.Status;
                    }
                }
            }
            if (diffs.Count > 0) result[kv.Key] = diffs;
        }
        return result;
    }

    /// <summary>
    /// Performs basic client-side validation for a single row based on current edit values.
    /// Returns tuples of (field, message) for validation errors.
    /// </summary>
    public override IEnumerable<(string Field, string Message)> ValidateRow(object item)
    {
        if (item is not StatementDraftEntryItem it) yield break;
        if (!_editValues.TryGetValue(it.Id, out var map)) yield break;

        // BookingDate must be set
        if (map.TryGetValue("BookingDate", out var bd) && bd is DateTime dt)
        {
            if (dt == DateTime.MinValue)
                yield return ("BookingDate", "Booking date is required");
        }

        // Amount must be a valid decimal
        if (map.TryGetValue("Amount", out var amt))
        {
            if (amt == null || !(amt is decimal))
                yield return ("Amount", "Amount is required");
        }

        // Subject length
        if (map.TryGetValue("Subject", out var subj) && subj is string s)
        {
            if (s.Length > 500) yield return ("Subject", "Subject too long");
        }

        // RecipientName length
        if (map.TryGetValue("RecipientName", out var rec) && rec is string r)
        {
            if (r.Length > 250) yield return ("RecipientName", "Recipient name too long");
        }
    }

    protected override async Task LoadPageAsync(bool resetPaging)
    {
        if (resetPaging)
        {
            _skip = 0;
            Items.Clear();
            _allEntries.Clear();
            // fetch full draft details once (API does not provide paged entries endpoint)
            try
            {
                var draft = await _api.StatementDrafts_GetAsync(_draftId, headerOnly: false, ct: CancellationToken.None);
                if (draft?.Entries != null)
                {
                    _allEntries = draft.Entries
                        .OrderBy(e => e.Status == StatementDraftEntryStatus.AlreadyBooked ? 2 : e.Status == StatementDraftEntryStatus.Announced ? 1 : 0)
                        .ThenBy(e => e.EntryNumber)
                        .ThenBy(e => e.BookingDate)
                        .ThenBy(e => e.BookingDescription)
                        .ThenBy(e => e.RecipientName)
                        .ToList();
                }
                // capture symbol/name maps from draft so list can show icons and names similar to StatementDraftDetail page
                _contactSymbols = draft?.ContactSymbols != null ? new Dictionary<Guid, Guid?>(draft.ContactSymbols) : new Dictionary<Guid, Guid?>();
                _contactNames = draft?.ContactNames != null ? new Dictionary<Guid, string?>(draft.ContactNames) : new Dictionary<Guid, string?>();
                _savingsPlanSymbols = draft?.SavingsPlanSymbols != null ? new Dictionary<Guid, Guid?>(draft.SavingsPlanSymbols) : new Dictionary<Guid, Guid?>();
                _savingsPlanNames = draft?.SavingsPlanNames != null ? new Dictionary<Guid, string?>(draft.SavingsPlanNames) : new Dictionary<Guid, string?>();
                _securitySymbols = draft?.SecuritySymbols != null ? new Dictionary<Guid, Guid?>(draft.SecuritySymbols) : new Dictionary<Guid, Guid?>();
                _securityNames = draft?.SecurityNames != null ? new Dictionary<Guid, string?>(draft.SecurityNames) : new Dictionary<Guid, string?>();
                _accountBankContactId = draft?.AccountBankContactId;
                _selfContactId = draft?.SelfContactId;
            }
            catch
            {
                _allEntries = new List<StatementDraftEntryDto>();
                _accountBankContactId = null;
                _selfContactId = null;
            }
        }

        // append next page from cached entries
        var pageDtos = _allEntries.Skip(_skip).Take(_take).ToList();
        if (pageDtos.Count > 0)
        {
            // convert DTOs to lightweight navigable items
            var pageItems = pageDtos.Select(d => new StatementDraftEntryItem
            {
                Id = d.Id,
                DraftId = _draftId,
                BookingDate = d.BookingDate,
                    ValutaDate = d.ValutaDate,
                Amount = d.Amount,
                RecipientName = d.RecipientName,
                    Subject = d.Subject,
                BookingDescription = d.BookingDescription,
                Status = d.Status,
                ContactId = d.ContactId,
                SavingsPlanId = d.SavingsPlanId,
                SecurityId = d.SecurityId,
                SecurityTransactionType = d.SecurityTransactionType,
                BudgetImpact = d.BudgetImpact
            }).ToList();

            Items.AddRange(pageItems);
            _skip += pageItems.Count;
        }
        CanLoadMore = _skip < _allEntries.Count;
        BuildRecords();
    }

    protected override void BuildRecords()
    {
        var L = ServiceProvider.GetRequiredService<IStringLocalizer<Pages>>();
        Columns = new ListColumn[] {
            new ListColumn("symbol", string.Empty, "2.5rem", ListColumnAlign.Left),
            new ListColumn("date", L["List_Th_Date"].Value ?? "Date", "8rem", ListColumnAlign.Left),
            new ListColumn("amount", L["List_Th_Amount"].Value ?? "Amount", "10rem", ListColumnAlign.Right),
            new ListColumn("recipient", L["List_Th_Recipient"].Value ?? "Recipient", "", ListColumnAlign.Left),
            new ListColumn("subject", L["List_Th_Subject"].Value ?? "Subject", "", ListColumnAlign.Left),
            new ListColumn("savingsplan", L["List_Th_SavingsPlan"].Value ?? "SavingsPlan", "12rem", ListColumnAlign.Left),
            new ListColumn("security", L["List_Th_Security"].Value ?? "Security", "12rem", ListColumnAlign.Left),
            new ListColumn("status", L["List_Th_Status"].Value ?? "Status", "8rem", ListColumnAlign.Left)
        };

        Records = Items.Select(i => {
            // resolve symbol ids and names from draft-level maps if available
            _contactSymbols.TryGetValue(i.Id, out var contactSym);
            _contactNames.TryGetValue(i.Id, out var contactName);
            _savingsPlanSymbols.TryGetValue(i.Id, out var planSym);
            _savingsPlanNames.TryGetValue(i.Id, out var planName);
            _securitySymbols.TryGetValue(i.Id, out var secSym);
            _securityNames.TryGetValue(i.Id, out var secName);

            var isMuted = i.Status == StatementDraftEntryStatus.AlreadyBooked;
            var securityText = BuildSecurityText(secName, i.SecurityTransactionType, L);
            var cells = new List<ListCell>
            {
                new ListCell(ListCellKind.Symbol, SymbolId: contactSym, Muted: isMuted),
                new ListCell(ListCellKind.Text, Text: i.BookingDate.ToString("d"), Muted: isMuted),
                new ListCell(ListCellKind.Currency, Amount: i.Amount, Muted: isMuted),
                new ListCell(ListCellKind.Text, Text: string.IsNullOrWhiteSpace(i.RecipientName) ? string.Empty : i.RecipientName, Muted: isMuted),
                new ListCell(ListCellKind.Text, Text: string.IsNullOrWhiteSpace(i.Subject) ? string.Empty : i.Subject, Muted: isMuted),
                new ListCell(ListCellKind.Text, Text: (string.IsNullOrWhiteSpace(planName) && planSym == null) ? string.Empty : (planName ?? string.Empty), Muted: isMuted),
                new ListCell(ListCellKind.Text, Text: (string.IsNullOrWhiteSpace(secName) && secSym == null) ? string.Empty : (secName ?? string.Empty), Muted: isMuted),
                new ListCell(ListCellKind.Text, Text: i.Status.ToString(), Muted: isMuted)
            };
            var mobileRows = BuildMobileRows(i, contactSym, contactName, planName, securityText, isMuted, L);
            // attach hint for this entry if available
            _entryHints.TryGetValue(i.Id, out var hint);
            return new ListRecord(cells.ToArray(), i, hint, mobileRows);
        }).ToList();
    }

    private IReadOnlyList<ListMobileRow> BuildMobileRows(
        StatementDraftEntryItem item,
        Guid? contactSymbol,
        string? contactName,
        string? savingsPlanName,
        string? securityText,
        bool isMuted,
        IStringLocalizer<Pages> localizer)
    {
        var rows = new List<ListMobileRow>();

        if (contactSymbol.HasValue)
        {
            rows.Add(new ListMobileRow(new[]
            {
                new ListMobileCell(null, new ListCell(ListCellKind.Symbol, SymbolId: contactSymbol, Muted: isMuted))
            }, CssClass: "statement-draft-entry-symbol"));
        }

        rows.Add(new ListMobileRow(new[]
        {
            new ListMobileCell(ResolveLabel(localizer, "List_Th_Date", "Date"), new ListCell(ListCellKind.Text, Text: item.BookingDate.ToString("d"), Muted: isMuted)),
            new ListMobileCell(ResolveLabel(localizer, "List_Th_Amount", "Amount"), new ListCell(ListCellKind.Currency, Amount: item.Amount, Muted: isMuted))
        }, ListMobileRowKind.TwoColumn, "statement-draft-entry-date-amount"));

        var contactOrRecipient = GetMobileContactOrRecipient(item, contactName, localizer);
        if (contactOrRecipient.Text != null)
        {
            rows.Add(new ListMobileRow(new[]
            {
                new ListMobileCell(contactOrRecipient.Label, new ListCell(ListCellKind.Text, Text: contactOrRecipient.Text, Muted: isMuted))
            }, CssClass: contactOrRecipient.CssClass));
        }

        if (!string.IsNullOrWhiteSpace(item.Subject))
        {
            rows.Add(new ListMobileRow(new[]
            {
                new ListMobileCell(ResolveLabel(localizer, "List_Th_Subject", "Subject"), new ListCell(ListCellKind.Text, Text: item.Subject, Muted: isMuted))
            }));
        }

        if (!string.IsNullOrWhiteSpace(savingsPlanName))
        {
            rows.Add(new ListMobileRow(new[]
            {
                new ListMobileCell(ResolveLabel(localizer, "List_Th_SavingsPlan", "SavingsPlan"), new ListCell(ListCellKind.Text, Text: savingsPlanName, Muted: isMuted))
            }));
        }

        if (!string.IsNullOrWhiteSpace(securityText))
        {
            rows.Add(new ListMobileRow(new[]
            {
                new ListMobileCell(ResolveLabel(localizer, "List_Th_Security", "Security"), new ListCell(ListCellKind.Text, Text: securityText, Muted: isMuted))
            }));
        }

        if (item.Status == StatementDraftEntryStatus.Open)
        {
            rows.Add(new ListMobileRow(new[]
            {
                new ListMobileCell(ResolveLabel(localizer, "List_Th_Status", "Status"), new ListCell(ListCellKind.Text, Text: BuildStatusText(item.Status, localizer), Muted: isMuted))
            }));
        }

        return rows;
    }

    private (string? Label, string? Text, string? CssClass) GetMobileContactOrRecipient(StatementDraftEntryItem item, string? contactName, IStringLocalizer<Pages> localizer)
    {
        if (item.ContactId.HasValue)
        {
            var contactId = item.ContactId.Value;
            var isBankContact = _accountBankContactId.HasValue && contactId == _accountBankContactId.Value;
            var isSelfContact = _selfContactId.HasValue && contactId == _selfContactId.Value;
            if (!isBankContact && !isSelfContact && !string.IsNullOrWhiteSpace(contactName))
            {
                return (ResolveLabel(localizer, "List_Th_Contact", "Contact"), contactName, "statement-draft-entry-contact");
            }

            return (null, null, null);
        }

        if (!string.IsNullOrWhiteSpace(item.RecipientName))
        {
            return (ResolveLabel(localizer, "List_Th_Recipient", "Recipient"), item.RecipientName, "statement-draft-entry-recipient");
        }

        return (null, null, null);
    }

    private static string? BuildSecurityText(string? securityName, SecurityTransactionType? transactionType, IStringLocalizer<Pages> localizer)
    {
        if (string.IsNullOrWhiteSpace(securityName))
        {
            return null;
        }

        if (!transactionType.HasValue)
        {
            return securityName;
        }

        var typeKey = $"EnumType_SecurityTransactionType_{transactionType.Value}";
        var localized = localizer[typeKey];
        var typeText = localized.ResourceNotFound || string.Equals(localized.Value, typeKey, StringComparison.Ordinal)
            ? transactionType.Value.ToString()
            : localized.Value;
        return $"{securityName} ({typeText})";
    }

    private static string BuildStatusText(StatementDraftEntryStatus status, IStringLocalizer<Pages> localizer)
    {
        var key = $"EnumType_StatementDraftEntryStatus_{status}";
        var localized = localizer[key];
        return localized.ResourceNotFound || string.IsNullOrWhiteSpace(localized.Value) || string.Equals(localized.Value, key, StringComparison.Ordinal)
            ? status.ToString()
            : localized.Value;
    }

    private static string ResolveLabel(IStringLocalizer<Pages> localizer, string key, string fallback)
    {
        var localized = localizer[key];
        return localized.ResourceNotFound || string.IsNullOrWhiteSpace(localized.Value) || string.Equals(localized.Value, key, StringComparison.Ordinal)
            ? fallback
            : localized.Value;
    }

    public void ApplyValidationMessages(DraftValidationResultDto? result)
    {
        _entryHints.Clear();
        var L = ServiceProvider.GetRequiredService<IStringLocalizer<Pages>>();
        if (result != null && result.Messages != null)
        {
            var byEntry = result.Messages.Where(m => m.EntryId != null)
                .GroupBy(m => m.EntryId!.Value);
            foreach (var g in byEntry)
            {
                // Build localized message per entry: translate severity and known message texts when possible
                var parts = new List<string>();
                foreach (var m in g)
                {
                    // translate severity (e.g. Error -> Fehler)
                    string severityKey = $"Validation_Severity_{m.Severity}";
                    var severityLocalized = L[severityKey].Value;
                    if (string.IsNullOrWhiteSpace(severityLocalized) || severityLocalized == severityKey)
                    {
                        severityLocalized = m.Severity ?? string.Empty;
                    }

                    // try to map common English message texts to resource keys (e.g. "Invalid date format")
                    string normalized = string.Empty;
                    if (!string.IsNullOrWhiteSpace(m.Message))
                    {
                        var partsWords = m.Message.Split(new[] { ' ', '\t', '\r', '\n', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
                        normalized = string.Concat(partsWords.Select(w => char.ToUpperInvariant(w[0]) + (w.Length > 1 ? w.Substring(1) : string.Empty)));
                    }

                    string msgLocalized = string.Empty;
                    if (!string.IsNullOrWhiteSpace(normalized))
                    {
                        var msgKey = $"Validation_Message_{normalized}";
                        var candidate = L[msgKey].Value;
                        if (!string.IsNullOrWhiteSpace(candidate) && candidate != msgKey)
                        {
                            msgLocalized = candidate;
                        }
                    }

                    if (string.IsNullOrWhiteSpace(msgLocalized))
                    {
                        // fallback to original server-provided message
                        msgLocalized = m.Message ?? string.Empty;
                    }

                    parts.Add($"[{severityLocalized}] {msgLocalized}");
                }

                var combined = string.Join("; ", parts);
                _entryHints[g.Key] = combined;
            }
        }
        // rebuild records so hints are applied
        BuildRecords();
        RaiseStateChanged();
    }

    /// <summary>
    /// Request that the UI focuses the first entry that has validation hints.
    /// The request is consumed by the component rendering the list.
    /// </summary>
    public void RequestFocusFirstInvalid()
    {
        _focusFirstInvalidRequested = true;
        RaiseStateChanged();
    }

    /// <summary>
    /// If a focus request was previously issued, returns the first entry id that has a hint and clears the request.
    /// </summary>
    public Guid? ConsumeFocusFirstInvalid()
    {
        if (!_focusFirstInvalidRequested) return null;
        _focusFirstInvalidRequested = false;
        if (_entryHints.Count == 0) return null;
        return _entryHints.Keys.FirstOrDefault();
    }

    /// <summary>
    /// Validates client-side edit state for all changed rows and returns whether all rows are valid.
    /// Also populates _entryHints for display.
    /// </summary>
    public bool ValidateAllChangedRows()
    {
        _entryHints.Clear();
        var changed = CollectChangedRows();
        foreach (var kv in changed)
        {
            var id = kv.Key;
            var recItem = Items.FirstOrDefault(i => i.Id == id);
            if (recItem == null) continue;
            var errors = ValidateRow(recItem).ToList();
            if (errors.Any())
            {
                _entryHints[id] = string.Join("; ", errors.Select(e => $"{e.Field}: {e.Message}"));
            }
        }
        BuildRecords();
        RaiseStateChanged();
        return !_entryHints.Any();
    }

    /// <summary>
    /// Returns true when there are any changed rows pending in the quick-edit buffer.
    /// </summary>
    public bool HasChangedRows()
    {
        var changed = CollectChangedRows();
        return changed != null && changed.Count > 0;
    }

    /// <summary>
    /// Performs a non-mutating client-side validation of changed rows and returns whether they are all valid.
    /// Does not populate hints or mutate state.
    /// </summary>
    public bool ChangedRowsAreValid()
    {
        var changed = CollectChangedRows();
        foreach (var kv in changed)
        {
            var id = kv.Key;
            var recItem = Items.FirstOrDefault(i => i.Id == id);
            if (recItem == null) continue;
            var errors = ValidateRow(recItem);
            if (errors.Any()) return false;
        }
        return true;
    }
}
