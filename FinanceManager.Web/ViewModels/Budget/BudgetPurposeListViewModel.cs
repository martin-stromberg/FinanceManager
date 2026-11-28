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

            var items = (list ?? Array.Empty<BudgetPurposeOverviewDto>())
                .Select(p =>
                    new BudgetPurposeListItem(
                        p.Id,
                        p.Name ?? string.Empty,
                        p.SourceName ?? string.Empty,
                        p.SourceSymbolAttachmentId,
                        p.BudgetCategoryName ?? string.Empty,
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

    /// <summary>
    /// Shifts the currently displayed date range by the given number of months and years, then
    /// requests a reload. The range width is preserved: both bounds move by the exact calendar
    /// delta (day-of-month is clamped, e.g. 31.03. → 28./29.02.); a range ending on a month end
    /// keeps ending on a month end, so a month-aligned view stays month-aligned. When a bound is
    /// unset the other bound's calendar month serves as the anchor (current month when both are
    /// unset), so the shifted range never inverts. A shift that would leave the DateTime domain
    /// is ignored (current range kept).
    /// </summary>
    /// <param name="months">Number of months to shift (negative = past).</param>
    /// <param name="years">Number of years to shift (negative = past).</param>
    private void ShiftRange(int months = 0, int years = 0)
    {
        var today = DateTime.Today;
        var fromDate = RangeFrom?.Date;
        var toDate = RangeTo?.Date;
        fromDate ??= new DateTime((toDate ?? today).Year, (toDate ?? today).Month, 1);
        toDate ??= new DateTime(fromDate.Value.Year, fromDate.Value.Month, DateTime.DaysInMonth(fromDate.Value.Year, fromDate.Value.Month));
        DateTime from;
        DateTime to;
        try
        {
            from = fromDate.Value.AddYears(years).AddMonths(months);
            to = toDate.Value.AddYears(years).AddMonths(months);
        }
        catch (ArgumentOutOfRangeException)
        {
            return;
        }
        if (toDate.Value.Day == DateTime.DaysInMonth(toDate.Value.Year, toDate.Value.Month))
        {
            to = new DateTime(to.Year, to.Month, DateTime.DaysInMonth(to.Year, to.Month));
        }
        if (to < from)
        {
            (from, to) = (to, from);
        }
        SetRange(from, to);
        RaiseUiActionRequested("Reload");
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
            new ListColumn("category", L["Budget_List_Th_Category"], "15%", ListColumnAlign.Left),
            new ListColumn("name", L["List_Th_Name"], "25%", ListColumnAlign.Left),
            new ListColumn("rules", L["Budget_List_Th_Rules"], "80px", ListColumnAlign.Right),
            new ListColumn("budget", L["Budget_List_Th_Budget"], "120px", ListColumnAlign.Right),
            new ListColumn("actual", L["Budget_List_Th_Actual"], "120px", ListColumnAlign.Right),
            new ListColumn("variance", L["Budget_List_Th_Variance"], "120px", ListColumnAlign.Right),
            new ListColumn("source", L["List_Th_Source"], "", ListColumnAlign.Left)
        };

        Records = Items.Select(i => new ListRecord(new List<ListCell>
        {
            new ListCell(ListCellKind.Text, Text: i.CategoryName),
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
        var manage = new UiRibbonTab(localizer["Ribbon_Group_Manage"], new List<UiRibbonAction>
        {
            new UiRibbonAction("New", localizer["Ribbon_New"], "<svg><use href='icons/sprite.svg#plus'/></svg>", UiRibbonItemSize.Large, false, null, () => { RaiseUiActionRequested("New"); return Task.CompletedTask; }) { MobileShortcut = true },
            new UiRibbonAction("Categories", localizer["Budget_Ribbon_Categories"], "<svg><use href='icons/sprite.svg#list'/></svg>", UiRibbonItemSize.Large, false, null, () => { RaiseUiActionRequested("OpenBudgetCategories"); return Task.CompletedTask; })
        }, Sort: 10);

        var view = new UiRibbonTab(localizer["Ribbon_Group_View"], new List<UiRibbonAction>
        {
            new UiRibbonAction("Reload", localizer["Ribbon_Reload"], "<svg><use href='icons/sprite.svg#refresh'/></svg>", UiRibbonItemSize.Large, false, null, () => { RaiseUiActionRequested("Reload"); return Task.CompletedTask; }),
            new UiRibbonAction("ClearFilter", localizer["Ribbon_ClearSearch"], "<svg><use href='icons/sprite.svg#clear'/></svg>", UiRibbonItemSize.Small, string.IsNullOrWhiteSpace(Search), null, () => { RaiseUiActionRequested("ClearSearch"); return Task.CompletedTask; })
        }, Sort: 30);

        var analysis = new UiRibbonTab(localizer["Ribbon_Group_Analysis"], new List<UiRibbonAction>
        {
            new UiRibbonAction("PrevMonth", localizer["Ribbon_PrevMonth"], "<svg><use href='icons/sprite.svg#prev'/></svg>", UiRibbonItemSize.Small, false, localizer["Ribbon_Tip_PrevMonth"], () =>
            {
                ShiftRange(months: -1);
                return Task.CompletedTask;
            }),
            new UiRibbonAction("ThisMonth", localizer["Ribbon_ThisMonth"], "<svg><use href='icons/sprite.svg#calendar'/></svg>", UiRibbonItemSize.Small, false, localizer["Ribbon_Tip_ThisMonth"], () =>
            {
                var today = DateTime.Today;
                var start = new DateTime(today.Year, today.Month, 1);
                var end = start.AddMonths(1).AddDays(-1);
                SetRange(start, end);
                RaiseUiActionRequested("Reload");
                return Task.CompletedTask;
            }),
            new UiRibbonAction("NextMonth", localizer["Ribbon_NextMonth"], "<svg><use href='icons/sprite.svg#next'/></svg>", UiRibbonItemSize.Small, false, localizer["Ribbon_Tip_NextMonth"], () =>
            {
                ShiftRange(months: +1);
                return Task.CompletedTask;
            }),
            new UiRibbonAction("PrevYear", localizer["Ribbon_PrevYear"], "<svg><use href='icons/sprite.svg#prev'/></svg>", UiRibbonItemSize.Small, false, localizer["Ribbon_Tip_PrevYear"], () =>
            {
                ShiftRange(years: -1);
                return Task.CompletedTask;
            }),
            new UiRibbonAction("ThisYear", localizer["Ribbon_ThisYear"], "<svg><use href='icons/sprite.svg#calendar'/></svg>", UiRibbonItemSize.Small, false, localizer["Ribbon_Tip_ThisYear"], () =>
            {
                var year = DateTime.Today.Year;
                var start = new DateTime(year, 1, 1);
                var end = new DateTime(year, 12, 31);
                SetRange(start, end);
                RaiseUiActionRequested("Reload");
                return Task.CompletedTask;
            }),
            new UiRibbonAction("NextYear", localizer["Ribbon_NextYear"], "<svg><use href='icons/sprite.svg#next'/></svg>", UiRibbonItemSize.Small, false, localizer["Ribbon_Tip_NextYear"], () =>
            {
                ShiftRange(years: +1);
                return Task.CompletedTask;
            })
        }, Sort: 40);

        var reports = new UiRibbonTab(localizer["Ribbon_Group_Reports"], new List<UiRibbonAction>
        {
            new UiRibbonAction("BudgetReport", localizer["Budget_Ribbon_Report"], "<svg><use href='icons/sprite.svg#chart'/></svg>", UiRibbonItemSize.Large, false, null, () =>
            {
                RaiseUiActionRequested("OpenBudgetReport");
                return Task.CompletedTask;
            })
        }, Sort: 70);

        return new List<UiRibbonRegister>
        {
            new UiRibbonRegister(UiRibbonRegisterKind.Actions, new List<UiRibbonTab> { manage, view, analysis, reports })
        };
    }
}
