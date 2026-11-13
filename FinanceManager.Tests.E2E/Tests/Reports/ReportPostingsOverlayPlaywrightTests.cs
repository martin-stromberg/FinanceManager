using FinanceManager.Domain.Accounts;
using FinanceManager.Domain.Postings;
using FinanceManager.Domain.Savings;
using FinanceManager.Domain.Securities;
using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Aggregates;
using FinanceManager.Shared.Dtos.Postings;
using FinanceManager.Shared.Dtos.SavingsPlans;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Tests.E2E;

/// <summary>
/// End-to-end acceptance of the postings drill-down on the report dashboard (issue #510): clicking the
/// postings button of a report cell opens the overlay with the individual postings the cell value is
/// composed of. Covered against a real app instance and browser: entity rows for all three value
/// columns (amount, previous period, year ago), the total row, type rows and the linked-account
/// savings plan source in a multi-kind report, and the netted security dividend variant.
/// Seed data and aggregates are written directly to the fixture's SQLite database; report variants
/// are configured through report favorites created via the API.
/// </summary>
[Collection(PlaywrightCollection.CollectionName)]
public sealed class ReportPostingsOverlayPlaywrightTests
{
    private readonly PlaywrightWebAppFixture _fixture;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportPostingsOverlayPlaywrightTests"/> class.
    /// </summary>
    /// <param name="fixture">Shared Playwright web app fixture providing the browser and test server.</param>
    public ReportPostingsOverlayPlaywrightTests(PlaywrightWebAppFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// Verifies the postings overlay on an entity row for all three value columns and on the total row:
    /// the overlay lists exactly the postings of the cell's period window.
    /// </summary>
    [Fact]
    public async Task PostingsOverlay_EntityRowColumnsAndTotalRow_ShowCellPostings()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        var auth = new AuthGateway(page, _fixture.BaseUrl);
        var seeder = new TestUserSeeder(_fixture.DatabasePath);

        var username = $"postings-e2e-{Guid.NewGuid():N}";
        const string password = "Secret123";
        var user = await seeder.EnsureUserAsync(username, password);
        await auth.LoginAsync(username, password);

        var today = DateTime.UtcNow.Date;
        var month = new DateTime(today.Year, today.Month, 1);
        await using (var db = CreateDbContext())
        {
            var account = new Account(user.Id, AccountType.Giro, "E2E Giro", null, Guid.NewGuid());
            db.Accounts.Add(account);
            db.Postings.Add(new Posting(Guid.NewGuid(), PostingKind.Bank, account.Id, null, null, null, month.AddDays(4), 100m, "E2E Gehalt", null, null, null));
            db.Postings.Add(new Posting(Guid.NewGuid(), PostingKind.Bank, account.Id, null, null, null, month.AddDays(10), 50m, "E2E Bonus Sonderzahlung mit einer bewusst ausführlichen und langen Beschreibung des Verwendungszwecks", null, null, null));
            db.Postings.Add(new Posting(Guid.NewGuid(), PostingKind.Bank, account.Id, null, null, null, month.AddMonths(-1).AddDays(3), -20m, "E2E Miete", null, null, null));
            db.Postings.Add(new Posting(Guid.NewGuid(), PostingKind.Bank, account.Id, null, null, null, month.AddYears(-1).AddDays(6), 30m, "E2E Zinsen", null, null, null));
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            await new PostingAggregateService(db).RebuildForUserAsync(user.Id, (_, _) => { }, TestContext.Current.CancellationToken);
        }

        var favorite = await CreateFavoriteAsync(page, new ReportFavoriteCreateApiRequest
        {
            Name = $"E2E Postings {Guid.NewGuid():N}",
            PostingKind = PostingKind.Bank,
            IncludeCategory = false,
            Interval = 0, // Month
            Take = 24,
            ComparePrevious = true,
            CompareYear = true,
            ShowChart = false,
            Expandable = true
        });

        await page.GotoAsync($"/reports/dashboard?favoriteId={favorite.Id}&edit=false");
        var table = page.Locator("table.report-table");
        await table.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 30000 });

        var row = table.Locator("tr.entity-row").Filter(new LocatorFilterOptions { HasTextString = "E2E Giro" });
        await row.WaitForAsync();

        // current amount cell: the two current-month postings, sum 150
        await row.Locator("td.amt-current button").ClickAsync();
        var overlay = await ExpectOverlayAsync(page, expectedRows: 2, expectedAmountSum: 150m, "E2E Gehalt", "E2E Bonus");
        await CloseOverlayAsync(overlay);

        // previous period cell: the previous-month posting only
        await row.Locator("td.amt-prev button").ClickAsync();
        overlay = await ExpectOverlayAsync(page, expectedRows: 1, expectedAmountSum: -20m, "E2E Miete");
        await CloseOverlayAsync(overlay);

        // year-ago cell: the same-month-last-year posting only
        await row.Locator("td.amt-year button").ClickAsync();
        overlay = await ExpectOverlayAsync(page, expectedRows: 1, expectedAmountSum: 30m, "E2E Zinsen");
        await CloseOverlayAsync(overlay);

        // total row: same current-month postings as the single entity row
        await table.Locator("tr.total-row td.amt-current button").ClickAsync();
        overlay = await ExpectOverlayAsync(page, expectedRows: 2, expectedAmountSum: 150m, "E2E Gehalt");
        await CloseOverlayAsync(overlay);
    }

    /// <summary>
    /// Verifies the linked-account savings plan source in a multi-kind report: the linked plan's cell
    /// lists the bank postings of its linked account (origin = account name), unlinked plans keep their
    /// own postings, the bank type row stays untouched, and the total row unions both selections.
    /// </summary>
    [Fact]
    public async Task PostingsOverlay_LinkedSavingsPlanSource_TypeAndTotalRows()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        var auth = new AuthGateway(page, _fixture.BaseUrl);
        var seeder = new TestUserSeeder(_fixture.DatabasePath);

        var username = $"postings-linked-e2e-{Guid.NewGuid():N}";
        const string password = "Secret123";
        var user = await seeder.EnsureUserAsync(username, password);
        await auth.LoginAsync(username, password);

        var today = DateTime.UtcNow.Date;
        var month = new DateTime(today.Year, today.Month, 1);
        await using (var db = CreateDbContext())
        {
            var linked = new SavingsPlan(user.Id, "E2E Plan", SavingsPlanType.Open, null, null, null);
            var plain = new SavingsPlan(user.Id, "E2E Einfach", SavingsPlanType.Open, null, null, null);
            var contract = new Account(user.Id, AccountType.Savings, "E2E Vertragskonto", null, Guid.NewGuid());
            var giro = new Account(user.Id, AccountType.Giro, "E2E Giro", null, Guid.NewGuid());
            db.SavingsPlans.AddRange(linked, plain);
            db.Accounts.AddRange(contract, giro);
            db.SavingsPlanAccountLinks.Add(new SavingsPlanAccountLink(linked.Id, contract.Id, user.Id));
            db.Postings.Add(new Posting(Guid.NewGuid(), PostingKind.SavingsPlan, null, null, linked.Id, null, month.AddDays(2), 111m, "E2E Eigenposten", null, null, null)); // shadowed by the link
            db.Postings.Add(new Posting(Guid.NewGuid(), PostingKind.SavingsPlan, null, null, plain.Id, null, month.AddDays(3), 55m, "E2E Rate", null, null, null));
            db.Postings.Add(new Posting(Guid.NewGuid(), PostingKind.Bank, contract.Id, null, null, null, month.AddDays(4), 700m, "E2E Vertragsbuchung", null, null, null));
            db.Postings.Add(new Posting(Guid.NewGuid(), PostingKind.Bank, giro.Id, null, null, null, month.AddDays(5), 42m, "E2E Girobuchung", null, null, null));
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            await new PostingAggregateService(db).RebuildForUserAsync(user.Id, (_, _) => { }, TestContext.Current.CancellationToken);
        }

        var favorite = await CreateFavoriteAsync(page, new ReportFavoriteCreateApiRequest
        {
            Name = $"E2E Linked {Guid.NewGuid():N}",
            PostingKind = PostingKind.Bank,
            PostingKinds = new[] { PostingKind.Bank, PostingKind.SavingsPlan },
            IncludeCategory = false,
            Interval = 0, // Month
            Take = 24,
            ComparePrevious = false,
            CompareYear = false,
            ShowChart = false,
            Expandable = true,
            SavingsPlanPostingSource = SavingsPlanPostingSource.LinkedAccountPostings
        });

        await page.GotoAsync($"/reports/dashboard?favoriteId={favorite.Id}&edit=false");
        var table = page.Locator("table.report-table");
        await table.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 30000 });

        // savings plan type row (755 = 700 linked bank posting + 55 own plan posting)
        var savingsTypeRow = table.Locator("tr.type-row").Filter(new LocatorFilterOptions { HasTextString = "755" });
        await savingsTypeRow.WaitForAsync();
        await savingsTypeRow.Locator("td.amt-current button").ClickAsync();
        var overlay = await ExpectOverlayAsync(page, expectedRows: 2, expectedAmountSum: 755m, "E2E Vertragsbuchung", "E2E Rate");
        await CloseOverlayAsync(overlay);

        // expand the savings plan type row to reach the linked plan's entity row
        await savingsTypeRow.Locator("button.toggle-btn").ClickAsync();
        var linkedPlanRow = table.Locator("tr.entity-row").Filter(new LocatorFilterOptions { HasTextString = "E2E Plan" });
        await linkedPlanRow.WaitForAsync();
        await linkedPlanRow.Locator("td.amt-current button").ClickAsync();
        overlay = await ExpectOverlayAsync(page, expectedRows: 1, expectedAmountSum: 700m, "E2E Vertragsbuchung", "E2E Vertragskonto");
        await CloseOverlayAsync(overlay);

        // bank type row: plain bank postings only (742 = 700 + 42)
        var bankTypeRow = table.Locator("tr.type-row").Filter(new LocatorFilterOptions { HasTextString = "742" });
        await bankTypeRow.Locator("td.amt-current button").ClickAsync();
        overlay = await ExpectOverlayAsync(page, expectedRows: 2, expectedAmountSum: 742m, "E2E Girobuchung");
        await CloseOverlayAsync(overlay);

        // total row unions the per-kind selections (1497 = 700 + 42 + 700 + 55)
        await table.Locator("tr.total-row td.amt-current button").ClickAsync();
        overlay = await ExpectOverlayAsync(page, expectedRows: 4, expectedAmountSum: 1497m, "E2E Vertragsbuchung", "E2E Rate");
        await CloseOverlayAsync(overlay);
    }

    /// <summary>
    /// Verifies the netted dividend variant: the security report with IncludeDividendRelated shows the
    /// netted cell value and the overlay lists all members of the dividend group (dividend + fee),
    /// summing up to exactly the cell value.
    /// </summary>
    [Fact]
    public async Task PostingsOverlay_NettedDividendRow_ShowsGroupMembers()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        var auth = new AuthGateway(page, _fixture.BaseUrl);
        var seeder = new TestUserSeeder(_fixture.DatabasePath);

        var username = $"postings-dividend-e2e-{Guid.NewGuid():N}";
        const string password = "Secret123";
        var user = await seeder.EnsureUserAsync(username, password);
        await auth.LoginAsync(username, password);

        var today = DateTime.UtcNow.Date;
        var month = new DateTime(today.Year, today.Month, 1);
        var group = Guid.NewGuid();
        await using (var db = CreateDbContext())
        {
            var security = new Security(user.Id, "E2E Sec", "E2E-1", null, null, "EUR", null);
            db.Securities.Add(security);
            db.Postings.Add(new Posting(Guid.NewGuid(), PostingKind.Security, null, null, null, security.Id, month.AddDays(10), 100m, "E2E Dividende", null, null, SecurityPostingSubType.Dividend).SetGroup(group));
            db.Postings.Add(new Posting(Guid.NewGuid(), PostingKind.Security, null, null, null, security.Id, month.AddDays(11), -10m, "E2E Gebühr", null, null, SecurityPostingSubType.Fee).SetGroup(group));
            db.Postings.Add(new Posting(Guid.NewGuid(), PostingKind.Security, null, null, null, security.Id, month.AddDays(12), -1000m, "E2E Kauf", null, null, SecurityPostingSubType.Buy).SetGroup(Guid.NewGuid()));
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            await new PostingAggregateService(db).RebuildForUserAsync(user.Id, (_, _) => { }, TestContext.Current.CancellationToken);
        }

        var favorite = await CreateFavoriteAsync(page, new ReportFavoriteCreateApiRequest
        {
            Name = $"E2E Dividends {Guid.NewGuid():N}",
            PostingKind = PostingKind.Security,
            IncludeCategory = false,
            Interval = 0, // Month
            Take = 24,
            ComparePrevious = false,
            CompareYear = false,
            ShowChart = false,
            Expandable = true,
            Filters = new ReportFavoriteFiltersDto(null, null, null, null, null, null, null, null, true)
        });

        await page.GotoAsync($"/reports/dashboard?favoriteId={favorite.Id}&edit=false");
        var table = page.Locator("table.report-table");
        await table.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 30000 });

        // the netted report shows a single Security:{id} row with the netted cell value 90 (= 100 - 10);
        // the buy posting is not part of the dividend report
        var row = table.Locator("tr.entity-row").Filter(new LocatorFilterOptions { HasTextString = "E2E Sec" });
        await row.WaitForAsync();
        (await row.CountAsync()).Should().Be(1);

        await row.Locator("td.amt-current button").ClickAsync();
        var overlay = await ExpectOverlayAsync(page, expectedRows: 2, expectedAmountSum: 90m, "E2E Dividende", "E2E Gebühr");
        await CloseOverlayAsync(overlay);
    }

    private async Task<ReportFavoriteDto> CreateFavoriteAsync(IPage page, ReportFavoriteCreateApiRequest request)
    {
        var favorite = await BrowserApiHelper.PostJsonAsync<ReportFavoriteCreateApiRequest, ReportFavoriteDto>(page, "/api/report-favorites", request);
        favorite.Should().NotBeNull();
        return favorite;
    }

    /// <summary>
    /// Waits for the postings overlay to render its rows, asserts the row count, the presence of the
    /// expected text fragments and the footer sum, and returns the overlay locator for further use.
    /// The amount check is culture-proof: the footer text is reduced to digits and minus signs, so
    /// "1.497,00", "1,497.00" and "1 497,00" all match an expected sum of 1497.
    /// Also asserts the dialog layout: it must use the available viewport width (up to 1100 px)
    /// instead of the generic 520 px modal cap, and its content must never overflow horizontally.
    /// </summary>
    /// <param name="page">Playwright page containing the report dashboard.</param>
    /// <param name="expectedRows">Expected number of posting rows.</param>
    /// <param name="expectedAmountSum">Expected exact footer sum.</param>
    /// <param name="expectedTexts">Text fragments expected in the overlay.</param>
    /// <returns>The overlay locator for further assertions.</returns>
    private static async Task<ILocator> ExpectOverlayAsync(IPage page, int expectedRows, decimal expectedAmountSum, params string[] expectedTexts)
    {
        var overlay = page.Locator(".modal-overlay");
        var rows = overlay.Locator("table tbody tr");
        await rows.First.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15000 });
        (await rows.CountAsync()).Should().Be(expectedRows);

        var metrics = await overlay.Locator(".postings-modal").EvaluateAsync<PostingsDialogMetrics>(
            @"el => {
                const rem = parseFloat(getComputedStyle(document.documentElement).fontSize) || 16;
                return {
                    scrollWidth: el.scrollWidth,
                    clientWidth: el.clientWidth,
                    width: el.getBoundingClientRect().width,
                    expectedWidth: Math.min(1100, window.innerWidth - 2.5 * rem)
                };
            }");
        metrics.ScrollWidth.Should().BeLessThanOrEqualTo(metrics.ClientWidth + 1, "the dialog content must not overflow horizontally");
        metrics.Width.Should().BeApproximately(metrics.ExpectedWidth, 2, "the dialog should use the available viewport width");

        var overlayText = await overlay.InnerTextAsync();
        foreach (var text in expectedTexts)
        {
            overlayText.Should().Contain(text);
        }

        var footerText = await overlay.Locator("tfoot").InnerTextAsync();
        var digits = new string(footerText.Where(c => char.IsDigit(c) || c == '-').ToArray());
        var expectedDigits = ((long)(expectedAmountSum * 100m)).ToString();
        digits.Should().Contain(expectedDigits);

        return overlay;
    }

    private static async Task CloseOverlayAsync(ILocator overlay)
    {
        await overlay.Locator(".projection-detail-header button").ClickAsync();
        await overlay.WaitForAsync(new() { State = WaitForSelectorState.Hidden });
    }

    private AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={_fixture.DatabasePath}")
            .Options;
        return new AppDbContext(options);
    }

    private sealed class PostingsDialogMetrics
    {
        public double ScrollWidth { get; set; }
        public double ClientWidth { get; set; }
        public double Width { get; set; }
        public double ExpectedWidth { get; set; }
    }
}
