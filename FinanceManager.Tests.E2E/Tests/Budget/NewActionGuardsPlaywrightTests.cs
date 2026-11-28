using System.Text.RegularExpressions;

namespace FinanceManager.Tests.E2E;

/// <summary>
/// End-to-end verification of the guarded "New" actions (issues #609 and #605): on an unsaved
/// budget purpose card the embedded rules list must render its NewRule ribbon action disabled
/// (previously clicking it threw on the null category id), and on the MCP access list the New
/// action must be disabled while the MCP integration is switched off. Screenshots of both
/// disabled and enabled states are captured for the visual acceptance.
/// </summary>
[Collection(PlaywrightCollection.CollectionName)]
public sealed class NewActionGuardsPlaywrightTests
{
    private readonly PlaywrightWebAppFixture _fixture;

    /// <summary>
    /// Initializes a new instance of the <see cref="NewActionGuardsPlaywrightTests"/> class.
    /// </summary>
    /// <param name="fixture">Shared Playwright web app fixture providing the browser and test server.</param>
    public NewActionGuardsPlaywrightTests(PlaywrightWebAppFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// A new, not yet saved budget purpose card must show the NewRule ribbon action disabled with
    /// an explanatory tooltip — without a purpose or category context there is nothing a rule
    /// could attach to.
    /// </summary>
    [Fact]
    public async Task BudgetPurposeCard_Unsaved_NewRuleDisabled()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        await LoginNewUserAsync(page, "newrule-guard");

        await page.GotoAsync("card/budget/purposes/new");
        var newRule = page.Locator("button#NewRule");
        await newRule.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 30000 });

        // disabled until the ribbon has re-evaluated after the card finished loading
        await Assertions.Expect(newRule).ToBeDisabledAsync(new() { Timeout = 15000 });
        // Budget_Rule_New_SaveFirst — de "…zuerst speichern" / en "Save the budget purpose…"
        await Assertions.Expect(newRule).ToHaveAttributeAsync("title",
            new Regex("speicher|save", RegexOptions.IgnoreCase), new() { Timeout = 5000 });

        // muted hint row in the embedded rules list explains the disabled action on every
        // device — the tooltip alone is unreachable via touch and keyboard
        var hint = page.Locator(".card-embedded-list").GetByText(new Regex("speicher|save", RegexOptions.IgnoreCase));
        await Assertions.Expect(hint.First).ToBeVisibleAsync(new() { Timeout = 15000 });

        await CaptureAsync(page, "s4-budget-purpose-newrule-disabled");

        // clicking must not navigate anywhere
        await newRule.ClickAsync(new() { Force = true });
        await page.WaitForTimeoutAsync(500);
        Assert.Contains("/card/budget/purposes/new", page.Url);
    }

    /// <summary>
    /// A new, not yet saved budget category card shows the same guarded pattern: the embedded
    /// rules section is present with the NewRule action disabled and the hint row visible.
    /// </summary>
    [Fact]
    public async Task BudgetCategoryCard_Unsaved_NewRuleDisabled()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        await LoginNewUserAsync(page, "newrule-guard");

        await page.GotoAsync("card/budget/categories/new");
        var newRule = page.Locator("button#NewRule");
        await newRule.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 30000 });

        await Assertions.Expect(newRule).ToBeDisabledAsync(new() { Timeout = 15000 });
        await Assertions.Expect(newRule).ToHaveAttributeAsync("title",
            new Regex("speicher|save", RegexOptions.IgnoreCase), new() { Timeout = 5000 });

        var hint = page.Locator(".card-embedded-list").GetByText(new Regex("speicher|save", RegexOptions.IgnoreCase));
        await Assertions.Expect(hint.First).ToBeVisibleAsync(new() { Timeout = 15000 });

        await CaptureAsync(page, "s4-budget-category-newrule-disabled");
    }

    /// <summary>
    /// On a saved budget purpose card the same NewRule action must be enabled so a rule can be
    /// created with the purpose prefill.
    /// </summary>
    [Fact]
    public async Task BudgetPurposeCard_Saved_NewRuleEnabled()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        await LoginNewUserAsync(page, "newrule-guard");

        var contactId = await BrowserApiHelper.PostJsonAsync<object, System.Text.Json.JsonElement>(page, "api/contacts",
            new { Name = "E2E NewRule Contact", Type = 2, CategoryId = (Guid?)null, Description = (string?)null, IsPaymentIntermediary = (bool?)false })
            .ContinueWith(t => t.Result.GetProperty("id").GetGuid());
        var purposeId = await BrowserApiHelper.PostJsonAsync<object, System.Text.Json.JsonElement>(page, "api/budget/purposes",
            new { Name = "E2E NewRule Purpose", SourceType = 0, SourceId = contactId, Description = (string?)null, BudgetCategoryId = (Guid?)null })
            .ContinueWith(t => t.Result.GetProperty("id").GetGuid());

        await page.GotoAsync($"card/budget/purposes/{purposeId}");
        var newRule = page.Locator("button#NewRule");
        await newRule.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 30000 });

        await Assertions.Expect(newRule).ToBeEnabledAsync(new() { Timeout = 15000 });
        await CaptureAsync(page, "s4-budget-purpose-newrule-enabled");
    }

    /// <summary>
    /// With the MCP integration switched off (default test state) the MCP access list must show
    /// its disabled hint and the New ribbon action must be disabled instead of opening the
    /// create wizard.
    /// </summary>
    [Fact]
    public async Task McpAccessList_McpDisabled_NewDisabled()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        await LoginNewUserAsync(page, "mcpnew-guard");

        await page.GotoAsync("list/mcp-accesses");
        var newButton = page.Locator("button#New");
        await newButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 30000 });

        await Assertions.Expect(newButton).ToBeDisabledAsync(new() { Timeout = 15000 });
        // Mcp_Disabled_Hint — de "…nicht aktiviert…" / en "…not activated…"
        await Assertions.Expect(newButton).ToHaveAttributeAsync("title",
            new Regex("aktiviert|activated", RegexOptions.IgnoreCase), new() { Timeout = 5000 });

        await CaptureAsync(page, "s4-mcp-accesses-new-disabled");

        // a forced click must not open the wizard overlay
        await newButton.ClickAsync(new() { Force = true });
        await page.WaitForTimeoutAsync(500);
        Assert.Equal(0, await page.Locator(".mcp-wizard").CountAsync());
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
