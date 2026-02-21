using FinanceManager.Shared;
using FinanceManager.Web.ViewModels.Common;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Components;

namespace FinanceManager.Web.ViewModels.Contacts;

/// <summary>
/// List view model that provides paging, searching and rendering logic for the contacts overview list.
/// </summary>
public sealed class ContactListViewModel : BaseListViewModel<ContactListItem>
{
    private readonly IApiClient _api;
    private readonly Dictionary<Guid, string> _categoryNames = new();

    /// <summary>
    /// Initializes a new instance of <see cref="ContactListViewModel"/>.
    /// </summary>
    /// <param name="services">Service provider used to resolve dependencies such as <see cref="IApiClient"/> and <see cref="IStringLocalizer{Pages}"/>.</param>
    public ContactListViewModel(IServiceProvider services) : base(services)
    {
        _api = services.GetRequiredService<IApiClient>();
    }

    /// <summary>
    /// Indicates whether the list supports range filtering. Contact list does not support range filtering.
    /// </summary>
    public override bool AllowRangeFiltering => false;

    private int _skip;
    private const int PageSize = 50;

    /// <summary>
    /// Loads a page of contacts from the API and appends them to the internal item collection.
    /// </summary>
    /// <param name="resetPaging">When <c>true</c> the paging offset will be reset and the first page will be loaded.</param>
    /// <returns>A task that completes when the page load has finished. Errors are captured via <see cref="SetError(string,string)"/> and do not propagate.</returns>
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

    /// <summary>
    /// Builds column definitions and list records used by the UI to render the contacts table.
    /// </summary>
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

    /// <summary>
    /// Returns ribbon register definitions for the contacts list view. The provided <paramref name="localizer"/> is used to resolve UI labels.
    /// </summary>
    /// <param name="localizer">Localizer used to obtain localized labels for ribbon actions.</param>
    /// <returns>A list of <see cref="UiRibbonRegister"/> describing the ribbon tabs and actions for this view.</returns>
    protected override IReadOnlyList<UiRibbonRegister>? GetRibbonRegisterDefinition(IStringLocalizer localizer)
    {
        var tab = new UiRibbonTab(localizer["Ribbon_Group_Navigation"], new List<UiRibbonAction>
        {
            new UiRibbonAction("New", localizer["Ribbon_New"], "<svg><use href='/icons/sprite.svg#plus'/></svg>", UiRibbonItemSize.Large, false, null, () => { RaiseUiActionRequested("New"); return Task.CompletedTask; }),
            new UiRibbonAction("Groups", localizer["Ribbon_Groups"], "<svg><use href='/icons/sprite.svg#layers'/></svg>", UiRibbonItemSize.Small, false, null, () => { RaiseUiActionRequested("OpenCategories"); return Task.CompletedTask; }),
            new UiRibbonAction("Reload", localizer["Ribbon_Reload"], "<svg><use href='/icons/sprite.svg#refresh'/></svg>", UiRibbonItemSize.Small, false, null, () => { RaiseUiActionRequested("Reload"); return Task.CompletedTask; }),
            new UiRibbonAction("ClearFilter", localizer["Ribbon_ClearSearch"], "<svg><use href='/icons/sprite.svg#clear'/></svg>", UiRibbonItemSize.Small, string.IsNullOrWhiteSpace(Search), null, () => { RaiseUiActionRequested("ClearSearch"); return Task.CompletedTask; })
        });

        return new List<UiRibbonRegister> { new UiRibbonRegister(UiRibbonRegisterKind.Actions, new List<UiRibbonTab>{tab}) };
    }
}
