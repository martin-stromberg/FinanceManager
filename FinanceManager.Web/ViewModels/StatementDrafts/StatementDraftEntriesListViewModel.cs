using FinanceManager.Shared;
using Microsoft.Extensions.Localization;

namespace FinanceManager.Web.ViewModels.StatementDrafts;

// Embedded list view model for statement draft entries (non-persistent, constructed from already-loaded draft)
internal sealed class StatementDraftEntriesListViewModel : BaseListViewModel<StatementDraftEntryItem>
{
    private readonly Guid _draftId;
    private List<StatementDraftEntryDto> _allEntries = new();
    private int _skip;
    private readonly int _take = 50;
    // API client and maps for symbols/names (per-instance)
    private readonly IApiClient _api;
    private Dictionary<Guid, Guid?> _contactSymbols = new();
    private Dictionary<Guid, Guid?> _savingsPlanSymbols = new();
    private Dictionary<Guid, string?> _savingsPlanNames = new();
    private Dictionary<Guid, Guid?> _securitySymbols = new();
    private Dictionary<Guid, string?> _securityNames = new();
    // map of entry id -> hint text
    private readonly Dictionary<Guid, string> _entryHints = new();

    public StatementDraftEntriesListViewModel(IServiceProvider sp, Guid draftId)
        : base(sp)
    {
        _draftId = draftId;
        _api = sp.GetRequiredService<IApiClient>();
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
                        .ThenBy(e => e.BookingDate)
                        .ThenBy(e => e.BookingDescription)
                        .ThenBy(e => e.RecipientName)
                        .ToList();
                }
                // capture symbol/name maps from draft so list can show icons and names similar to StatementDraftDetail page
                _contactSymbols = draft?.ContactSymbols != null ? new Dictionary<Guid, Guid?>(draft.ContactSymbols) : new Dictionary<Guid, Guid?>();
                _savingsPlanSymbols = draft?.SavingsPlanSymbols != null ? new Dictionary<Guid, Guid?>(draft.SavingsPlanSymbols) : new Dictionary<Guid, Guid?>();
                _savingsPlanNames = draft?.SavingsPlanNames != null ? new Dictionary<Guid, string?>(draft.SavingsPlanNames) : new Dictionary<Guid, string?>();
                _securitySymbols = draft?.SecuritySymbols != null ? new Dictionary<Guid, Guid?>(draft.SecuritySymbols) : new Dictionary<Guid, Guid?>();
                _securityNames = draft?.SecurityNames != null ? new Dictionary<Guid, string?>(draft.SecurityNames) : new Dictionary<Guid, string?>();
            }
            catch
            {
                _allEntries = new List<StatementDraftEntryDto>();
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
                Amount = d.Amount,
                RecipientName = d.RecipientName,
                Subject = d.Subject,
                Status = d.Status
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
            _savingsPlanSymbols.TryGetValue(i.Id, out var planSym);
            _savingsPlanNames.TryGetValue(i.Id, out var planName);
            _securitySymbols.TryGetValue(i.Id, out var secSym);
            _securityNames.TryGetValue(i.Id, out var secName);

            var isMuted = i.Status == StatementDraftEntryStatus.AlreadyBooked;
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
            // attach hint for this entry if available
            _entryHints.TryGetValue(i.Id, out var hint);
            return new ListRecord(cells.ToArray(), i, hint);
        }).ToList();
    }

    public void ApplyValidationMessages(DraftValidationResultDto? result)
    {
        _entryHints.Clear();
        if (result != null && result.Messages != null)
        {
            var byEntry = result.Messages.Where(m => m.EntryId != null)
                .GroupBy(m => m.EntryId!.Value);
            foreach (var g in byEntry)
            {
                var combined = string.Join("; ", g.Select(m => $"[{m.Severity}] {m.Message}"));
                _entryHints[g.Key] = combined;
            }
        }
        // rebuild records so hints are applied
        BuildRecords();
        RaiseStateChanged();
    }
}
