using FinanceManager.Domain.Portfolio;
using FinanceManager.Domain.Postings;
using FinanceManager.Domain.Securities;
using FinanceManager.Infrastructure;
using FinanceManager.Shared.Dtos.Postings;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Tests.E2E;

/// <summary>
/// E2E tests for the portfolio analysis report page: loading the report, navigating via the
/// securities ribbon, editing the KPI tile configuration and per-user data isolation.
/// </summary>
[Collection(PlaywrightCollection.CollectionName)]
public sealed class PortfolioAnalysisReportE2ETests
{
    private readonly PlaywrightWebAppFixture _fixture;

    /// <summary>
    /// Initializes a new instance of the <see cref="PortfolioAnalysisReportE2ETests"/> class.
    /// </summary>
    /// <param name="fixture">Shared Playwright web app fixture providing the browser and test server.</param>
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

    /// <summary>
    /// With seeded security postings and prices spanning multiple years, the depot development tile
    /// renders its combined bar/line chart (with tooltips) and the yearly table.
    /// </summary>
    [Fact]
    public async Task DepotDevelopmentScenario_ShouldRenderChartAndTable()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        var auth = new AuthGateway(page, _fixture.BaseUrl);
        var seeder = new TestUserSeeder(_fixture.DatabasePath);

        var username = $"portfolio-depot-{Guid.NewGuid():N}";
        const string password = "Secret123";
        var user = await seeder.EnsureUserAsync(username, password);
        await SeedDepotHistoryAsync(user.Id, DateTime.UtcNow.Year - 2);
        await auth.LoginAsync(username, password);

