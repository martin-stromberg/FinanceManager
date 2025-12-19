using FinanceManager.Shared.Dtos.SavingsPlans;
using FinanceManager.Web.ViewModels.Common;
using FinanceManager.Web.ViewModels.SavingsPlans;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using System.Globalization;

namespace FinanceManager.Web.ViewModels.SavingsPlans;

public sealed class SavingsPlansListViewModel : BaseListViewModel<SavingsPlanListItem>
{
    private readonly Shared.IApiClient _api;

    public SavingsPlansListViewModel(IServiceProvider sp) : base(sp)
    {
        _api = sp.GetRequiredService<Shared.IApiClient>();
    }

    public bool OnlyActive { get; private set; } = true;

    private readonly Dictionary<Guid, Guid?> _displaySymbolByPlan = new();
    private readonly Dictionary<Guid, SavingsPlanAnalysisDto> _analysisByPlan = new();
    private readonly Dictionary<Guid, SavingsPlanInterval?> _intervalByPlan = new();
    private readonly Dictionary<Guid, SavingsPlanType> _typeByPlan = new();

    public override Task InitializeAsync() => base.InitializeAsync();

    private int _skip;
    private const int PageSize = 100;

    protected override async Task LoadPageAsync(bool resetPaging)
    {
        try
        {
            if (resetPaging) { _skip = 0; }
            var list = await _api.SavingsPlans_ListAsync(OnlyActive, CancellationToken.None);

            // load categories to map names
            var categories = (await _api.SavingsPlanCategories_ListAsync(CancellationToken.None)).ToDictionary(c => c.Id, c => c.Name ?? string.Empty);

            // capture interval + type info for later rendering
            _intervalByPlan.Clear();
            _typeByPlan.Clear();

            var items = list.Select(p =>
            {
                _intervalByPlan[p.Id] = p.Interval;
                _typeByPlan[p.Id] = p.Type;
                return new SavingsPlanListItem(
                    p.Id,
                    p.Name ?? string.Empty,
                    p.Type.ToString(),
                    p.CategoryId.HasValue && categories.TryGetValue(p.CategoryId.Value, out var catName) ? catName : null,
                    p.SymbolAttachmentId
                );
            });

            if (resetPaging) Items.Clear();
            Items.AddRange(items);

            _skip += PageSize;
            CanLoadMore = false; // API returns full list for now
        }
        catch
        {
            Items.Clear();
            CanLoadMore = false;
        }

        _displaySymbolByPlan.Clear();
        foreach (var it in Items)
        {
            _displaySymbolByPlan[it.Id] = it.SymbolId;
        }

        // load analyses in parallel (best-effort)
        _analysisByPlan.Clear();
        var analysisTasks = Items.Select(async i =>
        {
            try
            {
                var dto = await _api.SavingsPlans_AnalyzeAsync(i.Id, CancellationToken.None);
                if (dto != null) _analysisByPlan[i.Id] = dto;
            }
            catch { }
        }).ToList();
        try { await Task.WhenAll(analysisTasks); } catch { }

        // build visual records
        BuildRecords();
    }

    private static int IntervalMonths(SavingsPlanInterval? iv)
    {
        return iv switch
        {
            SavingsPlanInterval.Monthly => 1,
            SavingsPlanInterval.BiMonthly => 2,
            SavingsPlanInterval.Quarterly => 3,
            SavingsPlanInterval.SemiAnnually => 6,
            SavingsPlanInterval.Annually => 12,
            _ => 0
        };
    }

