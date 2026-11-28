namespace FinanceManager.Tests.E2E;

/// <summary>
/// End-to-end verification of the list filter fixes (issues #599/#601/#604): the generic list
/// page's search box must actually narrow the savings-plan, securities and user lists, the
/// savings-plan date range must filter on due dates, and the meaningless date range must be
/// hidden on the securities and users lists. Screenshots of the filtered lists are captured
/// for the visual acceptance.
/// </summary>
[Collection(PlaywrightCollection.CollectionName)]
public sealed class ListFilterPlaywrightTests
{
    private readonly PlaywrightWebAppFixture _fixture;

    /// <summary>
    /// Initializes a new instance of the <see cref="ListFilterPlaywrightTests"/> class.
    /// </summary>
    /// <param name="fixture">Shared Playwright web app fixture providing the browser and test server.</param>
    public ListFilterPlaywrightTests(PlaywrightWebAppFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// Typing a search term on the savings-plan overview must reduce the visible rows to the
    /// matching plan — previously the search input reloaded the list but showed all plans.
    /// </summary>
    [Fact]
    public async Task SavingsPlansList_Search_FiltersVisiblePlans()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        await LoginNewUserAsync(page, "list-filter-plan");

        var token = Guid.NewGuid().ToString("N")[..8];
        await CreateSavingsPlanAsync(page, $"E2E {token} AlphaPlan", null);
        await CreateSavingsPlanAsync(page, $"E2E {token} BetaPlan", null);

        await page.GotoAsync("list/savings-plans");
        await WaitForRowAsync(page, "AlphaPlan");
        await WaitForRowAsync(page, "BetaPlan");

        await ApplySearchAsync(page, "alphaplan", "BetaPlan", 0);
        await WaitForRowAsync(page, "AlphaPlan");
        await CaptureAsync(page, "s2-savings-plans-search-filtered");

        await ApplySearchAsync(page, string.Empty, token, 2);
    }

    /// <summary>
    /// Setting a date range on the savings-plan overview must keep only plans whose target
    /// date falls inside the inclusive range.
    /// </summary>
    [Fact]
    public async Task SavingsPlansList_DateRange_FiltersVisiblePlans()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        await LoginNewUserAsync(page, "list-filter-planrange");

        await CreateSavingsPlanAsync(page, "E2E InRange Plan", "2026-06-15");
        await CreateSavingsPlanAsync(page, "E2E OutRange Plan", "2026-09-15");

        await page.GotoAsync("list/savings-plans");
        await WaitForRowAsync(page, "InRange Plan");
        await WaitForRowAsync(page, "OutRange Plan");

