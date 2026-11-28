using FinanceManager.Application;
using FinanceManager.Shared;
using FinanceManager.Shared.Dtos.Budget;
using FinanceManager.Web.ViewModels.Budget;
using FinanceManager.Web.ViewModels.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;

namespace FinanceManager.Tests.ViewModels;

/// <summary>
/// Covers the unified analysis-ribbon navigation of <see cref="BudgetPurposeListViewModel"/> (#603):
/// the five buttons must shift the displayed range relative to the current view (repeatable),
/// preserve the range width, and offer the same order as the budget report.
/// </summary>
public sealed class BudgetPurposeListAnalysisNavigationTests
{
    private sealed class TestCurrentUserService : ICurrentUserService
    {
        public Guid UserId { get; set; } = Guid.NewGuid();
        public string? PreferredLanguage { get; set; }
        public bool IsAuthenticated { get; set; } = true;
        public bool IsAdmin { get; set; }
    }

    private sealed class TestLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(name, arguments), resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) { yield break; }
    }

    private static IServiceProvider CreateSp(Mock<IApiClient> apiMock, bool authenticated = true)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ICurrentUserService>(new TestCurrentUserService { IsAuthenticated = authenticated });
        services.AddSingleton(apiMock.Object);
        services.AddSingleton(typeof(IStringLocalizer<>), typeof(TestLocalizer<>));
        return services.BuildServiceProvider();
    }

    private static BudgetPurposeListViewModel CreateVm(IServiceProvider sp)
        => new(sp);

    private static UiRibbonAction AnalysisAction(BudgetPurposeListViewModel vm, IServiceProvider sp, string id)
    {
        var loc = sp.GetRequiredService<IStringLocalizer<BudgetPurposeListAnalysisNavigationTests>>();
        var regs = vm.GetRibbonRegisters(loc) ?? Array.Empty<UiRibbonRegister>();
        return regs
            .SelectMany(r => r.Tabs ?? new List<UiRibbonTab>())
            .SelectMany(t => t.Items)
            .Single(a => a.Id == id);
    }

    private static async Task InvokeAsync(BudgetPurposeListViewModel vm, IServiceProvider sp, string id)
        => await AnalysisAction(vm, sp, id).Callback!();

    private static (DateTime From, DateTime To) RangeOf(BudgetPurposeListViewModel vm)
        => (vm.RangeFrom!.Value.Date, vm.RangeTo!.Value.Date);

    private static (DateTime From, DateTime To) CalendarMonth(int year, int month)
        => (new DateTime(year, month, 1), new DateTime(year, month, DateTime.DaysInMonth(year, month)));

    /// <summary>
    /// From the default current-month view, PrevMonth must move the range one calendar month back —
    /// relative to the view, not anchored on today.
    /// </summary>
    [Fact]
    public async Task PrevMonth_FromCurrentMonth_SetsPreviousCalendarMonth()
    {
        var apiMock = new Mock<IApiClient>();
        var sp = CreateSp(apiMock);
        var vm = CreateVm(sp);

        var today = DateTime.Today;
        var (from, to) = CalendarMonth(today.Year, today.Month);
        vm.SetRange(from, to);

        await InvokeAsync(vm, sp, "PrevMonth");

        var expected = CalendarMonth(from.AddMonths(-1).Year, from.AddMonths(-1).Month);
        Assert.Equal(expected, RangeOf(vm));
    }

    /// <summary>
    /// NextMonth must move the displayed range exactly one calendar month forward.
    /// </summary>
    [Fact]
    public async Task NextMonth_FromCurrentMonth_SetsNextCalendarMonth()
    {
        var apiMock = new Mock<IApiClient>();
        var sp = CreateSp(apiMock);
        var vm = CreateVm(sp);

        var today = DateTime.Today;
        var (from, to) = CalendarMonth(today.Year, today.Month);
        vm.SetRange(from, to);

        await InvokeAsync(vm, sp, "NextMonth");

        var expected = CalendarMonth(from.AddMonths(1).Year, from.AddMonths(1).Month);
        Assert.Equal(expected, RangeOf(vm));
    }

    /// <summary>
    /// PrevYear on a free (non month-aligned) range must shift both bounds exactly one year back —
    /// the range width is preserved and no longer snaps to a full calendar year.
    /// </summary>
    [Fact]
    public async Task PrevYear_OnCustomRange_PreservesWidth()
    {
        var apiMock = new Mock<IApiClient>();
        var sp = CreateSp(apiMock);
        var vm = CreateVm(sp);

        vm.SetRange(new DateTime(2025, 3, 15), new DateTime(2025, 5, 20));

        await InvokeAsync(vm, sp, "PrevYear");

        Assert.Equal((new DateTime(2024, 3, 15), new DateTime(2024, 5, 20)), RangeOf(vm));
    }

    /// <summary>
    /// NextYear on a free range must shift both bounds exactly one year forward.
    /// </summary>
    [Fact]
    public async Task NextYear_OnCustomRange_PreservesWidth()
    {
        var apiMock = new Mock<IApiClient>();
        var sp = CreateSp(apiMock);
        var vm = CreateVm(sp);

        vm.SetRange(new DateTime(2025, 3, 15), new DateTime(2025, 5, 20));

        await InvokeAsync(vm, sp, "NextYear");

        Assert.Equal((new DateTime(2026, 3, 15), new DateTime(2026, 5, 20)), RangeOf(vm));
    }

    /// <summary>
    /// ThisMonth resets the view to the current calendar month.
    /// </summary>
    [Fact]
    public async Task ThisMonth_SetsCurrentCalendarMonth()
    {
        var apiMock = new Mock<IApiClient>();
        var sp = CreateSp(apiMock);
        var vm = CreateVm(sp);

        vm.SetRange(new DateTime(2025, 3, 15), new DateTime(2025, 5, 20));

        await InvokeAsync(vm, sp, "ThisMonth");

        var today = DateTime.Today;
        Assert.Equal(CalendarMonth(today.Year, today.Month), RangeOf(vm));
    }

    /// <summary>
    /// ThisYear stays as the year quick-filter: it sets the whole current calendar year.
    /// </summary>
    [Fact]
    public async Task ThisYear_SetsCurrentCalendarYear()
    {
        var apiMock = new Mock<IApiClient>();
        var sp = CreateSp(apiMock);
        var vm = CreateVm(sp);

        vm.SetRange(new DateTime(2025, 3, 15), new DateTime(2025, 5, 20));

        await InvokeAsync(vm, sp, "ThisYear");

        var year = DateTime.Today.Year;
        Assert.Equal((new DateTime(year, 1, 1), new DateTime(year, 12, 31)), RangeOf(vm));
    }

    /// <summary>
    /// PrevMonth on a free range shifts both bounds exactly one calendar month back (no snapping).
    /// </summary>
    [Fact]
    public async Task PrevMonth_OnCustomRange_ShiftsBoundsExactly()
    {
        var apiMock = new Mock<IApiClient>();
        var sp = CreateSp(apiMock);
        var vm = CreateVm(sp);

        vm.SetRange(new DateTime(2025, 3, 15), new DateTime(2025, 5, 20));

        await InvokeAsync(vm, sp, "PrevMonth");

        Assert.Equal((new DateTime(2025, 2, 15), new DateTime(2025, 4, 20)), RangeOf(vm));
    }

    /// <summary>
    /// Shifting a range whose end is the 31st into a shorter month clamps the day-of-month
    /// (31.03. → 28.02. in a non-leap year).
    /// </summary>
    [Fact]
    public async Task PrevMonth_ClampsOnShortMonth()
    {
        var apiMock = new Mock<IApiClient>();
        var sp = CreateSp(apiMock);
        var vm = CreateVm(sp);

        vm.SetRange(new DateTime(2025, 3, 1), new DateTime(2025, 3, 31));

        await InvokeAsync(vm, sp, "PrevMonth");

        Assert.Equal((new DateTime(2025, 2, 1), new DateTime(2025, 2, 28)), RangeOf(vm));
    }

    /// <summary>
    /// With no range set (e.g. after clearing filters) PrevMonth anchors on the current
    /// calendar month and moves it one month back.
    /// </summary>
    [Fact]
    public async Task PrevMonth_NullRange_AnchorsCurrentMonth()
    {
        var apiMock = new Mock<IApiClient>();
        var sp = CreateSp(apiMock);
        var vm = CreateVm(sp);

        await InvokeAsync(vm, sp, "PrevMonth");

        var today = DateTime.Today;
        var prev = new DateTime(today.Year, today.Month, 1).AddMonths(-1);
        Assert.Equal(CalendarMonth(prev.Year, prev.Month), RangeOf(vm));
    }

    /// <summary>
    /// NextMonth crossing a year boundary rolls into January of the next year.
    /// </summary>
    [Fact]
    public async Task NextMonth_OnDecember_RollsIntoJanuary()
    {
        var apiMock = new Mock<IApiClient>();
        var sp = CreateSp(apiMock);
        var vm = CreateVm(sp);

        vm.SetRange(new DateTime(2025, 12, 1), new DateTime(2025, 12, 31));

        await InvokeAsync(vm, sp, "NextMonth");

        Assert.Equal((new DateTime(2026, 1, 1), new DateTime(2026, 1, 31)), RangeOf(vm));
    }

    /// <summary>
    /// PrevYear on a range ending on leap day clamps to the 28th in a non-leap target year,
    /// and because the source end is a month end the clamped result snaps back to month end.
    /// </summary>
    [Fact]
    public async Task PrevYear_OnLeapDayRange_ClampsToFebruary28()
    {
        var apiMock = new Mock<IApiClient>();
        var sp = CreateSp(apiMock);
        var vm = CreateVm(sp);

        vm.SetRange(new DateTime(2024, 2, 1), new DateTime(2024, 2, 29));

        await InvokeAsync(vm, sp, "PrevYear");

        Assert.Equal((new DateTime(2023, 2, 1), new DateTime(2023, 2, 28)), RangeOf(vm));
    }

    /// <summary>
    /// When only a lower bound is set, the upper bound is anchored to that bound's calendar
    /// month before shifting — the shifted range must not invert.
    /// </summary>
    [Fact]
    public async Task PrevMonth_OnlyFromSet_AnchorsToBoundsMonth()
    {
        var apiMock = new Mock<IApiClient>();
        var sp = CreateSp(apiMock);
        var vm = CreateVm(sp);

        vm.SetRange(new DateTime(2030, 6, 15), null);

        await InvokeAsync(vm, sp, "PrevMonth");

        Assert.Equal((new DateTime(2030, 5, 15), new DateTime(2030, 5, 31)), RangeOf(vm));
    }

    /// <summary>
    /// When only an upper bound is set, the lower bound is anchored to that bound's calendar
    /// month before shifting.
    /// </summary>
    [Fact]
    public async Task PrevMonth_OnlyToSet_AnchorsToBoundsMonth()
    {
        var apiMock = new Mock<IApiClient>();
        var sp = CreateSp(apiMock);
        var vm = CreateVm(sp);

        vm.SetRange(null, new DateTime(2030, 6, 20));

        await InvokeAsync(vm, sp, "PrevMonth");

        Assert.Equal((new DateTime(2030, 5, 1), new DateTime(2030, 5, 20)), RangeOf(vm));
    }

    /// <summary>
    /// An inverted user-entered range (Von > Bis) is normalized before applying — the shifted
    /// result must be a valid ascending range.
    /// </summary>
    [Fact]
    public async Task PrevMonth_InvertedRange_Normalizes()
    {
        var apiMock = new Mock<IApiClient>();
        var sp = CreateSp(apiMock);
        var vm = CreateVm(sp);

        vm.SetRange(new DateTime(2025, 5, 20), new DateTime(2025, 3, 15));

        await InvokeAsync(vm, sp, "PrevMonth");

        Assert.Equal((new DateTime(2025, 2, 15), new DateTime(2025, 4, 20)), RangeOf(vm));
    }

    /// <summary>
    /// After a navigation click a reload must pass the shifted range to the API.
    /// </summary>
    [Fact]
    public async Task NextMonth_LoadAsync_QueriesApiWithShiftedRange()
    {
        var apiMock = new Mock<IApiClient>();
        apiMock.Setup(a => a.Budgets_ListPurposesAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<BudgetSourceType?>(), It.IsAny<string?>(),
                It.IsAny<DateOnly?>(), It.IsAny<DateOnly?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BudgetPurposeOverviewDto>());
        var sp = CreateSp(apiMock);
        var vm = CreateVm(sp);

        vm.SetRange(new DateTime(2025, 3, 1), new DateTime(2025, 3, 31));

        await InvokeAsync(vm, sp, "NextMonth");
        await ((IListProvider)vm).LoadAsync();

        apiMock.Verify(a => a.Budgets_ListPurposesAsync(
            It.IsAny<int>(), It.IsAny<int>(), It.IsAny<BudgetSourceType?>(), It.IsAny<string?>(),
            new DateOnly(2025, 4, 1), new DateOnly(2025, 4, 30), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// A shift that would leave the DateTime domain (e.g. NextYear at year 9999) is a no-op —
    /// the current range is kept and no reload is triggered.
    /// </summary>
    [Fact]
    public async Task NextYear_AtDateTimeBoundary_KeepsRange()
    {
        var apiMock = new Mock<IApiClient>();
        var sp = CreateSp(apiMock);
        var vm = CreateVm(sp);

        string? raised = null;
        vm.UiActionRequested += (_, e) => raised = e?.Action;

        var from = new DateTime(9999, 6, 1);
        var to = new DateTime(9999, 6, 30);
        vm.SetRange(from, to);

        await InvokeAsync(vm, sp, "NextYear");

        Assert.Equal((from, to), RangeOf(vm));
        Assert.Null(raised);
    }

    /// <summary>
    /// Repeated clicks must keep walking backwards — the shift is relative to the current view,
    /// not re-anchored on today.
    /// </summary>
    [Fact]
    public async Task PrevMonth_Repeated_WalksBackwards()
    {
        var apiMock = new Mock<IApiClient>();
        var sp = CreateSp(apiMock);
        var vm = CreateVm(sp);

        var today = DateTime.Today;
        var (from, to) = CalendarMonth(today.Year, today.Month);
        vm.SetRange(from, to);

        await InvokeAsync(vm, sp, "PrevMonth");
        await InvokeAsync(vm, sp, "PrevMonth");

        var twoBack = from.AddMonths(-2);
        Assert.Equal(CalendarMonth(twoBack.Year, twoBack.Month), RangeOf(vm));
    }

    /// <summary>
    /// The analysis tab must offer the five unified buttons plus the kept ThisYear quick-filter.
    /// The ribbon renders three items per column, so ThisYear sits between PrevYear and NextYear
    /// giving both columns a symmetric prev/this/next layout; the five shared buttons keep the
    /// same relative order as the budget report.
    /// </summary>
    [Fact]
    public void AnalysisButtons_OrderedForSymmetricColumns()
    {
        var apiMock = new Mock<IApiClient>();
        var sp = CreateSp(apiMock);
        var vm = CreateVm(sp);

        var loc = sp.GetRequiredService<IStringLocalizer<BudgetPurposeListAnalysisNavigationTests>>();
        var analysisTab = (vm.GetRibbonRegisters(loc) ?? Array.Empty<UiRibbonRegister>())
            .SelectMany(r => r.Tabs ?? new List<UiRibbonTab>())
            .Single(t => t.Items.Any(i => i.Id == "PrevMonth") && t.Items.Any(i => i.Id == "ThisYear"));

        Assert.Equal(
            new[] { "PrevMonth", "ThisMonth", "NextMonth", "PrevYear", "ThisYear", "NextYear" },
            analysisTab.Items.Select(i => i.Id).ToArray());
    }

    /// <summary>
    /// Every navigation button must raise the "Reload" UI action so the list reloads with the
    /// shifted range.
    /// </summary>
    /// <param name="actionId">Ribbon action identifier under test.</param>
    [Theory]
    [InlineData("PrevMonth")]
    [InlineData("ThisMonth")]
    [InlineData("NextMonth")]
    [InlineData("PrevYear")]
    [InlineData("NextYear")]
    [InlineData("ThisYear")]
    public async Task NavigationButton_RaisesReload(string actionId)
    {
        var apiMock = new Mock<IApiClient>();
        var sp = CreateSp(apiMock);
        var vm = CreateVm(sp);

        string? raised = null;
        vm.UiActionRequested += (_, e) => raised = e?.Action;

        await InvokeAsync(vm, sp, actionId);

        Assert.Equal("Reload", raised);
    }
}
