using FinanceManager.Domain.Accounts;
using FinanceManager.Domain.Contacts;
using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceManager.Tests.Accounts;

/// <summary>
/// Covers <see cref="AccountService"/>'s core account lifecycle: creating accounts with per-user IBAN
/// uniqueness enforcement, and deleting accounts while correctly cascading the ownership of their
/// associated bank contact (removing the bank contact only when no other account still references it).
/// </summary>
public sealed class AccountServiceTests
{
    private static (AccountService sut, AppDbContext db) Create()
    {
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        var sp = services.BuildServiceProvider();
        var db = sp.GetRequiredService<AppDbContext>();
        var sut = new AccountService(db);
        return (sut, db);
    }

    /// <summary>
    /// Verifies that a valid account with a unique IBAN is persisted and the returned DTO reflects the
    /// values passed to <see cref="AccountService.CreateAsync"/>.
    /// </summary>
    [Fact]
    public async Task CreateAsync_ShouldCreate_WhenValidAndUniqueIbanPerUser()
    {
        var (sut, db) = Create();
        var owner = Guid.NewGuid();
        var bankContact = new Contact(owner, "Bank A", ContactType.Bank, null);
        db.Contacts.Add(bankContact);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var dto = await sut.CreateAsync(owner, "Konto 1", AccountType.Giro, "DE123", bankContact.Id, SavingsPlanExpectation.Optional, true, false, CancellationToken.None);

        Assert.Equal("Konto 1", dto.Name);
        Assert.Equal("DE123", dto.Iban);
        Assert.Equal(1, db.Accounts.Count());
    }

    /// <summary>
    /// Ensures IBAN uniqueness is enforced per user: creating a second account with an IBAN already used
    /// by the same owner is rejected with an <see cref="ArgumentException"/> that names the IBAN as the
    /// cause, preventing silent duplicate-account creation for the same bank account.
    /// </summary>
    [Fact]
    public async Task CreateAsync_ShouldFail_WhenDuplicateIbanForSameUser()
    {
        var (sut, db) = Create();
        var owner = Guid.NewGuid();
        var bankContact = new Contact(owner, "Bank A", ContactType.Bank, null);
        db.Contacts.Add(bankContact);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await sut.CreateAsync(owner, "A", AccountType.Giro, "DE999", bankContact.Id, SavingsPlanExpectation.Optional, true, false, CancellationToken.None);
        Func<Task> act = () => sut.CreateAsync(owner, "B", AccountType.Giro, "DE999", bankContact.Id, SavingsPlanExpectation.Optional, true, false, CancellationToken.None);

        var ex = await Assert.ThrowsAsync<ArgumentException>(act);
        Assert.Contains("IBAN", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Confirms that deleting the only account linked to a bank contact also removes that bank contact,
    /// so orphaned bank contacts do not accumulate once their last referencing account is gone.
    /// </summary>
    [Fact]
    public async Task DeleteAsync_ShouldDeleteBankContact_WhenLastAccountOfContact()
    {
        var (sut, db) = Create();
        var owner = Guid.NewGuid();
        var bankContact = new Contact(owner, "Bank B", ContactType.Bank, null);
        db.Contacts.Add(bankContact);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var acc = await sut.CreateAsync(owner, "Main", AccountType.Giro, null, bankContact.Id, SavingsPlanExpectation.Optional, true, false, CancellationToken.None);

        var ok = await sut.DeleteAsync(acc.Id, owner, CancellationToken.None);

        Assert.True(ok);
        Assert.False(db.Accounts.Any());
        Assert.False(db.Contacts.Any(c => c.Id == bankContact.Id));
    }

    /// <summary>
    /// Guards against over-eager cleanup: deleting one account must not delete a bank contact that is
    /// still referenced by another account of the same owner.
    /// </summary>
    [Fact]
    public async Task DeleteAsync_ShouldNotDeleteBankContact_WhenOtherAccountsExist()
    {
        var (sut, db) = Create();
        var owner = Guid.NewGuid();
        var bankContact = new Contact(owner, "Bank C", ContactType.Bank, null);
        db.Contacts.Add(bankContact);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var a1 = await sut.CreateAsync(owner, "A1", AccountType.Giro, null, bankContact.Id, SavingsPlanExpectation.Optional, true, false, CancellationToken.None);
        var a2 = await sut.CreateAsync(owner, "A2", AccountType.Giro, null, bankContact.Id, SavingsPlanExpectation.Optional, true, false, CancellationToken.None);

        var ok = await sut.DeleteAsync(a1.Id, owner, CancellationToken.None);

        Assert.True(ok);
        Assert.True(db.Contacts.Any(c => c.Id == bankContact.Id));
    }
}
