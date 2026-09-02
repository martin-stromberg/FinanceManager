namespace FinanceManager.Tests.E2E;

/// <summary>
/// End-to-end tests for the admin-only setup "security.txt" tab: editing contact/canonical/policy
/// fields and persisting them, and verifying the publicly served <c>/.well-known/security.txt</c>
/// document reflects the configured canonical URL, falling back to an API-base-address-derived
/// canonical when none is configured.
/// </summary>
[Collection(PlaywrightCollection.CollectionName)]
public sealed class SecurityTxtSetupPlaywrightTests
{
    private readonly PlaywrightWebAppFixture _fixture;

    /// <summary>
    /// Initializes a new instance of the <see cref="SecurityTxtSetupPlaywrightTests"/> class.
    /// </summary>
    /// <param name="fixture">Shared Playwright web app fixture providing the browser and test server.</param>
    public SecurityTxtSetupPlaywrightTests(PlaywrightWebAppFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// Admin sees exactly one security.txt setup tab, can enable save after editing and persists the values.
    /// </summary>
    [Fact]
    public async Task Admin_EditsSecurityTxtSettings_EnableSaveAndPersist()
    {
        await using var session = await LoginAsAdminAsync();
        var page = session.Page;

        await page.GotoAsync("/card/setup");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var securityToggle = page.Locator("button.setup-section-toggle").Filter(new LocatorFilterOptions { HasText = "security.txt" });
        (await securityToggle.CountAsync()).Should().Be(1);

        await securityToggle.ClickAsync();
        await page.Locator("#securitytxt-contact").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });

        var saveButton = page.Locator("#Save");
        (await saveButton.IsDisabledAsync()).Should().BeTrue();

        await page.Locator("#securitytxt-contact").FillAsync("mailto:security@example.org");
        await page.Locator("#securitytxt-contact").PressAsync("Tab");
        await page.Locator("#securitytxt-canonical").FillAsync("https://security.example.org/.well-known/security.txt");
        await page.Locator("#securitytxt-canonical").PressAsync("Tab");
        await page.Locator("#securitytxt-policy").FillAsync("https://example.org/security");
        await page.Locator("#securitytxt-policy").PressAsync("Tab");

        await Microsoft.Playwright.Assertions.Expect(saveButton).ToBeEnabledAsync(new LocatorAssertionsToBeEnabledOptions { Timeout = 5000 });
        await saveButton.ClickAsync();
        await Microsoft.Playwright.Assertions.Expect(saveButton).ToBeDisabledAsync(new LocatorAssertionsToBeDisabledOptions { Timeout = 5000 });