    protected override void BuildRecords()
    {
        var L = ServiceProvider.GetRequiredService<IStringLocalizer<Pages>>();
        Columns = new List<ListColumn>
        {
            new ListColumn("symbol", string.Empty, "48px", ListColumnAlign.Left),
            new ListColumn("name", L["List_Th_SavingsPlan_Name"], "28%", ListColumnAlign.Left),
            new ListColumn("target", L["List_Th_TargetAmount"], "12%", ListColumnAlign.Right),
            new ListColumn("balance", L["List_Th_Balance"], "12%", ListColumnAlign.Right),
            new ListColumn("remaining", L["List_Th_Remaining"], "12%", ListColumnAlign.Right),
            new ListColumn("date", L["List_Th_TargetDate"], "14%", ListColumnAlign.Left),
            new ListColumn("status", L["List_Th_Status"], "12%", ListColumnAlign.Left)
        };

        var culture = CultureInfo.CurrentCulture;

        // calendar badge SVG template (taken from legacy SavingsPlanList)
        var calendarSvgTemplate = @"<svg xmlns='http://www.w3.org/2000/svg' style='vertical-align:middle;' width='32' height='32' viewBox='0 0 32 32'>
  <rect x='2' y='5' width='28' height='25' rx='3' ry='3' fill='#f0f0f0' stroke='navy' stroke-width='1' />
  <rect x='2' y='5' width='28' height='5' rx='3' ry='3' fill='navy' />
  <line x1='8' y1='2' x2='8' y2='7' stroke='navy' stroke-width='1.5' />
  <line x1='24' y1='2' x2='24' y2='7' stroke='navy' stroke-width='1.5' />
  <g fill='white' stroke='navy' stroke-width='0.4'>
    <rect x='3' y='11' width='5' height='3' />
    <rect x='8' y='11' width='5' height='3' />
    <rect x='13' y='11' width='5' height='3' />
    <rect x='18' y='11' width='5' height='3' />
    <rect x='23' y='11' width='5' height='3' />
    <rect x='3' y='15' width='5' height='3' />
    <rect x='8' y='15' width='5' height='3' />
    <rect x='13' y='15' width='5' height='3' />
    <rect x='18' y='15' width='5' height='3' />
    <rect x='23' y='15' width='5' height='3' />
    <rect x='3' y='19' width='5' height='3' />
    <rect x='8' y='19' width='5' height='3' />
    <rect x='13' y='19' width='5' height='3' />
    <rect x='18' y='19' width='5' height='3' />
    <rect x='23' y='19' width='5' height='3' />
    <rect x='3' y='23' width='5' height='3' />
    <rect x='8' y='23' width='5' height='3' />
    <rect x='13' y='23' width='5' height='3' />
    <rect x='18' y='23' width='5' height='3' />
    <rect x='23' y='23' width='5' height='3' />
  </g>
  <circle cx='25' cy='25' r='6.5' fill='#ffffff' stroke='navy' stroke-width='1' />
  <path d='M25 18.5 A6.5 6.5 0 0 1 31 25 L29.5 23.5 M31 25 L28.5 25' fill='none' stroke='navy' stroke-width='1' stroke-linecap='round' />
  <text x='24' y='28' text-anchor='middle' font-size='10' fill='navy' font-family='Arial' font-weight='bold'>{0}</text>
</svg>";

        // base image used in SavingsPlanStatus component (kept as data URI)
        var statusBaseImageHref = @"data:image/png;base64,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";
        Records = Items.Select(i =>
        {
            _analysisByPlan.TryGetValue(i.Id, out var a);

            // target amount
            string targetText = string.Empty;
            if (a?.TargetAmount is decimal targ && targ != 0m) targetText = targ.ToString("C", culture);

            // balance (accumulated)
            string balanceText = string.Empty;
            if (a != null && a.AccumulatedAmount != 0m) balanceText = a.AccumulatedAmount.ToString("C", culture);

            // remaining = target - accumulated
            string remainingText = string.Empty;
            if (a?.TargetAmount is decimal ta)
            {
                var remaining = ta - a.AccumulatedAmount;
                if (remaining != 0m) remainingText = remaining.ToString("C", culture);
            }

            // target date and recurrence indicator
            string dateText = string.Empty;
            if (a != null && a.TargetDate.HasValue)
            {
                dateText = a.TargetDate.Value.ToShortDateString();
            }

            // recurrence: if plan is recurring, show calendar svg + months
            if (_typeByPlan.TryGetValue(i.Id, out var tpe) && tpe == SavingsPlanType.Recurring)
            {
                var months = _intervalByPlan.TryGetValue(i.Id, out var iv) ? IntervalMonths(iv) : 0;
                if (months > 0)
                {
                    var cal = string.Format(calendarSvgTemplate, months);
                    dateText = string.IsNullOrWhiteSpace(dateText) ? cal : $"{dateText} {cal}";
                }
                else
                {
                    var cal = string.Format(calendarSvgTemplate, string.Empty);
                    dateText = string.IsNullOrWhiteSpace(dateText) ? cal : $"{dateText} {cal}";
                }
            }

            // status: svg matching SavingsPlanStatus component visuals
            string statusSvg = string.Empty;
            if (a != null)
            {
                var completed = a.TargetAmount.HasValue ? (a.AccumulatedAmount >= a.TargetAmount.Value) : false;
                var overdue = a.TargetDate.HasValue && DateTime.UtcNow.Date > a.TargetDate.Value.Date && !completed;
                var unreachable = a.TargetAmount.HasValue && !a.TargetReachable && !completed;
                var reachable = a.TargetAmount.HasValue && a.TargetReachable && !completed;

                var svgBuilder = new System.Text.StringBuilder();
                svgBuilder.Append("<svg width=\"32\" height=\"32\" viewBox=\"0 0 32 32\" xmlns=\"http://www.w3.org/2000/svg\">\n");
                svgBuilder.Append($"<image width=\"32\" height=\"32\" href=\"{statusBaseImageHref}\" />\n");
                if (completed)
                {
                    svgBuilder.Append("<path d=\"M 22 22 L 25 25 L 29 19\" stroke=\"#32CD32\" stroke-width=\"2\" fill=\"none\" stroke-linecap=\"round\" stroke-linejoin=\"round\" />\n");
                }
                else if (overdue)
                {
                    svgBuilder.Append("<path d=\"M19 19 L25 25 M25 19 L19 25\" stroke=\"#e66\" stroke-width=\"2\" fill=\"none\" stroke-linecap=\"round\" stroke-linejoin=\"round\" />\n");
                }
                else if (unreachable)
                {
                    svgBuilder.Append("<path d=\"M 26 16 L 20 26 L 32 26 Z\" fill=\"#FFC107\" stroke=\"#FFC107\" stroke-width=\"2\" stroke-linejoin=\"round\" />\n");
                    svgBuilder.Append("<text x=\"26\" y=\"22\" font-family=\"Arial, sans-serif\" font-size=\"7px\" fill=\"#000000\" text-anchor=\"middle\" alignment-baseline=\"middle\">!</text>\n");
                }
                else if (reachable)
                {
                    svgBuilder.Append("<path d=\"M22 18 L25 21 L29 15\" stroke=\"#FFA500\" stroke-width=\"2\" fill=\"none\" stroke-linecap=\"round\" stroke-linejoin=\"round\" />\n");
                }
                svgBuilder.Append("</svg>");
                statusSvg = svgBuilder.ToString();
            }

            var cells = new List<ListCell>
            {
                new ListCell(ListCellKind.Symbol, SymbolId: i.SymbolId),
                new ListCell(ListCellKind.Text, Text: i.Name),
                new ListCell(ListCellKind.Text, Text: targetText),
                new ListCell(ListCellKind.Text, Text: balanceText),
                new ListCell(ListCellKind.Text, Text: remainingText),
                new ListCell(ListCellKind.Text, Text: dateText),
                new ListCell(ListCellKind.Text, Text: statusSvg)
            };

            return new ListRecord(cells, i);
        }).ToList();
    }

