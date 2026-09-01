using FinanceManager.Shared.Dtos.Accounts;

namespace FinanceManager.Tests.E2E;

/// <summary>
/// Creates accounts directly via the authenticated JSON API (through the browser session's cookies, see
/// <see cref="BrowserApiHelper"/>) instead of driving the account-creation UI. Tests use this to seed the
/// data they depend on quickly and reliably, keeping UI-driven flows in the test body focused on the
/// behavior actually under test rather than on account setup.
/// </summary>
public sealed class AccountsApiSeedHelper
{
    private readonly IPage _page;

    /// <summary>
    /// Creates the helper bound to an already-authenticated page whose session cookies will be used for
    /// the API calls.
    /// </summary>
    /// <param name="page">The Playwright page of the logged-in session to seed data for.</param>
    public AccountsApiSeedHelper(IPage page)
    {
        _page = page;
    }

    /// <summary>
    /// Creates a single Giro account with sensible defaults (a new "Test Bank" contact, security processing
    /// enabled, savings plan optional) via <c>POST /api/accounts</c>, so tests that only need "an account to
    /// exist" don't have to specify every field of <see cref="AccountCreateRequest"/> themselves.
    /// </summary>
    /// <param name="name">Display name for the account.</param>
    /// <param name="iban">IBAN to assign to the account.</param>
    /// <returns>The created account as returned by the API.</returns>
    public async Task<AccountDto> CreateAccountAsync(string name, string iban)
    {
        var request = new AccountCreateRequest(
            Name: name,
            Type: AccountType.Giro,
            Iban: iban,
            BankContactId: null,
            NewBankContactName: "Test Bank",
            SymbolAttachmentId: null,
            SavingsPlanExpectation: SavingsPlanExpectation.Optional,
            SecurityProcessingEnabled: true);

        return await BrowserApiHelper.PostJsonAsync<AccountCreateRequest, AccountDto>(_page, "/api/accounts", request);
    }
}
