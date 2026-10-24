using Microsoft.Playwright;

namespace FinanceManager.Tests.E2E;

/// <summary>
/// End-to-end tests for the setup card's dirty detection: editing a field in the
/// "Kontoauszüge" or "Renditeanalyse" sections must enable the global "Save" ribbon
/// button, and restoring the saved value must disable it again. Regression coverage
/// for the bug where several fields never activated the Save button (issue #431).
/// </summary>
[Collection(PlaywrightCollection.CollectionName)]
public sealed class SetupDirtyDetectionPlaywrightTests
{
    private readonly PlaywrightWebAppFixture _fixture;

    /// <summary>
    /// Initializes a new instance of the <see cref="SetupDirtyDetectionPlaywrightTests"/> class.
    /// </summary>
    /// <param name="fixture">Shared Playwright web app fixture providing the browser and test server.</param>
    public SetupDirtyDetectionPlaywrightTests(PlaywrightWebAppFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// Verifies that editing every field of the "Kontoauszüge" (statements) section toggles the
    /// Save button: each change enables it, restoring the saved value disables it again.
    /// </summary>
    /// <param name="locale">The browser language used for setup section titles.</param>
    [Theory]
    [InlineData("de-DE")]
    [InlineData("en-US")]
    public async Task Statements_Fields_ToggleSaveButton(string locale)
    {
        var session = await LoginAsAdminAsync(locale);
        await using var sessionGuard = session;
        var page = session.Page;

        await page.GotoAsync("/card/setup");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var toggle = page.Locator("button.setup-section-toggle").Filter(new LocatorFilterOptions { HasTextRegex = new System.Text.RegularExpressions.Regex("^(Kontoauszüge|Statements)") });
        (await toggle.CountAsync()).Should().Be(1);
        await ExpandSectionAsync(page, toggle);

        var section = page.Locator("section.setup-section-accordion-item").Filter(new LocatorFilterOptions { Has = toggle });
        var saveButton = page.Locator("#Save");

        await section.Locator(".setup-statement-tab select").First.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 15000 });
        await Microsoft.Playwright.Assertions.Expect(saveButton).ToBeDisabledAsync(new LocatorAssertionsToBeDisabledOptions { Timeout = 5000 });

        // Select fields: switch to another option, then back.
        var selects = section.Locator("select");
        for (var i = 0; i < await selects.CountAsync(); i++)
        {
            var sel = selects.Nth(i);
            if (!await sel.IsVisibleAsync()) { continue; }
            var original = await sel.InputValueAsync();
            string? alternative = null;
            foreach (var option in await sel.Locator("option").AllAsync())
            {
                var v = await option.GetAttributeAsync("value");
                if (v != original) { alternative = v; break; }
            }
            if (alternative is null) { continue; }

            await sel.SelectOptionAsync(alternative);
            await Microsoft.Playwright.Assertions.Expect(saveButton).ToBeEnabledAsync(new LocatorAssertionsToBeEnabledOptions { Timeout = 5000 });
            await sel.SelectOptionAsync(original);
            await Microsoft.Playwright.Assertions.Expect(saveButton).ToBeDisabledAsync(new LocatorAssertionsToBeDisabledOptions { Timeout = 5000 });
        }

        // Number fields: change to a different value, then back.
        var numbers = section.Locator("input[type=number]");
        for (var i = 0; i < await numbers.CountAsync(); i++)
        {
            var num = numbers.Nth(i);
            if (!await num.IsVisibleAsync()) { continue; }
            var original = await num.InputValueAsync();
            var alternative = (int.Parse(original, System.Globalization.CultureInfo.InvariantCulture) + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);

            await num.FillAsync(alternative);
            await num.PressAsync("Tab");
            await Microsoft.Playwright.Assertions.Expect(saveButton).ToBeEnabledAsync(new LocatorAssertionsToBeEnabledOptions { Timeout = 5000 });
            await num.FillAsync(original);
            await num.PressAsync("Tab");
            await Microsoft.Playwright.Assertions.Expect(saveButton).ToBeDisabledAsync(new LocatorAssertionsToBeDisabledOptions { Timeout = 5000 });
        }

