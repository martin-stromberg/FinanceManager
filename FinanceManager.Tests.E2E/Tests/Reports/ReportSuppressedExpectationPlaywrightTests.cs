using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Aggregates;
using FinanceManager.Shared.Dtos.Postings;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Tests.E2E;

/// <summary>
/// End-to-end tests for per-favorite suppression of expected postings in the projection
/// overlay (Issue #534): within a favorite-scoped report each expected item offers a
/// "Nicht mehr erwarten"/"Stop expecting" action that moves it into the "Ausgeblendete
/// Erwartungen"/"Hidden expectations" list, from where "Wieder einbeziehen"/"Expect again"
/// restores the original projection. These drive a real browser + app instance; they are not
/// run in unit CI.
/// </summary>
[Collection(PlaywrightCollection.CollectionName)]
public sealed class ReportSuppressedExpectationPlaywrightTests
{
    private readonly PlaywrightWebAppFixture _fixture;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportSuppressedExpectationPlaywrightTests"/> class.
    /// </summary>
    /// <param name="fixture">Shared Playwright web app fixture providing the browser and test server.</param>
    public ReportSuppressedExpectationPlaywrightTests(PlaywrightWebAppFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<(PlaywrightBrowserSession session, IPage page, Guid userId)> LoginAsync(string userPrefix)
    {
        var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        var auth = new AuthGateway(page, _fixture.BaseUrl);
        var seed = new TestUserSeeder(_fixture.DatabasePath);
        var username = $"{userPrefix}-{Guid.NewGuid():N}";
        const string password = "Secret123";
        var user = await seed.EnsureUserAsync(username, password);
        await auth.LoginAsync(username, password);
        return (session, page, user.Id);
    }

    /// <summary>
    /// Seeds an account with a single posting of the same month one year ago and rebuilds the
    /// posting aggregates so the report's projection recognizes an annual expectation for the
    /// current (still unbooked) month.
    /// </summary>
    /// <param name="page">Playwright page used for the API seed helper.</param>
    /// <param name="userId">Id of the seeded test user owning the account.</param>
    /// <returns>Name of the seeded account.</returns>
    private async Task<string> SeedAccountWithPriorYearPostingAsync(IPage page, Guid userId)
    {
        var accountName = $"E2E Konto {Guid.NewGuid():N}";
        var priorYearDate = new DateTime(DateTime.UtcNow.Year - 1, DateTime.UtcNow.Month, 10);
        await new AccountsApiSeedHelper(page, _fixture.DatabasePath, userId).CreateAccountWithBalanceAsync(
            accountName,
            $"DE507005000000{Random.Shared.Next(10000000, 99999999)}",
            AccountType.Giro,
            0m,
            "E2E Bank",
            new[]
            {
                new AccountsApiSeedHelper.SeededPosting(priorYearDate, 120m)
            });
        await using var db = OpenDb();
        await new PostingAggregateService(db).RebuildForUserAsync(userId, (_, _) => { }, CancellationToken.None);
        return accountName;
    }

    private AppDbContext OpenDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={_fixture.DatabasePath}")
            .Options;
        return new AppDbContext(options);
    }

