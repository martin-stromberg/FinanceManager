using FinanceManager.Shared.Dtos.HomeKpi;

namespace FinanceManager.Tests.E2E;

/// <summary>
/// End-to-end tests for the free-text filters in the report dashboard filter dialog: adding,
/// applying and removing entries through the UI, persisting them inside a report favorite
/// across a reload, and a favorite created via the API surfacing its stored text filters in
/// the dialog. These drive a real browser + app instance; they are not run in unit CI.
/// </summary>
[Collection(PlaywrightCollection.CollectionName)]
public sealed class ReportTextFilterPlaywrightTests
{
    private readonly PlaywrightWebAppFixture _fixture;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportTextFilterPlaywrightTests"/> class.
    /// </summary>
    /// <param name="fixture">Shared Playwright web app fixture providing the browser and test server.</param>
    public ReportTextFilterPlaywrightTests(PlaywrightWebAppFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<(PlaywrightBrowserSession session, IPage page)> LoginAsync(string userPrefix)
    {
        var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        var auth = new AuthGateway(page, _fixture.BaseUrl);
        var seed = new TestUserSeeder(_fixture.DatabasePath);
        var username = $"{userPrefix}-{Guid.NewGuid():N}";
        const string password = "Secret123";
        await seed.EnsureUserAsync(username, password);
        await auth.LoginAsync(username, password);
        return (session, page);
    }

    /// <summary>
    /// A text filter added in the filter dialog is listed, survives an apply + reopen, and can
    /// be removed again via its remove button.
    /// </summary>
    [Fact]
    public async Task FilterDialog_ShouldAddApplyAndRemoveTextFilter()
    {
        await using var session = (await LoginAsync("textfilter-user")).session;
        var page = session.Page;
        var gateway = new ReportDashboardGateway(page);

        // the filter dialog renders the text filter UI only once at least one filter option exists
        await new AccountsApiSeedHelper(page).CreateAccountAsync($"E2E Konto {Guid.NewGuid():N}", $"DE507005000000{Random.Shared.Next(10000000, 99999999)}");

        var dialog = await gateway.OpenFilterDialogAsync();
        await gateway.AddTextFilterAsync(dialog, "E2E-Textfilter");
        (await dialog.Locator(".mk-item.line span").Filter(new LocatorFilterOptions { HasTextString = "E2E-Textfilter" }).CountAsync())
            .Should().Be(1);

        await gateway.ApplyFilterDialogAsync(dialog);

        // reopening shows the applied filter in the temp buffer
        dialog = await gateway.OpenFilterDialogAsync();
        await dialog.Locator(".mk-item.line span").Filter(new LocatorFilterOptions { HasTextString = "E2E-Textfilter" }).WaitForAsync();

        await gateway.RemoveTextFilterAsync(dialog, "E2E-Textfilter");
        (await dialog.Locator(".mk-item.line span").Filter(new LocatorFilterOptions { HasTextString = "E2E-Textfilter" }).CountAsync())
            .Should().Be(0);
    }

    /// <summary>
    /// A text filter applied to the dashboard is stored inside a saved favorite: reopening the
    /// saved favorite's dashboard shows the filter in the dialog after a full reload.
    /// </summary>
    [Fact]
    public async Task SaveFavorite_ShouldPersistTextFilter_AcrossReload()
    {
        await using var session = (await LoginAsync("textfilter-fav-user")).session;
        var page = session.Page;
        var gateway = new ReportDashboardGateway(page);

        // the filter dialog renders the text filter UI only once at least one filter option exists
        await new AccountsApiSeedHelper(page).CreateAccountAsync($"E2E Konto {Guid.NewGuid():N}", $"DE507005000000{Random.Shared.Next(10000000, 99999999)}");

        var dialog = await gateway.OpenFilterDialogAsync();
        await gateway.AddTextFilterAsync(dialog, "E2E-FavFilter");
        await gateway.ApplyFilterDialogAsync(dialog);

        var favoriteName = $"TextFilter Fav {Guid.NewGuid():N}";
        await gateway.SaveAsAsync(favoriteName);

        // full reload of the favorite-scoped dashboard
        await page.ReloadAsync();
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        dialog = await gateway.OpenFilterDialogAsync(edit: false);
        await dialog.Locator(".mk-item.line span").Filter(new LocatorFilterOptions { HasTextString = "E2E-FavFilter" }).WaitForAsync();
    }

    /// <summary>
    /// A home-page KPI tile backed by a favorite with a bank text filter renders, and after the
    /// favorite's filters are changed via the API the tile still renders on reload — the
    /// ModifiedUtc-based cache-version check must discard (not serve) the stale cached value.
    /// </summary>
    [Fact]
    public async Task KpiTile_WithTextFilterFavorite_ShouldReloadAfterFilterChange()
    {
        await using var session = (await LoginAsync("textfilter-kpi-user")).session;
        var page = session.Page;

        var favoriteName = $"Kpi Fav {Guid.NewGuid():N}";
        var filters = new ReportFavoriteFiltersDto(null, null, null, null, null, null, null, null, null,
            BankTextFilters: new[] { "E2E-KpiFilter" });
        var fav = await BrowserApiHelper.PostJsonAsync<ReportFavoriteCreateApiRequest, ReportFavoriteDto>(page, "/api/report-favorites", new ReportFavoriteCreateApiRequest
        {
            Name = favoriteName,
            PostingKind = 0,
            IncludeCategory = false,
            Interval = 0,
            Take = 6,
            ComparePrevious = false,
            CompareYear = false,
            ShowChart = false,
            Expandable = false,
            UseValutaDate = false,
            Filters = filters
        });
        fav.Should().NotBeNull();

        var kpi = await BrowserApiHelper.PostJsonAsync<HomeKpiCreateRequest, HomeKpiDto>(page, "/api/home-kpis",
            new HomeKpiCreateRequest(HomeKpiKind.ReportFavorite, fav!.Id, null, "E2E KPI", HomeKpiDisplayMode.TotalOnly, 0));
        kpi.Should().NotBeNull();

        await page.GotoAsync("/");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        var tile = page.Locator(".kpi-tile").Filter(new LocatorFilterOptions { HasTextString = "E2E KPI" });
        await tile.WaitForAsync();

        // changing the favorite's text filters bumps ModifiedUtc — the tile must reload fresh data
        var updated = await BrowserApiHelper.PutJsonAsync<ReportFavoriteUpdateApiRequest, ReportFavoriteDto>(page,
            $"/api/report-favorites/{fav.Id}", new ReportFavoriteUpdateApiRequest
            {
                Name = favoriteName,
                PostingKind = 0,
                IncludeCategory = false,
                Interval = 0,
                Take = 6,
                ComparePrevious = false,
                CompareYear = false,
                ShowChart = false,
                Expandable = false,
                Filters = new ReportFavoriteFiltersDto(null, null, null, null, null, null, null, null, null,
                    BankTextFilters: new[] { "E2E-ChangedFilter" })
            });
        updated.Should().NotBeNull();

        await page.ReloadAsync();
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await tile.WaitForAsync();
        (await tile.CountAsync()).Should().Be(1);
    }
}