        await page.GotoAsync("/portfolio/analysis-report");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var yieldBasisSelect = page.Locator("select#dev-yield-basis");
        await yieldBasisSelect.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15_000 });

        (await page.Locator("svg.portfolio-bar-line-chart-svg").CountAsync()).Should().Be(1);
        (await page.Locator("rect.portfolio-bar-line-chart-bar").CountAsync()).Should().BeGreaterThan(0);
        (await page.Locator("g.portfolio-bar-line-chart-point title").CountAsync()).Should().BeGreaterThan(0);
        (await page.Locator("table.portfolio-dev-table tbody tr").CountAsync()).Should().BeGreaterThan(0);
    }

    /// <summary>
    /// Switching the yield-basis dropdown to invested capital swaps the combined chart for the
    /// grouped bar chart and updates the basis footnote client-side without reloading the page.
    /// </summary>
    [Fact]
    public async Task DepotDevelopmentScenario_SwitchingYieldBasisUpdatesTable()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        var auth = new AuthGateway(page, _fixture.BaseUrl);
        var seeder = new TestUserSeeder(_fixture.DatabasePath);

        var username = $"portfolio-depot-basis-{Guid.NewGuid():N}";
        const string password = "Secret123";
        var user = await seeder.EnsureUserAsync(username, password);
        await SeedDepotHistoryAsync(user.Id, DateTime.UtcNow.Year - 2);
        await auth.LoginAsync(username, password);

        await page.GotoAsync("/portfolio/analysis-report");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var footnote = page.Locator(".portfolio-tile:has(.portfolio-dev-filters) .portfolio-chart-footnote");
        await footnote.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15_000 });
        var footnoteBefore = await footnote.TextContentAsync();

        await page.Locator("select#dev-yield-basis").SelectOptionAsync("InvestedCapital");

        await Microsoft.Playwright.Assertions.Expect(footnote).Not.ToHaveTextAsync(footnoteBefore!);
        await Microsoft.Playwright.Assertions.Expect(page.Locator("svg.portfolio-grouped-bar-chart-svg")).ToHaveCountAsync(1);
        (await page.Locator("svg.portfolio-bar-line-chart-svg").CountAsync()).Should().Be(0);
        (await page.Locator("table.portfolio-dev-table thead th").CountAsync()).Should().Be(4);
    }

    /// <summary>
    /// The chosen yield basis and period filter are stored in the browser's local storage and
    /// restored on the next visit of the report page.
    /// </summary>
    [Fact]
    public async Task DepotDevelopmentScenario_FilterChoicesPersistAcrossReload()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        var auth = new AuthGateway(page, _fixture.BaseUrl);
        var seeder = new TestUserSeeder(_fixture.DatabasePath);

        var username = $"portfolio-depot-persist-{Guid.NewGuid():N}";
        const string password = "Secret123";
        var user = await seeder.EnsureUserAsync(username, password);
        await SeedDepotHistoryAsync(user.Id, DateTime.UtcNow.Year - 6);
        await auth.LoginAsync(username, password);

        await page.GotoAsync("/portfolio/analysis-report");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var yieldBasisSelect = page.Locator("select#dev-yield-basis");
        await yieldBasisSelect.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15_000 });
        await yieldBasisSelect.SelectOptionAsync("InvestedCapital");
        await page.Locator("select#dev-period-filter").SelectOptionAsync("Last5Years");

        await page.ReloadAsync();
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var restoredBasis = page.Locator("select#dev-yield-basis");
        await restoredBasis.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15_000 });
        await Microsoft.Playwright.Assertions.Expect(restoredBasis).ToHaveValueAsync("InvestedCapital");
        await Microsoft.Playwright.Assertions.Expect(page.Locator("select#dev-period-filter")).ToHaveValueAsync("Last5Years");
        (await page.Locator("table.portfolio-dev-table tbody tr").CountAsync()).Should().Be(5);
    }

    /// <summary>
    /// The period filter dropdown limits the rendered year rows to the selected number of
    /// trailing years.
    /// </summary>
    [Fact]
    public async Task DepotDevelopmentScenario_PeriodFilterLimitsRows()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        var auth = new AuthGateway(page, _fixture.BaseUrl);
        var seeder = new TestUserSeeder(_fixture.DatabasePath);

        var username = $"portfolio-depot-period-{Guid.NewGuid():N}";
        const string password = "Secret123";
        var user = await seeder.EnsureUserAsync(username, password);
        await SeedDepotHistoryAsync(user.Id, DateTime.UtcNow.Year - 6);
        await auth.LoginAsync(username, password);

        await page.GotoAsync("/portfolio/analysis-report");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var rows = page.Locator("table.portfolio-dev-table tbody tr");
        await rows.First.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15_000 });
        (await rows.CountAsync()).Should().Be(7);

        await page.Locator("select#dev-period-filter").SelectOptionAsync("Last5Years");
        await Microsoft.Playwright.Assertions.Expect(rows).ToHaveCountAsync(5);
    }

    /// <summary>
    /// A user who saved a KPI configuration before the depot development tile existed still gets the
    /// new tile: the read endpoint merges missing tile ids into the stored order and activates
    /// default-active tiles.
    /// </summary>
    [Fact]
    public async Task DepotDevelopmentScenario_MergedIntoSavedConfiguration()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        var auth = new AuthGateway(page, _fixture.BaseUrl);
        var seeder = new TestUserSeeder(_fixture.DatabasePath);

        var username = $"portfolio-depot-merge-{Guid.NewGuid():N}";
        const string password = "Secret123";
        var user = await seeder.EnsureUserAsync(username, password);
        await SeedSavedKpiConfigurationAsync(user.Id, "[0,1,2]", "[0,1,2]");
        await auth.LoginAsync(username, password);

        await page.GotoAsync("/portfolio/analysis-report");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var tile = page.Locator(".portfolio-tile:has(.portfolio-dev-filters)");
        await tile.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15_000 });
    }

    /// <summary>
    /// Without any security postings the depot development tile renders its empty-state text
    /// instead of the chart and table.
    /// </summary>
    [Fact]
    public async Task DepotDevelopmentScenario_NoPostings_ShowsEmptyState()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        var auth = new AuthGateway(page, _fixture.BaseUrl);
        var seeder = new TestUserSeeder(_fixture.DatabasePath);

        var username = $"portfolio-depot-empty-{Guid.NewGuid():N}";
        const string password = "Secret123";
        await seeder.EnsureUserAsync(username, password);
        await auth.LoginAsync(username, password);

        await page.GotoAsync("/portfolio/analysis-report");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var empty = page.Locator(".portfolio-tile:has(.portfolio-dev-filters) .mini-bar-chart-empty");
        await empty.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15_000 });
        (await page.Locator("table.portfolio-dev-table").CountAsync()).Should().Be(0);
    }

    /// <summary>
    /// The depot development tile appears in the edit list and can be deactivated; after saving,
    /// it is no longer rendered in view mode.
    /// </summary>
    [Fact]
    public async Task DepotDevelopmentScenario_CanBeDeactivatedInEditMode()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        var auth = new AuthGateway(page, _fixture.BaseUrl);
        var seeder = new TestUserSeeder(_fixture.DatabasePath);

        var username = $"portfolio-depot-edit-{Guid.NewGuid():N}";
        const string password = "Secret123";
        await seeder.EnsureUserAsync(username, password);
        await auth.LoginAsync(username, password);

        await page.GotoAsync("/portfolio/analysis-report");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await page.Locator("button#Edit").ClickAsync();
        var editList = page.Locator(".portfolio-edit-list");
        await editList.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15_000 });

        var depotCheckbox = page.Locator(".portfolio-edit-item", new() { HasTextRegex = new System.Text.RegularExpressions.Regex("Depotentwicklung|Depot development") }).Locator("input[type=checkbox]");
        await depotCheckbox.UncheckAsync();

        await page.Locator("button#SaveEdit").ClickAsync();
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var tileGrid = page.Locator(".portfolio-tile-grid");
        await tileGrid.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15_000 });
        (await page.Locator(".portfolio-dev-filters").CountAsync()).Should().Be(0);
    }

    /// <summary>Seeds one security with yearly buys, dividends and December prices for the given year range.</summary>
    /// <param name="userId">Owner user id of the seeded security.</param>
    /// <param name="firstYear">First calendar year to seed postings for; years up to the previous year are included.</param>
    private async Task SeedDepotHistoryAsync(Guid userId, int firstYear)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={_fixture.DatabasePath}")
            .Options;
        await using var db = new AppDbContext(options);

        var security = new Security(userId, "Depot Dev ETF", $"DEP{Guid.NewGuid():N}"[..8].ToUpperInvariant(), null, null, "EUR", null);
        db.Securities.Add(security);

        var currentYear = DateTime.UtcNow.Year;
        for (var year = firstYear; year < currentYear; year++)
        {
            db.Postings.Add(new Posting(Guid.NewGuid(), PostingKind.Security, null, null, null, security.Id,
                new DateTime(year, 3, 15), -1000m, null, null, null, SecurityPostingSubType.Buy, 10m));
            db.Postings.Add(new Posting(Guid.NewGuid(), PostingKind.Security, null, null, null, security.Id,
                new DateTime(year, 6, 15), 60m, null, null, null, SecurityPostingSubType.Dividend, null));
            db.SecurityPrices.Add(new SecurityPrice(security.Id, new DateTime(year, 12, 20), 110m));
        }

        db.SecurityPrices.Add(new SecurityPrice(security.Id, DateTime.UtcNow.Date, 120m));
        await db.SaveChangesAsync();
    }

    /// <summary>Seeds a saved KPI tile configuration (JSON tile-id arrays) directly into the E2E database.</summary>
    /// <param name="userId">Owner user id of the configuration.</param>
    /// <param name="activeJson">JSON array of active tile ids.</param>
    /// <param name="orderJson">JSON array of tile ids in display order.</param>
    private async Task SeedSavedKpiConfigurationAsync(Guid userId, string activeJson, string orderJson)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={_fixture.DatabasePath}")
            .Options;
        await using var db = new AppDbContext(options);
        db.PortfolioKpiConfigurations.Add(new PortfolioKpiConfiguration(userId, activeJson, orderJson));
        await db.SaveChangesAsync();
    }
}
