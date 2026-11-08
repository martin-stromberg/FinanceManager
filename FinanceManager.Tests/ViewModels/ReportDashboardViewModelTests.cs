using FinanceManager.Application;
using FinanceManager.Shared;
using FinanceManager.Web.ViewModels.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;

namespace FinanceManager.Tests.ViewModels;

/// <summary>
/// Covers <see cref="ReportDashboardViewModel"/>'s core reporting logic: querying and aggregating report
/// points per period, favorite save/update/delete round-trips, computed totals and column visibility
/// (category, previous/year comparison, projection), the projection-comparison eligibility rule (security-only,
/// single-kind, non-"all history" selections), grouped parent/child row derivation, and the negative-value marker
/// used to highlight unfavorable figures in the UI.
/// </summary>
public sealed class ReportDashboardViewModelTests
{
    private sealed class TestCurrentUserService : ICurrentUserService
    {
        public Guid UserId { get; set; } = Guid.NewGuid();
        public string? PreferredLanguage { get; set; }
        public bool IsAuthenticated { get; set; } = true;
        public bool IsAdmin { get; set; }
    }

    private static (ReportDashboardViewModel vm, Mock<IApiClient> apiMock) CreateVm(bool authenticated = true)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ICurrentUserService>(new TestCurrentUserService { IsAuthenticated = authenticated });
        var apiMock = new Mock<IApiClient>();
        services.AddSingleton(apiMock.Object);
        var sp = services.BuildServiceProvider();
        var vm = new ReportDashboardViewModel(sp);
        return (vm, apiMock);
    }

    private static List<ReportAggregatePointDto> CreatePoints(int count)
    {
        var start = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        return Enumerable.Range(0, count)
            .Select(i => new ReportAggregatePointDto(start.AddMonths(i), "Type:Bank", "Bank", null, 100 + i, null, null, null, null))
            .ToList();
    }

    /// <summary>
    /// Verifies that <c>LoadAsync</c> forwards the requested filters to the API, returns the resulting
    /// points unchanged in count, and does not request projection comparison unless explicitly asked for.
    /// </summary>
    [Fact]
    public async Task LoadAsync_ReturnsPoints()
    {
        var (vm, apiMock) = CreateVm();
        var result = new ReportAggregationResult(ReportInterval.Month, CreatePoints(3), false, false, false);

        apiMock.Setup(a => a.Reports_QueryAggregatesAsync(It.IsAny<ReportAggregatesQueryRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

        var resp = await vm.LoadAsync(PostingKind.Bank, 0, 24, false, false, false, false, new[] { PostingKind.Bank }, DateTime.UtcNow, null, ct: TestContext.Current.CancellationToken);

        Assert.Equal(3, resp.Points.Count);
        apiMock.Verify(a => a.Reports_QueryAggregatesAsync(
            It.Is<ReportAggregatesQueryRequest>(r => !r.CompareProjection),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Verifies the full favorite lifecycle: creating a favorite sends a create request with the
    /// projection-comparison flag set, updating sends an update request with the same flag preserved, and
    /// deleting reports success - covering the three API calls the "save report as favorite" feature depends on.
    /// </summary>
    [Fact]
    public async Task SaveUpdateDelete_Favorites_Roundtrip()
    {
        var (vm, apiMock) = CreateVm();
        var savedFav = new ReportFavoriteDto(Guid.NewGuid(), "Fav", PostingKind.Bank, false, 0, 24, false, false, true, true, DateTime.UtcNow, null, new[] { PostingKind.Bank }, null, false);
        var updatedFav = new ReportFavoriteDto(Guid.NewGuid(), "Fav2", PostingKind.Bank, false, 0, 24, false, false, true, true, DateTime.UtcNow, null, new[] { PostingKind.Bank }, null, false);

        apiMock.Setup(a => a.Reports_CreateFavoriteAsync(It.IsAny<ReportFavoriteCreateApiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(savedFav);
        apiMock.Setup(a => a.Reports_UpdateFavoriteAsync(It.IsAny<Guid>(), It.IsAny<ReportFavoriteUpdateApiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(updatedFav);
        apiMock.Setup(a => a.Reports_DeleteFavoriteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var saved = await vm.SaveFavoriteAsync("n", PostingKind.Bank, false, 0, 24, false, false, true, true, true, new[] { PostingKind.Bank }, null, ct: TestContext.Current.CancellationToken);
        Assert.NotNull(saved);
        apiMock.Verify(a => a.Reports_CreateFavoriteAsync(
            It.Is<ReportFavoriteCreateApiRequest>(r => r.CompareProjection),
            It.IsAny<CancellationToken>()), Times.Once);

        var updated = await vm.UpdateFavoriteAsync(Guid.NewGuid(), "n2", PostingKind.Bank, false, 0, 24, false, false, true, true, true, new[] { PostingKind.Bank }, null, ct: TestContext.Current.CancellationToken);
        Assert.NotNull(updated);
        apiMock.Verify(a => a.Reports_UpdateFavoriteAsync(
            It.IsAny<Guid>(),
            It.Is<ReportFavoriteUpdateApiRequest>(r => r.CompareProjection),
            It.IsAny<CancellationToken>()), Times.Once);

        var deleted = await vm.DeleteFavoriteAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);
        Assert.True(deleted);
    }

    /// <summary>
    /// Verifies that the per-period chart data sums all selected posting kinds within the same month
    /// into a single point (e.g. bank + contact for January combine into one 150m point) while keeping later
    /// months as separate entries, matching the chart's expected month-by-month aggregation.
    /// </summary>
    [Fact]
    public async Task GetChartByPeriod_ComputesSums_PerMonth()
    {
        var (vm, apiMock) = CreateVm();
        var start = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var points = new List<ReportAggregatePointDto>
        {
            new ReportAggregatePointDto(start, "Type:Bank", "Bank", null, 100m, null, null, null, null),
            new ReportAggregatePointDto(start, "Type:Contact", "Contact", null, 50m, null, null, null, null),
            new ReportAggregatePointDto(start.AddMonths(1), "Type:Bank", "Bank", null, 200m, null, null, null, null)
        };
        var result = new ReportAggregationResult(ReportInterval.Month, points, false, false, false);

        apiMock.Setup(a => a.Reports_QueryAggregatesAsync(It.IsAny<ReportAggregatesQueryRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

        vm.SelectedKinds = new List<PostingKind> { PostingKind.Bank, PostingKind.Contact };
        vm.Interval = (int)ReportInterval.Month;
        vm.IncludeCategory = false;
        vm.Take = 24;

        await vm.ReloadAsync(start, TestContext.Current.CancellationToken);
        var byPeriod = vm.GetChartByPeriod();

        Assert.Equal(2, byPeriod.Count);
        Assert.Equal(150m, byPeriod[0].Sum);
        Assert.Equal(200m, byPeriod[1].Sum);
    }

    /// <summary>
    /// Verifies that enabling category grouping and previous/year comparison flips the corresponding
    /// column-visibility flags on, and that <c>GetTotals</c> correctly sums current, previous, and
    /// year-over-year amounts across all rows.
    /// </summary>
    [Fact]
    public async Task Totals_And_ColumnVisibility_Work()
    {
        var (vm, apiMock) = CreateVm();
        var start = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var points = new List<ReportAggregatePointDto>
        {
            new ReportAggregatePointDto(start, "Type:Bank", "Bank", null, 120m, null, null, 100m, 80m),
            new ReportAggregatePointDto(start, "Type:Contact", "Contact", null, 30m, null, null, 25m, 20m)
        };
        var result = new ReportAggregationResult(ReportInterval.Month, points, true, true, false);

        apiMock.Setup(a => a.Reports_QueryAggregatesAsync(It.IsAny<ReportAggregatesQueryRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

        vm.SelectedKinds = new List<PostingKind> { PostingKind.Bank, PostingKind.Contact };
        vm.IncludeCategory = true;
        vm.ComparePrevious = true;
        vm.CompareYear = true;
        vm.Interval = (int)ReportInterval.Month;

        await vm.ReloadAsync(start, TestContext.Current.CancellationToken);

        Assert.True(vm.ShowCategoryColumn);
        Assert.True(vm.ShowPreviousColumns);

        var t = vm.GetTotals();
        Assert.Equal(150m, t.Amount);
        Assert.Equal(125m, t.Prev);
        Assert.Equal(100m, t.Year);
    }

    /// <summary>
    /// Verifies that when the selection is eligible for projection comparison (single, security posting
    /// kind) and the server confirms it compared projections, the view model shows the projection column,
    /// marks itself as having compared projection, includes the projection sum in totals, and sent the
    /// projection-comparison flag in its query.
    /// </summary>
    [Fact]
    public async Task ProjectionColumn_IsVisibleAndTotalsProjection_WhenServerComparedProjection()
    {
        var (vm, apiMock) = CreateVm();
        var start = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var points = new List<ReportAggregatePointDto>
        {
            new ReportAggregatePointDto(start, "Security:abc", "ABC", null, 120m, 150m, null, null, null)
        };
        var result = new ReportAggregationResult(ReportInterval.Month, points, false, false, true);

        apiMock.Setup(a => a.Reports_QueryAggregatesAsync(It.IsAny<ReportAggregatesQueryRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

        vm.SelectedKinds = new List<PostingKind> { PostingKind.Security };
        vm.CompareProjection = true;
        vm.Interval = (int)ReportInterval.Month;

        await vm.ReloadAsync(start, TestContext.Current.CancellationToken);

        Assert.True(vm.CanCompareProjection);
        Assert.True(vm.ShowProjectionColumn);
        Assert.True(vm.ComparedProjection);
        Assert.Equal(150m, vm.GetTotals().Projection);
        apiMock.Verify(a => a.Reports_QueryAggregatesAsync(
            It.Is<ReportAggregatesQueryRequest>(r => r.CompareProjection),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Verifies that projection comparison is force-disabled when the selection includes a non-security
    /// posting kind (e.g. bank), even if the caller previously requested it - projections only make sense
    /// for securities, so the view model must not send a projection request the server cannot honor.
    /// </summary>
    [Fact]
    public async Task ReloadAsync_DisablesProjection_ForNonSecuritySelection()
    {
        var (vm, apiMock) = CreateVm();
        var result = new ReportAggregationResult(ReportInterval.Month, CreatePoints(1), false, false, false);

        apiMock.Setup(a => a.Reports_QueryAggregatesAsync(It.IsAny<ReportAggregatesQueryRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

        vm.SelectedKinds = new List<PostingKind> { PostingKind.Bank };
        vm.CompareProjection = true;
        vm.Interval = (int)ReportInterval.Month;

        await vm.ReloadAsync(DateTime.UtcNow, TestContext.Current.CancellationToken);

        Assert.False(vm.CanCompareProjection);
        Assert.False(vm.CompareProjection);
        Assert.False(vm.ShowProjectionColumn);
        apiMock.Verify(a => a.Reports_QueryAggregatesAsync(
            It.Is<ReportAggregatesQueryRequest>(r => !r.CompareProjection),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Verifies that projection comparison is force-disabled when multiple posting kinds are selected
    /// (even including security), since projections are only meaningful for a single, dedicated security
    /// selection and would otherwise mix incomparable figures.
    /// </summary>
    [Fact]
    public async Task ReloadAsync_DisablesProjection_ForMultiKindSelection()
    {
        var (vm, apiMock) = CreateVm();
        var result = new ReportAggregationResult(ReportInterval.Month, CreatePoints(1), false, false, false);

        apiMock.Setup(a => a.Reports_QueryAggregatesAsync(It.IsAny<ReportAggregatesQueryRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

        vm.SelectedKinds = new List<PostingKind> { PostingKind.Security, PostingKind.Bank };
        vm.CompareProjection = true;
        vm.Interval = (int)ReportInterval.Month;

        await vm.ReloadAsync(DateTime.UtcNow, TestContext.Current.CancellationToken);

        Assert.False(vm.CanCompareProjection);
        Assert.False(vm.CompareProjection);
        Assert.False(vm.ShowProjectionColumn);
        apiMock.Verify(a => a.Reports_QueryAggregatesAsync(
            It.Is<ReportAggregatesQueryRequest>(r => !r.CompareProjection),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Verifies that projection comparison is force-disabled for the "all history" interval, since a
    /// forward-looking projection is not meaningful once the report already spans the entire history.
    /// </summary>
    [Fact]
    public async Task ReloadAsync_DisablesProjection_ForAllHistory()
    {
        var (vm, apiMock) = CreateVm();
        var result = new ReportAggregationResult(ReportInterval.AllHistory, CreatePoints(1), false, false, false);

        apiMock.Setup(a => a.Reports_QueryAggregatesAsync(It.IsAny<ReportAggregatesQueryRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

        vm.SelectedKinds = new List<PostingKind> { PostingKind.Security };
        vm.CompareProjection = true;
        vm.Interval = (int)ReportInterval.AllHistory;

        await vm.ReloadAsync(DateTime.UtcNow, TestContext.Current.CancellationToken);

        Assert.False(vm.CanCompareProjection);
        Assert.False(vm.CompareProjection);
        Assert.False(vm.ShowProjectionColumn);
        apiMock.Verify(a => a.Reports_QueryAggregatesAsync(
            It.Is<ReportAggregatesQueryRequest>(r => !r.CompareProjection),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Verifies that <see cref="ReportDashboardViewModel.IsNegative"/> flags a point as negative even
    /// when its own amount is zero, as long as its comparison baselines (previous/year) are negative - the
    /// UI should still highlight a zero-amount row as a decline relative to a negative baseline.
    /// </summary>
    [Fact]
    public void IsNegative_MarksZeroWithNegativeBaselines()
    {
        var p = new ReportAggregatePointDto(DateTime.UtcNow, "x", "n", null, 0m, null, null, -10m, -5m);
        Assert.True(ReportDashboardViewModel.IsNegative(p));
    }

    /// <summary>
    /// Verifies that with category grouping enabled and multiple posting kinds selected, each type group
    /// (e.g. "Type:Bank") only reports its own children as belonging to it: account-level children stay under
    /// the bank type while category-level children stay under the contact type, without cross-contamination
    /// between the two groupings' child rows.
    /// </summary>
    [Fact]
    public async Task PerType_Children_When_IncludeCategory_Multi()
    {
        var (vm, apiMock) = CreateVm();
        var start = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var points = new List<ReportAggregatePointDto>
        {
            new ReportAggregatePointDto(start, "Type:Bank", "Bank", null, 100m, null, null, null, null),
            new ReportAggregatePointDto(start, "Type:Contact", "Contact", null, 50m, null, null, null, null),
            new ReportAggregatePointDto(start, "Account:acc1", "Checking", null, 60m, null, "Type:Bank", null, null),
            new ReportAggregatePointDto(start, "Category:Contact:Food", "Food", "Food", 50m, null, "Type:Contact", null, null)
        };
        var result = new ReportAggregationResult(ReportInterval.Month, points, false, false, false);

        apiMock.Setup(a => a.Reports_QueryAggregatesAsync(It.IsAny<ReportAggregatesQueryRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

        vm.SelectedKinds = new List<PostingKind> { PostingKind.Bank, PostingKind.Contact };
        vm.IncludeCategory = true;

        await vm.ReloadAsync(start, TestContext.Current.CancellationToken);

        Assert.True(vm.HasChildren("Type:Bank"));
        Assert.True(vm.HasChildren("Type:Contact"));
        var bankChildren = vm.GetChildRows("Type:Bank").ToList();
        Assert.All(bankChildren, c => Assert.False(c.GroupKey.StartsWith("Category:")));
        var contactChildren = vm.GetChildRows("Type:Contact").ToList();
        Assert.All(contactChildren, c => Assert.True(c.GroupKey.StartsWith("Category:")));
    }

    /// <summary>
    /// Duplicate of <see cref="IsNegative_MarksZeroWithNegativeBaselines"/> - same scenario (zero amount,
    /// negative previous/year baselines) asserting the point is flagged negative.
    /// </summary>
    [Fact]
    public void IsNegative_Works()
    {
        var p = new ReportAggregatePointDto(DateTime.UtcNow, "x", "n", null, 0m, null, null, -10m, -5m);
        Assert.True(ReportDashboardViewModel.IsNegative(p));
    }

    /// <summary>
    /// Verifies that the chosen <c>SavingsPlanPostingSource</c> is forwarded into the API payloads for
    /// both favorite creation and update, so the server receives the selection instead of silently
    /// falling back to <c>PlanPostings</c>.
    /// </summary>
    [Fact]
    public async Task SaveAndUpdateFavorite_ForwardSavingsPlanPostingSource()
    {
        var (vm, apiMock) = CreateVm();
        var fav = new ReportFavoriteDto(Guid.NewGuid(), "Fav", PostingKind.SavingsPlan, false, ReportInterval.Month, 24, false, false, false, true, true, DateTime.UtcNow, null, new[] { PostingKind.SavingsPlan }, null, false, SavingsPlanPostingSource.LinkedAccountPostings);

        apiMock.Setup(a => a.Reports_CreateFavoriteAsync(It.IsAny<ReportFavoriteCreateApiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(fav);
        apiMock.Setup(a => a.Reports_UpdateFavoriteAsync(It.IsAny<Guid>(), It.IsAny<ReportFavoriteUpdateApiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(fav);

        await vm.SaveFavoriteAsync("n", PostingKind.SavingsPlan, false, 0, 24, false, false, false, true, true, new[] { PostingKind.SavingsPlan }, null, ct: TestContext.Current.CancellationToken, savingsPlanPostingSource: SavingsPlanPostingSource.LinkedAccountPostings);
        apiMock.Verify(a => a.Reports_CreateFavoriteAsync(
            It.Is<ReportFavoriteCreateApiRequest>(r => r.SavingsPlanPostingSource == SavingsPlanPostingSource.LinkedAccountPostings),
            It.IsAny<CancellationToken>()), Times.Once);

        await vm.UpdateFavoriteAsync(Guid.NewGuid(), "n2", PostingKind.SavingsPlan, false, 0, 24, false, false, false, true, true, new[] { PostingKind.SavingsPlan }, null, ct: TestContext.Current.CancellationToken, savingsPlanPostingSource: SavingsPlanPostingSource.LinkedAccountPostings);
        apiMock.Verify(a => a.Reports_UpdateFavoriteAsync(
            It.IsAny<Guid>(),
            It.Is<ReportFavoriteUpdateApiRequest>(r => r.SavingsPlanPostingSource == SavingsPlanPostingSource.LinkedAccountPostings),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Verifies that the ribbon's "open filters" action stays enabled outside edit mode so a filter can be
    /// edited (not only cleared via "clear filters"), matching the requirement that filters are editable
    /// for the currently displayed report.
    /// </summary>
    [Fact]
    public void FiltersOpen_StaysEnabled_OutsideEditMode()
    {
        var (vm, _) = CreateVm();
        vm.EditMode = false;

        var localizerMock = new Mock<IStringLocalizer>();
        localizerMock.Setup(l => l[It.IsAny<string>()])
            .Returns((string key) => new LocalizedString(key, key));

        var actions = vm.GetRibbonRegisters(localizerMock.Object)!
            .SelectMany(r => r.Tabs ?? new List<UiRibbonTab>())
            .SelectMany(t => t.Items)
            .ToList();

        var filtersOpen = actions.First(a => a.Id == "FiltersOpen");
        Assert.False(filtersOpen.Disabled);
    }

    /// <summary>
    /// Verifies the filter-editing flow: opening the filter dialog copies the active selection into the
    /// temporary buffers, toggling an option changes only the temporary state, and applying writes the
    /// temporary selection back to the active filters and triggers a reload.
    /// </summary>
    [Fact]
    public async Task FilterDialog_TempChanges_ApplyToActiveFilters()
    {
        var (vm, apiMock) = CreateVm();
        var planId = Guid.NewGuid();
        vm.SelectedKinds = new List<PostingKind> { PostingKind.SavingsPlan };

        apiMock.Setup(a => a.SavingsPlans_ListAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SavingsPlanDto> { new() { Id = planId, Name = "Bausparen" } });
        apiMock.Setup(a => a.Reports_QueryAggregatesAsync(It.IsAny<ReportAggregatesQueryRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReportAggregationResult(ReportInterval.Month, Array.Empty<ReportAggregatePointDto>(), false, false, false));

        vm.OpenFilterDialog();
        Assert.True(vm.ShowFilterDialog);
        Assert.Empty(vm.TempSavings);
        Assert.Empty(vm.SelectedSavingsPlans);

        vm.ToggleTempForKind(PostingKind.SavingsPlan, planId, true);
        Assert.Contains(planId, vm.TempSavings);
        Assert.Empty(vm.SelectedSavingsPlans); // not applied yet

        await vm.ApplyTempAndReloadAsync(DateTime.UtcNow, TestContext.Current.CancellationToken);

        Assert.False(vm.ShowFilterDialog);
        Assert.Contains(planId, vm.SelectedSavingsPlans);
        apiMock.Verify(a => a.Reports_QueryAggregatesAsync(
            It.Is<ReportAggregatesQueryRequest>(r => r.Filters != null && r.Filters.SavingsPlanIds != null && r.Filters.SavingsPlanIds.Contains(planId)),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