        await page.ReloadAsync();
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await securityToggle.ClickAsync();
        await page.Locator("#securitytxt-contact").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });

        (await page.Locator("#securitytxt-contact").InputValueAsync()).Should().Be("mailto:security@example.org");
        (await page.Locator("#securitytxt-canonical").InputValueAsync()).Should().Be("https://security.example.org/.well-known/security.txt");
        (await page.Locator("#securitytxt-policy").InputValueAsync()).Should().Be("https://example.org/security");
    }

    /// <summary>
    /// Verifies that editing only the canonical URL field (with the contact field also filled)
    /// enables the save button, and that the new canonical value persists across a page reload.
    /// </summary>
    [Fact]
    public async Task Admin_EditsCanonical_EnableSaveAndPersist()
    {
        await using var session = await LoginAsAdminAsync();
        var page = session.Page;

        await page.GotoAsync("/card/setup");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var securityToggle = page.Locator("button.setup-section-toggle").Filter(new LocatorFilterOptions { HasText = "security.txt" });
        await securityToggle.ClickAsync();
        await page.Locator("#securitytxt-canonical").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });

        var saveButton = page.Locator("#Save");
        await page.Locator("#securitytxt-contact").FillAsync("mailto:security+canonical@example.org");
        await page.Locator("#securitytxt-canonical").FillAsync("https://security-canonical.example.org/.well-known/security.txt");

        await Microsoft.Playwright.Assertions.Expect(saveButton).ToBeEnabledAsync(new LocatorAssertionsToBeEnabledOptions { Timeout = 5000 });
        await saveButton.ClickAsync();
        await Microsoft.Playwright.Assertions.Expect(saveButton).ToBeDisabledAsync(new LocatorAssertionsToBeDisabledOptions { Timeout = 5000 });

        await page.ReloadAsync();
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await securityToggle.ClickAsync();
        await page.Locator("#securitytxt-canonical").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });

        (await page.Locator("#securitytxt-canonical").InputValueAsync()).Should().Be("https://security-canonical.example.org/.well-known/security.txt");
    }

    /// <summary>
    /// Verifies that after saving a configured canonical URL, the publicly served
    /// <c>/.well-known/security.txt</c> document contains exactly one "Canonical:" line and that
    /// it matches the configured value.
    /// </summary>
    [Fact]
    public async Task PublicSecurityTxt_ContainsConfiguredCanonical()
    {
        await using var session = await LoginAsAdminAsync();
        var page = session.Page;

        await page.GotoAsync("/card/setup");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var securityToggle = page.Locator("button.setup-section-toggle").Filter(new LocatorFilterOptions { HasText = "security.txt" });
        await securityToggle.ClickAsync();
        await page.Locator("#securitytxt-canonical").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });

        await page.Locator("#securitytxt-contact").FillAsync("mailto:security+public@example.org");
        await page.Locator("#securitytxt-canonical").FillAsync("https://security-public.example.org/.well-known/security.txt");
        await page.Locator("#Save").ClickAsync();
        await Microsoft.Playwright.Assertions.Expect(page.Locator("#Save")).ToBeDisabledAsync(new LocatorAssertionsToBeDisabledOptions { Timeout = 5000 });

        var response = await page.GotoAsync("/.well-known/security.txt");
        var content = await response!.TextAsync();

        content.Should().Contain("Canonical: https://security-public.example.org/.well-known/security.txt");
        content.Split('\n').Count(line => line.StartsWith("Canonical: ")).Should().Be(1);
    }

    /// <summary>
    /// Verifies that when the canonical field is saved empty, the publicly served
    /// <c>/.well-known/security.txt</c> document falls back to a canonical URL derived from the
    /// API's own base address instead of leaking a previously configured value.
    /// </summary>
    [Fact]
    public async Task PublicSecurityTxt_UsesApiBaseAddressFallback_WhenCanonicalEmpty()
    {
        await using var session = await LoginAsAdminAsync();
        var page = session.Page;

        await page.GotoAsync("/card/setup");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var securityToggle = page.Locator("button.setup-section-toggle").Filter(new LocatorFilterOptions { HasText = "security.txt" });
        await securityToggle.ClickAsync();
        await page.Locator("#securitytxt-canonical").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });

        await page.Locator("#securitytxt-contact").FillAsync("mailto:security+fallback@example.org");
        await page.Locator("#securitytxt-canonical").FillAsync(string.Empty);
        await page.Locator("#Save").ClickAsync();
        await Microsoft.Playwright.Assertions.Expect(page.Locator("#Save")).ToBeDisabledAsync(new LocatorAssertionsToBeDisabledOptions { Timeout = 5000 });

        var response = await page.GotoAsync("/.well-known/security.txt");
        var content = await response!.TextAsync();
        var canonicalLine = content.Split('\n').First(line => line.StartsWith("Canonical: "));

        canonicalLine.Should().NotContain("security-public.example.org");
        canonicalLine.Should().EndWith("/.well-known/security.txt");
    }

    private async Task<PlaywrightBrowserSession> LoginAsAdminAsync()
    {
        var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        var auth = new AuthGateway(page, _fixture.BaseUrl);
        var seed = new TestUserSeeder(_fixture.DatabasePath);

        var username = $"securitytxt-admin-{Guid.NewGuid():N}";
        const string password = "Secret123";

        await seed.EnsureUserAsync(username, password, isAdmin: true);
        await auth.LoginAsync(username, password);

        return session;
    }
}
