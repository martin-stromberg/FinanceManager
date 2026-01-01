using FinanceManager.Application.Accounts;
using FinanceManager.Domain.Attachments; // added
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Accounts;

/// <summary>
/// Service providing CRUD operations for user bank accounts.
/// Implements <see cref="IAccountService"/> and performs validation against related contacts and uniqueness constraints.
/// </summary>
public sealed class AccountService : IAccountService
{
    private readonly AppDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="AccountService"/> class.
    /// </summary>
    /// <param name="db">Database context used to persist accounts and related entities.</param>
    public AccountService(AppDbContext db) => _db = db;

    /// <summary>
    /// Creates a new account for the specified owner with default savings plan expectation (<see cref="SavingsPlanExpectation.Optional"/>).
    /// </summary>
    /// <param name="ownerUserId">Owner user identifier.</param>
    /// <param name="name">Display name of the account. Must not be null or whitespace.</param>
    /// <param name="type">Type of the account.</param>
    /// <param name="iban">Optional IBAN; when provided it must be unique for the user.</param>
    /// <param name="bankContactId">Contact id of the bank (must exist and be of type <see cref="ContactType.Bank"/>).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created <see cref="AccountDto"/> representing the persisted account.</returns>
    /// <exception cref="ArgumentException">Thrown when the bank contact is invalid, the name is missing or already exists, or the IBAN already exists for the user.</exception>
    public async Task<AccountDto> CreateAsync(Guid ownerUserId, string name, AccountType type, string? iban, Guid bankContactId, CancellationToken ct)
    {
        // default expectation for older callers
        return await CreateAsync(ownerUserId, name, type, iban, bankContactId, SavingsPlanExpectation.Optional, ct);
    }

    /// <summary>
    /// Creates a new account for the specified owner.
    /// </summary>
    /// <param name="ownerUserId">Owner user identifier.</param>
    /// <param name="name">Display name of the account. Must not be null or whitespace.</param>
    /// <param name="type">Type of the account.</param>
    /// <param name="iban">Optional IBAN; when provided it must be unique for the user.</param>
    /// <param name="bankContactId">Contact id of the bank (must exist and be of type <see cref="ContactType.Bank"/>).</param>
    /// <param name="expectation">Savings plan expectation for the account.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created <see cref="AccountDto"/> representing the persisted account.</returns>
    /// <exception cref="ArgumentException">Thrown when the bank contact is invalid, the name is missing or already exists, or the IBAN already exists for the user.</exception>
    public async Task<AccountDto> CreateAsync(Guid ownerUserId, string name, AccountType type, string? iban, Guid bankContactId, SavingsPlanExpectation expectation, CancellationToken ct)
    {
        if (!await _db.Contacts.AsNoTracking().AnyAsync(c => c.Id == bankContactId && c.OwnerUserId == ownerUserId && c.Type == ContactType.Bank, ct))
        {
            throw new ArgumentException("Bank contact invalid", nameof(bankContactId));
        }

        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name required", nameof(name));
        name = name.Trim();

        var nameExists = await _db.Accounts.AsNoTracking().AnyAsync(a => a.OwnerUserId == ownerUserId && a.Name.ToLower() == name.ToLower(), ct);
        if (nameExists)
        {
            throw new ArgumentException("Account name already exists", nameof(name));
        }

        if (!string.IsNullOrWhiteSpace(iban))
        {
            var norm = iban.Trim();
            bool exists = await _db.Accounts.AsNoTracking().AnyAsync(a => a.OwnerUserId == ownerUserId && a.Iban == norm, ct);
            if (exists)
            {
                throw new ArgumentException("IBAN already exists for user", nameof(iban));
            }
            iban = norm;
        }
        var account = new Domain.Accounts.Account(ownerUserId, type, name, iban, bankContactId);
        account.SetSavingsPlanExpectation(expectation);
        _db.Accounts.Add(account);
        await _db.SaveChangesAsync(ct);
        return new AccountDto(account.Id, account.Name, account.Type, account.Iban, account.CurrentBalance, account.BankContactId, account.SymbolAttachmentId, account.SavingsPlanExpectation);
    }

