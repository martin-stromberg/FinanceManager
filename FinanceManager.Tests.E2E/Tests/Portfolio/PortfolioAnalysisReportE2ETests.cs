namespace FinanceManager.Tests.E2E;

/// <summary>
/// E2E tests for the portfolio analysis report page: loading the report, navigating via the
/// securities ribbon, editing the KPI tile configuration and per-user data isolation.
/// </summary>
[Collection(PlaywrightCollection.CollectionName)]
public sealed class PortfolioAnalysisReportE2ETests
{
    private readonly PlaywrightWebAppFixture _fixture;

    public PortfolioAnalysisReportE2ETests(PlaywrightWebAppFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// Happy path: an authenticated user navigates directly to the portfolio analysis report page
    /// and the tile grid renders (empty portfolio still renders the structure/performance/cashflow tiles).
    /// </summary>
    [Fact]
    public async Task LoadReportScenario_ShouldRenderTileGrid()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        var auth = new AuthGateway(page, _fixture.BaseUrl);
        var seeder = new TestUserSeeder(_fixture.DatabasePath);

        var username = $"portfolio-load-{Guid.NewGuid():N}";
        const string password = "Secret123";
        await seeder.EnsureUserAsync(username, password);
        await auth.LoginAsync(username, password);

        await page.GotoAsync("/portfolio/analysis-report");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var tileGrid = page.Locator(".portfolio-tile-grid");
        await tileGrid.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15_000 });

        (await page.Locator(".portfolio-tile").CountAsync()).Should().BeGreaterThan(0);
    }

    /// <summary>
    /// Verifies that clicking the "Depot-Bericht" ribbon button on the securities list navigates
    /// to the portfolio analysis report page.
    /// </summary>
    [Fact]
    public async Task RibbonNavigationScenario_ShouldNavigateFromSecuritiesList()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        var auth = new AuthGateway(page, _fixture.BaseUrl);
        var seeder = new TestUserSeeder(_fixture.DatabasePath);

        var username = $"portfolio-ribbon-{Guid.NewGuid():N}";
        const string password = "Secret123";
        await seeder.EnsureUserAsync(username, password);
        await auth.LoginAsync(username, password);

        await page.GotoAsync("/list/securities");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await page.Locator("button#PortfolioAnalysisReport").ClickAsync();

        await page.WaitForURLAsync(url => url.Contains("/portfolio/analysis-report"), new() { Timeout = 15_000 });
        page.Url.Should().Contain("/portfolio/analysis-report");
    }

    /// <summary>
    /// Verifies that entering edit mode, deactivating a tile and saving persists the configuration:
    /// the deactivated tile is no longer rendered in view mode after saving.
    /// </summary>
    [Fact]
    public async Task EditConfigurationScenario_ShouldHideDeactivatedTileAfterSave()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        var auth = new AuthGateway(page, _fixture.BaseUrl);
        var seeder = new TestUserSeeder(_fixture.DatabasePath);

        var username = $"portfolio-edit-{Guid.NewGuid():N}";
        const string password = "Secret123";
        await seeder.EnsureUserAsync(username, password);
        await auth.LoginAsync(username, password);

        await page.GotoAsync("/portfolio/analysis-report");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var editButton = page.Locator("button#Edit");
        await editButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15_000 });
        await editButton.ClickAsync();

        var editList = page.Locator(".portfolio-edit-list");
        await editList.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15_000 });

        // Deactivate the Cashflow tile checkbox (third item: Structure, Performance, Cashflow).
        var cashflowCheckbox = page.Locator(".portfolio-edit-item", new() { HasText = "Cashflow" }).Locator("input[type=checkbox]");
        await cashflowCheckbox.UncheckAsync();

        await page.Locator("button#SaveEdit").ClickAsync();
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var tileGrid = page.Locator(".portfolio-tile-grid");
        await tileGrid.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15_000 });

        (await page.Locator(".portfolio-tile-title", new() { HasText = "Cashflow" }).CountAsync()).Should().Be(0);
    }

    /// <summary>
    /// Verifies that two different users each see their own portfolio KPI configuration:
    /// a tile deactivation saved by user A must not affect user B's default configuration.
    /// </summary>
    [Fact]
    public async Task MultiUserIsolationScenario_ShouldNotLeakConfigurationBetweenUsers()
    {
        await using var sessionA = await _fixture.CreateSessionAsync();
        var pageA = sessionA.Page;
        var authA = new AuthGateway(pageA, _fixture.BaseUrl);
        var seeder = new TestUserSeeder(_fixture.DatabasePath);

        var userA = $"portfolio-isolation-a-{Guid.NewGuid():N}";
        var userB = $"portfolio-isolation-b-{Guid.NewGuid():N}";
        const string password = "Secret123";
        await seeder.EnsureUserAsync(userA, password);
        await seeder.EnsureUserAsync(userB, password);

        await authA.LoginAsync(userA, password);
        await pageA.GotoAsync("/portfolio/analysis-report");
        await pageA.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var editButtonA = pageA.Locator("button#Edit");
        await editButtonA.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15_000 });
        await editButtonA.ClickAsync();
        var editListA = pageA.Locator(".portfolio-edit-list");
        await editListA.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15_000 });
        var cashflowCheckboxA = pageA.Locator(".portfolio-edit-item", new() { HasText = "Cashflow" }).Locator("input[type=checkbox]");
        await cashflowCheckboxA.UncheckAsync();
        await pageA.Locator("button#SaveEdit").ClickAsync();
        await pageA.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await using var sessionB = await _fixture.CreateSessionAsync();
        var pageB = sessionB.Page;
        var authB = new AuthGateway(pageB, _fixture.BaseUrl);
        await authB.LoginAsync(userB, password);
        await pageB.GotoAsync("/portfolio/analysis-report");
        await pageB.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var tileGridB = pageB.Locator(".portfolio-tile-grid");
        await tileGridB.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15_000 });

        (await pageB.Locator(".portfolio-tile-title", new() { HasText = "Cashflow" }).CountAsync()).Should().BeGreaterThan(0);
    }
}