        // Checkbox fields: toggle, then back.
        var checkboxes = section.Locator("input[type=checkbox]");
        for (var i = 0; i < await checkboxes.CountAsync(); i++)
        {
            var cb = checkboxes.Nth(i);
            if (!await cb.IsVisibleAsync()) { continue; }
            var original = await cb.IsCheckedAsync();

            await cb.SetCheckedAsync(!original);
            await Microsoft.Playwright.Assertions.Expect(saveButton).ToBeEnabledAsync(new LocatorAssertionsToBeEnabledOptions { Timeout = 5000 });
            await cb.SetCheckedAsync(original);
            await Microsoft.Playwright.Assertions.Expect(saveButton).ToBeDisabledAsync(new LocatorAssertionsToBeDisabledOptions { Timeout = 5000 });
        }
    }

    /// <summary>
    /// Verifies that editing every field of the "Renditeanalyse" (return analysis) section toggles
    /// the Save button: each change enables it, restoring the saved value disables it again.
    /// </summary>
    /// <param name="locale">The browser language used for setup section titles.</param>
    [Theory]
    [InlineData("de-DE")]
    [InlineData("en-US")]
    public async Task ReturnAnalysis_Fields_ToggleSaveButton(string locale)
    {
        var session = await LoginAsAdminAsync(locale);
        await using var sessionGuard = session;
        var page = session.Page;

        await page.GotoAsync("/card/setup");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var toggle = page.Locator("button.setup-section-toggle").Filter(new LocatorFilterOptions { HasTextRegex = new System.Text.RegularExpressions.Regex("^(Renditeanalyse|Return analysis)", System.Text.RegularExpressions.RegexOptions.IgnoreCase) });
        (await toggle.CountAsync()).Should().Be(1);
        await ExpandSectionAsync(page, toggle);

        var section = page.Locator("section.setup-section-accordion-item").Filter(new LocatorFilterOptions { Has = toggle });
        var saveButton = page.Locator("#Save");
        await section.Locator("#benchmark-select").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 15000 });
        await Microsoft.Playwright.Assertions.Expect(saveButton).ToBeDisabledAsync(new LocatorAssertionsToBeDisabledOptions { Timeout = 5000 });

        // Sharpe-Ratio checkbox toggles the Save button.
        var sharpe = section.Locator("#show-sharpe");
        var sharpeOriginal = await sharpe.IsCheckedAsync();
        await sharpe.SetCheckedAsync(!sharpeOriginal);
        await Microsoft.Playwright.Assertions.Expect(saveButton).ToBeEnabledAsync(new LocatorAssertionsToBeEnabledOptions { Timeout = 5000 });
        await sharpe.SetCheckedAsync(sharpeOriginal);
        await Microsoft.Playwright.Assertions.Expect(saveButton).ToBeDisabledAsync(new LocatorAssertionsToBeDisabledOptions { Timeout = 5000 });

        // Risk-free rate number field toggles the Save button.
        var riskFree = section.Locator("#risk-free");
        var riskFreeOriginal = await riskFree.InputValueAsync();
        await riskFree.FillAsync("0.05");
        await riskFree.PressAsync("Tab");
        await Microsoft.Playwright.Assertions.Expect(saveButton).ToBeEnabledAsync(new LocatorAssertionsToBeEnabledOptions { Timeout = 5000 });
        await riskFree.FillAsync(riskFreeOriginal);
        await riskFree.PressAsync("Tab");
        await Microsoft.Playwright.Assertions.Expect(saveButton).ToBeDisabledAsync(new LocatorAssertionsToBeDisabledOptions { Timeout = 5000 });

        // Benchmark select toggles the Save button (a seeded security is required for a non-empty option).
        var benchmark = section.Locator("#benchmark-select");
        var benchmarkOriginal = await benchmark.InputValueAsync();
        string? alternative = null;
        foreach (var option in await benchmark.Locator("option").AllAsync())
        {
            var v = await option.GetAttributeAsync("value");
            if (!string.IsNullOrEmpty(v) && v != benchmarkOriginal) { alternative = v; break; }
        }
        if (!string.IsNullOrEmpty(alternative))
        {
            await benchmark.SelectOptionAsync(alternative);
            await Microsoft.Playwright.Assertions.Expect(saveButton).ToBeEnabledAsync(new LocatorAssertionsToBeEnabledOptions { Timeout = 5000 });
            await benchmark.SelectOptionAsync(benchmarkOriginal);
            await Microsoft.Playwright.Assertions.Expect(saveButton).ToBeDisabledAsync(new LocatorAssertionsToBeDisabledOptions { Timeout = 5000 });
        }
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

    private async Task<PlaywrightBrowserSession> LoginAsAdminAsync(string locale)
    {
        var session = await _fixture.CreateSessionAsync(new() { Locale = locale });
        var page = session.Page;
        var auth = new AuthGateway(page, _fixture.BaseUrl);
        var seed = new TestUserSeeder(_fixture.DatabasePath);

        var username = $"setup-dirty-{Guid.NewGuid():N}";
        const string password = "Secret123";
        await seed.EnsureUserAsync(username, password, isAdmin: true);
        await auth.LoginAsync(username, password);
        return session;
    }
}
