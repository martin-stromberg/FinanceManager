using FinanceManager.Shared.Dtos.Accounts;
using FinanceManager.Shared.Dtos.Common;
using FinanceManager.Shared.Dtos.Contacts;
using FinanceManager.Shared.Dtos.SavingsPlans;
using FinanceManager.Shared.Dtos.Securities;
using FinanceManager.Shared.Dtos.Statements;
using System.Text;

namespace FinanceManager.Tests.E2E;

[Collection(PlaywrightCollection.CollectionName)]
public sealed class ListNavigationPlaywrightTests
{
    private readonly PlaywrightWebAppFixture _fixture;

    public ListNavigationPlaywrightTests(PlaywrightWebAppFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// Verifies that clicking an account row opens the expected detail route.
    /// </summary>
    [Fact]
    public async Task ClickAccountRow_ShouldNavigateToDetailPage()
    {
        await ClickAccountRowShouldNavigateToDetailPageAsync(
            () => _fixture.CreateSessionAsync(),
            "list-user",
            "Navigated Account");
    }

    [Fact]
    public async Task ClickAccountRow_ShouldNavigateToDetailPage_OnMobileViewport()
    {
        await ClickAccountRowShouldNavigateToDetailPageAsync(
            () => _fixture.CreateMobileSessionAsync(),
            "list-mobile-user",
            "Navigated Mobile Account");
    }

    private async Task ClickAccountRowShouldNavigateToDetailPageAsync(
        Func<Task<PlaywrightBrowserSession>> createSessionAsync,
        string userPrefix,
        string accountPrefix)
    {
        await using var session = await createSessionAsync();
        var page = session.Page;
        var auth = new AuthGateway(page, _fixture.BaseUrl);
        var userSeed = new TestUserSeeder(_fixture.DatabasePath);
        var accountSeed = new AccountsApiSeedHelper(page);
        var list = new ListPageGateway(page);

        var username = $"{userPrefix}-{Guid.NewGuid():N}";
        const string password = "Secret123";
        await userSeed.EnsureUserAsync(username, password);
        await auth.LoginAsync(username, password);

        var accountName = $"{accountPrefix} {Guid.NewGuid():N}";
        var account = await accountSeed.CreateAccountAsync(accountName, "DE50700500000007882996");

        await list.OpenAccountsAsync();
        await list.WaitForAccountVisibleAsync(accountName);
        await list.OpenRowAsync(accountName);

        await page.WaitForURLAsync($"**/card/accounts/{account.Id}");
    }

    [Fact]
    public async Task Create_Edit_AndAliasContact_FromContactsPage_ShouldWork()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        await EnsureAuthenticatedAsync(page, "contacts-page-user");

        await page.GotoAsync("/list/contacts");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var created = await BrowserApiHelper.PostJsonAsync<ContactCreateRequest, ContactDto>(
            page,
            "/api/contacts",
            new ContactCreateRequest($"Kontakt {Guid.NewGuid():N}", ContactType.Bank, null, null, false));

        await page.GotoAsync($"/card/contacts/{created.Id}");
        await page.WaitForURLAsync($"**/card/contacts/{created.Id}");

        var updated = await BrowserApiHelper.PutJsonAsync<ContactUpdateRequest, ContactDto>(
            page,
            $"/api/contacts/{created.Id}",
            new ContactUpdateRequest("Kontakt Bearbeitet", ContactType.Bank, null, "E2E Update", false));
        updated.Name.Should().Be("Kontakt Bearbeitet");

        await BrowserApiHelper.PostJsonAsync(page, $"/api/contacts/{created.Id}/aliases", new AliasCreateRequest("E2E-ALIAS"));
        var aliases = await BrowserApiHelper.GetJsonAsync<IReadOnlyList<AliasNameDto>>(page, $"/api/contacts/{created.Id}/aliases");
        aliases.Should().ContainSingle(x => x.Pattern == "E2E-ALIAS");
    }

    [Fact]
    public async Task CreateContact_FromStatementEntryPage_ShouldAssignContactDirectly()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        await EnsureAuthenticatedAsync(page, "contacts-entry-user");

        var account = await BrowserApiHelper.PostJsonAsync<AccountCreateRequest, AccountDto>(
            page,
            "/api/accounts",
            new AccountCreateRequest(
                Name: $"Statement Konto {Guid.NewGuid():N}",
                Type: AccountType.Giro,
                Iban: "DE50700500000007882995",
                BankContactId: null,
                NewBankContactName: "Test Bank",
                SymbolAttachmentId: null,
                SavingsPlanExpectation: SavingsPlanExpectation.Optional,
                SecurityProcessingEnabled: true));

