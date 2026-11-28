namespace FinanceManager.Tests.E2E;

/// <summary>
/// End-to-end verification of the conditional back navigation on the budget report: reaching the
/// report via the budget purpose list's "Bericht" action carries a <c>?back=</c> parameter that
/// renders a "Navigieren" group with a large Back action returning to the list, while opening
/// the report directly renders neither a Back action nor an empty navigation group.
/// </summary>
[Collection(PlaywrightCollection.CollectionName)]
public sealed class BudgetReportBackNavigationPlaywrightTests
{
    private readonly PlaywrightWebAppFixture _fixture;

    /// <summary>
    /// Initializes a new instance of the <see cref="BudgetReportBackNavigationPlaywrightTests"/> class.
    /// </summary>
    /// <param name="fixture">Shared Playwright web app fixture providing the browser and test server.</param>
    public BudgetReportBackNavigationPlaywrightTests(PlaywrightWebAppFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// From <c>/list/budgets</c>, clicking the budget report ribbon action navigates to
    /// <c>/reports/budget?back=…</c> where the "Navigieren" group with a visible Back action
    /// appears first; clicking Back returns to the budget list.
    /// </summary>
    [Fact]
    public async Task BudgetReport_ViaBudgetList_ShowsBackAndReturnsToList()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        await LoginNewUserAsync(page, "budget-back-chain");

        await page.GotoAsync("/list/budgets");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var reportAction = page.Locator("button#BudgetReport");
        await reportAction.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15000 });
        await reportAction.ClickAsync();
        await page.WaitForURLAsync(url => url.Contains("/reports/budget"));

        var groups = page.Locator(".fm-ribbon-group");
        await groups.First.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15000 });

        (await groups.First.Locator("#Back").CountAsync())
            .Should().Be(1, "the first ribbon group must contain the Back navigation action when the report was opened from the list");

        await page.Locator("#Back").ClickAsync();
        await page.WaitForURLAsync(url => url.Contains("/list/budgets"));
    }

    /// <summary>
    /// Opening <c>/reports/budget</c> directly renders no Back action and no empty "Navigieren"
    /// group — the conditional group is only generated when a back context exists.
    /// </summary>
    [Fact]
    public async Task BudgetReport_DirectAccess_RendersNoBackAndNoNavigationGroup()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        await LoginNewUserAsync(page, "budget-back-direct");

        await page.GotoAsync("/reports/budget");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var groups = page.Locator(".fm-ribbon-group");
        await groups.First.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15000 });

        (await page.Locator("#Back").CountAsync())
            .Should().Be(0, "no Back action may render on direct access without a ?back= parameter");

        var footers = page.Locator(".fm-ribbon-group-footer");
        var count = await footers.CountAsync();
        for (var i = 0; i < count; i++)
        {
            var text = (await footers.Nth(i).InnerTextAsync()).Trim();
            text.Should().NotContain("Navigieren", "no empty navigation group may render on direct access");
            text.Should().NotContain("Navigation", "no empty navigation group may render on direct access");
        }
    }

    private async Task LoginNewUserAsync(IPage page, string prefix)
    {
        var username = $"{prefix}-{Guid.NewGuid():N}";
        const string password = "Secret123";
        var auth = new AuthGateway(page, _fixture.BaseUrl);
        var seeder = new TestUserSeeder(_fixture.DatabasePath);
        await seeder.EnsureUserAsync(username, password, timeZoneId: "UTC");
        await auth.LoginAsync(username, password);
    }
}
