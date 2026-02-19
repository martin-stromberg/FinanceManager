using FinanceManager.Shared;
using FinanceManager.Shared.Dtos.Budget;
using FinanceManager.Shared.Dtos.Contacts;
using FinanceManager.Shared.Dtos.SavingsPlans;
using FinanceManager.Web.ViewModels.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using System.Threading;

namespace FinanceManager.Web.ViewModels.Budget;

/// <summary>
/// View model for the budget report page.
/// </summary>
public sealed class BudgetReportViewModel : BaseViewModel
{
    private int _loadVersion;
    private CancellationTokenSource? _loadCts;
    private readonly IApiClient _api;
    private readonly IStringLocalizer<FinanceManager.Web.Pages>? _localizer;

    private readonly Dictionary<Guid, ContactDto?> _contactCache = new();
    private readonly Dictionary<Guid, SavingsPlanDto?> _savingsPlanCache = new();

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
    /// Opens the postings overlay for cost-neutral self-contact postings that are not covered by any purpose.
    /// </summary>
    public async Task ShowUnbudgetedSelfCostNeutralPostingsAsync()
    {
        if (!CheckAuthentication())
        {
            return;
        }

        PurposePostingsVisible = true;
        PurposePostingsKind = PostingsOverlayKind.Unbudgeted;
        PurposePostingsPurpose = null;
        PurposePostings = Array.Empty<PostingServiceDto>();
        PurposePostingsLoading = true;
        RaiseStateChanged();

        try
        {
            var (fromDt, toDt) = GetOverlayDateRange();

            var basis = Settings.DateBasis == FinanceManager.Web.ViewModels.Budget.BudgetReportDateBasis.ValutaDate
                ? FinanceManager.Shared.Dtos.Budget.BudgetReportDateBasis.ValutaDate
                : FinanceManager.Shared.Dtos.Budget.BudgetReportDateBasis.BookingDate;

            var rows = await _api.Budgets_GetUnbudgetedPostingsAsync(fromDt, toDt, basis, kind: "selfCostNeutral", ct: CancellationToken.None);
            var fromDate = DateOnly.FromDateTime(fromDt ?? DateTime.MinValue);
            var toDate = DateOnly.FromDateTime(toDt ?? DateTime.MaxValue);

            IEnumerable<PostingServiceDto> filteredRows = Settings.DateBasis == FinanceManager.Web.ViewModels.Budget.BudgetReportDateBasis.ValutaDate
                ? rows.Where(p => DateOnly.FromDateTime(p.ValutaDate) >= fromDate && DateOnly.FromDateTime(p.ValutaDate) <= toDate)
                : rows.Where(p => DateOnly.FromDateTime(p.BookingDate) >= fromDate && DateOnly.FromDateTime(p.BookingDate) <= toDate);

            PurposePostings = Settings.DateBasis == FinanceManager.Web.ViewModels.Budget.BudgetReportDateBasis.ValutaDate
                ? filteredRows.OrderByDescending(p => p.ValutaDate).ToList()
                : filteredRows.OrderByDescending(p => p.BookingDate).ToList();
        }
        catch (Exception ex)
        {
            SetError(_api.LastErrorCode ?? null, _api.LastError ?? ex.Message);
            PurposePostings = Array.Empty<PostingServiceDto>();
        }
        finally
        {
            PurposePostingsLoading = false;
            RaiseStateChanged();
        }
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
    /// Start date of the currently loading report range.
    /// </summary>
    public DateOnly? LoadingFrom { get; private set; }

    /// <summary>
    /// End date of the currently loading report range.
    /// </summary>
    public DateOnly? LoadingTo { get; private set; }

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
        var loadVersion = Interlocked.Increment(ref _loadVersion);
        var newCts = new CancellationTokenSource();
        var previousCts = Interlocked.Exchange(ref _loadCts, newCts);
        previousCts?.Cancel();
        previousCts?.Dispose();

        var to = Settings.AsOfDate;
        var from = new DateOnly(to.Year, to.Month, 1).AddMonths(-(Math.Max(1, Settings.Months) - 1));
        LoadingFrom = from;
        LoadingTo = to;
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
                newCts.Token);