    private static string GetRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 4; i++)
        {
            dir = dir.Parent ?? throw new InvalidOperationException("Unable to resolve repository root.");
        }

        return dir.FullName;
    }

    /// <summary>
    /// Within a favorite-scoped report, the expected-items overlay offers a per-item
    /// "Stop expecting" action that moves the item into the hidden-expectations list; the
    /// "Expect again" action restores it and the projection returns to its original value.
    /// </summary>
    [Fact]
    public async Task SuppressExpectedItem_MovesItToHiddenList_AndUnsuppressRestores()
    {
        var (session, page, userId) = await LoginAsync("suppression-ui-user");
        await using var _ = session;
        var accountName = await SeedAccountWithPriorYearPostingAsync(page, userId);

        var favoriteName = $"Suppression Fav {Guid.NewGuid():N}";
        var created = await BrowserApiHelper.PostJsonAsync<ReportFavoriteCreateApiRequest, ReportFavoriteDto>(page, "/api/report-favorites", new ReportFavoriteCreateApiRequest
        {
            Name = favoriteName,
            PostingKind = 0,
            IncludeCategory = false,
            Interval = 0,
            Take = 6,
            ComparePrevious = false,
            CompareYear = false,
            CompareProjection = true,
            ShowChart = false,
            Expandable = false,
            UseValutaDate = false
        });
        created.Should().NotBeNull();

        await page.GotoAsync($"/reports/dashboard?favoriteId={created!.Id}&edit=false");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var infoButton = page.Locator(".projection-info-btn").First;
        await infoButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 30000 });
        await infoButton.ClickAsync();

        var overlay = page.Locator(".projection-detail-overlay");
        await overlay.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        var expectedRow = overlay.Locator(".projection-detail-table tbody tr", new() { HasText = accountName }).First;
        await expectedRow.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 30000 });

        // Suppress the expected item via the per-row action.
        var suppressButton = overlay.Locator(".projection-suppress-btn").First;
        await suppressButton.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        await suppressButton.ClickAsync();

        // The item leaves the active list and appears in the hidden-expectations list.
        var unsuppressButton = overlay.Locator(".projection-unsuppress-btn").First;
        await unsuppressButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 30000 });
        var hiddenSection = overlay.Locator(".projection-suppressed-title");
        (await hiddenSection.CountAsync()).Should().Be(1, "a separate hidden-expectations list must exist");
        var hiddenRow = overlay.Locator(".projection-suppressed-table tbody tr", new() { HasText = accountName });
        (await hiddenRow.CountAsync()).Should().Be(1, "the suppressed item must appear in the hidden list");
        var activeRow = overlay.Locator(".projection-detail-table:not(.projection-suppressed-table) tbody tr", new() { HasText = accountName });
        (await activeRow.CountAsync()).Should().Be(0, "the suppressed item must leave the active list");

        var artifactDir = Path.Combine(GetRepoRoot(), "TestResults", "E2E", "artifacts");
        Directory.CreateDirectory(artifactDir);
        await overlay.ScreenshotAsync(new() { Path = Path.Combine(artifactDir, "projection-suppressed-overlay.png") });

        // Restore the expectation.
        await unsuppressButton.ClickAsync();
        await hiddenRow.WaitForAsync(new() { State = WaitForSelectorState.Detached, Timeout = 30000 });

        // Reopening the overlay shows the expectation again with the original values.
        await overlay.Locator(".icon-btn").First.ClickAsync();
        await page.Locator(".projection-info-btn").First.ClickAsync();
        var overlay2 = page.Locator(".projection-detail-overlay");
        await overlay2.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        var restoredRow = overlay2.Locator(".projection-detail-table:not(.projection-suppressed-table) tbody tr", new() { HasText = accountName });
        await restoredRow.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 30000 });
    }

    /// <summary>
    /// Without an active favorite the overlay offers no suppression actions — ad-hoc reports
    /// are unaffected by the feature.
    /// </summary>
    [Fact]
    public async Task SuppressAction_RequiresFavoriteContext()
    {
        var (session, page, userId) = await LoginAsync("suppression-nofav-user");
        await using var _ = session;
        var accountName = await SeedAccountWithPriorYearPostingAsync(page, userId);

        await page.GotoAsync("/reports/dashboard?edit=true");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        var checkbox = page.Locator("label")
            .Filter(new LocatorFilterOptions { HasTextRegex = new System.Text.RegularExpressions.Regex("Hochrechnung|Projection") })
            .Locator("input[type=checkbox]");
        await checkbox.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 30000 });
        await checkbox.CheckAsync();
        await page.Locator("#Reload").ClickAsync();

        var infoButton = page.Locator(".projection-info-btn").First;
        await infoButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 30000 });
        await infoButton.ClickAsync();

        var overlay = page.Locator(".projection-detail-overlay");
        await overlay.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        (await overlay.InnerTextAsync()).Should().Contain(accountName);
        (await overlay.Locator(".projection-suppress-btn").CountAsync()).Should().Be(0, "ad-hoc reports must not offer suppression");
        (await overlay.Locator(".projection-suppressed-title").CountAsync()).Should().Be(0, "ad-hoc reports must not show a hidden-expectations list");
    }
}
