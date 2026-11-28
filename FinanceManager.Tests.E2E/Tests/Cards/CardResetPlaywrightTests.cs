using FinanceManager.Domain.Mcp;
using FinanceManager.Shared.Dtos.Mcp;
using FinanceManager.Shared.Dtos.SavingsPlans;

namespace FinanceManager.Tests.E2E;

/// <summary>
/// End-to-end verification of the "Zurücksetzen" ribbon action on the card detail pages: the
/// action lives in the manage group right behind Save, stays disabled while no pending edits
/// exist and — when clicked — visibly restores the persisted (or initial) field values without
/// reloading the record. The MCP access card additionally proves the documented special case
/// that a reset only discards the card fields while the immediately-persisted scope profile
/// stays untouched.
/// </summary>
[Collection(PlaywrightCollection.CollectionName)]
public sealed class CardResetPlaywrightTests
{
    private readonly PlaywrightWebAppFixture _fixture;

    /// <summary>
    /// Initializes a new instance of the <see cref="CardResetPlaywrightTests"/> class.
    /// </summary>
    /// <param name="fixture">Shared Playwright web app fixture providing the browser and test server.</param>
    public CardResetPlaywrightTests(PlaywrightWebAppFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// Happy path on the contact card: an edited name flips Save/Reset to enabled; clicking
    /// Reset visibly restores the persisted name and disables both actions again.
    /// </summary>
    [Fact]
    public async Task ContactCard_Reset_RestoresPersistedValues()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        await LoginNewUserAsync(page, "card-reset-contact");

        var originalName = $"Kontakt {Guid.NewGuid():N}";
        var contact = await BrowserApiHelper.PostJsonAsync<ContactCreateRequest, ContactDto>(
            page,
            "/api/contacts",
            new ContactCreateRequest(originalName, ContactType.Organization, null, null, false));

        await page.GotoAsync($"/card/contacts/{contact.Id}");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var nameInput = FieldInput(page, "Name");
        await nameInput.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15000 });
        (await nameInput.InputValueAsync()).Should().Be(originalName);

        var resetButton = page.Locator("button#Reset");
        var saveButton = page.Locator("button#Save");
        await Microsoft.Playwright.Assertions.Expect(resetButton).ToBeDisabledAsync();
        await Microsoft.Playwright.Assertions.Expect(saveButton).ToBeDisabledAsync();

        await nameInput.FillAsync($"{originalName}-GEAENDERT");

        await Microsoft.Playwright.Assertions.Expect(resetButton).ToBeEnabledAsync();
        await Microsoft.Playwright.Assertions.Expect(saveButton).ToBeEnabledAsync();

        await resetButton.ClickAsync();

        await Microsoft.Playwright.Assertions.Expect(nameInput).ToHaveValueAsync(originalName);
        await Microsoft.Playwright.Assertions.Expect(resetButton).ToBeDisabledAsync();
        await Microsoft.Playwright.Assertions.Expect(saveButton).ToBeDisabledAsync();
    }

    /// <summary>
    /// New-create mode on the contact card: the prefill name is the initial state; a discarded
    /// edit restores it and no contact is persisted as a side effect.
    /// </summary>
    [Fact]
    public async Task ContactCardNew_Reset_RestoresInitialState()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        await LoginNewUserAsync(page, "card-reset-contact-new");

        var prefillName = $"NeuKontakt {Guid.NewGuid():N}";
        var discardedName = $"Verworfen {Guid.NewGuid():N}";

        await page.GotoAsync($"/card/contacts/new?prefill={Uri.EscapeDataString(prefillName)}");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var nameInput = FieldInput(page, "Name");
        await nameInput.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15000 });
        (await nameInput.InputValueAsync()).Should().Be(prefillName);

        await nameInput.FillAsync(discardedName);
        await Microsoft.Playwright.Assertions.Expect(page.Locator("button#Reset")).ToBeEnabledAsync();
        await page.Locator("button#Reset").ClickAsync();

        await Microsoft.Playwright.Assertions.Expect(nameInput).ToHaveValueAsync(prefillName);
        await Microsoft.Playwright.Assertions.Expect(page.Locator("button#Reset")).ToBeDisabledAsync();

        var contacts = await BrowserApiHelper.GetJsonAsync<List<ContactDto>>(page, "/api/contacts");
        contacts.Should().NotContain(c => c.Name == discardedName,
            "a reset in create mode must not persist the discarded input");
    }

    /// <summary>
    /// Savings plan card: the view model mutates its edit model during input, so Reset must
    /// re-derive the model from the loaded dto — the persisted name must reappear in the card.
    /// </summary>
    [Fact]
    public async Task SavingsPlanCard_Reset_RestoresPersistedValues()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        await LoginNewUserAsync(page, "card-reset-plan");

        var originalName = $"Plan {Guid.NewGuid():N}";
        var plan = await BrowserApiHelper.PostJsonAsync<SavingsPlanCreateRequest, SavingsPlanDto>(
            page,
            "/api/savings-plans",
            new SavingsPlanCreateRequest(originalName, SavingsPlanType.OneTime, 100m, DateTime.UtcNow.Date.AddMonths(6), null, null, null));

        await page.GotoAsync($"/card/savings-plans/{plan.Id}");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var nameInput = FieldInput(page, "Name");
        await nameInput.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15000 });
        (await nameInput.InputValueAsync()).Should().Be(originalName);

        var resetButton = page.Locator("button#Reset");
        await Microsoft.Playwright.Assertions.Expect(resetButton).ToBeDisabledAsync();

        await nameInput.FillAsync($"{originalName}-GEAENDERT");
        await Microsoft.Playwright.Assertions.Expect(resetButton).ToBeEnabledAsync();

        await resetButton.ClickAsync();

        await Microsoft.Playwright.Assertions.Expect(nameInput).ToHaveValueAsync(originalName);
        await Microsoft.Playwright.Assertions.Expect(resetButton).ToBeDisabledAsync();
        await Microsoft.Playwright.Assertions.Expect(page.Locator("button#Save")).ToBeDisabledAsync();
    }

    /// <summary>
    /// MCP access card special case: Reset only discards the card fields — the scope profile
    /// persisted immediately through its own endpoint must be left untouched.
    /// </summary>
    [Fact]
    public async Task McpAccessCard_Reset_KeepsScopes()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        await LoginNewUserAsync(page, "card-reset-mcp", isAdmin: true);

        // The access management only renders editable when MCP is enabled (admin setting).
        await BrowserApiHelper.PutNoContentAsync(
            page,
            "/api/mcp-audit/settings",
            new McpSettingsUpdateRequest(true, true));

        var originalName = $"zugriff-{Guid.NewGuid():N}";
        var scope = new McpAccessScopeDto(Guid.Empty, (short)McpDataType.Postings, (short)McpRightKind.Read, null, null, null, null, null);
        var created = await BrowserApiHelper.PostJsonAsync<McpAccessCreateRequest, McpAccessCreatedDto>(
            page,
            "/api/mcp-accesses",
            new McpAccessCreateRequest(originalName, null, (short)McpAccessLevel.Individuell, (short)McpApprovalMode.PerAction, null, null, new[] { scope }));

        var detail = await BrowserApiHelper.GetJsonAsync<McpAccessDetailDto>(page, $"/api/mcp-accesses/{created.Access.Id}");
        detail.EffectiveScopes.Should().ContainSingle(
            s => s.DataType == (short)McpDataType.Postings && s.Right == (short)McpRightKind.Read,
            "the seeded individual scope profile must persist before the reset test starts");

        await page.GotoAsync($"/card/mcp-accesses/{created.Access.Id}");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var nameInput = FieldInput(page, "Name");
        await nameInput.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15000 });
        (await nameInput.InputValueAsync()).Should().Be(originalName);

        var resetButton = page.Locator("button#Reset");
        await Microsoft.Playwright.Assertions.Expect(resetButton).ToBeDisabledAsync();

        await nameInput.FillAsync($"{originalName}-GEAENDERT");
        await Microsoft.Playwright.Assertions.Expect(resetButton).ToBeEnabledAsync();

        await resetButton.ClickAsync();

        await Microsoft.Playwright.Assertions.Expect(nameInput).ToHaveValueAsync(originalName);
        await Microsoft.Playwright.Assertions.Expect(resetButton).ToBeDisabledAsync();

        // The scope profile persists immediately through its own endpoint — Reset must not touch it.
        var afterReset = await BrowserApiHelper.GetJsonAsync<McpAccessDetailDto>(page, $"/api/mcp-accesses/{created.Access.Id}");
        afterReset.EffectiveScopes.Should().ContainSingle(
            s => s.DataType == (short)McpDataType.Postings && s.Right == (short)McpRightKind.Read,
            "the scope profile must survive a card-field reset unchanged");
    }

    /// <summary>
    /// Locates the editable input inside the card-table row whose header cell carries the
    /// given (locale-independent) caption.
    /// </summary>
    /// <param name="page">The Playwright page that shows the card detail view.</param>
    /// <param name="caption">The exact caption text of the card-table row header cell.</param>
    /// <returns>A locator for the first matching editable input inside the row.</returns>
    private static ILocator FieldInput(IPage page, string caption)
        => page.Locator($"table.card-table tr:has(th:text-is(\"{caption}\")) td .card-input").First;

    private async Task<FinanceManager.Domain.Users.User> LoginNewUserAsync(IPage page, string prefix, bool isAdmin = false)
    {
        var username = $"{prefix}-{Guid.NewGuid():N}";
        const string password = "Secret123";
        var auth = new AuthGateway(page, _fixture.BaseUrl);
        var seeder = new TestUserSeeder(_fixture.DatabasePath);
        var user = await seeder.EnsureUserAsync(username, password, timeZoneId: "UTC", isAdmin: isAdmin);
        await auth.LoginAsync(username, password);
        return user;
    }
}
