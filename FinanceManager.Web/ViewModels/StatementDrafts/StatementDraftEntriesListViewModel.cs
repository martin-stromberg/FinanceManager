using FinanceManager.Shared;
using Microsoft.Extensions.Localization;
using System.Linq;
using System.Collections.Generic;

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
    // id of the row whose BookingDate input should receive focus when quick-edit opens
    private Guid? _focusQuickEditBookingDateId;
    private readonly HashSet<Guid> _pendingDeleteIds = new();
    private readonly HashSet<Guid> _newEntryIds = new();
    private Guid? _placeholderId;

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
            return sdi.IsNew || sdi.IsPlaceholder || (sdi.Status != StatementDraftEntryStatus.AlreadyBooked && !sdi.IsAnnounced);
        return false;
    }

    public IReadOnlyList<StatementDraftEntryItem> VisibleQuickEditItems => Items
        .Where(i => !_pendingDeleteIds.Contains(i.Id) || _entryHints.ContainsKey(i.Id))
        .ToList();

    public bool CanDeleteRow(StatementDraftEntryItem item)
        => !item.IsPlaceholder
           && (item.IsNew || item.Status != StatementDraftEntryStatus.AlreadyBooked);

    private StatementDraftEntryItem ToItem(StatementDraftEntryDto d) => new()
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
        IsAnnounced = d.IsAnnounced,
        ContactId = d.ContactId,
        SavingsPlanId = d.SavingsPlanId,
        SecurityId = d.SecurityId,
        SecurityTransactionType = d.SecurityTransactionType,
        BudgetImpact = d.BudgetImpact,
        CanDelete = d.Status != StatementDraftEntryStatus.AlreadyBooked
    };

    private Dictionary<string, object?> CreateEditSnapshot(StatementDraftEntryItem it) => new()
    {
        ["BookingDate"] = it.IsPlaceholder ? null : it.BookingDate,
        ["ValutaDate"] = it.ValutaDate,
        ["RecipientName"] = it.RecipientName,
        ["Subject"] = it.Subject,
        ["Amount"] = it.IsPlaceholder ? null : it.Amount,
        ["BookingDescription"] = it.BookingDescription,
        ["Status"] = it.Status
    };

    private void AddPlaceholderRow()
    {
        var id = Guid.NewGuid();
        _placeholderId = id;
        var placeholder = new StatementDraftEntryItem
        {
            Id = id,
            DraftId = _draftId,
            BookingDate = DateTime.MinValue,
            Status = StatementDraftEntryStatus.Open,
            IsPlaceholder = true
        };
        Items.Add(placeholder);
        _editValues[id] = CreateEditSnapshot(placeholder);
    }

    private void RestoreLoadedItems()
    {
        var loadedCount = _skip > 0 ? _skip : _take;
        Items.Clear();
        Items.AddRange(_allEntries.Take(loadedCount).Select(ToItem));
        CanLoadMore = _skip < _allEntries.Count;
        BuildRecords();
    }

    /// <summary>
    /// Begins quick-edit session by preparing original and edit snapshots for currently loaded items.
    /// </summary>
    public override Task BeginQuickEditAsync()
    {
        _editValues.Clear();
        _originalValues.Clear();
        _pendingDeleteIds.Clear();
        _newEntryIds.Clear();
        _entryHints.Clear();
        _placeholderId = null;
        Items.RemoveAll(i => i.IsNew || i.IsPlaceholder);
        foreach (var it in Items)
        {
            it.IsNew = false;
            it.IsPlaceholder = false;
            it.CanDelete = CanDeleteRow(it);
            var dict = CreateEditSnapshot(it);
            _originalValues[it.Id] = new Dictionary<string, object?>(dict);
            _editValues[it.Id] = new Dictionary<string, object?>(dict);
        }
        AddPlaceholderRow();
        BuildRecords();
        _focusQuickEditBookingDateId = Items.FirstOrDefault()?.Id;
        return base.BeginQuickEditAsync();
    }

    /// <summary>
    /// Ends quick-edit session. Default implementation clears edit snapshots.
    /// </summary>
    public override Task EndQuickEditAsync()
    {
        _editValues.Clear();
        _originalValues.Clear();
        _pendingDeleteIds.Clear();
        _newEntryIds.Clear();
        _entryHints.Clear();
        _placeholderId = null;
        RestoreLoadedItems();
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
        var item = Items.FirstOrDefault(i => i.Id == entryId);
        if (item != null)
        {
            switch (field)
            {
                case "BookingDate":
                    item.BookingDate = value is DateTime bd ? bd : DateTime.MinValue;
                    break;
                case "ValutaDate":
                    item.ValutaDate = value is DateTime vd ? vd : null;
                    break;
                case "Amount":
                    item.Amount = value is decimal amount ? amount : 0m;
                    break;
                case "RecipientName":
                    item.RecipientName = value as string;
                    break;
                case "Subject":
                    item.Subject = value as string;
                    break;
                case "BookingDescription":
                    item.BookingDescription = value as string;
                    break;
                case "Status":
                    if (value is StatementDraftEntryStatus status) item.Status = status;
                    break;
            }
        }
        if (_placeholderId == entryId && PlaceholderHasUserInput(map))
        {
            PromotePlaceholder(entryId);
        }
        RaiseStateChanged();
    }

    private static bool PlaceholderHasUserInput(IDictionary<string, object?> map)
        => (map.TryGetValue("BookingDate", out var bd) && bd is DateTime)
           || (map.TryGetValue("ValutaDate", out var vd) && vd is DateTime)
           || (map.TryGetValue("Amount", out var amount) && amount is decimal)
           || (map.TryGetValue("Subject", out var subject) && !string.IsNullOrWhiteSpace(subject as string))
           || (map.TryGetValue("BookingDescription", out var desc) && !string.IsNullOrWhiteSpace(desc as string))
           || (map.TryGetValue("RecipientName", out var rec) && !string.IsNullOrWhiteSpace(rec as string));

    private void PromotePlaceholder(Guid entryId)
    {
        var item = Items.FirstOrDefault(i => i.Id == entryId);
        if (item == null || !item.IsPlaceholder) return;
        item.IsPlaceholder = false;
        item.IsNew = true;
        item.CanDelete = true;
        _newEntryIds.Add(entryId);
        _placeholderId = null;
        AddPlaceholderRow();
    }

    public void MarkRowForDeletion(Guid entryId)
    {
        var item = Items.FirstOrDefault(i => i.Id == entryId);
        if (item == null || item.IsPlaceholder || !CanDeleteRow(item)) return;

        _entryHints.Remove(entryId);
        if (item.IsNew)
        {
            Items.Remove(item);
            _newEntryIds.Remove(entryId);
            _editValues.Remove(entryId);
        }
        else
        {
            _pendingDeleteIds.Add(entryId);
        }
        BuildRecords();
        RaiseStateChanged();
    }

    /// <summary>
    /// Resets the edited values for a given entry to the original snapshot.
    /// </summary>
    public void ResetRow(Guid entryId)
    {
        _pendingDeleteIds.Remove(entryId);
        _entryHints.Remove(entryId);
        if (_newEntryIds.Contains(entryId))
        {
            MarkRowForDeletion(entryId);
            return;
        }
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
            if (_pendingDeleteIds.Contains(kv.Key) || _newEntryIds.Contains(kv.Key) || _placeholderId == kv.Key) continue;
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

    public IReadOnlyList<Guid> CollectPendingDeleteIds() => _pendingDeleteIds.ToList();

    public IReadOnlyList<EntryCreateDto> CollectCreateRows()
    {
        var result = new List<EntryCreateDto>();
        foreach (var id in _newEntryIds.ToList())
        {
            if (!_editValues.TryGetValue(id, out var map)) continue;
            var bookingDate = map.TryGetValue("BookingDate", out var bd) && bd is DateTime bdt ? bdt.Date : DateTime.MinValue;
            var valutaDate = map.TryGetValue("ValutaDate", out var vd) && vd is DateTime vdt ? vdt.Date : (DateTime?)null;
            var amount = map.TryGetValue("Amount", out var am) && am is decimal dec ? dec : 0m;
            var subject = map.TryGetValue("Subject", out var subj) ? subj as string : null;
            var bookingDescription = map.TryGetValue("BookingDescription", out var desc) ? desc as string : null;
            var recipientName = map.TryGetValue("RecipientName", out var rec) ? rec as string : null;
            result.Add(new EntryCreateDto
            {
                ClientId = id,
                BookingDate = bookingDate,
                ValutaDate = valutaDate,
                Amount = amount,
                Subject = subject ?? string.Empty,
                BookingDescription = bookingDescription,
                RecipientName = recipientName
            });
        }
        return result;
    }

    public BatchUpdateRequestDto CollectQuickEditSaveRequest()
    {
        var req = new BatchUpdateRequestDto();
        foreach (var kv in CollectChangedRows())
        {
            req.Updates.Add(new EntryUpdateDto { EntryId = kv.Key, Fields = new Dictionary<string, object?>(kv.Value) });
        }
        req.Deletes.AddRange(CollectPendingDeleteIds());
        req.Creates.AddRange(CollectCreateRows());
        return req;
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
        if (!map.TryGetValue("BookingDate", out var bd) || bd is not DateTime dt)
        {
            if (it.IsNew || it.IsPlaceholder)
                yield return ("BookingDate", "Booking date is required");
        }
        else
        {
            if (dt == DateTime.MinValue)
                yield return ("BookingDate", "Booking date is required");
        }

        // Amount must be a valid decimal
        if (map.TryGetValue("Amount", out var amt))
        {
            if (amt == null || !(amt is decimal))
                yield return ("Amount", "Amount is required");
            else if (amt is decimal dec && dec == 0m)
                yield return ("Amount", "Amount must not be zero");
        }
        else if (it.IsNew || it.IsPlaceholder)
        {
            yield return ("Amount", "Amount is required");
        }

        // Subject length
        if (map.TryGetValue("Subject", out var subj) && subj is string s)
        {
            if ((it.IsNew || it.IsPlaceholder) && string.IsNullOrWhiteSpace(s)) yield return ("Subject", "Subject is required");
            if (s.Length > 1000) yield return ("Subject", "Subject too long");
        }
        else if (it.IsNew || it.IsPlaceholder)
        {
            yield return ("Subject", "Subject is required");
        }

        // RecipientName length
        if (map.TryGetValue("RecipientName", out var rec) && rec is string r)
        {
            if (r.Length > 250) yield return ("RecipientName", "Recipient name too long");
        }

        if (map.TryGetValue("BookingDescription", out var desc) && desc is string bookingDescription)
        {
            if (bookingDescription.Length > 1000) yield return ("BookingDescription", "Booking description too long");
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
            var pageItems = pageDtos.Select(ToItem).ToList();

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

                    // First check if the message is a direct translation key (e.g. "Validation_ENTRY_NO_CONTACT")
                    string msgLocalized = string.Empty;
                    if (!string.IsNullOrWhiteSpace(m.Message))
                    {
                        // Check if the message contains a pipe separator (indicating translation key with parameters)
                        if (m.Message.Contains('|'))
                        {
                            var keyParts = m.Message.Split('|');
                            var key = keyParts[0];
                            var parameters = keyParts.Skip(1).ToArray();

                            // Try to get the localized string
                            try
                            {
                                var localizedString = L?[key];
                                if (localizedString != null && !localizedString.ResourceNotFound && localizedString.Value != null)
                                {
                                    // Format the message with parameters
                                    try
                                    {
                                        var formattedParams = new List<object>();
                                        foreach (var p in parameters)
                                        {
                                            // Try to parse as DateTime for date formatting
                                            if (DateTime.TryParse(p, out var dateValue))
                                            {
                                                formattedParams.Add(dateValue);
                                            }
                                            else
                                            {
                                                formattedParams.Add(p);
                                            }
                                        }
                                        
                                        msgLocalized = string.Format(localizedString.Value, formattedParams.ToArray());
                                    }
                                    catch (FormatException)
                                    {
                                        // If formatting fails, use the key as fallback
                                        msgLocalized = m.Message;
                                    }
                                }
                            }
                            catch (Exception)
                            {
                                // If localization fails, use the original message as fallback
                                msgLocalized = m.Message;
                            }
                        }
                        else
                        {
                            // Simple translation key without parameters
                            try
                            {
                                var localizedString = L?[m.Message];
                                if (localizedString != null && !localizedString.ResourceNotFound && localizedString.Value != null)
                                {
                                    msgLocalized = localizedString.Value;
                                }
                            }
                            catch (Exception)
                            {
                                // If localization fails, use the original message as fallback
                                msgLocalized = m.Message;
                            }
                        }
                    }

                    // If direct translation didn't work, try the old normalization approach
                    if (string.IsNullOrWhiteSpace(msgLocalized))
                    {
                        try
                        {
                            string normalized = string.Empty;
                            if (!string.IsNullOrWhiteSpace(m.Message))
                            {
                                var partsWords = m.Message.Split(new[] { ' ', '\t', '\r', '\n', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
                                normalized = string.Concat(partsWords.Select(w => char.ToUpperInvariant(w[0]) + (w.Length > 1 ? w.Substring(1) : string.Empty)));
                            }

                            if (!string.IsNullOrWhiteSpace(normalized))
                            {
                                var msgKey = $"Validation_Message_{normalized}";
                                var candidate = L?[msgKey]?.Value;
                                if (!string.IsNullOrWhiteSpace(candidate) && candidate != msgKey)
                                {
                                    msgLocalized = candidate;
                                }
                            }
                        }
                        catch (Exception)
                        {
                            // If normalization fails, use the original message as fallback
                            msgLocalized = m.Message ?? string.Empty;
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

    public void ApplyBatchValidationErrors(BatchUpdateErrorResponseDto? error)
    {
        _entryHints.Clear();
        if (error?.Errors != null)
        {
            foreach (var entryError in error.Errors)
            {
                var id = entryError.EntryId ?? entryError.ClientId;
                if (!id.HasValue) continue;
                _entryHints[id.Value] = string.Join("; ", entryError.FieldErrors.Select(e => $"{e.Field}: {e.Message}"));
            }
        }
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
    /// If quick-edit just opened, returns the id of the first row whose BookingDate input should be focused.
    /// The request is consumed by the component rendering the list.
    /// </summary>
    public Guid? ConsumeFocusQuickEditBookingDate()
    {
        var id = _focusQuickEditBookingDateId;
        _focusQuickEditBookingDateId = null;
        return id;
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

    public bool ValidateAllQuickEditRows()
    {
        _entryHints.Clear();
        var idsToValidate = CollectChangedRows().Keys
            .Concat(_newEntryIds)
            .Distinct()
            .ToList();
        foreach (var id in idsToValidate)
        {
            var recItem = Items.FirstOrDefault(i => i.Id == id);
            if (recItem == null || recItem.IsPlaceholder) continue;
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

    public bool HasPendingQuickEditChanges()
        => HasChangedRows() || _pendingDeleteIds.Count > 0 || _newEntryIds.Count > 0;

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

    public bool QuickEditRowsAreValid()
    {
        var idsToValidate = CollectChangedRows().Keys
            .Concat(_newEntryIds)
            .Distinct();
        foreach (var id in idsToValidate)
        {
            var recItem = Items.FirstOrDefault(i => i.Id == id);
            if (recItem == null || recItem.IsPlaceholder) continue;
            var errors = ValidateRow(recItem);
            if (errors.Any()) return false;
        }
        return true;
    }
}
