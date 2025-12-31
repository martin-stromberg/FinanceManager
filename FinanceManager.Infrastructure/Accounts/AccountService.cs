using FinanceManager.Application.Accounts;
using FinanceManager.Domain.Attachments; // added
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Accounts;

public sealed class AccountService : IAccountService
{
    private readonly AppDbContext _db;
    public AccountService(AppDbContext db) => _db = db;

    public async Task<AccountDto> CreateAsync(Guid ownerUserId, string name, AccountType type, string? iban, Guid bankContactId, CancellationToken ct)
    {
        // default expectation for older callers
        return await CreateAsync(ownerUserId, name, type, iban, bankContactId, SavingsPlanExpectation.Optional, ct);
    }

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

    public async Task<AccountDto?> GetAsync(Guid id, Guid ownerUserId, CancellationToken ct)
    {
        return await _db.Accounts.AsNoTracking()
            .Where(a => a.Id == id && a.OwnerUserId == ownerUserId)
            .Select(a => new AccountDto(a.Id, a.Name, a.Type, a.Iban, a.CurrentBalance, a.BankContactId, a.SymbolAttachmentId, a.SavingsPlanExpectation))
            .FirstOrDefaultAsync(ct);
    }

    public async Task SetSymbolAttachmentAsync(Guid id, Guid ownerUserId, Guid? attachmentId, CancellationToken ct)
    {
        var account = await _db.Accounts.FirstOrDefaultAsync(a => a.Id == id && a.OwnerUserId == ownerUserId, ct);
        if (account == null) throw new ArgumentException("Account not found", nameof(id));
        account.SetSymbolAttachment(attachmentId);
        await _db.SaveChangesAsync(ct);
    }
}
