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
    /// Initializes the view model and sets the default range filter to the current month.
    /// </summary>
    public override async Task InitializeAsync()
    {
        var now = DateTime.Today;
        var from = new DateTime(now.Year, now.Month, 1);
        var to = from.AddMonths(1).AddDays(-1);
        SetRange(from, to);

        await base.InitializeAsync();
    }

    /// <inheritdoc />
    protected override async Task LoadPageAsync(bool resetPaging)
    {
        if (resetPaging)
        {
            _skip = 0;
        }

        try
        {
            var range = GetEffectiveMonthlyRange();
            var list = await _api.Budgets_ListPurposesAsync(
                _skip,
                PageSize,
                null,
                string.IsNullOrWhiteSpace(Search) ? null : Search,
                range.From,
                range.To);

            var items = (list ?? Array.Empty<BudgetPurposeOverviewDto>()).Select(p =>
                new BudgetPurposeListItem(
                    p.Id,
                    p.Name ?? string.Empty,
                    p.SourceName ?? string.Empty,
                    p.SourceSymbolAttachmentId,
                    p.RuleCount,
                    p.BudgetSum,
                    p.ActualSum,
                    p.Variance));

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

    private (DateOnly From, DateOnly To) GetEffectiveMonthlyRange()
    {
        var fromDt = RangeFrom?.Date ?? DateTime.Today;
        var toDt = RangeTo?.Date ?? fromDt;

        // normalize: ensure from <= to
        if (toDt < fromDt)
        {
            (fromDt, toDt) = (toDt, fromDt);
        }

        return (DateOnly.FromDateTime(fromDt), DateOnly.FromDateTime(toDt));
    }

    /// <inheritdoc />
    protected override void BuildRecords()
    {
        var L = ServiceProvider.GetRequiredService<IStringLocalizer<Pages>>();
        Columns = new List<ListColumn>
        {
            new ListColumn("name", L["List_Th_Name"], "30%", ListColumnAlign.Left),
            new ListColumn("rules", L["Budget_List_Th_Rules"], "80px", ListColumnAlign.Right),
            new ListColumn("budget", L["Budget_List_Th_Budget"], "120px", ListColumnAlign.Right),
            new ListColumn("actual", L["Budget_List_Th_Actual"], "120px", ListColumnAlign.Right),
            new ListColumn("variance", L["Budget_List_Th_Variance"], "120px", ListColumnAlign.Right),
            new ListColumn("source", L["List_Th_Source"], "", ListColumnAlign.Left)
        };

        Records = Items.Select(i => new ListRecord(new List<ListCell>
        {
            new ListCell(ListCellKind.Text, Text: i.Name),
            new ListCell(ListCellKind.Text, Text: i.RuleCount.ToString(System.Globalization.CultureInfo.CurrentCulture)),
            new ListCell(ListCellKind.Currency, Amount: i.BudgetSum),
            new ListCell(ListCellKind.Currency, Amount: i.ActualSum),
            new ListCell(ListCellKind.Currency, Amount: i.Variance),
            new ListCell(ListCellKind.Symbol, SymbolId: i.SourceSymbolAttachmentId, Text: i.SourceName)
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

        var analysis = new UiRibbonTab(localizer["Ribbon_Group_Analysis"], new List<UiRibbonAction>
        {
            new UiRibbonAction("PrevMonth", localizer["Ribbon_PrevMonth"], "<svg><use href='/icons/sprite.svg#prev'/></svg>", UiRibbonItemSize.Small, false, null, () =>
            {
                var today = DateTime.Today;
                var start = new DateTime(today.Year, today.Month, 1).AddMonths(-1);
                var end = start.AddMonths(1).AddDays(-1);
                SetRange(start, end);
                RaiseUiActionRequested("Reload");
                return Task.CompletedTask;
            }),
            new UiRibbonAction("ThisMonth", localizer["Ribbon_ThisMonth"], "<svg><use href='/icons/sprite.svg#calendar'/></svg>", UiRibbonItemSize.Small, false, null, () =>
            {
                var today = DateTime.Today;
                var start = new DateTime(today.Year, today.Month, 1);
                var end = start.AddMonths(1).AddDays(-1);
                SetRange(start, end);
                RaiseUiActionRequested("Reload");
                return Task.CompletedTask;
            }),
            new UiRibbonAction("PrevYear", localizer["Ribbon_PrevYear"], "<svg><use href='/icons/sprite.svg#prev'/></svg>", UiRibbonItemSize.Small, false, null, () =>
            {
                var year = DateTime.Today.Year - 1;
                var start = new DateTime(year, 1, 1);
                var end = new DateTime(year, 12, 31);
                SetRange(start, end);
                RaiseUiActionRequested("Reload");
                return Task.CompletedTask;
            }),
            new UiRibbonAction("ThisYear", localizer["Ribbon_ThisYear"], "<svg><use href='/icons/sprite.svg#calendar'/></svg>", UiRibbonItemSize.Small, false, null, () =>
            {
                var year = DateTime.Today.Year;
                var start = new DateTime(year, 1, 1);
                var end = new DateTime(year, 12, 31);
                SetRange(start, end);
                RaiseUiActionRequested("Reload");
                return Task.CompletedTask;
            })
        });

        return new List<UiRibbonRegister>
        {
            new UiRibbonRegister(UiRibbonRegisterKind.Actions, new List<UiRibbonTab> { tab, analysis })
        };
    }
}