            if (loadVersion != _loadVersion)
            {
                return;
            }

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
                            BudgetReportCategoryRowKind.UnbudgetedSelfCostNeutral => _localizer?["Budget_Report_Unbudgeted_SelfCostNeutral"].Value ?? c.Name,
                            BudgetReportCategoryRowKind.Result => _localizer?["Budget_Report_Result"].Value ?? c.Name,
                            _ => c.Name
                        };

                        var purposes = c.Purposes.Select(pp =>
                        {
                            var purposeName = pp.Name;
                            if (c.Kind == BudgetReportCategoryRowKind.Unbudgeted)
                            {
                                if (pp.Name == "Unbudgeted (Self, cost-neutral)")
                                {
                                    purposeName = _localizer?["Budget_Report_Unbudgeted_SelfCostNeutral"].Value ?? pp.Name;
                                }
                                else if (pp.Name == "Unbudgeted")
                                {
                                    purposeName = _localizer?["Budget_Report_Unbudgeted_General"].Value ?? pp.Name;
                                }
                            }

                            return new BudgetReportPurposeRow(pp.Id, purposeName, pp.Budget, pp.Actual, pp.Delta, pp.DeltaPct, pp.SourceType, pp.SourceId);
                        }).ToList();

                        return new BudgetReportCategoryRow(
                            c.Id,
                            name,
                            c.Kind,
                            c.Budget,
                            c.Actual,
                            c.Delta,
                            c.DeltaPct,
                            purposes);
                    })
                    .ToList()
                : Array.Empty<BudgetReportCategoryRow>();
        }
        catch (OperationCanceledException) when (newCts.Token.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex)
        {
            if (loadVersion != _loadVersion)
            {
                return;
            }

            SetError(_api.LastErrorCode ?? null, _api.LastError ?? ex.Message);
            Periods = Array.Empty<BudgetReportPeriodRow>();
            Categories = Array.Empty<BudgetReportCategoryRow>();
        }
        finally
        {
            if (loadVersion == _loadVersion)
            {
                Loading = false;
                LoadingFrom = null;
                LoadingTo = null;
                RaiseStateChanged();
            }
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

        var export = new UiRibbonTab(localizer["Ribbon_Group_Export"], new List<UiRibbonAction>
        {
            new UiRibbonAction(
                "ExportExcel",
                localizer["Ribbon_ExportExcel"],
                "<svg><use href='/icons/sprite.svg#download'/></svg>",
                UiRibbonItemSize.Small,
                false,
                null,
                () =>
                {
                    RaiseUiActionRequested("ExportExcel");
                    return Task.CompletedTask;
                })
        });

        return new List<UiRibbonRegister>
        {
            new UiRibbonRegister(UiRibbonRegisterKind.Actions, new List<UiRibbonTab> { manage, quickRange, export })
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
                    return new BudgetReportPurposeRow(p.Id, p.Name, p.BudgetSum, p.ActualSum, delta, pct, p.SourceType, p.SourceId);
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

    /// <summary>
    /// Identifies which kind of postings is shown in the postings overlay.
    /// </summary>
    public enum PostingsOverlayKind
    {
        /// <summary>
        /// Postings for a specific budget purpose.
        /// </summary>
        Purpose = 0,

        /// <summary>
        /// Postings that are not covered by any purpose.
        /// </summary>
        Unbudgeted = 1
    }

    /// <summary>
    /// Current postings overlay kind.
    /// </summary>
    public PostingsOverlayKind PurposePostingsKind { get; private set; } = PostingsOverlayKind.Purpose;

    /// <summary>
    /// Whether the purpose postings overlay is currently visible.
    /// </summary>
    public bool PurposePostingsVisible { get; private set; }

    /// <summary>
    /// The purpose currently selected for showing postings.
    /// </summary>
    public BudgetReportPurposeRow? PurposePostingsPurpose { get; private set; }

    /// <summary>
    /// Loaded postings for <see cref="PurposePostingsPurpose"/> within the current report range.
    /// </summary>
    public IReadOnlyList<PostingServiceDto> PurposePostings { get; private set; } = Array.Empty<PostingServiceDto>();

    /// <summary>
    /// Whether postings for <see cref="PurposePostingsPurpose"/> are currently loading.
    /// </summary>
    public bool PurposePostingsLoading { get; private set; }

    /// <summary>
    /// Returns origin display info (name + optional symbol attachment id) for the given posting.
    /// </summary>
    public async Task<(string Name, Guid? SymbolAttachmentId)> GetPostingOriginAsync(PostingServiceDto posting)
    {
        ArgumentNullException.ThrowIfNull(posting);

        if (posting.ContactId.HasValue)
        {
            var dto = await GetContactCachedAsync(posting.ContactId.Value);
            return (dto?.Name ?? string.Empty, dto?.SymbolAttachmentId);
        }

        if (posting.SavingsPlanId.HasValue)
        {
            var dto = await GetSavingsPlanCachedAsync(posting.SavingsPlanId.Value);
            return (dto?.Name ?? string.Empty, dto?.SymbolAttachmentId);
        }

        return (string.Empty, null);
    }

    private async Task<ContactDto?> GetContactCachedAsync(Guid id)
    {
        if (_contactCache.TryGetValue(id, out var cached))
        {
            return cached;
        }

        var dto = await _api.Contacts_GetAsync(id, CancellationToken.None);
        _contactCache[id] = dto;
        return dto;
    }

    private async Task<SavingsPlanDto?> GetSavingsPlanCachedAsync(Guid id)
    {
        if (_savingsPlanCache.TryGetValue(id, out var cached))
        {
            return cached;
        }

        var dto = await _api.SavingsPlans_GetAsync(id, CancellationToken.None);
        _savingsPlanCache[id] = dto;
        return dto;
    }

    /// <summary>
    /// Closes the purpose postings overlay.
    /// </summary>
    public void HidePurposePostings()
    {
        PurposePostingsVisible = false;
        PurposePostingsKind = PostingsOverlayKind.Purpose;
        PurposePostingsPurpose = null;
        PurposePostings = Array.Empty<PostingServiceDto>();
        PurposePostingsLoading = false;
        RaiseStateChanged();
    }

    /// <summary>
    /// Opens the postings overlay for unbudgeted postings in the current interval.
    /// </summary>
    public async Task ShowUnbudgetedPostingsAsync()
    {
        if (!CheckAuthentication())
        {
            return;
        }

        PurposePostingsVisible = true;
        PurposePostingsKind = PostingsOverlayKind.Unbudgeted;
        PurposePostingsPurpose = null;
        PurposePostings = Array.Empty<PostingServiceDto>();
        PurposePostingsLoading = true;
        RaiseStateChanged();

        try
        {
            var (fromDt, toDt) = GetOverlayDateRange();

            var basis = Settings.DateBasis == FinanceManager.Web.ViewModels.Budget.BudgetReportDateBasis.ValutaDate
                ? FinanceManager.Shared.Dtos.Budget.BudgetReportDateBasis.ValutaDate
                : FinanceManager.Shared.Dtos.Budget.BudgetReportDateBasis.BookingDate;

            var rows = await _api.Budgets_GetUnbudgetedPostingsAsync(fromDt, toDt, basis, kind: "remaining", ct: CancellationToken.None);
            PurposePostings = (Settings.DateBasis == FinanceManager.Web.ViewModels.Budget.BudgetReportDateBasis.ValutaDate
                ? rows.OrderByDescending(p => p.ValutaDate).ThenBy(p => p.RecipientName).ThenBy(p => p.Description)
                : rows.OrderByDescending(p => p.BookingDate).ThenBy(p => p.RecipientName).ThenBy(p => p.Description)).ToList();
        }
        catch (Exception ex)
        {
            SetError(_api.LastErrorCode ?? null, _api.LastError ?? ex.Message);
            PurposePostings = Array.Empty<PostingServiceDto>();
        }
        finally
        {
            PurposePostingsLoading = false;
            RaiseStateChanged();
        }
    }

    /// <summary>
    /// Opens the purpose postings overlay and loads postings for the given purpose.
    /// </summary>
    /// <param name="purpose">The selected purpose.</param>
    public async Task ShowPurposePostingsAsync(BudgetReportPurposeRow purpose)
    {
        ArgumentNullException.ThrowIfNull(purpose);
        if (!CheckAuthentication())
        {
            return;
        }

        PurposePostingsVisible = true;
        PurposePostingsKind = PostingsOverlayKind.Purpose;
        PurposePostingsPurpose = purpose;
        PurposePostings = Array.Empty<PostingServiceDto>();
        PurposePostingsLoading = true;
        RaiseStateChanged();

        try
        {
            var (fromDt, toDt) = GetOverlayDateRange();

            IReadOnlyList<PostingServiceDto> rows;
            switch (purpose.SourceType)
            {
                case BudgetSourceType.Contact:
                    rows = await _api.Postings_GetContactAsync(purpose.SourceId, skip: 0, take: 250, q: null, from: fromDt, to: toDt, ct: CancellationToken.None);
                    break;
                case BudgetSourceType.SavingsPlan:
                    rows = await _api.Postings_GetSavingsPlanAsync(purpose.SourceId, skip: 0, take: 250, from: fromDt, to: toDt, q: null, ct: CancellationToken.None);
                    break;
                case BudgetSourceType.ContactGroup:
                    // Load postings for all contacts assigned to this contact group.
                    var contacts = await _api.Contacts_ListAsync(skip: 0, take: 5000, type: null, all: true, nameFilter: null, ct: CancellationToken.None);
                    var contactIds = contacts
                        .Where(c => c.CategoryId == purpose.SourceId)
                        .Select(c => c.Id)
                        .ToList();

                    var list = new List<PostingServiceDto>();
                    foreach (var contactId in contactIds)
                    {
                        var part = await _api.Postings_GetContactAsync(contactId, skip: 0, take: 250, q: null, from: fromDt, to: toDt, ct: CancellationToken.None);
                        if (part.Count > 0)
                        {
                            list.AddRange(part);
                        }
                    }

                    rows = list
                        .GroupBy(p => p.Id)
                        .Select(g => g.First())
                        .ToList();
                    break;
                default:
                    rows = Array.Empty<PostingServiceDto>();
                    break;
            }

            PurposePostings = Settings.DateBasis == FinanceManager.Web.ViewModels.Budget.BudgetReportDateBasis.ValutaDate
                ? rows.OrderByDescending(p => p.ValutaDate).ToList()
                : rows.OrderByDescending(p => p.BookingDate).ToList();
        }
        catch (Exception ex)
        {
            SetError(_api.LastErrorCode ?? null, _api.LastError ?? ex.Message);
            PurposePostings = Array.Empty<PostingServiceDto>();
        }
        finally
        {
            PurposePostingsLoading = false;
            RaiseStateChanged();
        }
    }

    private (DateTime? From, DateTime? To) GetOverlayDateRange()
    {
        var asOf = Settings.AsOfDate;
        var rangeTo = new DateOnly(asOf.Year, asOf.Month, DateTime.DaysInMonth(asOf.Year, asOf.Month));
        var rangeFrom = new DateOnly(asOf.Year, asOf.Month, 1).AddMonths(-(Math.Max(1, Settings.Months) - 1));

        var intervalFrom = rangeFrom;
        var intervalTo = rangeTo;
        if (Settings.CategoryValueScope == BudgetReportValueScope.LastInterval)
        {
            intervalFrom = new DateOnly(rangeTo.Year, rangeTo.Month, 1);
            intervalTo = rangeTo;
        }

        return (intervalFrom.ToDateTime(TimeOnly.MinValue), intervalTo.ToDateTime(TimeOnly.MaxValue));
    }
}

/// <summary>
/// Period row for the report.
/// </summary>
public sealed record BudgetReportPeriodRow(DateOnly PeriodStart, decimal Budget, decimal Actual, decimal Delta, decimal DeltaPct);

/// <summary>
/// Purpose row inside a category.
/// </summary>
public sealed record BudgetReportPurposeRow(Guid Id, string Name, decimal Budget, decimal Actual, decimal Delta, decimal DeltaPct, BudgetSourceType SourceType, Guid SourceId);

/// <summary>
/// Category row including nested purposes.
/// </summary>
public sealed record BudgetReportCategoryRow(Guid Id, string Name, BudgetReportCategoryRowKind Kind, decimal Budget, decimal Actual, decimal Delta, decimal DeltaPct, IReadOnlyList<BudgetReportPurposeRow> Purposes);
