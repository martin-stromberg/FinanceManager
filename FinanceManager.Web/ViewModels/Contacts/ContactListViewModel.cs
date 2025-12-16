using FinanceManager.Shared;
using FinanceManager.Web.ViewModels.Common;
using Microsoft.Extensions.Localization;

namespace FinanceManager.Web.ViewModels.Contacts;

public sealed class ContactListViewModel : BaseListViewModel<ContactListItem>
{
    private readonly IApiClient _api;
    private readonly Dictionary<Guid, string> _categoryNames = new();

    public ContactListViewModel(IServiceProvider services) : base(services)
    {
        _api = services.GetRequiredService<IApiClient>();
    }

    public override bool AllowRangeFiltering => false;

    private int _skip;
    private const int PageSize = 50;

    protected override async Task LoadPageAsync(bool resetPaging)
    {
        if (resetPaging) { _skip = 0; }
        try
        {
            // ensure categories loaded once so we can map ids -> names
            if (_categoryNames.Count == 0)
            {
                try
                {
                    var cats = await _api.ContactCategories_ListAsync();
                    foreach (var c in cats ?? Enumerable.Empty<ContactCategoryDto>())
                    {
                        if (c != null) _categoryNames[c.Id] = c.Name ?? string.Empty;
                    }
                }
                catch
                {
                    // ignore category load failure; leave dictionary empty
                }
            }

            var list = await _api.Contacts_ListAsync(skip: _skip, take: PageSize, type: null, all: false, nameFilter: string.IsNullOrWhiteSpace(Search) ? null : Search);
            var items = (list ?? Array.Empty<ContactDto>()).Select(c =>
            {
                var catName = c.CategoryId.HasValue && _categoryNames.TryGetValue(c.CategoryId.Value, out var nm) ? nm : string.Empty;
                return new ContactListItem(c.Id, c.Name ?? string.Empty, c.Type.ToString(), catName, c.SymbolAttachmentId);
            });
            if (resetPaging) Items.Clear();
            Items.AddRange(items);
            _skip += PageSize;
            CanLoadMore = list != null && list.Count >= PageSize;
        }
        catch (Exception ex)
        {
            SetError(_api.LastErrorCode ?? null, _api.LastError ?? ex.Message);
            CanLoadMore = false;
        }
        finally { RaiseStateChanged(); }
    }

    protected override void BuildRecords()
    {
        var L = ServiceProvider.GetRequiredService<IStringLocalizer<Pages>>();
        Columns = new List<ListColumn>
        {
            new ListColumn("symbol", string.Empty, "56px", ListColumnAlign.Left),
            new ListColumn("name", L["List_Th_Contact_Name"], "40%", ListColumnAlign.Left),
            new ListColumn("group", L["List_Th_Contact_Group"], "30%", ListColumnAlign.Left),
            new ListColumn("type", L["List_Th_Contact_Type"], "20%", ListColumnAlign.Left)
        };

        Records = Items.Select(i => new ListRecord(new List<ListCell>
        {
            new ListCell(ListCellKind.Symbol, SymbolId: i.SymbolId),
            new ListCell(ListCellKind.Text, Text: i.Name),
            new ListCell(ListCellKind.Text, Text: i.CategoryName ?? string.Empty),
            new ListCell(ListCellKind.Text, Text: i.Type)
        }, i)).ToList();
    }

    public override IReadOnlyList<UiRibbonRegister>? GetRibbonRegisters(IStringLocalizer localizer)
    {
        var tab = new UiRibbonTab(localizer["Ribbon_Group_Navigate"], new List<UiRibbonAction>
        {
            new UiRibbonAction("New", localizer["Ribbon_New"], "<svg><use href='/icons/sprite.svg#plus'/></svg>", UiRibbonItemSize.Large, false, null, "New", () => { RaiseUiActionRequested("New"); return Task.CompletedTask; }),
            new UiRibbonAction("Reload", localizer["Ribbon_Reload"], "<svg><use href='/icons/sprite.svg#refresh'/></svg>", UiRibbonItemSize.Small, false, null, "Reload", () => { RaiseUiActionRequested("Reload"); return Task.CompletedTask; }),
            new UiRibbonAction("ClearFilter", localizer["Ribbon_ClearSearch"], "<svg><use href='/icons/sprite.svg#clear'/></svg>", UiRibbonItemSize.Small, string.IsNullOrWhiteSpace(Search), null, "ClearSearch", () => { RaiseUiActionRequested("ClearSearch"); return Task.CompletedTask; })
        });

        return new List<UiRibbonRegister> { new UiRibbonRegister(UiRibbonRegisterKind.Actions, new List<UiRibbonTab>{tab}) };
    }
}
