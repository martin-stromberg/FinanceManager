using FinanceManager.Application.Accounts;

namespace FinanceManager.Tests.TestHelpers;

internal sealed class StubAccountService : IAccountService
{
    public Task<AccountDto> CreateAsync(Guid ownerUserId, string name, AccountType type, string? iban, Guid bankContactId, SavingsPlanExpectation expectation, bool securityProcessingEnabled, bool isCollectionAccount, CancellationToken ct)
        => throw new NotImplementedException();

    public Task<AccountDto?> UpdateAsync(Guid id, Guid ownerUserId, string name, string? iban, Guid bankContactId, SavingsPlanExpectation expectation, bool securityProcessingEnabled, bool isCollectionAccount, CancellationToken ct)
        => throw new NotImplementedException();

    public Task<bool> DeleteAsync(Guid id, Guid ownerUserId, CancellationToken ct)
        => throw new NotImplementedException();

    public Task<IReadOnlyList<AccountDto>> ListAsync(Guid ownerUserId, int skip, int take, CancellationToken ct)
        => throw new NotImplementedException();

    public Task<AccountDto?> GetAsync(Guid id, Guid ownerUserId, CancellationToken ct)
        => throw new NotImplementedException();

    public AccountDto? Get(Guid id, Guid ownerUserId)
        => throw new NotImplementedException();

    public Task SetSymbolAttachmentAsync(Guid id, Guid ownerUserId, Guid? attachmentId, CancellationToken ct)
        => throw new NotImplementedException();

    public Task AddLinkedIbanAsync(Guid accountId, Guid ownerUserId, string iban, CancellationToken ct)
        => throw new NotImplementedException();

    public Task<bool> RemoveLinkedIbanAsync(Guid accountId, Guid ownerUserId, string iban, CancellationToken ct)
        => throw new NotImplementedException();

    public Task<IReadOnlyList<string>?> GetLinkedIbansAsync(Guid accountId, Guid ownerUserId, CancellationToken ct)
        => throw new NotImplementedException();
}
