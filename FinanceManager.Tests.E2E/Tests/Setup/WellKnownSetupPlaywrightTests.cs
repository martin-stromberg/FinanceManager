using System.Net;
using FinanceManager.Domain.WellKnown;
using FinanceManager.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Tests.E2E;

/// <summary>
/// End-to-end tests for the admin-only setup "Well-Known" tab: configuring the change-password
/// redirect target and verifying the public <c>/.well-known/change-password</c> endpoint follows
/// the configured value, plus the section visibility for non-admin users.
/// </summary>
[Collection(PlaywrightCollection.CollectionName)]
public sealed class WellKnownSetupPlaywrightTests
{
    private readonly PlaywrightWebAppFixture _fixture;

    /// <summary>
    /// Initializes a new instance of the <see cref="WellKnownSetupPlaywrightTests"/> class.
    /// </summary>
    /// <param name="fixture">Shared Playwright web app fixture providing the browser and test server.</param>
    public WellKnownSetupPlaywrightTests(PlaywrightWebAppFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// Verifies that an admin can edit the change-password URL in the setup tab, save it, and that
    /// the public well-known endpoint then redirects to the configured target.
    /// </summary>
    [Fact]
    public async Task Admin_ConfiguresChangePasswordUrl_PublicEndpointRedirects()
    {
        var (session, _) = await LoginAsAdminAsync();
        await using var sessionGuard = session;
        var page = session.Page;

        // Ensure the settings row exists before the page calls the admin API: on a fresh E2E
        // database the server's first read would otherwise INSERT the default row itself, which
        // can intermittently hit a SQLite lock left behind by the just-finished user seeding and
        // turn the tab's load into an error instead of rendering the form.
        await EnsureWellKnownSettingsRowAsync();

        await page.GotoAsync("/card/setup");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var toggle = page.Locator("button.setup-section-toggle").Filter(new LocatorFilterOptions { HasText = "Well-Known" });
        (await toggle.CountAsync()).Should().Be(1);
        await ExpandSectionAsync(page, toggle);

        // Wait for the tab to settle: either the edit field or a load error must appear.
        var input = page.Locator("#wellknown-changepasswordurl");
        var errorBox = page.Locator(".setup-wellknown-tab div.error");
        await page.Locator("#wellknown-changepasswordurl, .setup-wellknown-tab div.error").First
            .WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 15000 });
        if (await errorBox.CountAsync() > 0)
        {
            var errorText = await errorBox.InnerTextAsync();
            errorText.Should().BeNullOrWhiteSpace($"the well-known setup tab reported a load error: {errorText}");
        }

