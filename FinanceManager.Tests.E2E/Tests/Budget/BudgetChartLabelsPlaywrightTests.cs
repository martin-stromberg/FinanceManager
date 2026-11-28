namespace FinanceManager.Tests.E2E;

/// <summary>
/// End-to-end verification of the chart edge-label fix (issue #610): the last x-axis label of the
/// budget report line chart was clipped at the right SVG edge because all labels were
/// middle-anchored. Edge labels now anchor inward (first 'start', last 'end'). Screenshots for
/// desktop and a narrow mobile viewport are captured for the visual acceptance.
/// </summary>
[Collection(PlaywrightCollection.CollectionName)]
public sealed class BudgetChartLabelsPlaywrightTests
{
    private readonly PlaywrightWebAppFixture _fixture;

    /// <summary>
    /// Initializes a new instance of the <see cref="BudgetChartLabelsPlaywrightTests"/> class.
    /// </summary>
    /// <param name="fixture">Shared Playwright web app fixture providing the browser and test server.</param>
    public BudgetChartLabelsPlaywrightTests(PlaywrightWebAppFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// The budget report line chart must draw at least two x-axis labels, with the first label
    /// start-anchored and the last label end-anchored so both stay fully inside the viewBox —
    /// on desktop and on a narrow mobile viewport.
    /// </summary>
    [Fact]
    public async Task BudgetReport_ChartEdgeLabels_AnchorInward()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        await LoginNewUserAsync(page, "chart-labels");

        await page.GotoAsync("reports/budget");
        var labels = page.Locator(".budget-linechart .labels text");
        await labels.First.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 60000 });

        var count = await labels.CountAsync();
        Assert.True(count >= 2, $"expected at least 2 x-axis labels, got {count}");
        Assert.Equal("start", await labels.First.GetAttributeAsync("text-anchor"));
        Assert.Equal("end", await labels.Last.GetAttributeAsync("text-anchor"));

        // last label must end at or before the right drawing edge (x=980 of the 1000-unit viewBox)
        var lastX = double.Parse(await labels.Last.GetAttributeAsync("x") ?? "0",
            System.Globalization.CultureInfo.InvariantCulture);
        Assert.True(lastX <= 980.0, $"last label x={lastX} exceeds the right drawing edge");

        await CaptureAsync(page, "s5-budget-report-chart-labels-desktop");

        // narrow mobile viewport — the same viewBox rule keeps labels inside the scaled chart
        await page.SetViewportSizeAsync(420, 800);
        await page.WaitForTimeoutAsync(800);
        Assert.Equal("start", await labels.First.GetAttributeAsync("text-anchor"));
        Assert.Equal("end", await labels.Last.GetAttributeAsync("text-anchor"));
        await CaptureAsync(page, "s5-budget-report-chart-labels-mobile");
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
