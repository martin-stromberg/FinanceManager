using System.Text.RegularExpressions;
using FinanceManager.Domain.Securities;
using FinanceManager.Domain.Users;
using FinanceManager.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Tests.E2E;

/// <summary>
/// End-to-end tests for the trend reversal (bearish reversal signals) feature: the widget on the
/// security card page shows detected signals for a bearish price series and stays hidden for an
/// intact uptrend, and the notifications setup tab exposes and persists the new user setting.
/// </summary>
[Collection(PlaywrightCollection.CollectionName)]
public sealed class SecurityTrendReversalE2ETests
{
    private readonly PlaywrightWebAppFixture _fixture;

    /// <summary>
    /// Initializes a new instance of the <see cref="SecurityTrendReversalE2ETests"/> class.
    /// </summary>
    /// <param name="fixture">Shared Playwright web app fixture providing the browser and test server.</param>
    public SecurityTrendReversalE2ETests(PlaywrightWebAppFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// A security with a bearish price history (rise followed by collapse below the long-term average)
    /// renders the trend reversal widget on its card page with at least one listed signal.
    /// </summary>
    [Fact]
    public async Task CardPage_BearishPrices_ShowsTrendReversalWidgetWithSignals()
    {
        var session = await LoginAndSeedAsync(BearishSeries());
        await using var sessionGuard = session.Session;
        var page = session.Page;

        await page.GotoAsync($"/card/securities/{session.SecurityId}");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var widget = page.Locator("div.trend-reversal-widget");
        await widget.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 15000 });

        (await widget.Locator(".trw-signal").CountAsync()).Should().BeGreaterThanOrEqualTo(1);
        (await widget.Locator(".trw-status").CountAsync()).Should().Be(1);
    }

    /// <summary>
    /// A security with an intact uptrend renders no trend reversal widget on its card page.
    /// </summary>
    [Fact]
    public async Task CardPage_BullishPrices_HidesTrendReversalWidget()
    {
        var session = await LoginAndSeedAsync(BullishSeries());
        await using var sessionGuard = session.Session;
        var page = session.Page;

        await page.GotoAsync($"/card/securities/{session.SecurityId}");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        // Give the page a moment for the widget's asynchronous API call to complete.
        await page.Locator("body").WaitForAsync();
        await page.WaitForTimeoutAsync(2000);

        (await page.Locator("div.trend-reversal-widget").CountAsync()).Should().Be(0);
    }

    /// <summary>
    /// The notifications setup tab shows the trend reversal checkbox; toggling and saving persists the value.
    /// </summary>
    [Fact]
    public async Task SetupNotificationsTab_TrendReversalCheckbox_PersistsToggle()
    {
        var session = await LoginAndSeedAsync(BullishSeries());
        await using var sessionGuard = session.Session;
        var page = session.Page;

        await page.GotoAsync("/card/setup");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var toggle = page.Locator("button.setup-section-toggle")
            .Filter(new LocatorFilterOptions { HasTextRegex = new Regex("Benachrichtigungen|Notifications") });
        (await toggle.CountAsync()).Should().Be(1);
        await ExpandSectionAsync(page, toggle);

        var checkbox = page.Locator("#notif-trend-reversal");
        await checkbox.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 15000 });

        var original = await checkbox.IsCheckedAsync();
        await checkbox.SetCheckedAsync(!original);

        var saveButton = page.Locator("#Save");
        await Microsoft.Playwright.Assertions.Expect(saveButton).ToBeEnabledAsync(new LocatorAssertionsToBeEnabledOptions { Timeout = 5000 });
        await saveButton.ClickAsync();
        await Microsoft.Playwright.Assertions.Expect(saveButton).ToBeDisabledAsync(new LocatorAssertionsToBeDisabledOptions { Timeout = 5000 });

        await page.ReloadAsync();
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        var toggleAfter = page.Locator("button.setup-section-toggle")
            .Filter(new LocatorFilterOptions { HasTextRegex = new Regex("Benachrichtigungen|Notifications") });
        await ExpandSectionAsync(page, toggleAfter);
        await page.Locator("#notif-trend-reversal").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 15000 });

        (await page.Locator("#notif-trend-reversal").IsCheckedAsync()).Should().Be(!original);
    }

    /// <summary>Rising series then collapse: death cross, structural line break and bearish structure.</summary>
    /// <returns>The result.</returns>
    private static decimal[] BearishSeries()
    {
        var closes = new decimal[240];
        for (var i = 0; i < 160; i++)
        {
            closes[i] = 100m + i * 0.4m;
        }
        for (var i = 160; i < closes.Length; i++)
        {
            closes[i] = 164m - (i - 160) * 1.3m;
        }
        return closes;
    }

    /// <summary>Monotone rise: no bearish reversal signal.</summary>
    /// <returns>The result.</returns>
    private static decimal[] BullishSeries()
    {
        var closes = new decimal[240];
        for (var i = 0; i < closes.Length; i++)
        {
            closes[i] = 100m + i * 0.5m;
        }
        return closes;
    }

    private async Task<SessionWithSecurity> LoginAndSeedAsync(decimal[] closes)
    {
        var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        var auth = new AuthGateway(page, _fixture.BaseUrl);
        var seed = new TestUserSeeder(_fixture.DatabasePath);

        var username = $"trend-rev-{Guid.NewGuid():N}";
        const string password = "Secret123";
        var user = await seed.EnsureUserAsync(username, password);

        var securityId = await SeedSecurityAsync(user.Id, closes);
        await auth.LoginAsync(username, password);
        return new SessionWithSecurity(session, securityId);
    }

    private sealed record SessionWithSecurity(PlaywrightBrowserSession Session, Guid SecurityId)
    {
        public IPage Page => Session.Page;
    }

    /// <summary>Seeds a security plus the given price history directly into the E2E SQLite database.</summary>
    /// <param name="userId">Owner user id for the seeded security.</param>
    /// <param name="closes">Chronological closing prices, one per day, oldest first.</param>
    /// <returns>The id of the seeded security.</returns>
    private async Task<Guid> SeedSecurityAsync(Guid userId, decimal[] closes)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={_fixture.DatabasePath}")
            .Options;
        await using var db = new AppDbContext(options);

        var security = new Security(userId, "Trend Test ETF", "TRND01", null, null, "EUR", null);
        db.Securities.Add(security);

        var start = DateTime.UtcNow.Date.AddDays(-closes.Length);
        for (var i = 0; i < closes.Length; i++)
        {
            db.SecurityPrices.Add(new SecurityPrice(security.Id, start.AddDays(i), closes[i]));
        }
        await db.SaveChangesAsync();
        return security.Id;
    }

    private static async Task ExpandSectionAsync(IPage page, ILocator toggle)
    {
        // On a freshly loaded page the Blazor circuit may not be connected yet; retry the click
        // until the section reports the expanded state via the aria-expanded attribute.
        for (var attempt = 0; attempt < 10; attempt++)
        {
            await toggle.ClickAsync();
            try
            {
                await Microsoft.Playwright.Assertions.Expect(toggle)
                    .ToHaveAttributeAsync("aria-expanded", "", new LocatorAssertionsToHaveAttributeOptions { Timeout = 2000 });
                return;
            }
            catch (PlaywrightException)
            {
            }
        }

        (await toggle.GetAttributeAsync("aria-expanded")).Should().NotBeNull("the section must expand after clicking the toggle");
    }
}
