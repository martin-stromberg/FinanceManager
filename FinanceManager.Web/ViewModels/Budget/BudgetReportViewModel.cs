using FinanceManager.Shared;
using FinanceManager.Shared.Dtos.Budget;
using FinanceManager.Web.ViewModels.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace FinanceManager.Web.ViewModels.Budget;

/// <summary>
/// View model for the budget report page.
/// </summary>
public sealed class BudgetReportViewModel : BaseViewModel
{
    private readonly IApiClient _api;
    private readonly IStringLocalizer<FinanceManager.Web.Pages>? _localizer;

    /// <summary>
    /// Creates a new instance.
    /// </summary>
    /// <param name="services">Service provider.</param>
    public BudgetReportViewModel(IServiceProvider services) : base(services)
    {
        _api = services.GetRequiredService<IApiClient>();
        _localizer = services.GetService<IStringLocalizer<FinanceManager.Web.Pages>>();
    }

    /// <summary>
    /// Current report settings.
    /// </summary>
    public BudgetReportSettings Settings { get; private set; } = BudgetReportSettings.Default;

    /// <summary>
    /// Whether the settings overlay is visible.
    /// </summary>
    public bool SettingsVisible { get; private set; }

    /// <summary>
    /// Period rows for the chart and monthly table.
    /// </summary>
    public IReadOnlyList<BudgetReportPeriodRow> Periods { get; private set; } = Array.Empty<BudgetReportPeriodRow>();

    /// <summary>
    /// Category rows for the detail table.
    /// </summary>
    public IReadOnlyList<BudgetReportCategoryRow> Categories { get; private set; } = Array.Empty<BudgetReportCategoryRow>();

    /// <summary>
    /// Initializes the report and loads data.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (!CheckAuthentication())
        {
            return;
        }

