namespace FinanceManager.Tests.E2E;

/// <summary>
/// End-to-end verification of the card back-navigation fixes (issues #600/#602/#606/#607/#608):
/// the ribbon Back action on cards that send app-relative route payloads must navigate to the
/// actual origin card instead of a mangled legacy list route, and opening a savings-plan category
/// card must propagate the originating card as <c>?back=</c> target so its Back action returns
/// to the savings plan. Screenshots of the landed pages are captured for the visual acceptance.
/// </summary>
[Collection(PlaywrightCollection.CollectionName)]
public sealed class CardBackNavigationPlaywrightTests
{
    private readonly PlaywrightWebAppFixture _fixture;

    /// <summary>
    /// Initializes a new instance of the <see cref="CardBackNavigationPlaywrightTests"/> class.
    /// </summary>
    /// <param name="fixture">Shared Playwright web app fixture providing the browser and test server.</param>
    public CardBackNavigationPlaywrightTests(PlaywrightWebAppFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// Opening a budget rule card directly and clicking the ribbon Back action must navigate to
    /// the parent budget purpose card (<c>card/budget/purposes/{id}</c>) — previously the payload
    /// produced the invalid route <c>list/budget/card/budget/purposes/{id}</c>.
    /// </summary>
    [Fact]
    public async Task BudgetRuleCard_Back_ReturnsToPurposeCard()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        await LoginNewUserAsync(page, "card-back-rule");

        var contactId = await CreateContactAsync(page);
        var purposeId = await CreateBudgetPurposeAsync(page, contactId);
        var ruleId = await CreateBudgetRuleAsync(page, purposeId);

        await page.GotoAsync($"card/budget/rules/{ruleId}");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var backAction = page.Locator("button#Back");
        await backAction.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15000 });
        await backAction.ClickAsync();

        await page.WaitForURLAsync(url => url.Contains($"/card/budget/purposes/{purposeId}"), new() { Timeout = 15000 });
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await CaptureAsync(page, "s1-budget-rule-back-purpose-card");
    }

    /// <summary>
    /// From a savings-plan card the ribbon Category action must open the category card with the
    /// plan card URL propagated as <c>?back=</c>; the category card's Back action then returns to
    /// the originating savings-plan card instead of the category list.
    /// </summary>
    [Fact]
    public async Task SavingsPlanCard_OpenCategory_ReturnsToPlanCard()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        await LoginNewUserAsync(page, "card-back-plan");

        var categoryId = await CreateSavingsPlanCategoryAsync(page);
        var planId = await CreateSavingsPlanAsync(page, categoryId);

        await page.GotoAsync($"card/savings-plans/{planId}");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var categoryAction = page.Locator("button#Category");
        await categoryAction.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15000 });
        await categoryAction.ClickAsync();

        await page.WaitForURLAsync(url => url.Contains($"/card/savings-plans/categories/{categoryId}") && url.Contains("back="), new() { Timeout = 15000 });
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await CaptureAsync(page, "s1-savings-plan-category-card");

        var backAction = page.Locator("button#Back");
        await backAction.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15000 });
        await backAction.ClickAsync();

        await page.WaitForURLAsync(url => url.Contains($"/card/savings-plans/{planId}") && !url.Contains("/categories/"), new() { Timeout = 15000 });
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await CaptureAsync(page, "s1-savings-plan-card-returned");
    }

    /// <summary>
    /// The MCP audit card's Back action sends the app-relative payload <c>card/setup</c>; clicking
    /// it must land on the setup card — previously the payload was routed to the invalid list
    /// route <c>list/mcp-audit/card/setup</c>.
    /// </summary>
    [Fact]
    public async Task McpAuditCard_Back_ReturnsToSetupCard()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        await LoginNewUserAsync(page, "card-back-mcp");

        await page.GotoAsync("card/mcp-audit");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var backAction = page.Locator("button#Back");
        await backAction.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15000 });
        await backAction.ClickAsync();

        await page.WaitForURLAsync(url => url.Contains("/card/setup"), new() { Timeout = 15000 });
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await CaptureAsync(page, "s1-mcp-audit-back-setup-card");
    }

    private static async Task CaptureAsync(IPage page, string name)
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "acceptance-shots");
        Directory.CreateDirectory(dir);
        await page.ScreenshotAsync(new() { Path = Path.Combine(dir, $"{name}.png"), FullPage = true });
    }

    private static async Task<Guid> CreateContactAsync(IPage page)
    {
        var result = await BrowserApiHelper.PostJsonAsync<object, System.Text.Json.JsonElement>(page, "api/contacts",
            new { Name = "E2E Back Nav Contact", Type = 2, CategoryId = (Guid?)null, Description = (string?)null, IsPaymentIntermediary = (bool?)false });
        return result.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateBudgetPurposeAsync(IPage page, Guid contactId)
    {
        var result = await BrowserApiHelper.PostJsonAsync<object, System.Text.Json.JsonElement>(page, "api/budget/purposes",
            new { Name = "E2E Back Nav Purpose", SourceType = 0, SourceId = contactId, Description = (string?)null, BudgetCategoryId = (Guid?)null });
        return result.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateBudgetRuleAsync(IPage page, Guid purposeId)
    {
        var result = await BrowserApiHelper.PostJsonAsync<object, System.Text.Json.JsonElement>(page, "api/budget/rules",
            new { BudgetPurposeId = (Guid?)purposeId, BudgetCategoryId = (Guid?)null, Amount = 42m, Interval = 0, CustomIntervalMonths = (int?)null, StartDate = "2025-01-01", EndDate = (string?)null });
        return result.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateSavingsPlanCategoryAsync(IPage page)
    {
        var result = await BrowserApiHelper.PostJsonAsync<object, System.Text.Json.JsonElement>(page, "api/savings-plan-categories",
            new { Name = "E2E Back Nav Category" });
        return result.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateSavingsPlanAsync(IPage page, Guid categoryId)
    {
        var result = await BrowserApiHelper.PostJsonAsync<object, System.Text.Json.JsonElement>(page, "api/savings-plans",
            new { Name = "E2E Back Nav Plan", Type = 0, TargetAmount = (decimal?)null, TargetDate = (string?)null, Interval = (int?)null, CategoryId = (Guid?)categoryId, ContractNumber = (string?)null });
        return result.GetProperty("id").GetGuid();
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
