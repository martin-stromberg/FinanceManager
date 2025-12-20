using FinanceManager.Shared.Dtos.Securities;
using FinanceManager.Web.ViewModels.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.AspNetCore.Components;

namespace FinanceManager.Web.ViewModels.Securities.Categories;

public sealed record SecurityCategoryItem(Guid Id, string Name, Guid? SymbolId) : IListItemNavigation
{
    public string GetNavigateUrl() => $"/card/securities/categories/{Id}";
}

public sealed class SecurityCategoriesListViewModel : BaseListViewModel<SecurityCategoryItem>
{
    private readonly Shared.IApiClient _api;
    private readonly NavigationManager _nav;

    public SecurityCategoriesListViewModel(IServiceProvider sp) : base(sp)
    {
        _api = sp.GetRequiredService<Shared.IApiClient>();
        _nav = sp.GetRequiredService<NavigationManager>();
    }
    public override bool AllowSearchFiltering { get => false; protected set => base.AllowSearchFiltering = value; }
    public override bool AllowRangeFiltering { get => false; protected set => base.AllowRangeFiltering = value; }
    public async Task LoadAsync(CancellationToken ct = default)
    {
        if (!IsAuthenticated) return;
        try
        {
            var list = await _api.SecurityCategories_ListAsync(ct);
            Items.Clear();
            Items.AddRange(list.Select(c => new SecurityCategoryItem(c.Id, c.Name ?? string.Empty, c.SymbolAttachmentId)));
        }
        catch
        {
            Items.Clear();
            RaiseStateChanged();
            return;
        }

        RaiseStateChanged();
    }

    protected override async Task LoadPageAsync(bool resetPaging)
    {
        // categories are small - load all
        await LoadAsync();
        CanLoadMore = false;
    }

    protected override void BuildRecords()
    {
        var L = ServiceProvider.GetRequiredService<IStringLocalizer<Pages>>();
        Columns = new List<ListColumn>
        {
            new ListColumn("symbol", string.Empty, "48px", ListColumnAlign.Left),
            new ListColumn("name", L["List_Th_Name"], "", ListColumnAlign.Left)
        };

        Records = Items.Select(i => new ListRecord(new List<ListCell>
        {
            new ListCell(ListCellKind.Symbol, SymbolId: i.SymbolId),
            new ListCell(ListCellKind.Text, Text: i.Name)
        }, i)).ToList();
    }

    public override IReadOnlyList<UiRibbonRegister>? GetRibbonRegisters(IStringLocalizer localizer)
    {
        var actions = new List<UiRibbonAction>
        {
            new UiRibbonAction("Back", localizer["Ribbon_Back"].Value, "<svg><use href='/icons/sprite.svg#back'/></svg>", UiRibbonItemSize.Small, false, null, "Back", () => { RaiseUiActionRequested("Back"); return Task.CompletedTask; })
        };
        var manage = new List<UiRibbonAction>
        {
            new UiRibbonAction("New", localizer["Ribbon_New"].Value, "<svg><use href='/icons/sprite.svg#plus'/></svg>", UiRibbonItemSize.Small, false, null, "New", () => { RaiseUiActionRequested("New"); return Task.CompletedTask; })
        };
        var tabs = new List<UiRibbonTab> { new UiRibbonTab(localizer["Ribbon_Group_Navigation"].Value, actions), new UiRibbonTab(localizer["Ribbon_Group_Manage"].Value, manage) };
        return new List<UiRibbonRegister> { new UiRibbonRegister(UiRibbonRegisterKind.Actions, tabs) };
    }
}
