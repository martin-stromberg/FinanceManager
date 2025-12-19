using FinanceManager.Shared;
using FinanceManager.Shared.Dtos.SavingsPlans;
using FinanceManager.Web.ViewModels.Common;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Components;

namespace FinanceManager.Web.ViewModels.SavingsPlans.Categories;

public sealed class SavingsPlanCategoryListViewModel : BaseListViewModel<SavingsPlanCategoryListViewModel.SavingsPlanCategoryListItem>
{
    private readonly IApiClient _api;
    public SavingsPlanCategoryListViewModel(IServiceProvider services) : base(services)
    {
        _api = services.GetRequiredService<IApiClient>();
    }

    public override bool AllowRangeFiltering => false;

    protected override async Task LoadPageAsync(bool resetPaging)
    {
        try
        {
            var list = await _api.SavingsPlanCategories_ListAsync();
            Items.Clear();
            if (list != null)
            {
                Items.AddRange(list.Select(c => new SavingsPlanCategoryListItem(c.Id, c.Name ?? string.Empty, c.SymbolAttachmentId)));
            }
            CanLoadMore = false;
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
            new ListColumn("name", L["List_Th_SavingsPlan_Name"], "", ListColumnAlign.Left)
        };

        Records = Items.Select(i => new ListRecord(new List<ListCell>
        {
            new ListCell(ListCellKind.Symbol, SymbolId: i.SymbolId),
            new ListCell(ListCellKind.Text, Text: i.Name)
        }, i)).ToList();
    }

    public override IReadOnlyList<UiRibbonRegister>? GetRibbonRegisters(Microsoft.Extensions.Localization.IStringLocalizer localizer)
    {
        var nav = new UiRibbonTab(localizer["Ribbon_Group_Navigation"], new List<UiRibbonAction>
        {
            new UiRibbonAction("Back", localizer["Ribbon_Back"].Value, "<svg><use href='/icons/sprite.svg#back'/></svg>", UiRibbonItemSize.Large, false, null, "Back", () => {
                var nav = ServiceProvider.GetRequiredService<NavigationManager>();
                nav.NavigateTo("/list/savings-plans");
                return Task.CompletedTask;
            })
        });

        var manage = new UiRibbonTab(localizer["Ribbon_Group_Manage"], new List<UiRibbonAction>
        {
            new UiRibbonAction("New", localizer["Ribbon_New"].Value, "<svg><use href='/icons/sprite.svg#plus'/></svg>", UiRibbonItemSize.Large, false, null, "New", () => {
                RaiseUiActionRequested("New");
                return Task.CompletedTask;
            })
        });

        return new List<UiRibbonRegister> { new UiRibbonRegister(UiRibbonRegisterKind.Actions, new List<UiRibbonTab>{nav, manage}) };
    }

    public sealed record SavingsPlanCategoryListItem(Guid Id, string Name, Guid? SymbolId) : IListItemNavigation
    {
        public string GetNavigateUrl() => $"/card/savings-plans/categories/{Id}";
    }
}
