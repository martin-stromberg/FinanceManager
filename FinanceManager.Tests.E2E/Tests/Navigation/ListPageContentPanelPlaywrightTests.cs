namespace FinanceManager.Tests.E2E;

/// <summary>
/// End-to-end verification that list pages wrap their whole content (ribbon, title, list) in the
/// same <c>.card-page-content</c> panel that card pages use — closing the visual gap where the
/// list content sat unframed below the ribbon and caused a layout jump between overview and
/// detail views.
/// </summary>
[Collection(PlaywrightCollection.CollectionName)]
public sealed class ListPageContentPanelPlaywrightTests
{
    private readonly PlaywrightWebAppFixture _fixture;

    /// <summary>
    /// Initializes a new instance of the <see cref="ListPageContentPanelPlaywrightTests"/> class.
    /// </summary>
    /// <param name="fixture">Shared Playwright web app fixture providing the browser and test server.</param>
    public ListPageContentPanelPlaywrightTests(PlaywrightWebAppFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// The accounts list renders its ribbon, title and list content inside a single
    /// <c>.card-page-content</c> container, and the panel's geometry matches the equivalent
    /// panel on a card page — no visual jump when navigating between overview and detail.
    /// </summary>
    [Fact]
    public async Task ListPage_RendersContentInsideCardPageContentPanel()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        var user = await LoginNewUserAsync(page, "list-content-panel");
        var seed = new AccountsApiSeedHelper(page, _fixture.DatabasePath, user.Id);
        var account = await seed.CreateAccountWithBalanceAsync(
            $"Panel Konto {Guid.NewGuid():N}",
            "DE44100100100000000099",
            AccountType.Giro,
            10m,
            "Panel Bank");

        await page.GotoAsync("/list/accounts");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var listPanel = page.Locator(".card-page-content").First;
        await listPanel.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15000 });

        // Ribbon, title and the search input of the list must live inside the panel.
        (await listPanel.Locator(".fm-ribbon").CountAsync())
            .Should().BeGreaterThan(0, "the ribbon must render inside the card-page-content panel");
        (await listPanel.Locator(".list-page-title").CountAsync())
            .Should().Be(1, "the list title must render inside the card-page-content panel");

        var searchInput = listPanel.Locator("input.list-filter-search");
        await searchInput.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15000 });
        var panelBox = await listPanel.BoundingBoxAsync();
        var searchBox = await searchInput.BoundingBoxAsync();
        panelBox.Should().NotBeNull();
        searchBox.Should().NotBeNull();
        searchBox!.X.Should().BeGreaterThanOrEqualTo(panelBox!.X - 0.5f,
            "the list content must not overflow the left edge of the panel");
        (searchBox.X + searchBox.Width).Should().BeLessThanOrEqualTo(panelBox.X + panelBox.Width + 0.5f,
            "the list content must not overflow the right edge of the panel");

        // The card page renders the same panel at the same horizontal position/width.
        await page.GotoAsync($"/card/accounts/{account.AccountId}");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        var cardPanel = page.Locator(".card-page-content").First;
        await cardPanel.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15000 });
        var cardBox = await cardPanel.BoundingBoxAsync();
        cardBox.Should().NotBeNull();
        Math.Abs(cardBox!.X - panelBox.X).Should().BeLessThan(2f,
            "list and card page panels must share the same horizontal offset to avoid a layout jump");
        Math.Abs(cardBox.Width - panelBox.Width).Should().BeLessThan(2f,
            "list and card page panels must share the same width to avoid a layout jump");
    }

    private async Task<FinanceManager.Domain.Users.User> LoginNewUserAsync(IPage page, string prefix)
    {
        var username = $"{prefix}-{Guid.NewGuid():N}";
        const string password = "Secret123";
        var auth = new AuthGateway(page, _fixture.BaseUrl);
        var seeder = new TestUserSeeder(_fixture.DatabasePath);
        var user = await seeder.EnsureUserAsync(username, password, timeZoneId: "UTC");
        await auth.LoginAsync(username, password);
        return user;
    }
}
