using System.Globalization;
using System.Text.RegularExpressions;

namespace FinanceManager.Tests.E2E;

/// <summary>
/// End-to-end verification of the unified analysis navigation (issue #603): the budget purpose
/// overview must offer the same five relative-navigation buttons as the budget report — previous/
/// current/next month and previous/next year — each shifting the displayed range relative to the
/// current view rather than anchoring on today. Screenshots of the shifted ranges are captured for
/// the visual acceptance.
/// </summary>
[Collection(PlaywrightCollection.CollectionName)]
public sealed class BudgetAnalysisNavigationPlaywrightTests
{
    private readonly PlaywrightWebAppFixture _fixture;

    /// <summary>
    /// Initializes a new instance of the <see cref="BudgetAnalysisNavigationPlaywrightTests"/> class.
    /// </summary>
    /// <param name="fixture">Shared Playwright web app fixture providing the browser and test server.</param>
    public BudgetAnalysisNavigationPlaywrightTests(PlaywrightWebAppFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// The overview must expose the five unified buttons plus the kept ThisYear quick filter, and
    /// each navigation click must shift the displayed Von/Bis range relative to the current view.
    /// </summary>
    [Fact]
    public async Task BudgetOverview_AnalysisButtons_ShiftRangeRelatively()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        await LoginNewUserAsync(page, "budget-analyse-nav");

        await page.GotoAsync("list/budget/purposes");
        await page.Locator("button#PrevMonth").WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 30000 });

        // unified five buttons + kept ThisYear quick filter
        foreach (var id in new[] { "PrevMonth", "ThisMonth", "NextMonth", "PrevYear", "NextYear", "ThisYear" })
        {
            Assert.Equal(1, await page.Locator($"button#{id}").CountAsync());
        }

        var today = DateTime.Today;
        var thisMonth = MonthBounds(today.Year, today.Month);

        // default view: current calendar month
        await WaitForRangeAsync(page, thisMonth, 30000);

        // Vormonat: one calendar month back from the displayed month (repeatable, not re-anchored)
        var prevMonth = MonthBounds(thisMonth.From.AddMonths(-1).Year, thisMonth.From.AddMonths(-1).Month);
        await ClickUntilRangeAsync(page, "PrevMonth", prevMonth.From, prevMonth.To);
        await CaptureAsync(page, "s3-budget-overview-prev-month");

        // second click walks one more month back
        var twoBack = MonthBounds(prevMonth.From.AddMonths(-1).Year, prevMonth.From.AddMonths(-1).Month);
        await ClickUntilRangeAsync(page, "PrevMonth", twoBack.From, twoBack.To);

        // Nächster Monat twice brings the view back to the current month
        await ClickUntilRangeAsync(page, "NextMonth", prevMonth.From, prevMonth.To);
        await ClickUntilRangeAsync(page, "NextMonth", thisMonth.From, thisMonth.To);

        // Vorjahr: same month width, one year back
        var prevYear = MonthBounds(today.Year - 1, today.Month);
        await ClickUntilRangeAsync(page, "PrevYear", prevYear.From, prevYear.To);
        await CaptureAsync(page, "s3-budget-overview-prev-year");

        // Aktuelles Jahr keeps working as the year quick filter
        await ClickUntilRangeAsync(page, "ThisYear", new DateTime(today.Year, 1, 1), new DateTime(today.Year, 12, 31));
        await CaptureAsync(page, "s3-budget-overview-this-year");
    }

    /// <summary>
    /// The budget report must offer the same five navigation buttons in the same order, and a
    /// click must shift the displayed period.
    /// </summary>
    [Fact]
    public async Task BudgetReport_AnalysisButtons_OfferSameFive()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        await LoginNewUserAsync(page, "budget-report-nav");

        await page.GotoAsync("reports/budget");
        await page.Locator("button#PrevMonth").WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 30000 });
        await page.Locator(".budget-report-table").First.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 60000 });

        foreach (var id in new[] { "PrevMonth", "ThisMonth", "NextMonth", "PrevYear", "NextYear" })
        {
            Assert.Equal(1, await page.Locator($"button#{id}").CountAsync());
        }

        // NextMonth moves the report anchor one month forward (period end = month end of next month)
        var today = DateTime.Today;
        var expectedEnd = new DateTime(today.Year, today.Month, 1).AddMonths(2).AddDays(-1);
        var pattern = $"{Regex.Escape(expectedEnd.ToString("d", CultureInfo.GetCultureInfo("de-DE")))}|{Regex.Escape(expectedEnd.ToString("d", CultureInfo.GetCultureInfo("en-US")))}";
        var period = page.Locator(".budget-report-period");
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var before = await period.InnerTextAsync();
            await page.Locator("button#NextMonth").ClickAsync();
            try
            {
                await Assertions.Expect(period).Not.ToHaveTextAsync(before, new() { Timeout = 15000 });
                break;
            }
            catch (PlaywrightException)
            {
                // click was lost (e.g. Blazor circuit still starting) — retry
            }
        }
        await Assertions.Expect(period).ToContainTextAsync(new Regex(pattern), new() { Timeout = 15000 });

        var loading = page.Locator(".budget-report-loading");
        if (await loading.CountAsync() > 0)
        {
            await loading.WaitForAsync(new() { State = WaitForSelectorState.Hidden, Timeout = 60000 });
        }
        await CaptureAsync(page, "s3-budget-report-analysis-buttons");
    }

    private static (DateTime From, DateTime To) MonthBounds(int year, int month)
        => (new DateTime(year, month, 1), new DateTime(year, month, DateTime.DaysInMonth(year, month)));

    /// <summary>
    /// Clicks a ribbon analysis button until the range inputs show the expected bounds. Right after
    /// navigation the Blazor circuit may still be starting, so the click is retried until the
    /// shifted range holds stably.
    /// </summary>
    /// <param name="page">Playwright page.</param>
    /// <param name="buttonId">Ribbon action id of the button to click.</param>
    /// <param name="expectedFrom">Expected lower range bound.</param>
    /// <param name="expectedTo">Expected upper range bound.</param>
    private static async Task ClickUntilRangeAsync(IPage page, string buttonId, DateTime expectedFrom, DateTime expectedTo)
    {
        var expected = (expectedFrom, expectedTo);
        for (var attempt = 0; attempt < 5; attempt++)
        {
            await page.Locator($"button#{buttonId}").ClickAsync();
            if (await TryWaitForRangeAsync(page, expected, 10000))
            {
                await page.WaitForTimeoutAsync(800);
                var (from, to) = await ReadRangeAsync(page);
                if (from == expectedFrom && to == expectedTo)
                {
                    return;
                }
            }
        }
        var (actualFrom, actualTo) = await ReadRangeAsync(page);
        throw new TimeoutException($"button '{buttonId}' never produced range {expectedFrom:yyyy-MM-dd}..{expectedTo:yyyy-MM-dd} (actual {actualFrom:yyyy-MM-dd}..{actualTo:yyyy-MM-dd})");
    }

    /// <summary>
    /// Waits until the range inputs show the expected bounds; throws when the timeout elapses.
    /// </summary>
    /// <param name="page">Playwright page.</param>
    /// <param name="From">Expected lower range bound.</param>
    /// <param name="expected">Expected range bounds.</param>
    /// <param name="timeoutMs">Wait timeout in milliseconds.</param>
    private static async Task WaitForRangeAsync(IPage page, (DateTime From, DateTime To) expected, int timeoutMs)
    {
        if (!await TryWaitForRangeAsync(page, expected, timeoutMs))
        {
            var (actualFrom, actualTo) = await ReadRangeAsync(page);
            throw new TimeoutException($"range never reached {expected.From:yyyy-MM-dd}..{expected.To:yyyy-MM-dd} (actual {actualFrom:yyyy-MM-dd}..{actualTo:yyyy-MM-dd})");
        }
    }

    /// <summary>
    /// Polls until the range inputs show the expected bounds or the timeout elapses.
    /// </summary>
    /// <param name="page">Playwright page.</param>
    /// <param name="From">Expected lower range bound.</param>
    /// <param name="expected">Expected range bounds.</param>
    /// <param name="timeoutMs">Wait timeout in milliseconds.</param>
    /// <returns><see langword="true"/> when the expected bounds appeared in time.</returns>
    private static async Task<bool> TryWaitForRangeAsync(IPage page, (DateTime From, DateTime To) expected, int timeoutMs)
    {
        try
        {
            await page.WaitForFunctionAsync(
                "([from, to]) => { const i = document.querySelectorAll('.list-filter-range input');" +
                " return i.length >= 2 && i[0].value === from && i[1].value === to; }",
                arg: new object[] { expected.From.ToString("yyyy-MM-dd"), expected.To.ToString("yyyy-MM-dd") },
                options: new() { Timeout = timeoutMs });
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    /// <summary>
    /// Reads the current Von/Bis range input values.
    /// </summary>
    /// <param name="page">Playwright page.</param>
    /// <returns>The displayed range bounds.</returns>
    private static async Task<(DateTime From, DateTime To)> ReadRangeAsync(IPage page)
    {
        var rangeInputs = page.Locator(".list-filter-range input");
        var from = DateTime.Parse(await rangeInputs.Nth(0).InputValueAsync());
        var to = DateTime.Parse(await rangeInputs.Nth(1).InputValueAsync());
        return (from, to);
    }

    /// <summary>
    /// Captures a full-page acceptance screenshot into the test output directory.
    /// </summary>
    /// <param name="page">Playwright page.</param>
    /// <param name="name">Screenshot file name without extension.</param>
    private static async Task CaptureAsync(IPage page, string name)
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "acceptance-shots");
        Directory.CreateDirectory(dir);
        await page.ScreenshotAsync(new() { Path = Path.Combine(dir, $"{name}.png"), FullPage = true });
    }

    /// <summary>
    /// Creates a fresh user via the test seeder and logs in through the UI.
    /// </summary>
    /// <param name="page">Playwright page.</param>
    /// <param name="prefix">Username prefix.</param>
    /// <returns>The generated username.</returns>
    private async Task<string> LoginNewUserAsync(IPage page, string prefix)
    {
        var username = $"{prefix}-{Guid.NewGuid():N}";
        const string password = "Secret123";
        var auth = new AuthGateway(page, _fixture.BaseUrl);
        var seeder = new TestUserSeeder(_fixture.DatabasePath);
        await seeder.EnsureUserAsync(username, password, timeZoneId: "UTC");
        await auth.LoginAsync(username, password);
        return username;
    }
}