        await input.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 5000 });

        var saveButton = page.Locator("#Save");
        await page.Locator("#wellknown-changepasswordurl").FillAsync("/account/password");
        await page.Locator("#wellknown-changepasswordurl").PressAsync("Tab");

        await Microsoft.Playwright.Assertions.Expect(saveButton).ToBeEnabledAsync(new LocatorAssertionsToBeEnabledOptions { Timeout = 5000 });
        await saveButton.ClickAsync();

        // Wait for the tab's save-success indicator instead of the save button's disabled state:
        // the button is already disabled while Saving=true is in flight, so it cannot be used to
        // detect that the PUT actually committed.
        var saveResult = page.Locator(".setup-wellknown-tab div.info, .setup-wellknown-tab div.error");
        await saveResult.First.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 15000 });
        var successBox = page.Locator(".setup-wellknown-tab div.info");
        if (await successBox.CountAsync() == 0)
        {
            var saveErrorText = await page.Locator(".setup-wellknown-tab div.error").InnerTextAsync();
            saveErrorText.Should().BeNullOrWhiteSpace($"the well-known settings save failed: {saveErrorText}");
        }

        try
        {
            using var handler = new HttpClientHandler { AllowAutoRedirect = false };
            using var client = new HttpClient(handler) { BaseAddress = new Uri(_fixture.BaseUrl) };
            var response = await client.GetAsync("/.well-known/change-password", TestContext.Current.CancellationToken);

            response.StatusCode.Should().Be(HttpStatusCode.Found);
            response.Headers.Location.Should().NotBeNull();
            response.Headers.Location!.OriginalString.Should().Be("/account/password");
        }
        finally
        {
            // Restore the default so the shared E2E database does not leak the configured
            // target into other tests of the suite.
            await ResetChangePasswordUrlAsync();
        }
    }

    /// <summary>
    /// Verifies that the "Well-Known" section is listed for non-admin users but shows the
    /// administrators-only hint instead of the edit field.
    /// </summary>
    [Fact]
    public async Task NonAdmin_SeesSectionButAdminOnlyMessage()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        var auth = new AuthGateway(page, _fixture.BaseUrl);
        var seed = new TestUserSeeder(_fixture.DatabasePath);

        var username = $"wellknown-user-{Guid.NewGuid():N}";
        const string password = "Secret123";
        await seed.EnsureUserAsync(username, password, isAdmin: false);
        await auth.LoginAsync(username, password);

        await page.GotoAsync("/card/setup");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var toggle = page.Locator("button.setup-section-toggle").Filter(new LocatorFilterOptions { HasText = "Well-Known" });
        (await toggle.CountAsync()).Should().Be(1);
        await ExpandSectionAsync(page, toggle);

        // The admin-only hint is rendered and the edit input is absent.
        var section = page.Locator("section.setup-section-accordion-item").Filter(new LocatorFilterOptions { HasText = "Well-Known" });
        await Microsoft.Playwright.Assertions.Expect(section)
            .ToContainTextAsync(AdminOnlyText, new LocatorAssertionsToContainTextOptions { Timeout = 15000 });
        (await page.Locator("#wellknown-changepasswordurl").CountAsync()).Should().Be(0);
    }

    private static readonly System.Text.RegularExpressions.Regex AdminOnlyText =
        new("Administrators only|Nur fuer Administratoren|Nur für Administratoren", System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>
    /// Clicks an accordion section toggle until <c>aria-expanded</c> reports the expanded state.
    /// Blazor renders boolean-bound attributes only when true (as an empty value) and omits them
    /// when false, so expansion is detected by attribute presence, not by its value.
    /// On a freshly loaded page the Blazor Server circuit may not be connected yet, in which case
    /// the first click is silently lost; the retry loop makes the expansion deterministic.
    /// </summary>
    /// <param name="page">The Playwright page containing the setup accordion.</param>
    /// <param name="toggle">The locator of the accordion section toggle to click.</param>
    private static async Task ExpandSectionAsync(IPage page, ILocator toggle)
    {
        for (var attempt = 0; attempt < 15; attempt++)
        {
            if (await toggle.GetAttributeAsync("aria-expanded") is not null)
            {
                return;
            }

            await toggle.ClickAsync();
            await page.WaitForTimeoutAsync(700);
        }

        (await toggle.GetAttributeAsync("aria-expanded")).Should().NotBeNull("the setup section did not expand");
    }

    private async Task ResetChangePasswordUrlAsync()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={_fixture.DatabasePath}")
            .Options;
        using var db = new AppDbContext(options);
        var entity = await db.WellKnownSettings.FirstOrDefaultAsync();
        if (entity is null)
        {
            return;
        }

        entity.Update(WellKnownSettings.DefaultChangePasswordUrl);
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Creates the singleton <see cref="WellKnownSettings"/> row in the shared test database when
    /// it does not exist yet, retrying while SQLite still reports a lock from the user seeding.
    /// </summary>
    private async Task EnsureWellKnownSettingsRowAsync()
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            try
            {
                var options = new DbContextOptionsBuilder<AppDbContext>()
                    .UseSqlite($"Data Source={_fixture.DatabasePath}")
                    .Options;
                using var db = new AppDbContext(options);
                if (!await db.WellKnownSettings.AnyAsync())
                {
                    db.WellKnownSettings.Add(WellKnownSettings.CreateDefault());
                    await db.SaveChangesAsync();
                }

                return;
            }
            catch (Exception ex) when (attempt < 9 && IsSqliteLock(ex))
            {
                await Task.Delay(300);
            }
        }
    }

    private static bool IsSqliteLock(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException!)
        {
            if (current is Microsoft.Data.Sqlite.SqliteException sqlite && sqlite.SqliteErrorCode == 5)
            {
                return true;
            }
        }

        return false;
    }

    private async Task<(PlaywrightBrowserSession session, string username)> LoginAsAdminAsync()
    {
        var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        var auth = new AuthGateway(page, _fixture.BaseUrl);
        var seed = new TestUserSeeder(_fixture.DatabasePath);

        var username = $"wellknown-admin-{Guid.NewGuid():N}";
        const string password = "Secret123";

        await seed.EnsureUserAsync(username, password, isAdmin: true);
        await auth.LoginAsync(username, password);

        return (session, username);
    }
}