        var (draftId, entryId) = await UploadDraftWithSingleEntryAsync(page, account.Iban!, "entry_contact.csv");
        await page.GotoAsync($"/card/statement-drafts/entries/{entryId}?draftId={draftId}");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var request = new ContactCreateRequest(
            Name: $"Inline Kontakt {Guid.NewGuid():N}",
            Type: ContactType.Other,
            CategoryId: null,
            Description: "Created from statement entry page",
            IsPaymentIntermediary: false,
            Parent: new ParentLinkRequest("statement-drafts/entries", entryId, "ContactId"));

        var created = await BrowserApiHelper.PostJsonAsync<ContactCreateRequest, ContactDto>(page, "/api/contacts", request);
        var detail = await BrowserApiHelper.GetJsonAsync<StatementDraftDetailDto>(page, $"/api/statement-drafts/{draftId}?headerOnly=false");

        detail.Entries.Should().ContainSingle(x => x.Id == entryId && x.ContactId == created.Id);
    }

    [Fact]
    public async Task Create_Edit_Delete_BankAccount_ShouldWork()
    {
        await CreateEditDeleteBankAccountShouldWorkAsync(
            () => _fixture.CreateSessionAsync(),
            "account-user",
            "Konto",
            "Bank A",
            "Konto Bearbeitet");
    }

    [Fact]
    public async Task Create_Edit_Delete_BankAccount_ShouldWork_OnMobileViewport()
    {
        await CreateEditDeleteBankAccountShouldWorkAsync(
            () => _fixture.CreateMobileSessionAsync(),
            "account-mobile-user",
            "Mobiles Konto",
            "Mobile Bank A",
            "Mobiles Konto Bearbeitet");
    }

    private async Task CreateEditDeleteBankAccountShouldWorkAsync(
        Func<Task<PlaywrightBrowserSession>> createSessionAsync,
        string userPrefix,
        string accountPrefix,
        string bankName,
        string updatedName)
    {
        await using var session = await createSessionAsync();
        var page = session.Page;
        await EnsureAuthenticatedAsync(page, userPrefix);

        await page.GotoAsync("/list/accounts");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var created = await BrowserApiHelper.PostJsonAsync<AccountCreateRequest, AccountDto>(
            page,
            "/api/accounts",
            new AccountCreateRequest(
                Name: $"{accountPrefix} {Guid.NewGuid():N}",
                Type: AccountType.Giro,
                Iban: "DE50700500000007882996",
                BankContactId: null,
                NewBankContactName: bankName,
                SymbolAttachmentId: null,
                SavingsPlanExpectation: SavingsPlanExpectation.Optional,
                SecurityProcessingEnabled: true));

        await page.GotoAsync($"/card/accounts/{created.Id}");
        await page.WaitForURLAsync($"**/card/accounts/{created.Id}");

        var updated = await BrowserApiHelper.PutJsonAsync<AccountUpdateRequest, AccountDto>(
            page,
            $"/api/accounts/{created.Id}",
            new AccountUpdateRequest(
                Name: updatedName,
                Type: created.Type,
                Iban: created.Iban,
                BankContactId: created.BankContactId,
                NewBankContactName: null,
                SymbolAttachmentId: null,
                SavingsPlanExpectation: created.SavingsPlanExpectation,
                SecurityProcessingEnabled: created.SecurityProcessingEnabled,
                Archived: false));
        updated.Name.Should().Be(updatedName);

        var deleteStatus = await BrowserApiHelper.DeleteAsync(page, $"/api/accounts/{created.Id}");
        deleteStatus.Should().BeOneOf(200, 204);
    }

    [Fact]
    public async Task Create_Edit_Delete_SavingsPlan_ShouldWork()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        await EnsureAuthenticatedAsync(page, "savings-user");

        await page.GotoAsync("/list/savings-plans");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var created = await BrowserApiHelper.PostJsonAsync<SavingsPlanCreateRequest, SavingsPlanDto>(
            page,
            "/api/savings-plans",
            new SavingsPlanCreateRequest("Plan A", SavingsPlanType.OneTime, 100m, DateTime.UtcNow.Date.AddMonths(6), null, null, null));

        await page.GotoAsync($"/card/savings-plans/{created.Id}");
        await page.WaitForURLAsync($"**/card/savings-plans/{created.Id}");

        var updated = await BrowserApiHelper.PutJsonAsync<SavingsPlanCreateRequest, SavingsPlanDto>(
            page,
            $"/api/savings-plans/{created.Id}",
            new SavingsPlanCreateRequest("Plan B", SavingsPlanType.Recurring, 200m, DateTime.UtcNow.Date.AddMonths(12), SavingsPlanInterval.Monthly, null, "CN-123"));
        updated.Name.Should().Be("Plan B");

        await BrowserApiHelper.PostNoContentAsync(page, $"/api/savings-plans/{created.Id}/archive");
        var deletedStatus = await BrowserApiHelper.DeleteAsync(page, $"/api/savings-plans/{created.Id}");
        deletedStatus.Should().BeOneOf(200, 204);
    }

    [Fact]
    public async Task Create_Edit_Delete_Security_AndImportPrices_OnPricesPage_ShouldWork()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        await EnsureAuthenticatedAsync(page, "security-user");

        await page.GotoAsync("/list/securities");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var created = await BrowserApiHelper.PostJsonAsync<SecurityRequest, SecurityDto>(
            page,
            "/api/securities",
            new SecurityRequest
            {
                Name = $"Security {Guid.NewGuid():N}",
                Identifier = $"SEC-{Guid.NewGuid():N}",
                CurrencyCode = "EUR"
            });

        await page.GotoAsync($"/card/securities/{created.Id}");
        await page.WaitForURLAsync($"**/card/securities/{created.Id}");

        var updated = await BrowserApiHelper.PutJsonAsync<SecurityRequest, SecurityDto>(
            page,
            $"/api/securities/{created.Id}",
            new SecurityRequest
            {
                Name = "Security Bearbeitet",
                Identifier = created.Identifier,
                CurrencyCode = "EUR",
                Description = "E2E"
            });
        updated.Name.Should().Be("Security Bearbeitet");

        await page.GotoAsync($"/list/securities/prices/{created.Id}");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var csv = string.Join('\n', "sep=;", "Zeit;Test Security", "01.07.2026 02:00:00;42,61", "02.07.2026 02:00:00;44,00") + '\n';
        var import = await BrowserApiHelper.PostMultipartAsync<SecurityPriceImportResultDto>(
            page,
            $"/api/securities/{created.Id}/prices/import",
            "prices.csv",
            "text/csv",
            Encoding.UTF8.GetBytes(csv),
            new Dictionary<string, string> { ["provider"] = "ing" });
        import.Inserted.Should().BeGreaterThan(0);

        await BrowserApiHelper.PostNoContentAsync(page, $"/api/securities/{created.Id}/archive");
        var deletedStatus = await BrowserApiHelper.DeleteAsync(page, $"/api/securities/{created.Id}");
        deletedStatus.Should().BeOneOf(200, 204);
    }

    private async Task EnsureAuthenticatedAsync(IPage page, string userPrefix)
    {
        var auth = new AuthGateway(page, _fixture.BaseUrl);
        var userSeed = new TestUserSeeder(_fixture.DatabasePath);
        var username = $"{userPrefix}-{Guid.NewGuid():N}";
        const string password = "Secret123";
        await userSeed.EnsureUserAsync(username, password);
        await auth.LoginAsync(username, password);
    }

    private static async Task<(Guid DraftId, Guid EntryId)> UploadDraftWithSingleEntryAsync(IPage page, string iban, string fileName)
    {
        var csv = "Umsatzanzeige;Datei erstellt am: 02.12.2025 19:04\r\n" +
                  "\r\n" +
                  $"IBAN;{iban}\r\n" +
                  "Kontoname;Girokonto\r\n" +
                  "Bank;ING\r\n" +
                  "Kunde;Admin\r\n" +
                  "Zeitraum;02.11.2025 - 02.12.2025\r\n" +
                  "Saldo;2.776,45;EUR\r\n" +
                  "\r\n" +
                  "Sortierung;Datum absteigend\r\n" +
                  "\r\n" +
                  "\r\n" +
                  "Buchung;Wertstellungsdatum;Auftraggeber/Empfänger;Buchungstext;Verwendungszweck;Saldo;Währung;Betrag;Währung\r\n" +
                  "02.12.2025;02.12.2025;Inline Contact;Überweisung;Ihr Einkauf;2.776,45;EUR;-206,44;EUR\r\n";

        var upload = await BrowserApiHelper.PostMultipartAsync<StatementDraftUploadResult>(
            page,
            "/api/statement-drafts/upload",
            fileName,
            "text/csv",
            Encoding.UTF8.GetBytes(csv));
        upload.FirstDraft.Should().NotBeNull();

        var draftId = upload.FirstDraft!.DraftId;
        var detail = await BrowserApiHelper.GetJsonAsync<StatementDraftDetailDto>(page, $"/api/statement-drafts/{draftId}?headerOnly=false");
        return (draftId, detail.Entries.First().Id);
    }
}