        await LoadAsync();
    }

    /// <summary>
    /// Loads report data based on the current <see cref="Settings"/>.
    /// </summary>
    public async Task LoadAsync()
    {
        Loading = true;
        SetError(null, null);
        RaiseStateChanged();

        try
        {
            var dto = await _api.Budgets_GetReportAsync(
                new BudgetReportRequest(
                    AsOfDate: Settings.AsOfDate,
                    Months: Settings.Months,
                    Interval: (FinanceManager.Shared.Dtos.Budget.BudgetReportInterval)Settings.Interval,
                    ShowTitle: Settings.ShowTitle,
                    ShowLineChart: Settings.ShowLineChart,
                    ShowMonthlyTable: Settings.ShowMonthlyTable,
                    ShowDetailsTable: Settings.ShowDetailsTable,
                    CategoryValueScope: (FinanceManager.Shared.Dtos.Budget.BudgetReportValueScope)Settings.CategoryValueScope,
                    IncludePurposeRows: Settings.ShowPurposeRows,
                    DateBasis: Settings.DateBasis == FinanceManager.Web.ViewModels.Budget.BudgetReportDateBasis.ValutaDate
                        ? FinanceManager.Shared.Dtos.Budget.BudgetReportDateBasis.ValutaDate
                        : FinanceManager.Shared.Dtos.Budget.BudgetReportDateBasis.BookingDate),
                CancellationToken.None);

            Periods = dto.Periods
                .OrderBy(p => p.From)
                .Select(p => new BudgetReportPeriodRow(p.From, p.Budget, p.Actual, p.Delta, p.DeltaPct))
                .ToList();

            Categories = Settings.ShowDetailsTable
                ? dto.Categories.Select(c =>
                    {
                        var name = c.Kind switch
                        {
                            BudgetReportCategoryRowKind.Sum => _localizer?["List_Sum"].Value ?? c.Name,
                            BudgetReportCategoryRowKind.Unbudgeted => _localizer?["Budget_Report_Unbudgeted"].Value ?? c.Name,
                            BudgetReportCategoryRowKind.Result => _localizer?["Budget_Report_Result"].Value ?? c.Name,
                            _ => c.Name
                        };

                        return new BudgetReportCategoryRow(
                            c.Id,
                            name,
                            c.Kind,
                            c.Budget,
                            c.Actual,
                            c.Delta,
                            c.DeltaPct,
                            c.Purposes.Select(pp => new BudgetReportPurposeRow(pp.Id, pp.Name, pp.Budget, pp.Actual, pp.Delta, pp.DeltaPct)).ToList());
                    })
                    .ToList()
                : Array.Empty<BudgetReportCategoryRow>();
        }
        catch (Exception ex)
        {
            SetError(_api.LastErrorCode ?? null, _api.LastError ?? ex.Message);
            Periods = Array.Empty<BudgetReportPeriodRow>();
            Categories = Array.Empty<BudgetReportCategoryRow>();
        }
        finally
        {
            Loading = false;
            RaiseStateChanged();
        }
    }

    private static IReadOnlyList<(DateOnly From, DateOnly To)> BuildPeriodBoundaries(DateOnly from, DateOnly to, BudgetReportInterval interval)
    {
        if (from > to)
        {
            (from, to) = (to, from);
        }

        var list = new List<(DateOnly From, DateOnly To)>();
        var cur = StartOfMonth(from);
        var end = EndOfMonth(to);

        var stepMonths = interval switch
        {
            BudgetReportInterval.Month => 1,
            BudgetReportInterval.Quarter => 3,
            BudgetReportInterval.Year => 12,
            _ => 1
        };

        while (cur <= end)
        {
            var pFrom = cur;
            var pTo = EndOfMonth(cur.AddMonths(stepMonths - 1));
            if (pTo > end)
            {
                pTo = end;
            }

            list.Add((pFrom, pTo));
            cur = cur.AddMonths(stepMonths);
        }

        return list;
    }

    private static DateOnly StartOfMonth(DateOnly d) => new(d.Year, d.Month, 1);

    private static DateOnly EndOfMonth(DateOnly d)
        => new(d.Year, d.Month, DateTime.DaysInMonth(d.Year, d.Month));

    /// <summary>
    /// Shows the settings overlay.
    /// </summary>
    public void ShowSettings()
    {
        SettingsVisible = true;
        RaiseStateChanged();
    }

    /// <summary>
    /// Hides the settings overlay.
    /// </summary>
    public void HideSettings()
    {
        SettingsVisible = false;
        RaiseStateChanged();
    }

    /// <summary>
    /// Applies new settings and reloads the report.
    /// </summary>
    /// <param name="settings">New settings.</param>
    public async Task ApplySettingsAsync(BudgetReportSettings settings)
    {
        Settings = settings;
        SettingsVisible = false;
        await LoadAsync();
    }

    /// <inheritdoc />
    protected override IReadOnlyList<UiRibbonRegister>? GetRibbonRegisterDefinition(IStringLocalizer localizer)
    {
        var manage = new UiRibbonTab(localizer["Ribbon_Group_Manage"], new List<UiRibbonAction>
        {
            new UiRibbonAction(
                "Settings",
                localizer["Ribbon_Settings"],
                "<svg><use href='/icons/sprite.svg#settings'/></svg>",
                UiRibbonItemSize.Small,
                false,
                null,
                () =>
                {
                    RaiseUiActionRequested("ShowSettings");
                    return Task.CompletedTask;
                })
        });

        var quickRange = new UiRibbonTab(localizer["Ribbon_Group_Analysis"], new List<UiRibbonAction>
        {
            new UiRibbonAction(
                "PrevMonth",
                localizer["Ribbon_PrevMonth"],
                "<svg><use href='/icons/sprite.svg#prev'/></svg>",
                UiRibbonItemSize.Small,
                false,
                null,
                () => ShiftAsOfMonthAsync(-1)),
            new UiRibbonAction(
                "ThisMonth",
                localizer["Ribbon_ThisMonth"],
                "<svg><use href='/icons/sprite.svg#calendar'/></svg>",
                UiRibbonItemSize.Small,
                false,
                null,
                SetAsOfToCurrentMonthAsync),
            new UiRibbonAction(
                "NextMonth",
                localizer["Ribbon_NextMonth"],
                "<svg><use href='/icons/sprite.svg#next'/></svg>",
                UiRibbonItemSize.Small,
                false,
                null,
                () => ShiftAsOfMonthAsync(1)),
            new UiRibbonAction(
                "PrevYear",
                localizer["Ribbon_PrevYear"],
                "<svg><use href='/icons/sprite.svg#prev'/></svg>",
                UiRibbonItemSize.Small,
                false,
                null,
                () => ShiftAsOfYearAsync(-1)),
            new UiRibbonAction(
                "NextYear",
                localizer["Ribbon_NextYear"],
                "<svg><use href='/icons/sprite.svg#next'/></svg>",
                UiRibbonItemSize.Small,
                false,
                null,
                () => ShiftAsOfYearAsync(1))
         });

        return new List<UiRibbonRegister>
        {
            new UiRibbonRegister(UiRibbonRegisterKind.Actions, new List<UiRibbonTab> { manage, quickRange })
        };
    }

    private static IReadOnlyList<BudgetReportPeriodRow> BuildPeriods(DateOnly from, DateOnly to, BudgetReportInterval interval, IReadOnlyList<BudgetPurposeOverviewDto> purposes)
    {
        var buckets = new Dictionary<DateOnly, (decimal Budget, decimal Actual)>();

        foreach (var p in purposes)
        {
            // Purposes overview for from/to already returns the totals for the whole range.
            // For a first version we show one bucket that equals the whole range.
            // TODO: Replace with per-period aggregation from API.
            var key = from;
            if (!buckets.TryGetValue(key, out var agg))
            {
                agg = (0m, 0m);
            }

            agg.Budget += p.BudgetSum;
            agg.Actual += p.ActualSum;
            buckets[key] = agg;
        }

        return buckets
            .OrderBy(kvp => kvp.Key)
            .Select(kvp =>
            {
                var (budget, actual) = kvp.Value;
                var delta = budget - actual;
                var pct = budget == 0m ? 0m : (delta / budget) * 100m;
                return new BudgetReportPeriodRow(kvp.Key, budget, actual, delta, pct);
            })
            .ToList();
    }

    private static IReadOnlyList<BudgetReportCategoryRow> BuildCategories(IReadOnlyList<BudgetCategoryOverviewDto> categories, IReadOnlyList<BudgetPurposeOverviewDto> purposes)
    {
        var purposeLookup = purposes
            .GroupBy(p => p.BudgetCategoryId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var result = new List<BudgetReportCategoryRow>();
        foreach (var cat in categories.OrderBy(c => c.Name))
        {
            purposeLookup.TryGetValue(cat.Id, out var list);
            list ??= new List<BudgetPurposeOverviewDto>();

            var purposeRows = list
                .OrderBy(p => p.Name)
                .Select(p =>
                {
                    var delta = p.BudgetSum - p.ActualSum;
                    var pct = p.BudgetSum == 0m ? 0m : (delta / p.BudgetSum) * 100m;
                    return new BudgetReportPurposeRow(p.Id, p.Name, p.BudgetSum, p.ActualSum, delta, pct);
                })
                .ToList();

            var deltaCat = cat.Budget - cat.Actual;
            var pctCat = cat.Budget == 0m ? 0m : (deltaCat / cat.Budget) * 100m;

            result.Add(new BudgetReportCategoryRow(
                cat.Id,
                cat.Name,
                BudgetReportCategoryRowKind.Data,
                cat.Budget,
                cat.Actual,
                deltaCat,
                pctCat,
                purposeRows));
        }

        return result;
    }

    private async Task ShiftAsOfMonthAsync(int monthDelta)
    {
        var cur = Settings.AsOfDate;
        var shifted = cur.AddMonths(monthDelta);
        var monthEnd = new DateOnly(shifted.Year, shifted.Month, DateTime.DaysInMonth(shifted.Year, shifted.Month));
        Settings = Settings with { AsOfDate = monthEnd };
        await LoadAsync();
    }

    private async Task SetAsOfToCurrentMonthAsync()
    {
        var today = DateTime.Today;
        var monthEnd = new DateOnly(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month));
        Settings = Settings with { AsOfDate = monthEnd };
        await LoadAsync();
    }

    private async Task ShiftAsOfYearAsync(int yearDelta)
    {
        var cur = Settings.AsOfDate;
        var year = cur.Year + yearDelta;
        var month = cur.Month;
        var monthEnd = new DateOnly(year, month, DateTime.DaysInMonth(year, month));
        Settings = Settings with { AsOfDate = monthEnd };
        await LoadAsync();
    }
}

/// <summary>
/// Period row for the report.
/// </summary>
public sealed record BudgetReportPeriodRow(DateOnly PeriodStart, decimal Budget, decimal Actual, decimal Delta, decimal DeltaPct);

/// <summary>
/// Purpose row inside a category.
/// </summary>
public sealed record BudgetReportPurposeRow(Guid Id, string Name, decimal Budget, decimal Actual, decimal Delta, decimal DeltaPct);

/// <summary>
/// Category row including nested purposes.
/// </summary>
public sealed record BudgetReportCategoryRow(Guid Id, string Name, BudgetReportCategoryRowKind Kind, decimal Budget, decimal Actual, decimal Delta, decimal DeltaPct, IReadOnlyList<BudgetReportPurposeRow> Purposes);
