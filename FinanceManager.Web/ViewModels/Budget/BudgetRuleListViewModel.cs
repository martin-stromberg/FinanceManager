using System.Globalization;
using FinanceManager.Shared;
using FinanceManager.Shared.Dtos.Budget;
using FinanceManager.Web.ViewModels.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace FinanceManager.Web.ViewModels.Budget;

/// <summary>
/// Embedded list view model for budget rules belonging to a specific budget purpose.
/// </summary>
public sealed class BudgetRuleListViewModel : BaseListViewModel<BudgetRuleListItem>
{
    private readonly IApiClient _api;

    /// <summary>
    /// Budget purpose id for which rules are listed.
    /// </summary>
    public Guid BudgetPurposeId { get; private set; }

    /// <summary>
    /// Creates a new instance.
    /// </summary>
    public BudgetRuleListViewModel(IServiceProvider services) : base(services)
    {
        _api = services.GetRequiredService<IApiClient>();
        AllowRangeFiltering = false;
        AllowSearchFiltering = false;
    }

    /// <summary>
    /// Sets the budget purpose id and triggers loading.
    /// </summary>
    public async Task InitializeAsync(Guid budgetPurposeId)
    {
        BudgetPurposeId = budgetPurposeId;
        await base.InitializeAsync();
    }

    /// <summary>
    /// Loads the list content.
    /// </summary>
    /// <param name="resetPaging">Ignored for this embedded list (no paging).</param>
    protected override async Task LoadPageAsync(bool resetPaging)
    {
        // No paging for embedded list right now.
        CanLoadMore = false;

        if (BudgetPurposeId == Guid.Empty)
        {
            Items.Clear();
            return;
        }

        try
        {
            var list = await _api.Budgets_ListRulesByPurposeAsync(BudgetPurposeId);
            Items.Clear();

            foreach (var r in list ?? Array.Empty<BudgetRuleDto>())
            {
                Items.Add(new BudgetRuleListItem(
                    r.Id,
                    r.Interval.ToString(),
                    r.Amount.ToString("C", CultureInfo.CurrentUICulture),
                    r.StartDate.ToString("d", CultureInfo.CurrentUICulture),
                    r.EndDate?.ToString("d", CultureInfo.CurrentUICulture) ?? string.Empty));
            }
        }
        catch (Exception ex)
        {
            SetError(_api.LastErrorCode ?? null, _api.LastError ?? ex.Message);
        }
        finally
        {
            RaiseStateChanged();
        }
    }

    /// <summary>
    /// Builds column definitions and list records.
    /// </summary>
    protected override void BuildRecords()
    {
        var L = ServiceProvider.GetRequiredService<IStringLocalizer<Pages>>();

        Columns = new List<ListColumn>
        {
            new ListColumn("interval", L["List_Th_Interval"], "20%", ListColumnAlign.Left),
            new ListColumn("amount", L["List_Th_Amount"], "20%", ListColumnAlign.Right),
            new ListColumn("start", L["List_Th_Start"], "30%", ListColumnAlign.Left),
            new ListColumn("end", L["List_Th_End"], "30%", ListColumnAlign.Left)
        };

        Records = Items.Select(i => new ListRecord(new List<ListCell>
        {
            new ListCell(ListCellKind.Text, Text: i.Interval),
            new ListCell(ListCellKind.Text, Text: i.Amount),
            new ListCell(ListCellKind.Text, Text: i.Start),
            new ListCell(ListCellKind.Text, Text: i.End)
        }, i)).ToList();
    }

    /// <inheritdoc />
    protected override IReadOnlyList<UiRibbonRegister>? GetRibbonRegisterDefinition(IStringLocalizer localizer)
    {
        var tab = new UiRibbonTab(localizer["Ribbon_Group_Rules"], new List<UiRibbonAction>
        {
            new UiRibbonAction(
                "NewRule",
                localizer["Ribbon_New"].Value,
                "<svg><use href='/icons/sprite.svg#plus'/></svg>",
                UiRibbonItemSize.Large,
                false,
                null,
                "NewRule",
                () =>
                {
                    RaiseUiActionRequested("NewRule", BudgetPurposeId.ToString());
                    return Task.CompletedTask;
                })
        });

        return new List<UiRibbonRegister>
        {
            new UiRibbonRegister(UiRibbonRegisterKind.Actions, new List<UiRibbonTab> { tab })
        };
    }
}
