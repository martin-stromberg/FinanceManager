using System.Text.RegularExpressions;
using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Aggregates;
using FinanceManager.Shared.Dtos.Postings;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Tests.E2E;

/// <summary>
/// End-to-end tests for the kind-neutral report projection ("Hochrechnung"): enabling the
/// projection checkbox on a non-security report renders the projection column, the expected-item
/// info button opens the detail overlay with the entity's expected posting, and a favorite with
/// <c>CompareProjection</c> persists the same flow across a fresh dashboard load.
/// These drive a real browser + app instance; they are not run in unit CI.
/// </summary>
[Collection(PlaywrightCollection.CollectionName)]
public sealed class ReportProjectionPlaywrightTests
{
    private readonly PlaywrightWebAppFixture _fixture;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportProjectionPlaywrightTests"/> class.
    /// </summary>
    /// <param name="fixture">Shared Playwright web app fixture providing the browser and test server.</param>
    public ReportProjectionPlaywrightTests(PlaywrightWebAppFixture fixture)
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
    /// Seeds an account with a single recurring posting of the same month one year ago and rebuilds
    /// the posting aggregates so the report's projection recognizes an annual expectation for the
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

        await RebuildAggregatesAsync(userId);
        return accountName;
    }

    private static ILocator ProjectionCheckbox(IPage page)
    {
        // The projection option lives in the comparisons filter group; match it by its localized
        // label so the selector works in both UI languages.
        return page.Locator("label")
            .Filter(new LocatorFilterOptions { HasTextRegex = new Regex("Hochrechnung|Projection") })
            .Locator("input[type=checkbox]");
    }

    /// <summary>
    /// Selects an interval value and waits until the projection checkbox reached the expected
    /// enabled state. Select changes dispatched before the interactive Blazor circuit has
    /// connected are lost while the DOM keeps the selection; toggling away and back re-raises a
    /// fresh change event once the circuit is up.
    /// </summary>
    /// <param name="intervalSelect">Locator of the interval select element.</param>
    /// <param name="checkbox">Locator of the projection checkbox.</param>
    /// <param name="intervalValue">Interval option value to select.</param>
    /// <param name="expectEnabled">Expected enabled state of the projection checkbox.</param>
    /// <returns>A task that completes once the checkbox reached the expected state or retries ran out.</returns>
    private static async Task SelectIntervalUntilProjectionEnabledAsync(ILocator intervalSelect, ILocator checkbox, string intervalValue, bool expectEnabled)
    {
        var ct = TestContext.Current.CancellationToken;
        for (var attempt = 0; attempt < 100 && await checkbox.IsEnabledAsync() != expectEnabled; attempt++)
        {
            if (attempt == 0)
            {
                await intervalSelect.SelectOptionAsync(intervalValue);
            }
            else if (attempt % 20 == 19)
            {
                // Re-fire the change event: toggle to the opposite interval first so selecting the
                // target value again raises a real change for the (now connected) circuit.
                await intervalSelect.SelectOptionAsync(expectEnabled ? "AllHistory" : "Month");
                await Task.Delay(150, ct);
                await intervalSelect.SelectOptionAsync(intervalValue);
            }
            await Task.Delay(200, ct);
        }
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

    private async Task RebuildAggregatesAsync(Guid userId)
    {
        await using var db = OpenDb();
        await new PostingAggregateService(db).RebuildForUserAsync(userId, (_, _) => { }, CancellationToken.None);
    }

    /// <summary>
    /// Seeds a contact with a recurring prior-year posting directly in the E2E database.
    /// </summary>
    /// <param name="userId">Id of the seeded test user owning the contact.</param>
    /// <returns>Name of the seeded contact.</returns>
    private async Task<string> SeedContactWithPriorYearPostingAsync(Guid userId)
    {
        var contactName = $"E2E Kontakt {Guid.NewGuid():N}";
        var priorYearDate = new DateTime(DateTime.UtcNow.Year - 1, DateTime.UtcNow.Month, 10);
        await using var db = OpenDb();
        var contact = new FinanceManager.Domain.Contacts.Contact(userId, contactName, ContactType.Person, null, null);
        db.Contacts.Add(contact);
        db.Postings.Add(new FinanceManager.Domain.Postings.Posting(
            Guid.NewGuid(), PostingKind.Contact, null, contact.Id, null, null, priorYearDate, 80m));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return contactName;
    }

    /// <summary>
    /// Seeds a security with a positive holding (buy) and a prior-year dividend group so the
    /// netted-dividend path projects an expectation for the current month.
    /// </summary>
    /// <param name="userId">Id of the seeded test user owning the security.</param>
    /// <returns>Name of the seeded security.</returns>
    private async Task<string> SeedSecurityWithDividendAsync(Guid userId)
    {
        var securityName = $"E2E Wertpapier {Guid.NewGuid():N}";
        var priorYearDate = new DateTime(DateTime.UtcNow.Year - 1, DateTime.UtcNow.Month, 10);
        await using var db = OpenDb();
        var security = new FinanceManager.Domain.Securities.Security(userId, securityName, $"E2E-{Guid.NewGuid():N}", null, null, "EUR", null);
        db.Securities.Add(security);
        var dividendGroup = Guid.NewGuid();
        db.Postings.Add(new FinanceManager.Domain.Postings.Posting(
            Guid.NewGuid(), PostingKind.Security, null, null, null, security.Id,
            priorYearDate, priorYearDate, 90m, "E2E Dividend", null, null,
            FinanceManager.Shared.Dtos.Securities.SecurityPostingSubType.Dividend)
            .SetGroup(dividendGroup));
        db.Postings.Add(new FinanceManager.Domain.Postings.Posting(
            Guid.NewGuid(), PostingKind.Security, null, null, null, security.Id,
            priorYearDate.AddYears(-1), priorYearDate.AddYears(-1), -500m, "E2E Buy", null, null,
            FinanceManager.Shared.Dtos.Securities.SecurityPostingSubType.Buy, 10m));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return securityName;
    }

    /// <summary>
    /// On a bank report the projection checkbox is offered (it was security-only before this
    /// feature): enabling it and re-running the report renders the projection column with an
    /// expected-item info button whose overlay lists the account's expected posting.
    /// </summary>
    [Fact]
    public async Task CompareProjection_CheckboxEnabledForBankReport_ShowsExpectedItemsOverlay()
    {
        var (session, page, userId) = await LoginAsync("projection-ui-user");
        await using var _ = session;
        var accountName = await SeedAccountWithPriorYearPostingAsync(page, userId);

        await page.GotoAsync("/reports/dashboard?edit=true");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        var checkbox = ProjectionCheckbox(page);
        await checkbox.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 30000 });
        (await checkbox.IsEnabledAsync()).Should().BeTrue("the projection option must be offered for non-security posting kinds");

        await checkbox.CheckAsync();

        // Checking the option alone does not reload; the ribbon "Reload" action re-runs the
        // report with the synced projection option.
        await page.Locator("#Reload").ClickAsync();

        var header = page.Locator("th.th-projection").First;
        await header.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 30000 });

        var infoButton = page.Locator(".projection-info-btn").First;
        await infoButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 30000 });
        await infoButton.ClickAsync();

        var overlay = page.Locator(".projection-detail-overlay");
        await overlay.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        var overlayText = await overlay.InnerTextAsync();
        overlayText.Should().Contain(accountName);
    }

    /// <summary>
    /// Regression: the expected-items overlay must use the available viewport width and keep all
    /// table cells inside the table — no horizontal overflow and no overlapping cell content.
    /// The overlay is additionally captured as an artifact for visual inspection.
    /// </summary>
    [Fact]
    public async Task ProjectionDetailsOverlay_RendersWithoutHorizontalOverflowOrOverlappingCells()
    {
        var (session, page, userId) = await LoginAsync("projection-overflow-user");
        await using var _ = session;
        var accountName = await SeedAccountWithPriorYearPostingAsync(page, userId);

        var favoriteName = $"Projection Overlay {Guid.NewGuid():N}";
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
        (await overlay.InnerTextAsync()).Should().Contain(accountName);

        var modal = page.Locator(".projection-detail-modal");
        var modalOverflow = await modal.EvaluateAsync<int>("el => el.scrollWidth - el.clientWidth");
        modalOverflow.Should().BeLessThanOrEqualTo(1, "the expected-items dialog must not overflow the viewport horizontally");

        var escapedCells = await overlay.Locator(".projection-detail-table").EvaluateAsync<int>(@"table => {
            const box = table.getBoundingClientRect();
            return Array.from(table.querySelectorAll('th, td'))
                .filter(c => c.getBoundingClientRect().right > box.right + 1 || c.getBoundingClientRect().left < box.left - 1)
                .length;
        }");
        escapedCells.Should().Be(0, "every table cell must stay inside the table bounds");

        var artifactDir = Path.Combine(GetRepoRoot(), "TestResults", "E2E", "artifacts");
        Directory.CreateDirectory(artifactDir);
        await overlay.ScreenshotAsync(new() { Path = Path.Combine(artifactDir, "projection-detail-overlay.png") });
    }

    /// <summary>
    /// The projection checkbox stays disabled for the AllHistory interval and becomes enabled
    /// again when switching back to a projecting interval.
    /// </summary>
    [Fact]
    public async Task CompareProjection_CheckboxDisabledForAllHistory()
    {
        var (session, page, userId) = await LoginAsync("projection-allhist-user");
        await using var _ = session;
        await SeedAccountWithPriorYearPostingAsync(page, userId);

        await page.GotoAsync("/reports/dashboard?edit=true");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        var checkbox = ProjectionCheckbox(page);
        await checkbox.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 30000 });
        (await checkbox.IsEnabledAsync()).Should().BeTrue();

        var intervalSelect = page.Locator(".filter-group select").First;
        await SelectIntervalUntilProjectionEnabledAsync(intervalSelect, checkbox, "AllHistory", expectEnabled: false);
        (await checkbox.IsEnabledAsync()).Should().BeFalse("AllHistory never projects");

        await SelectIntervalUntilProjectionEnabledAsync(intervalSelect, checkbox, "Month", expectEnabled: true);
        (await checkbox.IsEnabledAsync()).Should().BeTrue();
    }

    /// <summary>
    /// A favorite saved with <c>CompareProjection</c> reopens the dashboard with the projection
    /// column and the expected-item overlay — the persisted happy path of the feature.
    /// </summary>
    [Fact]
    public async Task FavoriteWithCompareProjection_ShowsProjectionColumnAndDetails()
    {
        var (session, page, userId) = await LoginAsync("projection-fav-user");
        await using var _ = session;
        var accountName = await SeedAccountWithPriorYearPostingAsync(page, userId);

        var favoriteName = $"Projection Fav {Guid.NewGuid():N}";
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

        var header = page.Locator("th.th-projection").First;
        await header.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 30000 });

        var infoButton = page.Locator(".projection-info-btn").First;
        await infoButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 30000 });
        await infoButton.ClickAsync();

        var overlay = page.Locator(".projection-detail-overlay");
        await overlay.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        var overlayText = await overlay.InnerTextAsync();
        overlayText.Should().Contain(accountName);
    }

    /// <summary>
    /// A multi-kind favorite (Bank + Contact) with <c>CompareProjection</c> projects both kinds:
    /// the type rows of the expanded grid carry the projected values of their entities.
    /// </summary>
    [Fact]
    public async Task FavoriteWithCompareProjection_MultiKind_ShowsPerKindValues()
    {
        var (session, page, userId) = await LoginAsync("projection-multi-user");
        await using var _ = session;
        var accountName = await SeedAccountWithPriorYearPostingAsync(page, userId);
        await SeedContactWithPriorYearPostingAsync(userId);
        await RebuildAggregatesAsync(userId);

        var favoriteName = $"Projection Multi {Guid.NewGuid():N}";
        var created = await BrowserApiHelper.PostJsonAsync<ReportFavoriteCreateApiRequest, ReportFavoriteDto>(page, "/api/report-favorites", new ReportFavoriteCreateApiRequest
        {
            Name = favoriteName,
            PostingKind = 0,
            PostingKinds = new[] { PostingKind.Bank, PostingKind.Contact },
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

        var header = page.Locator("th.th-projection").First;
        await header.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 30000 });

        // One type row per selected kind; both carry projected values (and thus info buttons).
        var typeRows = page.Locator("tr.type-row");
        (await typeRows.CountAsync()).Should().Be(2);
        var infoButtons = typeRows.Locator(".projection-info-btn");
        (await infoButtons.CountAsync()).Should().Be(2);
        await infoButtons.First.ClickAsync();

        var overlay = page.Locator(".projection-detail-overlay");
        await overlay.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        var overlayText = await overlay.InnerTextAsync();
        overlayText.Should().Contain(accountName);
    }

    /// <summary>
    /// Regression: a security favorite with <c>CompareProjection</c> still renders the projection
    /// column and the (now kind-neutral) expected-item overlay for the dividend expectation.
    /// </summary>
    [Fact]
    public async Task FavoriteWithCompareProjection_SecurityFavorite_StillShowsOverlay()
    {
        var (session, page, userId) = await LoginAsync("projection-sec-user");
        await using var _ = session;
        var securityName = await SeedSecurityWithDividendAsync(userId);
        await RebuildAggregatesAsync(userId);

        var favoriteName = $"Projection Sec {Guid.NewGuid():N}";
        var created = await BrowserApiHelper.PostJsonAsync<ReportFavoriteCreateApiRequest, ReportFavoriteDto>(page, "/api/report-favorites", new ReportFavoriteCreateApiRequest
        {
            Name = favoriteName,
            PostingKind = PostingKind.Security,
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

        var header = page.Locator("th.th-projection").First;
        await header.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 30000 });

        var infoButton = page.Locator(".projection-info-btn").First;
        await infoButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 30000 });
        await infoButton.ClickAsync();

        var overlay = page.Locator(".projection-detail-overlay");
        await overlay.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        var overlayText = await overlay.InnerTextAsync();
        overlayText.Should().Contain(securityName);
    }
}
