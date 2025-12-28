using FinanceManager.Shared.Dtos;
using FinanceManager.Web.ViewModels.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.AspNetCore.Components;
using FinanceManager.Web.Extensions;
using FinanceManager.Shared.Dtos.Securities;
using FinanceManager.Shared.Dtos.Postings;

namespace FinanceManager.Web.ViewModels.Securities;

public sealed partial class SecuritiesListViewModel : BaseListViewModel<SecurityListItem>
{
    private readonly Shared.IApiClient _api;
    private readonly NavigationManager _nav;

    public SecuritiesListViewModel(IServiceProvider sp) : base(sp)
    {
        _api = sp.GetRequiredService<Shared.IApiClient>();
        _nav = sp.GetRequiredService<NavigationManager>();
    }

    public bool OnlyActive { get; private set; } = true;

    // mapping securityId -> display symbol attachment id (security symbol or category fallback)
    private readonly Dictionary<Guid, Guid?> _displaySymbolBySecurity = new();

    public async Task LoadAsync(CancellationToken ct = default)
    {
        if (!IsAuthenticated) { return; }
        try
        {
            var list = await _api.Securities_ListAsync(OnlyActive, ct);
            Items.Clear();
            Items.AddRange(list.Select(s => new SecurityListItem(s.Id, s.Name ?? string.Empty, s.Identifier ?? string.Empty, s.AlphaVantageCode, s.CategoryId, s.CategoryName, s.IsActive, s.SymbolAttachmentId)));
        }
        catch
        {
            Items.Clear();
            _displaySymbolBySecurity.Clear();
            RaiseStateChanged();
            return;
        }

        // Load categories to get category symbol fallbacks
        var categorySymbolMap = new Dictionary<Guid, Guid?>();
        try
        {
            var clist = await _api.SecurityCategories_ListAsync(ct);
            foreach (var c in clist)
            {
                if (c.Id != Guid.Empty)
                {
                    categorySymbolMap[c.Id] = c.SymbolAttachmentId;
                }
            }
        }
        catch { }

        _displaySymbolBySecurity.Clear();
        foreach (var s in Items)
        {
            Guid? display = null;
            if (s.SymbolId.HasValue)
            {
                display = s.SymbolId;
            }
            else if (s.CategoryId.HasValue)
            {
                if (categorySymbolMap.TryGetValue(s.CategoryId.Value, out var catSym) && catSym.HasValue)
                {
                    display = catSym;
                }
            }
            _displaySymbolBySecurity[s.Id] = display;
        }

        RaiseStateChanged();
    }

    public void ToggleActive()
    {
        OnlyActive = !OnlyActive;
        _ = InitializeAsync();
        RaiseStateChanged();
    }

    protected override async Task LoadPageAsync(bool resetPaging)
    {
        // For generic list provider load we delegate to LoadAsync which fills Items fully
        await LoadAsync();
        CanLoadMore = false;
    }

    protected override void BuildRecords()
    {
        var L = ServiceProvider.GetRequiredService<IStringLocalizer<Pages>>();
        Columns = new List<ListColumn>
        {
            new ListColumn("symbol", string.Empty, "56px", ListColumnAlign.Left),
            new ListColumn("name", L["List_Th_Name"], "", ListColumnAlign.Left),
            new ListColumn("identifier", L["List_Th_Identifier"], "", ListColumnAlign.Left),
            new ListColumn("alphavantage", L["List_Th_AlphaVantage"], "", ListColumnAlign.Left),
            new ListColumn("category", L["List_Th_Category"], "", ListColumnAlign.Left),
            new ListColumn("status", L["List_Th_Status"], "120px", ListColumnAlign.Left)
        };

        Records = Items.Select(i => new ListRecord(new List<ListCell>
        {
            new ListCell(ListCellKind.Symbol, SymbolId: _displaySymbolBySecurity.TryGetValue(i.Id, out var sym) ? sym : null),
            new ListCell(ListCellKind.Text, Text: i.Name),
            new ListCell(ListCellKind.Text, Text: i.Identifier),
            new ListCell(ListCellKind.Text, Text: string.IsNullOrWhiteSpace(i.AlphaVantageCode) ? "-" : i.AlphaVantageCode),
            new ListCell(ListCellKind.Text, Text: string.IsNullOrWhiteSpace(i.CategoryName) ? "-" : i.CategoryName),
            new ListCell(ListCellKind.Text, Text: i.IsActive ? (ServiceProvider.GetRequiredService<IStringLocalizer<Pages>>()?[
                "StatusActive"].Value ?? "Active") : (ServiceProvider.GetRequiredService<IStringLocalizer<Pages>>()?["StatusArchived"].Value ?? "Archived"))
        }, i)).ToList();
    }

    protected override IReadOnlyList<UiRibbonRegister>? GetRibbonRegisterDefinition(IStringLocalizer localizer)
    {
        // Actions tab
        var actions = new List<UiRibbonAction>
        {
            new UiRibbonAction(
                "New",
                localizer["Ribbon_New"].Value,
                "<svg><use href='/icons/sprite.svg#plus'/></svg>",
                UiRibbonItemSize.Large,
                false,
                null,
                "New",
                new Func<Task>(() => { RaiseUiActionRequested("New"); return Task.CompletedTask; })
            ),
            new UiRibbonAction(
                "Categories",
                localizer["Ribbon_Categories"].Value,
                "<svg><use href='/icons/sprite.svg#groups'/></svg>",
                UiRibbonItemSize.Small,
                false,
                null,
                "Categories",
                () => { RaiseUiActionRequested("OpenCategories"); return Task.CompletedTask; }
            )
        };

        // Filter tab - ToggleActive can be handled directly by VM via Callback
        var filter = new List<UiRibbonAction>
        {
            new UiRibbonAction(
                "ToggleActive",
                localizer["Ribbon_ToggleActive"].Value,
                "<svg><use href='/icons/sprite.svg#check'/></svg>",
                UiRibbonItemSize.Small,
                false,
                null,
                null,
                new Func<Task>(() => { ToggleActive(); return Task.CompletedTask; })
            )
        };

        var tabsActions = new List<UiRibbonTab> { new UiRibbonTab(localizer["Ribbon_Group_Actions"].Value, actions) };
        var tabsFilter = new List<UiRibbonTab> { new UiRibbonTab(localizer["Ribbon_Group_Filter"].Value, filter) };

        return new List<UiRibbonRegister>
        {
            new UiRibbonRegister(UiRibbonRegisterKind.Actions, tabsActions),
            new UiRibbonRegister(UiRibbonRegisterKind.Custom, tabsFilter)
        };
    }

    // Public helper for UI to get display symbol attachment id (security symbol or category fallback)
    public Guid? GetDisplaySymbolAttachmentId(SecurityListItem security)
    {
        if (security == null) return null;
        return _displaySymbolBySecurity.TryGetValue(security.Id, out var v) ? v : null;
    }
}