    /// <summary>
    /// Updates an existing account.
    /// </summary>
    /// <param name="id">Identifier of the account to update.</param>
    /// <param name="ownerUserId">Owner user identifier.</param>
    /// <param name="name">New display name. Must not be null or whitespace.</param>
    /// <param name="iban">Optional new IBAN; when provided it must be unique for the user (excluding this account).</param>
    /// <param name="bankContactId">Contact id of the bank (must exist and be of type <see cref="ContactType.Bank"/>).</param>
    /// <param name="expectation">Savings plan expectation.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated <see cref="AccountDto"/>, or <c>null</c> when the account was not found.</returns>
    /// <exception cref="ArgumentException">Thrown when the bank contact is invalid, the name is missing or already exists, or the IBAN already exists for the user.</exception>
    public async Task<AccountDto?> UpdateAsync(Guid id, Guid ownerUserId, string name, string? iban, Guid bankContactId, SavingsPlanExpectation expectation, CancellationToken ct)
    {
        var account = await _db.Accounts.FirstOrDefaultAsync(a => a.Id == id && a.OwnerUserId == ownerUserId, ct);
        if (account == null) return null;
        if (!await _db.Contacts.AsNoTracking().AnyAsync(c => c.Id == bankContactId && c.OwnerUserId == ownerUserId && c.Type == ContactType.Bank, ct))
        {
            throw new ArgumentException("Bank contact invalid", nameof(bankContactId));
        }

        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name required", nameof(name));
        name = name.Trim();

        var duplicateName = await _db.Accounts.AsNoTracking().AnyAsync(a => a.OwnerUserId == ownerUserId && a.Id != id && a.Name.ToLower() == name.ToLower(), ct);
        if (duplicateName)
        {
            throw new ArgumentException("Account name already exists", nameof(name));
        }

        if (!string.IsNullOrWhiteSpace(iban))
        {
            iban = iban.Trim();
            bool exists = await _db.Accounts.AsNoTracking().AnyAsync(a => a.OwnerUserId == ownerUserId && a.Iban == iban && a.Id != id, ct);
            if (exists) throw new ArgumentException("IBAN already exists for user", nameof(iban));
        }
        account.Rename(name);
        account.SetIban(iban);
        account.SetBankContact(bankContactId);
        account.SetSavingsPlanExpectation(expectation);
        await _db.SaveChangesAsync(ct);
        return new AccountDto(account.Id, account.Name, account.Type, account.Iban, account.CurrentBalance, account.BankContactId, account.SymbolAttachmentId, account.SavingsPlanExpectation);
    }

    /// <summary>
    /// Deletes the account and related attachments. If the associated bank contact becomes unused it is removed as well.
    /// </summary>
    /// <param name="id">Identifier of the account to delete.</param>
    /// <param name="ownerUserId">Owner user identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><c>true</c> when the account existed and was removed; otherwise <c>false</c>.</returns>
    public async Task<bool> DeleteAsync(Guid id, Guid ownerUserId, CancellationToken ct)
    {
        var account = await _db.Accounts.FirstOrDefaultAsync(a => a.Id == id && a.OwnerUserId == ownerUserId, ct);
        if (account == null) return false;
        var bankContactId = account.BankContactId;

        // Delete attachments linked to this account
        var accountAttQuery = _db.Attachments
            .Where(a => a.OwnerUserId == ownerUserId && a.EntityKind == AttachmentEntityKind.Account && a.EntityId == account.Id);
        if (_db.Database.IsRelational())
        {
            await accountAttQuery.ExecuteDeleteAsync(ct);
        }
        else
        {
            var atts = await accountAttQuery.ToListAsync(ct);
            if (atts.Count > 0) _db.Attachments.RemoveRange(atts);
        }

        _db.Accounts.Remove(account);
        await _db.SaveChangesAsync(ct);

        bool anyOther = await _db.Accounts.AsNoTracking().AnyAsync(a => a.BankContactId == bankContactId, ct);
        if (!anyOther)
        {
            var bankContact = await _db.Contacts.FirstOrDefaultAsync(c => c.Id == bankContactId && c.Type == ContactType.Bank, ct);
            if (bankContact != null)
            {
                // Delete attachments linked to the bank contact being auto-removed
                var contactAttQuery = _db.Attachments
                    .Where(a => a.OwnerUserId == bankContact.OwnerUserId && a.EntityKind == AttachmentEntityKind.Contact && a.EntityId == bankContact.Id);
                if (_db.Database.IsRelational())
                {
                    await contactAttQuery.ExecuteDeleteAsync(ct);
                }
                else
                {
                    var catts = await contactAttQuery.ToListAsync(ct);
                    if (catts.Count > 0) _db.Attachments.RemoveRange(catts);
                }

                _db.Contacts.Remove(bankContact);
                await _db.SaveChangesAsync(ct);
            }
        }
        return true;
    }

