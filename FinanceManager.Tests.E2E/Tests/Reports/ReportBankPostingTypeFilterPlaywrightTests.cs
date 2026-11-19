using System.Text.RegularExpressions;
using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Aggregates;
using FinanceManager.Shared.Dtos.HomeKpi;
using FinanceManager.Shared.Dtos.Postings;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Tests.E2E;

/// <summary>
/// End-to-end tests for the bank posting-type filter in the report dashboard: selecting posting
/// types in the filter dialog restricts the projection's expected items to the chosen types, the
/// filter survives the save-as-favorite round-trip across a full reload, and a home-page KPI tile
/// backed by a filtered favorite renders the filtered total.
/// These drive a real browser + app instance; they are not run in unit CI.
/// </summary>
[Collection(PlaywrightCollection.CollectionName)]
public sealed class ReportBankPostingTypeFilterPlaywrightTests
{
    private readonly PlaywrightWebAppFixture _fixture;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportBankPostingTypeFilterPlaywrightTests"/> class.
    /// </summary>
    /// <param name="fixture">Shared Playwright web app fixture providing the browser and test server.</param>
    public ReportBankPostingTypeFilterPlaywrightTests(PlaywrightWebAppFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<(PlaywrightBrowserSession session, IPage page, Guid userId)> LoginAsync(string userPrefix)
    {
        var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        var auth = new AuthGateway(page, _fixture.BaseUrl);
        var seed = new TestUserSeeder(_fixture.DatabasePath);
        var username = $"{userPrefix}-{Guid.NewGuid():N}";
        const string password = "Secret123";
        var user = await seed.EnsureUserAsync(username, password);
        await auth.LoginAsync(username, password);
        return (session, page, user.Id);
    }

    private AppDbContext OpenDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={_fixture.DatabasePath}")
            .Options;
        return new AppDbContext(options);
    }

    private async Task RebuildAggregatesAsync(Guid userId)
    {
        await using var db = OpenDb();
        await new PostingAggregateService(db).RebuildForUserAsync(userId, (_, _) => { }, CancellationToken.None);
    }

    private static ILocator ProjectionCheckbox(IPage page)
    {
        // The projection option lives in the comparisons filter group; match it by its localized
        // label so the selector works in both UI languages.
        return page.Locator("label")
            .Filter(new LocatorFilterOptions { HasTextRegex = new Regex("Hochrechnung|Projection") })
            .Locator("input[type=checkbox]");
    }

    /// <summary>
    /// Seeds an account with prior-year postings of two different posting types so the projection
    /// would report an expectation for each type when unfiltered.
    /// </summary>
    /// <param name="page">Playwright page used for the API seed helper.</param>
    /// <param name="userId">Id of the seeded test user owning the account.</param>
    /// <returns>Name of the seeded account.</returns>
    private async Task<string> SeedAccountWithTypedPriorYearPostingsAsync(IPage page, Guid userId)
    {
        var accountName = $"E2E Konto {Guid.NewGuid():N}";
        var priorYearDate = new DateTime(DateTime.UtcNow.Year - 1, DateTime.UtcNow.Month, 10);
        await new AccountsApiSeedHelper(page, _fixture.DatabasePath, userId).CreateAccountWithBalanceAsync(
            accountName,
            $"DE507005000000{Random.Shared.Next(10000000, 99999999)}",
            AccountType.Giro,
            0m,
            "E2E Bank",
            new[]
            {
                new AccountsApiSeedHelper.SeededPosting(priorYearDate, 120m, PostingType: BankPostingType.Inflow),
                new AccountsApiSeedHelper.SeededPosting(priorYearDate.AddDays(2), -40m, PostingType: BankPostingType.Outflow)
            });

        await RebuildAggregatesAsync(userId);
        return accountName;
    }

