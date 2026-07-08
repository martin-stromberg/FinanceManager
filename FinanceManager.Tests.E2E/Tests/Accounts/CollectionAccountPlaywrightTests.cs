using FinanceManager.Shared.Dtos.Accounts;
using FinanceManager.Shared.Dtos.Statements;

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
    /// Verifies that marking an existing account as a collection account persists the flag.
    /// </summary>
    [Fact]
    public async Task MarkAccountAsCollectionAccount_ShouldPersistFlag()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        var auth = new AuthGateway(page, _fixture.BaseUrl);
        var seeder = new TestUserSeeder(_fixture.DatabasePath);

        var username = $"collection-flag-{Guid.NewGuid():N}";
        const string password = "Secret123";
        await seeder.EnsureUserAsync(username, password);
        await auth.LoginAsync(username, password);

        // Create a normal account first
        var account = await BrowserApiHelper.PostJsonAsync<AccountCreateRequest, AccountDto>(
            page,
            "/api/accounts",
            new AccountCreateRequest(
                Name: $"Sammelkonto {Guid.NewGuid():N}",
                Type: AccountType.Giro,
                Iban: "DE12345678901234567890",
                BankContactId: null,
                NewBankContactName: "Test Bank",
                SymbolAttachmentId: null,
                SavingsPlanExpectation: SavingsPlanExpectation.Optional,
                SecurityProcessingEnabled: true,
                IsCollectionAccount: false));

        account.IsCollectionAccount.Should().BeFalse();

        // Update to collection account
        var updated = await BrowserApiHelper.PutJsonAsync<AccountUpdateRequest, AccountDto>(
            page,
            $"/api/accounts/{account.Id}",
            new AccountUpdateRequest(
                Name: account.Name,
                Type: account.Type,
                Iban: account.Iban,
                BankContactId: account.BankContactId == Guid.Empty ? null : account.BankContactId,
                NewBankContactName: null,
                SymbolAttachmentId: account.SymbolAttachmentId,
                SavingsPlanExpectation: account.SavingsPlanExpectation,
                SecurityProcessingEnabled: account.SecurityProcessingEnabled,
                Archived: false,
                IsCollectionAccount: true));

        updated.IsCollectionAccount.Should().BeTrue();

        // Verify via GET
        var fetched = await BrowserApiHelper.GetJsonAsync<AccountDto>(page, $"/api/accounts/{account.Id}");
        fetched.IsCollectionAccount.Should().BeTrue();
    }

    /// <summary>
    /// Verifies that a linked IBAN added to a collection account appears in the account's IBAN list.
    /// </summary>
    [Fact]
    public async Task AddLinkedIban_ShouldAppearInAccountDetail()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        var auth = new AuthGateway(page, _fixture.BaseUrl);
        var seeder = new TestUserSeeder(_fixture.DatabasePath);

        var username = $"collection-add-iban-{Guid.NewGuid():N}";
        const string password = "Secret123";
        await seeder.EnsureUserAsync(username, password);
        await auth.LoginAsync(username, password);

        // Create a collection account directly
        var account = await BrowserApiHelper.PostJsonAsync<AccountCreateRequest, AccountDto>(
            page,
            "/api/accounts",
            new AccountCreateRequest(
                Name: $"Sammelkonto Add {Guid.NewGuid():N}",
                Type: AccountType.Giro,
                Iban: "DE50700500000007882995",
                BankContactId: null,
                NewBankContactName: "Spar Bank",
                SymbolAttachmentId: null,
                SavingsPlanExpectation: SavingsPlanExpectation.Optional,
                SecurityProcessingEnabled: false,
                IsCollectionAccount: true));

        account.IsCollectionAccount.Should().BeTrue();

        // Add a linked IBAN (with spaces, should be stripped by server)
        const string linkedIban = "DE12500105170648489890";
        await BrowserApiHelper.PostJsonAsync(
            page,
            $"/api/accounts/{account.Id}/linked-ibans",
            new AccountLinkedIbanUpsertRequest(linkedIban));

        // Verify via GET of linked IBANs
        var ibans = await BrowserApiHelper.GetJsonAsync<IReadOnlyList<string>>(page, $"/api/accounts/{account.Id}/linked-ibans");
        ibans.Should().Contain(linkedIban);

        // Verify in account DTO
        var fetched = await BrowserApiHelper.GetJsonAsync<AccountDto>(page, $"/api/accounts/{account.Id}");
        fetched.LinkedIbans.Should().Contain(linkedIban);
    }

    /// <summary>
    /// Verifies that a linked IBAN removed from a collection account no longer appears in the list.
    /// </summary>
    [Fact]
    public async Task RemoveLinkedIban_ShouldDisappearFromAccountDetail()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        var auth = new AuthGateway(page, _fixture.BaseUrl);
        var seeder = new TestUserSeeder(_fixture.DatabasePath);

        var username = $"collection-remove-iban-{Guid.NewGuid():N}";
        const string password = "Secret123";
        await seeder.EnsureUserAsync(username, password);
        await auth.LoginAsync(username, password);

        // Create a collection account
        var account = await BrowserApiHelper.PostJsonAsync<AccountCreateRequest, AccountDto>(
            page,
            "/api/accounts",
            new AccountCreateRequest(
                Name: $"Sammelkonto Remove {Guid.NewGuid():N}",
                Type: AccountType.Giro,
                Iban: "DE50700500000007882996",
                BankContactId: null,
                NewBankContactName: "Remove Bank",
                SymbolAttachmentId: null,
                SavingsPlanExpectation: SavingsPlanExpectation.Optional,
                SecurityProcessingEnabled: false,
                IsCollectionAccount: true));

        const string linkedIban = "DE12500105170648489891";

        // Add then remove the IBAN
        await BrowserApiHelper.PostJsonAsync(
            page,
            $"/api/accounts/{account.Id}/linked-ibans",
            new AccountLinkedIbanUpsertRequest(linkedIban));

        var ibansBeforeRemove = await BrowserApiHelper.GetJsonAsync<IReadOnlyList<string>>(page, $"/api/accounts/{account.Id}/linked-ibans");
        ibansBeforeRemove.Should().Contain(linkedIban);

        var status = await BrowserApiHelper.DeleteAsync(page, $"/api/accounts/{account.Id}/linked-ibans/{Uri.EscapeDataString(linkedIban)}");
        status.Should().Be(204);

        // Verify IBAN is no longer listed
        var ibansAfterRemove = await BrowserApiHelper.GetJsonAsync<IReadOnlyList<string>>(page, $"/api/accounts/{account.Id}/linked-ibans");
        ibansAfterRemove.Should().NotContain(linkedIban);

        var fetched = await BrowserApiHelper.GetJsonAsync<AccountDto>(page, $"/api/accounts/{account.Id}");
        fetched.LinkedIbans.Should().NotContain(linkedIban);
    }
}