        await ApplyRangeAsync(page, "2026-06-01", "2026-06-30", "OutRange Plan", 0);
        await WaitForRowAsync(page, "InRange Plan");
        await CaptureAsync(page, "s2-savings-plans-range-filtered");
    }

    /// <summary>
    /// The securities overview must no longer render the (meaningless) date range filter,
    /// and its search must narrow the list to matching securities.
    /// </summary>
    [Fact]
    public async Task SecuritiesList_Search_FiltersAndRangeHidden()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        await LoginNewUserAsync(page, "list-filter-sec");

        var token = Guid.NewGuid().ToString("N")[..8];
        await CreateSecurityAsync(page, $"E2E {token} AlphaSec", $"SEC-{token}-A");
        await CreateSecurityAsync(page, $"E2E {token} BetaSec", $"SEC-{token}-B");

        await page.GotoAsync("list/securities");
        await WaitForRowAsync(page, "AlphaSec");
        await WaitForRowAsync(page, "BetaSec");

        var rangeCount = await page.Locator(".list-filter-range").CountAsync();
        Assert.Equal(0, rangeCount);

        await ApplySearchAsync(page, "alphasec", "BetaSec", 0);
        await WaitForRowAsync(page, "AlphaSec");
        await CaptureAsync(page, "s2-securities-search-filtered");
    }

    /// <summary>
    /// The admin users overview must no longer render the date range filter, and its search
    /// must narrow the list by username.
    /// </summary>
    [Fact]
    public async Task UsersList_Search_FiltersAndRangeHidden()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        var adminName = await LoginNewUserAsync(page, "list-filter-admin", isAdmin: true);

        await page.GotoAsync("list/users");
        await WaitForRowAsync(page, adminName);

        var rangeCount = await page.Locator(".list-filter-range").CountAsync();
        Assert.Equal(0, rangeCount);

        // verify against the shared prefix: unfiltered shows every "list-filter-" user,
        // filtered keeps only the admin — counting adminName itself could not tell the two apart
        await ApplySearchAsync(page, adminName, "list-filter-", 1);
        await CaptureAsync(page, "s2-users-search-filtered");
    }

    /// <summary>
    /// Types a search term and waits until the visible rows match the expectation. Right after
    /// navigation the Blazor circuit can still be starting: input events may get lost and a
    /// provider recreation can overwrite the filtered state with a late unfiltered load, so the
    /// fill is retried until the expectation holds stably.
    /// </summary>
    /// <param name="page">The Playwright page under test.</param>
    /// <param name="value">Search term to type into the search input.</param>
    /// <param name="verifyText">Text whose visible row count is verified after the search applies.</param>
    /// <param name="expectedCount">Expected number of visible rows containing <paramref name="verifyText"/>.</param>
    private static async Task ApplySearchAsync(IPage page, string value, string verifyText, int expectedCount)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            await page.Locator("input.list-filter-search").FillAsync(value);
            if (await TryWaitForStableRowCountAsync(page, verifyText, expectedCount, 10000))
            {
                return;
            }
        }
        throw new TimeoutException($"search '{value}' never produced the expected stable row count {expectedCount} for '{verifyText}'");
    }

    /// <summary>
    /// Sets both date range inputs and waits until the visible rows match the expectation.
    /// Retries clear the inputs first so the change event fires again on each attempt.
    /// </summary>
    /// <param name="page">The Playwright page under test.</param>
    /// <param name="from">Value for the range-from date input.</param>
    /// <param name="to">Value for the range-to date input.</param>
    /// <param name="verifyText">Text whose visible row count is verified after the range applies.</param>
    /// <param name="expectedCount">Expected number of visible rows containing <paramref name="verifyText"/>.</param>
    private static async Task ApplyRangeAsync(IPage page, string from, string to, string verifyText, int expectedCount)
    {
        var rangeInputs = page.Locator(".list-filter-range input");
        for (var attempt = 0; attempt < 5; attempt++)
        {
            if (attempt > 0)
            {
                await rangeInputs.Nth(0).FillAsync(string.Empty);
                await rangeInputs.Nth(0).BlurAsync();
                await rangeInputs.Nth(1).FillAsync(string.Empty);
                await rangeInputs.Nth(1).BlurAsync();
            }
            await rangeInputs.Nth(0).FillAsync(from);
            await rangeInputs.Nth(0).BlurAsync();
            await rangeInputs.Nth(1).FillAsync(to);
            await rangeInputs.Nth(1).BlurAsync();
            if (await TryWaitForStableRowCountAsync(page, verifyText, expectedCount, 10000))
            {
                return;
            }
        }
        throw new TimeoutException($"range '{from}'..'{to}' never produced the expected stable row count {expectedCount} for '{verifyText}'");
    }

    private static ILocator RowContaining(IPage page, string text)
        => page.Locator(".generic-list-mobile-card:visible, tbody tr:visible").Filter(new() { HasText = text });

    private static async Task WaitForRowAsync(IPage page, string text)
        => await RowContaining(page, text).First.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 30000 });

    /// <summary>
    /// Waits until the expected row count is reached and still holds after a settle window —
    /// a provider recreation during circuit startup can overwrite the filtered state, so a
    /// momentary match is not sufficient.
    /// </summary>
    /// <param name="page">The Playwright page under test.</param>
    /// <param name="text">Text to look for inside visible rows.</param>
    /// <param name="expected">Expected number of visible rows containing <paramref name="text"/>.</param>
    /// <param name="timeoutMs">Maximum time in milliseconds to wait for the initial match.</param>
    /// <returns><c>true</c> when the count matched and stayed stable; otherwise <c>false</c>.</returns>
    private static async Task<bool> TryWaitForStableRowCountAsync(IPage page, string text, int expected, int timeoutMs)
    {
        if (!await TryWaitForRowCountAsync(page, text, expected, timeoutMs))
        {
            return false;
        }
        await page.WaitForTimeoutAsync(1200);
        return await EvaluateRowCountAsync(page, text) == expected;
    }

    /// <summary>
    /// Counts the currently visible list rows whose text contains the given string.
    /// </summary>
    /// <param name="page">The Playwright page under test.</param>
    /// <param name="text">Text to look for inside visible rows.</param>
    /// <returns>Number of visible rows containing <paramref name="text"/>.</returns>
    private static async Task<int> EvaluateRowCountAsync(IPage page, string text)
        => await page.EvaluateAsync<int>(
            "(text) => [...document.querySelectorAll('.generic-list-mobile-card, tbody tr')]" +
            ".filter(r => r.offsetParent !== null && (r.textContent || '').includes(text)).length",
            text);

    /// <summary>
    /// Polls until the expected visible row count is reached or the timeout elapses.
    /// </summary>
    /// <param name="page">The Playwright page under test.</param>
    /// <param name="text">Text to look for inside visible rows.</param>
    /// <param name="expected">Expected number of visible rows containing <paramref name="text"/>.</param>
    /// <param name="timeoutMs">Maximum time in milliseconds to wait.</param>
    /// <returns><c>true</c> when the count was reached within the timeout; otherwise <c>false</c>.</returns>
    private static async Task<bool> TryWaitForRowCountAsync(IPage page, string text, int expected, int timeoutMs)
    {
        try
        {
            await page.WaitForFunctionAsync(
                $"([text, expected]) => [...document.querySelectorAll('.generic-list-mobile-card, tbody tr')]" +
                ".filter(r => r.offsetParent !== null && (r.textContent || '').includes(text)).length === expected",
                arg: new object[] { text, expected },
                options: new() { Timeout = timeoutMs });
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    private static async Task CaptureAsync(IPage page, string name)
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "acceptance-shots");
        Directory.CreateDirectory(dir);
        await page.ScreenshotAsync(new() { Path = Path.Combine(dir, $"{name}.png"), FullPage = true });
    }

    private static async Task<Guid> CreateSavingsPlanAsync(IPage page, string name, string? targetDate)
    {
        var result = await BrowserApiHelper.PostJsonAsync<object, System.Text.Json.JsonElement>(page, "api/savings-plans",
            new { Name = name, Type = 0, TargetAmount = (decimal?)null, TargetDate = targetDate, Interval = (int?)null, CategoryId = (Guid?)null, ContractNumber = (string?)null });
        return result.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateSecurityAsync(IPage page, string name, string identifier)
    {
        var result = await BrowserApiHelper.PostJsonAsync<object, System.Text.Json.JsonElement>(page, "api/securities",
            new { Name = name, Identifier = identifier, CurrencyCode = "EUR", Description = (string?)null, AlphaVantageCode = (string?)null, CategoryId = (Guid?)null });
        return result.GetProperty("id").GetGuid();
    }

    private async Task<string> LoginNewUserAsync(IPage page, string prefix, bool isAdmin = false)
    {
        var username = $"{prefix}-{Guid.NewGuid():N}";
        const string password = "Secret123";
        var auth = new AuthGateway(page, _fixture.BaseUrl);
        var seeder = new TestUserSeeder(_fixture.DatabasePath);
        await seeder.EnsureUserAsync(username, password, isAdmin: isAdmin, timeZoneId: "UTC");
        await auth.LoginAsync(username, password);
        return username;
    }
}