    /// <summary>
    /// A bank posting-type selection in the filter dialog restricts the projection: the
    /// expected-items overlay only lists the expectation of the selected posting type, while the
    /// unselected type's prior-year posting produces no expectation.
    /// </summary>
    [Fact]
    public async Task BankPostingTypeFilter_ProjectionShowsOnlySelectedTypeExpectations()
    {
        var (session, page, userId) = await LoginAsync("banktype-proj-user");
        await using var _ = session;
        await SeedAccountWithTypedPriorYearPostingsAsync(page, userId);

        var gateway = new ReportDashboardGateway(page);
        await page.GotoAsync("/reports/dashboard?edit=true");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var dialog = await gateway.OpenFilterDialogAsync();
        await gateway.BankPostingTypeCheckbox(dialog, BankPostingType.Inflow).CheckAsync();
        await gateway.ApplyFilterDialogAsync(dialog);

        var checkbox = ProjectionCheckbox(page);
        await checkbox.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 30000 });
        await checkbox.CheckAsync();
        await page.Locator("#Reload").ClickAsync();

        var header = page.Locator("th.th-projection").First;
        await header.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 30000 });

        var infoButton = page.Locator(".projection-info-btn").First;
        await infoButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 30000 });
        await infoButton.ClickAsync();

        var overlay = page.Locator(".projection-detail-overlay");
        await overlay.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        var rows = overlay.Locator(".projection-detail-table tbody tr");
        (await rows.CountAsync()).Should().Be(1, "only the selected posting type may produce an expectation");
        (await overlay.InnerTextAsync()).Should().Contain("120");
    }

    /// <summary>
    /// A bank posting-type selection applied to the dashboard is stored inside a saved favorite:
    /// after a full reload of the favorite-scoped dashboard the reopened filter dialog shows the
    /// same posting-type checkbox checked again.
    /// </summary>
    [Fact]
    public async Task SaveFavorite_ShouldPersistBankPostingTypeFilter_AcrossReload()
    {
        var (session, page, userId) = await LoginAsync("banktype-fav-user");
        await using var _ = session;
        var gateway = new ReportDashboardGateway(page);

        await new AccountsApiSeedHelper(page, _fixture.DatabasePath, userId).CreateAccountWithBalanceAsync(
            $"E2E Konto {Guid.NewGuid():N}",
            $"DE507005000000{Random.Shared.Next(10000000, 99999999)}",
            AccountType.Giro,
            0m,
            "E2E Bank");

        var dialog = await gateway.OpenFilterDialogAsync();
        await gateway.BankPostingTypeCheckbox(dialog, BankPostingType.Inflow).CheckAsync();
        await gateway.ApplyFilterDialogAsync(dialog);

        var favoriteName = $"BankType Fav {Guid.NewGuid():N}";
        await gateway.SaveAsAsync(favoriteName);

        // full reload of the favorite-scoped dashboard
        await page.ReloadAsync();
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        dialog = await gateway.OpenFilterDialogAsync(edit: false);
        var inflow = gateway.BankPostingTypeCheckbox(dialog, BankPostingType.Inflow);
        await inflow.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        (await inflow.IsCheckedAsync()).Should().BeTrue("the saved favorite must restore the posting-type selection");
        (await gateway.BankPostingTypeCheckbox(dialog, BankPostingType.Outflow).IsCheckedAsync()).Should().BeFalse();
    }

    /// <summary>
    /// A home-page KPI tile backed by a favorite with a bank posting-type filter renders the
    /// filtered total only: postings of the unselected type do not contribute to the tile amount.
    /// </summary>
    [Fact]
    public async Task KpiTile_WithBankPostingTypeFavorite_ShowsFilteredTotal()
    {
        var (session, page, userId) = await LoginAsync("banktype-kpi-user");
        await using var _ = session;

        var month = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 5);
        await new AccountsApiSeedHelper(page, _fixture.DatabasePath, userId).CreateAccountWithBalanceAsync(
            $"E2E Konto {Guid.NewGuid():N}",
            $"DE507005000000{Random.Shared.Next(10000000, 99999999)}",
            AccountType.Giro,
            0m,
            "E2E Bank",
            new[]
            {
                new AccountsApiSeedHelper.SeededPosting(month, 100m, PostingType: BankPostingType.Inflow),
                new AccountsApiSeedHelper.SeededPosting(month.AddDays(1), -30m, PostingType: BankPostingType.Outflow)
            });
        await RebuildAggregatesAsync(userId);

        var favoriteName = $"BankType Kpi Fav {Guid.NewGuid():N}";
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
            Filters = new ReportFavoriteFiltersDto(null, null, null, null, null, null, null, null, null,
                BankPostingTypes: new[] { (int)BankPostingType.Inflow })
        });
        fav.Should().NotBeNull();

        var kpi = await BrowserApiHelper.PostJsonAsync<HomeKpiCreateRequest, HomeKpiDto>(page, "/api/home-kpis",
            new HomeKpiCreateRequest(HomeKpiKind.ReportFavorite, fav!.Id, null, "E2E KPI", HomeKpiDisplayMode.TotalOnly, 0));
        kpi.Should().NotBeNull();

        await page.GotoAsync("/");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        var tile = page.Locator(".kpi-tile").Filter(new LocatorFilterOptions { HasTextString = "E2E KPI" });
        await tile.WaitForAsync();

        // Assert the exact amount on the dedicated value element of the TotalOnly tile: the
        // unfiltered total (100 - 30 = 70) must not be displayed. N2 renders as "100,00" or
        // "100.00" depending on the resolved request culture.
        await Microsoft.Playwright.Assertions.Expect(tile.Locator(".kpi-amount"))
            .ToHaveTextAsync(new Regex(@"^100[.,]00$"));
    }
}