    public void ToggleActive()
    {
        OnlyActive = !OnlyActive;
        _ = InitializeAsync();
        RaiseStateChanged();
    }

    public override IReadOnlyList<UiRibbonRegister>? GetRibbonRegisters(IStringLocalizer localizer)
    {
        var actions = new List<UiRibbonAction>
        {
            new UiRibbonAction("New", localizer["Ribbon_New"].Value, "<svg><use href='/icons/sprite.svg#plus'/></svg>", UiRibbonItemSize.Large, false, null, "New", null),
            new UiRibbonAction("Categories", localizer["Ribbon_Categories"].Value, "<svg><use href='/icons/sprite.svg#groups'/></svg>", UiRibbonItemSize.Small, false, null, "Categories", new Func<Task>(() => {
                var nav = ServiceProvider.GetRequiredService<NavigationManager>();
                nav.NavigateTo("/list/savings-plans/categories");
                return Task.CompletedTask;
            })),
        };

        var filter = new List<UiRibbonAction>
        {
            new UiRibbonAction("ToggleActive", OnlyActive ? localizer["OnlyActive"].Value : localizer["StatusArchived"].Value, "<svg><use href='/icons/sprite.svg#refresh'/></svg>", UiRibbonItemSize.Small, false, null, null, new Func<Task>(() => { ToggleActive(); return Task.CompletedTask; }))
        };

        var tabsActions = new List<UiRibbonTab> { new UiRibbonTab(localizer["Ribbon_Group_Manage"].Value, actions) };
        var tabsFilter = new List<UiRibbonTab> { new UiRibbonTab(localizer["Ribbon_Group_Filter"].Value, filter) };

        return new List<UiRibbonRegister>
        {
            new UiRibbonRegister(UiRibbonRegisterKind.Actions, tabsActions),
            new UiRibbonRegister(UiRibbonRegisterKind.Custom, tabsFilter)
        };
    }

    // Public helper for UI to get display symbol attachment id (plan symbol or category fallback)
    public Guid? GetDisplaySymbolAttachmentId(SavingsPlanListItem plan)
    {
        if (plan == null) return null;
        return _displaySymbolByPlan.TryGetValue(plan.Id, out var v) ? v : null;
    }
}
