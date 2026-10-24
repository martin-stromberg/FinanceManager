using System.Text.RegularExpressions;
using System.Globalization;
using FinanceManager.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Tests.E2E;

/// <summary>
/// End-to-end regression test for the demo-data seeded budget report using a real browser session:
/// register first user with demo-data generation, wait for background task completion, open budget report,
/// move to previous month, and verify expected budget/actual values.
/// </summary>
[Collection(BudgetDemoDataCollection.Name)]
public sealed class BudgetReportDemoDataE2ETests
{
    private static readonly CultureInfo GermanCulture = CultureInfo.GetCultureInfo("de-DE");
    private readonly PlaywrightWebAppFixture _fixture;

    /// <summary>
    /// Initializes a new instance of the <see cref="BudgetReportDemoDataE2ETests"/> class.
    /// </summary>
    /// <param name="fixture">Shared Playwright web app fixture.</param>
    public BudgetReportDemoDataE2ETests(PlaywrightWebAppFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// Registers a fresh first user, triggers demo-data creation, waits until the background task panel
    /// disappears, then validates that the previous-month budget report contains expected positions and values.
    /// </summary>
    /// <param name="clickDispatchDelayMilliseconds">The delay before forwarding the month-navigation click.</param>
    [Theory]
    [InlineData(750)]
    public async Task DemoDataBudgetReport_PreviousMonth_ShouldContainAllExpectedRows(int clickDispatchDelayMilliseconds)
    {
        await using (var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={_fixture.DatabasePath}").Options))
        {
            (await db.Users.AnyAsync(TestContext.Current.CancellationToken)).Should().BeFalse("the demo-data registration flow requires the first user in an isolated database");
        }
        await using var session = await _fixture.CreateSessionAsync(new() { Locale = "de-DE" });
        var page = session.Page;
        var auth = new AuthGateway(page, _fixture.BaseUrl);

        var username = $"demo-budget-e2e-{Guid.NewGuid():N}";
        const string password = "Secret123";
        var negotiationStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowNegotiation = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await page.RouteAsync("**/_blazor/negotiate*", async route =>
        {
            negotiationStarted.TrySetResult();
            await allowNegotiation.Task;
            await route.ContinueAsync();
        });
        try
        {
            await page.GotoAsync("/register");
            await negotiationStarted.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            await Assertions.Expect(page.Locator("#username")).ToBeVisibleAsync();
            await Assertions.Expect(page.Locator("button[type=submit]")).ToBeDisabledAsync();
        }
        finally
        {
            allowNegotiation.TrySetResult();
        }
        await SubmitRegisterFormAsync(page, username, password);

        var backgroundPanel = page.Locator(".bgt-panel");

        await auth.LogoutAsync();
        await auth.LoginThroughUiAsync(username, password);
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await backgroundPanel.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 30_000 });
        await backgroundPanel.WaitForAsync(new() { State = WaitForSelectorState.Hidden, Timeout = 240_000 });

        await page.GotoAsync("/list/budgets");
        await page.Locator("#BudgetReport").ClickAsync();
        await page.WaitForURLAsync("**/reports/budget");

        await WaitForBudgetReportReadyAsync(page);
        var previousMonthEnd = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddDays(-1);
        if (clickDispatchDelayMilliseconds > 0)
        {
            await page.EvaluateAsync("""
                delay => {
                    const button = document.querySelector('#PrevMonth');
                    button.addEventListener('click', event => {
                        event.stopImmediatePropagation();
                        event.preventDefault();
                        setTimeout(() => button.click(), delay);
                    }, { capture: true, once: true });
                }
                """, clickDispatchDelayMilliseconds);
        }
        await page.Locator("#PrevMonth").ClickAsync();
        await WaitForBudgetReportReadyAsync(page, previousMonthEnd.ToString("d", GermanCulture));
        (await page.Locator(".budget-report-period").InnerTextAsync()).Should().Contain(previousMonthEnd.ToString("d", GermanCulture));

        var detailsRows = await ReadDetailsRowsAsync(page);
        var names = detailsRows
            .Select(static x => x.Name)
            .ToList();

        names.Should().Contain("Gehalt");
        names.Should().Contain("Rückstellung Hausratversicherung");
        names.Should().Contain("Hausratversicherung");
        names.Should().Contain("Rückstellung SDAC");
        names.Should().Contain("SDAC");
        names.Should().Contain("Wohnungsmiete");
        names.Should().Contain("Supermärkte & Einzelhandel");
        names.Should().Contain("Bäckereien & Cafés");

        AssertRowValues(detailsRows, "Gehalt", 3642.50m, 3642.50m);
        AssertRowValues(detailsRows, "Rückstellung Hausratversicherung", -5.22m, -5.22m);
        AssertRowValues(detailsRows, "Rückstellung SDAC", -8.25m, -8.25m);
        AssertRowValues(detailsRows, "Wohnungsmiete", -845.00m, -845.00m);

        var bakeryRow = RequireRow(detailsRows, "Bäckereien & Cafés");
        var marketRow = RequireRow(detailsRows, "Supermärkte & Einzelhandel");
        var shoppingCategoryRow = RequireRow(detailsRows, "Einkaufen & Verpflegung");

        shoppingCategoryRow.Budget.Should().NotBeNull();
        shoppingCategoryRow.Budget!.Value.Should().Be(-300.00m);
        bakeryRow.Actual.Should().NotBeNull();
        marketRow.Actual.Should().NotBeNull();
        shoppingCategoryRow.Actual.Should().NotBeNull();

        bakeryRow.Actual!.Value.Should().BeLessThan(0m, "demo seeding creates weekly bakery postings");
        marketRow.Actual!.Value.Should().BeLessThan(0m, "demo seeding creates weekly supermarket postings");
        shoppingCategoryRow.Actual!.Value.Should().BeApproximately(
            bakeryRow.Actual.Value + marketRow.Actual.Value,
            0.01m,
            "shopping category actual should aggregate bakery and supermarket actuals");

        foreach (var row in detailsRows.Where(static x => x.Budget.HasValue && x.Actual.HasValue && x.Delta.HasValue))
        {
            row.Delta!.Value.Should().BeApproximately(
                row.Actual!.Value - row.Budget!.Value,
                0.01m,
                $"delta for row '{row.Name}' must match actual-budget");
        }

        await AssertShowPostingsMatchesActualAsync(page, "Gehalt");
        await AssertShowPostingsMatchesActualAsync(page, "Bäckereien & Cafés");
        await AssertShowPostingsMatchesActualAsync(page, "Supermärkte & Einzelhandel");
        await AssertShowPostingsMatchesActualAsync(page, "Rückstellung Hausratversicherung");
        await AssertShowPostingsMatchesActualAsync(page, "Rückstellung SDAC");
        await AssertShowPostingsMatchesActualAsync(page, "Strom");
        await AssertShowPostingsMatchesActualAsync(page, "Wohnungsmiete");
        await AssertShowPostingsMatchesActualAsync(page, "Nicht budgetiert");
    }

