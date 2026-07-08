namespace FinanceManager.Tests.E2E;

[Collection(PlaywrightCollection.CollectionName)]
public sealed class CollectionAccountPlaywrightTests
{
    private readonly PlaywrightWebAppFixture _fixture;

    public CollectionAccountPlaywrightTests(PlaywrightWebAppFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// Verifies that a new collection account can be created through the UI form:
    /// fills name and IBAN, enables the collection-account toggle, saves and asserts
    /// that the browser navigates to the new account's detail page.
    /// </summary>
    [Fact]
    public async Task CreateCollectionAccount_ViaUi_ShouldNavigateToDetailPage()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        var auth = new AuthGateway(page, _fixture.BaseUrl);
        var seeder = new TestUserSeeder(_fixture.DatabasePath);

        var username = $"coll-create-ui-{Guid.NewGuid():N}";
        const string password = "Secret123";
        await seeder.EnsureUserAsync(username, password);
        await auth.LoginAsync(username, password);

        // Pre-create a bank contact via API so the lookup field can be filled
        var bankContact = await BrowserApiHelper.PostJsonAsync<ContactCreateRequest, ContactDto>(
            page,
            "/api/contacts",
            new ContactCreateRequest("UI Spar Bank", ContactType.Bank, null, null, false));

        // Navigate to the "new account" card form
        await page.GotoAsync("/card/accounts/new");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var accountName = $"UI Sammelkonto {Guid.NewGuid():N}";

        // Fill the Name field (label "Name" in the table row header)
        await page.Locator("tr")
            .Filter(new() { Has = page.Locator("th").Filter(new() { HasText = "Name" }) })
            .Locator("input.card-input")
            .FillAsync(accountName);

        // Fill the IBAN field
        await page.Locator("tr")
            .Filter(new() { Has = page.Locator("th").Filter(new() { HasText = "IBAN" }) })
            .Locator("input.card-input")
            .FillAsync("DE50700500000007882999");

        // Select the bank contact via the lookup field: type the name → wait for dropdown → click the item
        var contactInput = page.Locator("tr")
            .Filter(new() { Has = page.Locator("th").Filter(new() { HasText = "Bank contact" }) })
            .Locator("input.card-input");
        await contactInput.FillAsync("UI Spar Bank");
        var lookupItem = page.Locator(".lookup-item").Filter(new() { HasText = "UI Spar Bank" }).First;
        await lookupItem.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10_000 });
        await lookupItem.ClickAsync();

        // Enable the "Collection account" checkbox
        await page.Locator("tr")
            .Filter(new() { Has = page.Locator("th").Filter(new() { HasText = "Collection account" }) })
            .Locator("input.card-input-checkbox")
            .CheckAsync();

        // Click the Save ribbon button
        await page.Locator("button#Save").ClickAsync();

        // After a successful save the page navigates to the new account's detail URL
        await page.WaitForURLAsync("**/card/accounts/**", new() { Timeout = 15_000 });
        page.Url.Should().Contain("/card/accounts/");
        page.Url.Should().NotContain("/new");
    }

    /// <summary>
    /// Verifies that typing a new IBAN into the LinkedIbansPanel and clicking "Add"
    /// makes the IBAN appear in the panel's list.
    /// </summary>
    [Fact]
    public async Task AddLinkedIban_ViaUi_ShouldAppearInPanel()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        var auth = new AuthGateway(page, _fixture.BaseUrl);
        var seeder = new TestUserSeeder(_fixture.DatabasePath);

        var username = $"coll-add-iban-ui-{Guid.NewGuid():N}";
        const string password = "Secret123";
        await seeder.EnsureUserAsync(username, password);
        await auth.LoginAsync(username, password);

        // Seed a collection account via API
        var account = await BrowserApiHelper.PostJsonAsync<AccountCreateRequest, AccountDto>(
            page,
            "/api/accounts",
            new AccountCreateRequest(
                Name: $"Sammelkonto AddIban {Guid.NewGuid():N}",
                Type: AccountType.Giro,
                Iban: "DE50700500000007882980",
                BankContactId: null,
                NewBankContactName: "Spar Bank",
                SymbolAttachmentId: null,
                SavingsPlanExpectation: SavingsPlanExpectation.Optional,
                SecurityProcessingEnabled: false,
                IsCollectionAccount: true));

        const string linkedIban = "DE12500105170648489870";

        // Navigate to the account detail page
        await page.GotoAsync($"/card/accounts/{account.Id}");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Wait for the LinkedIbansPanel to render
        var panel = page.Locator(".linked-ibans-panel");
        await panel.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15_000 });

        // Type the IBAN into the input field and click "Add"
        await panel.Locator("input.card-input").FillAsync(linkedIban);
        await panel.Locator("button.btn-primary").ClickAsync();

        // The IBAN should now be visible as a row in the panel
        await panel.GetByText(linkedIban)
            .WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10_000 });
        (await panel.InnerTextAsync()).Should().Contain(linkedIban);
    }

    /// <summary>
    /// Verifies that clicking the "Remove" button next to a linked IBAN
    /// causes it to disappear from the LinkedIbansPanel.
    /// </summary>
    [Fact]
    public async Task RemoveLinkedIban_ViaUi_ShouldDisappearFromPanel()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        var auth = new AuthGateway(page, _fixture.BaseUrl);
        var seeder = new TestUserSeeder(_fixture.DatabasePath);

        var username = $"coll-rem-iban-ui-{Guid.NewGuid():N}";
        const string password = "Secret123";
        await seeder.EnsureUserAsync(username, password);
        await auth.LoginAsync(username, password);

        // Seed: collection account + a pre-linked IBAN
        var account = await BrowserApiHelper.PostJsonAsync<AccountCreateRequest, AccountDto>(
            page,
            "/api/accounts",
            new AccountCreateRequest(
                Name: $"Sammelkonto RemIban {Guid.NewGuid():N}",
                Type: AccountType.Giro,
                Iban: "DE50700500000007882981",
                BankContactId: null,
                NewBankContactName: "Spar Bank",
                SymbolAttachmentId: null,
                SavingsPlanExpectation: SavingsPlanExpectation.Optional,
                SecurityProcessingEnabled: false,
                IsCollectionAccount: true));

        const string linkedIban = "DE12500105170648489871";
        await BrowserApiHelper.PostJsonAsync(
            page,
            $"/api/accounts/{account.Id}/linked-ibans",
            new AccountLinkedIbanUpsertRequest(linkedIban));

        // Navigate to the account detail page
        await page.GotoAsync($"/card/accounts/{account.Id}");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Wait for the IBAN to appear in the panel
        var panel = page.Locator(".linked-ibans-panel");
        await panel.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15_000 });
        await panel.GetByText(linkedIban)
            .WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10_000 });

        // Click the "Remove" button in the row that shows this IBAN
        await panel.Locator("tr")
            .Filter(new() { HasText = linkedIban })
            .Locator("button.btn-danger")
            .ClickAsync();

        // The IBAN row should disappear from the panel
        await panel.GetByText(linkedIban)
            .WaitForAsync(new() { State = WaitForSelectorState.Hidden, Timeout = 10_000 });
        (await panel.InnerTextAsync()).Should().NotContain(linkedIban);
    }
}
