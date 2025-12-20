using FinanceManager.Shared;
using FinanceManager.Web.ViewModels.Common;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Components;

namespace FinanceManager.Web.ViewModels.Contacts.Groups;

public sealed class ContactGroupListViewModel : BaseListViewModel<ContactGroupListViewModel.ContactGroupListItem>
{
    private readonly IApiClient _api;
    public ContactGroupListViewModel(IServiceProvider services) : base(services)
    {
        _api = services.GetRequiredService<IApiClient>();
    }

    public override bool AllowRangeFiltering => false;

    protected override async Task LoadPageAsync(bool resetPaging)
    {
        try
        {
            var list = await _api.ContactCategories_ListAsync();
            Items.Clear();
            if (list != null)
            {
                Items.AddRange(list.Select(c => new ContactGroupListItem(c.Id, c.Name ?? string.Empty, c.SymbolAttachmentId)));
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
            new ListColumn("name", L["List_Th_Contact_Name"], "", ListColumnAlign.Left)
        };

        Records = Items.Select(i => new ListRecord(new List<ListCell>
        {
            new ListCell(ListCellKind.Symbol, SymbolId: i.SymbolId),
            new ListCell(ListCellKind.Text, Text: i.Name)
        }, i)).ToList();
    }

    public override IReadOnlyList<UiRibbonRegister>? GetRibbonRegisters(IStringLocalizer localizer)
    {
        var tab = new UiRibbonTab(localizer["Ribbon_Group_Navigation"], new List<UiRibbonAction>
        {
            new UiRibbonAction("New", localizer["Ribbon_New"], "<svg><use href='/icons/sprite.svg#plus'/></svg>", UiRibbonItemSize.Large, false, null, "New", () => {
                RaiseUiActionRequested("New");
                //var nav = ServiceProvider.GetRequiredService<NavigationManager>();
                //nav.NavigateTo("/card/contacts/categories/new");
                return Task.CompletedTask;
            }),
            new UiRibbonAction("Back", localizer["Ribbon_Back"], "<svg><use href='/icons/sprite.svg#back'/></svg>", UiRibbonItemSize.Small, false, null, "Back", () => {
                var nav = ServiceProvider.GetRequiredService<NavigationManager>();
                nav.NavigateTo("/list/contacts");
                return Task.CompletedTask;
            })
        });

        return new List<UiRibbonRegister> { new UiRibbonRegister(UiRibbonRegisterKind.Actions, new List<UiRibbonTab>{tab}) };
    }

    public sealed record ContactGroupListItem(Guid Id, string Name, Guid? SymbolId) : IListItemNavigation
    {
        public string GetNavigateUrl() => $"/card/contacts/categories/{Id}";
    }
}
