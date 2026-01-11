using FinanceManager.Shared;
using FinanceManager.Shared.Dtos.Budget;
using FinanceManager.Web.ViewModels.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace FinanceManager.Web.ViewModels.Budget;

/// <summary>
/// List view model for the budget purposes overview.
/// </summary>
public sealed class BudgetPurposeListViewModel : BaseListViewModel<BudgetPurposeListItem>
{
    private const int PageSize = 50;

    private readonly IApiClient _api;

    private int _skip;

    /// <summary>
    /// Creates a new instance.
    /// </summary>
    /// <param name="services">Service provider used to resolve dependencies.</param>
    public BudgetPurposeListViewModel(IServiceProvider services) : base(services)
    {
        _api = services.GetRequiredService<IApiClient>();
    }

    /// <summary>
    /// Budget purposes list does not support range filtering.
    /// </summary>
    public override bool AllowRangeFiltering => false;

    /// <inheritdoc />
    protected override async Task LoadPageAsync(bool resetPaging)
    {
        if (resetPaging)
        {
            _skip = 0;
        }

        try
        {
            var list = await _api.Budgets_ListPurposesAsync(skip: _skip, take: PageSize, sourceType: null, q: string.IsNullOrWhiteSpace(Search) ? null : Search);
            var items = (list ?? Array.Empty<BudgetPurposeDto>()).Select(p =>
                new BudgetPurposeListItem(p.Id, p.Name ?? string.Empty, p.SourceType.ToString()));

            if (resetPaging)
            {
                Items.Clear();
            }

            Items.AddRange(items);
            _skip += PageSize;
            CanLoadMore = list != null && list.Count >= PageSize;
        }
        catch (Exception ex)
        {
            SetError(_api.LastErrorCode ?? null, _api.LastError ?? ex.Message);
            CanLoadMore = false;
        }
        finally
        {
            RaiseStateChanged();
        }
    }

    /// <inheritdoc />
    protected override void BuildRecords()
    {
        var L = ServiceProvider.GetRequiredService<IStringLocalizer<Pages>>();
        Columns = new List<ListColumn>
        {
            new ListColumn("name", L["List_Th_Name"], "60%", ListColumnAlign.Left),
            new ListColumn("source", L["List_Th_Source"], "40%", ListColumnAlign.Left)
        };

        Records = Items.Select(i => new ListRecord(new List<ListCell>
        {
            new ListCell(ListCellKind.Text, Text: i.Name),
            new ListCell(ListCellKind.Text, Text: i.SourceType)
        }, i)).ToList();
    }

    /// <inheritdoc />
    protected override IReadOnlyList<UiRibbonRegister>? GetRibbonRegisterDefinition(IStringLocalizer localizer)
    {
        var tab = new UiRibbonTab(localizer["Ribbon_Group_Navigation"], new List<UiRibbonAction>
        {
            new UiRibbonAction("New", localizer["Ribbon_New"], "<svg><use href='/icons/sprite.svg#plus'/></svg>", UiRibbonItemSize.Large, false, null, () => { RaiseUiActionRequested("New"); return Task.CompletedTask; }),
            new UiRibbonAction("Reload", localizer["Ribbon_Reload"], "<svg><use href='/icons/sprite.svg#refresh'/></svg>", UiRibbonItemSize.Small, false, null, () => { RaiseUiActionRequested("Reload"); return Task.CompletedTask; }),
            new UiRibbonAction("ClearFilter", localizer["Ribbon_ClearSearch"], "<svg><use href='/icons/sprite.svg#clear'/></svg>", UiRibbonItemSize.Small, string.IsNullOrWhiteSpace(Search), null, () => { RaiseUiActionRequested("ClearSearch"); return Task.CompletedTask; })
        });

        return new List<UiRibbonRegister> { new UiRibbonRegister(UiRibbonRegisterKind.Actions, new List<UiRibbonTab> { tab }) };
    }
}