    private static async Task WaitForBudgetReportReadyAsync(IPage page, string? expectedPeriodEnd = null)
    {
        if (expectedPeriodEnd != null)
        {
            await Assertions.Expect(page.Locator(".budget-report-period"))
                .ToContainTextAsync(expectedPeriodEnd, new() { Timeout = 60_000 });
        }
        await page.Locator(".budget-report-table").First.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 60_000 });
        await page.Locator(".budget-report-loading").WaitForAsync(new() { State = WaitForSelectorState.Hidden, Timeout = 60_000 });
    }

    private static async Task SubmitRegisterFormAsync(IPage page, string username, string password)
    {
        var usernameInput = page.Locator("#username");
        var passwordInput = page.Locator("#password");
        var demoCheckbox = page.Locator("#create-demo-data");

        await demoCheckbox.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15_000 });

        await Assertions.Expect(page.Locator("button[type=submit]")).ToBeEnabledAsync();
        await usernameInput.FillAsync(username);
        await usernameInput.PressAsync("Tab");
        await passwordInput.FillAsync(password);
        await passwordInput.PressAsync("Tab");
        await demoCheckbox.CheckAsync();
        await page.Locator("button[type=submit]").ClickAsync();
        await page.WaitForURLAsync(url => new Uri(url).AbsolutePath != "/register");
    }

    private static string NormalizeWhitespace(string input)
        => Regex.Replace(input, "\\s+", " ").Trim();

    private static async Task AssertShowPostingsMatchesActualAsync(IPage page, string rowName)
    {
        var detailsTable = page.Locator(".budget-report-table").Nth(1);
        var row = await FindDetailsRowByNameAsync(detailsTable, rowName);
        row.Should().NotBeNull($"row '{rowName}' must exist for show-postings validation");

        var showPostingsButton = row.Locator("button.icon-btn").First;
        await showPostingsButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 30_000 });
        await showPostingsButton.ClickAsync();

        var overlay = page.Locator(".split-dialog").First;
        await overlay.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 30_000 });
        await page.WaitForFunctionAsync(
            """
            () => {
                const dialog = document.querySelector(".split-dialog");
                if (!dialog) {
                    return false;
                }

                if (dialog.querySelectorAll("tbody tr").length > 0) {
                    return true;
                }

                const paragraphs = Array.from(dialog.querySelectorAll("p"))
                    .map(p => (p.textContent ?? "").trim())
                    .filter(p => p.length > 0);

                if (paragraphs.length === 0) {
                    return false;
                }

                const loadingTokens = ["loading", "laden", "load"];
                return paragraphs.some(text => {
                    const lower = text.toLowerCase();
                    return !loadingTokens.some(token => lower.includes(token));
                });
            }
            """,
            null,
            new() { Timeout = 60_000 });

        if (!string.Equals(rowName, "Nicht budgetiert", StringComparison.Ordinal))
        {
            var overlayTitle = NormalizeWhitespace(await overlay.Locator("h3").First.InnerTextAsync());
            overlayTitle.Should().Be(rowName, "show-postings should open the overlay of the clicked detail row");
        }

        var overlayRows = overlay.Locator("tbody tr");
        var overlayCount = await overlayRows.CountAsync();
        overlayCount.Should().BeGreaterThan(0, $"show-postings overlay for '{rowName}' should contain postings");
        for (var i = 0; i < overlayCount; i++)
        {
            var overlayRow = overlayRows.Nth(i);
            var amountText = await overlayRow.Locator("td").Nth(3).InnerTextAsync();
            var amount = ParseAmount(amountText);
            amount.Should().NotBeNull($"overlay amount in row {i + 1} for '{rowName}' must be parseable");
        }

        await overlay.Locator("button.icon-btn").First.ClickAsync();
        await overlay.WaitForAsync(new() { State = WaitForSelectorState.Hidden, Timeout = 30_000 });
    }

    private static async Task<ILocator?> FindDetailsRowByNameAsync(ILocator detailsTable, string rowName)
    {
        var rows = detailsTable.Locator("tbody tr");
        var count = await rows.CountAsync();
        for (var i = 0; i < count; i++)
        {
            var row = rows.Nth(i);
            var cells = row.Locator("td");
            if (await cells.CountAsync() == 0)
            {
                continue;
            }

            var name = NormalizeWhitespace(await cells.Nth(0).InnerTextAsync());
            if (string.Equals(name, rowName, StringComparison.Ordinal))
            {
                return row;
            }
        }

        return null;
    }

    private static async Task<List<BudgetRow>> ReadDetailsRowsAsync(IPage page)
    {
        var detailsTable = page.Locator(".budget-report-table").Nth(1);
        var rowLocator = detailsTable.Locator("tbody tr");
        var rowCount = await rowLocator.CountAsync();
        var rows = new List<BudgetRow>(rowCount);

        for (var i = 0; i < rowCount; i++)
        {
            var row = rowLocator.Nth(i);
            var cells = row.Locator("td");
            if (await cells.CountAsync() < 4)
            {
                continue;
            }

            var name = NormalizeWhitespace(await cells.Nth(0).InnerTextAsync());
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            rows.Add(new BudgetRow(
                name,
                ParseAmount(await cells.Nth(1).InnerTextAsync()),
                ParseAmount(await cells.Nth(2).InnerTextAsync()),
                ParseAmount(await cells.Nth(3).InnerTextAsync())));
        }

        return rows;
    }

    private static decimal? ParseAmount(string raw)
    {
        var normalized = NormalizeWhitespace(raw)
            .Replace('\u00A0', ' ')
            .Trim();

        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        if (decimal.TryParse(normalized, NumberStyles.Number | NumberStyles.AllowLeadingSign, GermanCulture, out var germanValue))
        {
            return germanValue;
        }

        if (decimal.TryParse(normalized, NumberStyles.Number | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var invariantValue))
        {
            return invariantValue;
        }

        throw new InvalidOperationException($"Unable to parse decimal amount from '{raw}'.");
    }

    private static void AssertRowValues(IEnumerable<BudgetRow> rows, string rowName, decimal expectedBudget, decimal expectedActual)
    {
        var row = RequireRow(rows, rowName);
        row!.Budget.Should().NotBeNull($"row '{rowName}' must provide budget value");
        row.Actual.Should().NotBeNull($"row '{rowName}' must provide actual value");
        row.Budget!.Value.Should().Be(expectedBudget);
        row.Actual!.Value.Should().Be(expectedActual);
    }

    private static BudgetRow RequireRow(IEnumerable<BudgetRow> rows, string rowName)
    {
        var row = rows.FirstOrDefault(x => x.Name.Equals(rowName, StringComparison.Ordinal));
        row.Should().NotBeNull($"row '{rowName}' must exist in budget details table");
        return row!;
    }

    private sealed record BudgetRow(string Name, decimal? Budget, decimal? Actual, decimal? Delta);
}
