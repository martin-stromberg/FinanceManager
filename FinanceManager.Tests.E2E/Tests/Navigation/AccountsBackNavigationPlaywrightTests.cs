namespace FinanceManager.Tests.E2E;

/// <summary>
/// End-to-end verification of the conditional back-navigation ribbon group on the bank account
/// list: opened directly (no <c>?back=</c> query) neither a Back button nor an empty
/// "Navigieren" group renders; opened with <c>?back=</c> the "Navigieren" group appears first
/// with a large Back action that navigates to the supplied target.
/// </summary>
[Collection(PlaywrightCollection.CollectionName)]
public sealed class AccountsBackNavigationPlaywrightTests
{
    private readonly PlaywrightWebAppFixture _fixture;

    /// <summary>
    /// Initializes a new instance of the <see cref="AccountsBackNavigationPlaywrightTests"/> class.
    /// </summary>
    /// <param name="fixture">Shared Playwright web app fixture providing the browser and test server.</param>
    public AccountsBackNavigationPlaywrightTests(PlaywrightWebAppFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// Opening <c>/list/accounts</c> without a back parameter renders only the categorized
    /// groups (Verwalten, Filter) — no Back action and no leftover empty "Navigieren" group frame.
    /// </summary>
    [Fact]
    public async Task AccountsList_WithoutBackParam_RendersNoNavigationGroup()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        await LoginNewUserAsync(page, "accounts-back-none");

        await page.GotoAsync("/list/accounts");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var groups = page.Locator(".fm-ribbon-group");
        await groups.First.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15000 });

        (await page.Locator("#Back").CountAsync())
            .Should().Be(0, "no Back action may render without a ?back= parameter");

        var footerTexts = await GetGroupFooterTextsAsync(page);
        footerTexts.Should().NotContain(
            t => t.Contains("Navigieren", StringComparison.OrdinalIgnoreCase) || t.Contains("Navigation", StringComparison.OrdinalIgnoreCase),
            "the navigation group must not leave an empty visible group frame");
    }

    /// <summary>
    /// Opening <c>/list/accounts?back=…</c> renders the "Navigieren" group first with a large,
    /// visible Back action; clicking it navigates to the supplied target URL.
    /// </summary>
    [Fact]
    public async Task AccountsList_WithBackParam_RendersBackActionAndNavigatesToTarget()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        await LoginNewUserAsync(page, "accounts-back-with");

        var backTarget = "/list/contacts";
        await page.GotoAsync($"/list/accounts?back={Uri.EscapeDataString(backTarget)}");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var groups = page.Locator(".fm-ribbon-group");
        await groups.First.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15000 });

        (await groups.First.Locator("#Back").CountAsync())
            .Should().Be(1, "the first ribbon group must contain the Back navigation action when ?back= is set");

        await page.Locator("#Back").ClickAsync();
        await page.WaitForURLAsync(url => url.Contains(backTarget));
    }

    private static async Task<List<string>> GetGroupFooterTextsAsync(IPage page)
    {
        var footers = page.Locator(".fm-ribbon-group-footer");
        var texts = new List<string>();
        var count = await footers.CountAsync();
        for (var i = 0; i < count; i++)
        {
            texts.Add((await footers.Nth(i).InnerTextAsync()).Trim());
        }

        return texts;
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