    /// <summary>
    /// Lists accounts for the specified owner with optional paging. The method returns symbol resolution that falls back from account -> contact -> contact category.
    /// </summary>
    /// <param name="ownerUserId">Owner user identifier.</param>
    /// <param name="skip">Items to skip for paging.</param>
    /// <param name="take">Items to take for paging.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of <see cref="AccountDto"/> instances.</returns>
    public async Task<IReadOnlyList<AccountDto>> ListAsync(Guid ownerUserId, int skip, int take, CancellationToken ct)
    {
        // Left join Accounts -> Contacts -> ContactCategories to resolve fallback symbol attachment id
        var query = from a in _db.Accounts.AsNoTracking()
                    where a.OwnerUserId == ownerUserId
                    join c in _db.Contacts.AsNoTracking() on a.BankContactId equals c.Id into cj
                    from c in cj.DefaultIfEmpty()
                    join cat in _db.ContactCategories.AsNoTracking() on c.CategoryId equals cat.Id into catj
                    from cat in catj.DefaultIfEmpty()
                    orderby a.Name
                    select new AccountDto(
                        a.Id,
                        a.Name,
                        a.Type,
                        a.Iban,
                        a.CurrentBalance,
                        a.BankContactId,
                        // Treat Guid.Empty as not present and fall back to contact then category
                        (a.SymbolAttachmentId.HasValue && a.SymbolAttachmentId.Value != Guid.Empty) ? a.SymbolAttachmentId
                            : (c != null && c.SymbolAttachmentId.HasValue && c.SymbolAttachmentId.Value != Guid.Empty) ? c.SymbolAttachmentId
                            : cat.SymbolAttachmentId,
                        a.SavingsPlanExpectation
                    );

        return await query.Skip(skip).Take(take).ToListAsync(ct);
    }

    /// <summary>
    /// Retrieves a single account with resolved symbol attachment fallback.
    /// </summary>
    /// <param name="id">Account identifier.</param>
    /// <param name="ownerUserId">Owner user identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The mapped <see cref="AccountDto"/>, or <c>null</c> when not found.</returns>
    public async Task<AccountDto?> GetAsync(Guid id, Guid ownerUserId, CancellationToken ct)
    {
        var query = from a in _db.Accounts.AsNoTracking()
                    where a.OwnerUserId == ownerUserId && a.Id == id
                    join c in _db.Contacts.AsNoTracking() on a.BankContactId equals c.Id into cj
                    from c in cj.DefaultIfEmpty()
                    join cat in _db.ContactCategories.AsNoTracking() on c.CategoryId equals cat.Id into catj
                    from cat in catj.DefaultIfEmpty()
                    orderby a.Name
                    select new AccountDto(
                        a.Id,
                        a.Name,
                        a.Type,
                        a.Iban,
                        a.CurrentBalance,
                        a.BankContactId,
                        (a.SymbolAttachmentId.HasValue && a.SymbolAttachmentId.Value != Guid.Empty) ? a.SymbolAttachmentId
                            : (c != null && c.SymbolAttachmentId.HasValue && c.SymbolAttachmentId.Value != Guid.Empty) ? c.SymbolAttachmentId
                            : cat.SymbolAttachmentId,
                        a.SavingsPlanExpectation
                    );

        return await query.FirstOrDefaultAsync(ct);
    }

    /// <summary>
    /// Synchronous variant of <see cref="GetAsync(Guid, Guid, CancellationToken)"/> for read-only scenarios.
    /// </summary>
    /// <param name="id">Account identifier.</param>
    /// <param name="ownerUserId">Owner user identifier.</param>
    /// <returns>The mapped <see cref="AccountDto"/>, or <c>null</c> when not found.</returns>
    public AccountDto? Get(Guid id, Guid ownerUserId)
    {
        var query = from a in _db.Accounts.AsNoTracking()
                    where a.OwnerUserId == ownerUserId && a.Id == id
                    join c in _db.Contacts.AsNoTracking() on a.BankContactId equals c.Id into cj
                    from c in cj.DefaultIfEmpty()
                    join cat in _db.ContactCategories.AsNoTracking() on c.CategoryId equals cat.Id into catj
                    from cat in catj.DefaultIfEmpty()
                    orderby a.Name
                    select new AccountDto(
                        a.Id,
                        a.Name,
                        a.Type,
                        a.Iban,
                        a.CurrentBalance,
                        a.BankContactId,
                        (a.SymbolAttachmentId.HasValue && a.SymbolAttachmentId.Value != Guid.Empty) ? a.SymbolAttachmentId
                            : (c != null && c.SymbolAttachmentId.HasValue && c.SymbolAttachmentId.Value != Guid.Empty) ? c.SymbolAttachmentId
                            : cat.SymbolAttachmentId,
                        a.SavingsPlanExpectation
                    );

        return query.FirstOrDefault();
    }

    /// <summary>
    /// Sets or clears the symbol attachment for an account.
    /// </summary>
    /// <param name="id">Account identifier.</param>
    /// <param name="ownerUserId">Owner user identifier.</param>
    /// <param name="attachmentId">Attachment identifier to set, or <c>null</c> to clear.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the operation has finished.</returns>
    /// <exception cref="ArgumentException">Thrown when the account was not found for the specified id and owner.</exception>
    public async Task SetSymbolAttachmentAsync(Guid id, Guid ownerUserId, Guid? attachmentId, CancellationToken ct)
    {
        var account = await _db.Accounts.FirstOrDefaultAsync(a => a.Id == id && a.OwnerUserId == ownerUserId, ct);
        if (account == null) throw new ArgumentException("Account not found", nameof(id));
        account.SetSymbolAttachment(attachmentId);
        await _db.SaveChangesAsync(ct);
    }
}
