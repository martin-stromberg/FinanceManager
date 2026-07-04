using FinanceManager.Shared.Dtos.Accounts;

namespace FinanceManager.Tests.E2E;

public sealed class AccountsApiSeedHelper
{
    private readonly IPage _page;

    public AccountsApiSeedHelper(IPage page)
    {
        _page = page;
    }

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
