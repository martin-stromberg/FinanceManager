using FinanceManager.Domain.Accounts;
using FinanceManager.Domain.Contacts;
using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Accounts;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Tests.Accounts;

/// <summary>
/// Covers the collection-account feature of <see cref="AccountService"/>: creating and toggling the
/// <c>IsCollectionAccount</c> flag, and managing the set of linked IBANs (add, remove, list, and their
/// inclusion in the account DTO) that a collection account aggregates.
/// </summary>
public sealed class AccountServiceTests_CollectionAccount
{
    private static (AccountService sut, AppDbContext db) Create()
    {
        var conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(conn)
            .Options;
        var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        var sut = new AccountService(db);
        return (sut, db);
    }

    private static async Task<(AccountService sut, AppDbContext db, Guid bankContactId)> CreateWithContactAsync()
    {
        var (sut, db) = Create();
        var owner = Guid.NewGuid();
        var contact = new Contact(owner, "Bank", ContactType.Bank, null);
        db.Contacts.Add(contact);
        await db.SaveChangesAsync();
        return (sut, db, contact.Id);
    }

    /// <summary>
    /// Verifies that passing <c>isCollectionAccount: true</c> to <see cref="AccountService.CreateAsync"/>
    /// is persisted on the created account, both in the returned DTO and in the database.
    /// </summary>
    [Fact]
    public async Task CreateAsync_ShouldSetIsCollectionAccount_WhenFlagIsTrue()
    {
        var (sut, db, bankContactId) = await CreateWithContactAsync();
        var owner = db.Contacts.First().OwnerUserId;

        var dto = await sut.CreateAsync(owner, "Sammelkonto", AccountType.Savings, null, bankContactId, SavingsPlanExpectation.Optional, true, isCollectionAccount: true, CancellationToken.None);

        Assert.True(dto.IsCollectionAccount);
        Assert.True(db.Accounts.Single().IsCollectionAccount);
    }

    /// <summary>
    /// Ensures an existing, non-collection account can be converted into a collection account through
    /// <see cref="AccountService.UpdateAsync"/>, i.e. the flag is not fixed at creation time.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_ShouldToggleIsCollectionAccount()
    {
        var (sut, db, bankContactId) = await CreateWithContactAsync();
        var owner = db.Contacts.First().OwnerUserId;

        var created = await sut.CreateAsync(owner, "Konto", AccountType.Giro, null, bankContactId, SavingsPlanExpectation.Optional, true, false, CancellationToken.None);
        Assert.False(created.IsCollectionAccount);

        var updated = await sut.UpdateAsync(created.Id, owner, "Konto", null, bankContactId, SavingsPlanExpectation.Optional, true, true, CancellationToken.None);

        Assert.NotNull(updated);
        Assert.True(updated!.IsCollectionAccount);
        Assert.True(db.Accounts.Single().IsCollectionAccount);
    }

    /// <summary>
    /// Verifies that a new, unique IBAN can be linked to a collection account and is stored as an
    /// <c>AccountLinkedIbans</c> entry.
    /// </summary>
    [Fact]
    public async Task AddLinkedIbanAsync_ShouldAddIban_WhenValidAndUnique()
    {
        var (sut, db, bankContactId) = await CreateWithContactAsync();
        var owner = db.Contacts.First().OwnerUserId;
        var acc = await sut.CreateAsync(owner, "Sammelkonto", AccountType.Savings, null, bankContactId, SavingsPlanExpectation.Optional, true, true, CancellationToken.None);

        await sut.AddLinkedIbanAsync(acc.Id, owner, "DE12345678901234567890", CancellationToken.None);

        Assert.Equal(1, db.AccountLinkedIbans.Count());
        Assert.Equal("DE12345678901234567890", db.AccountLinkedIbans.Single().Iban);
    }

    /// <summary>
    /// Ensures the same IBAN cannot be linked twice to the same collection account: a second attempt
    /// throws <see cref="InvalidOperationException"/> naming the IBAN, preventing duplicate entries in
    /// the linked-IBAN list.
    /// </summary>
    [Fact]
    public async Task AddLinkedIbanAsync_ShouldFail_WhenDuplicateIbanForSameAccount()
    {
        var (sut, db, bankContactId) = await CreateWithContactAsync();
        var owner = db.Contacts.First().OwnerUserId;
        var acc = await sut.CreateAsync(owner, "Sammelkonto", AccountType.Savings, null, bankContactId, SavingsPlanExpectation.Optional, true, true, CancellationToken.None);
        await sut.AddLinkedIbanAsync(acc.Id, owner, "DE12345678901234567890", CancellationToken.None);

        Func<Task> act = () => sut.AddLinkedIbanAsync(acc.Id, owner, "DE12345678901234567890", CancellationToken.None);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(act);
        Assert.Contains("IBAN", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies that a previously linked IBAN can be removed from a collection account again, and that
    /// the removal is reflected in the database.
    /// </summary>
    [Fact]
    public async Task RemoveLinkedIbanAsync_ShouldRemoveIban_WhenExists()
    {
        var (sut, db, bankContactId) = await CreateWithContactAsync();
        var owner = db.Contacts.First().OwnerUserId;
        var acc = await sut.CreateAsync(owner, "Sammelkonto", AccountType.Savings, null, bankContactId, SavingsPlanExpectation.Optional, true, true, CancellationToken.None);
        await sut.AddLinkedIbanAsync(acc.Id, owner, "DE12345678901234567890", CancellationToken.None);

        var ok = await sut.RemoveLinkedIbanAsync(acc.Id, owner, "DE12345678901234567890", CancellationToken.None);

        Assert.True(ok);
        Assert.Equal(0, db.AccountLinkedIbans.Count());
    }

    /// <summary>
    /// Confirms that all IBANs linked to a collection account are returned together, and none are
    /// dropped when more than one is present.
    /// </summary>
    [Fact]
    public async Task GetLinkedIbansAsync_ShouldReturnIbans_ForCollectionAccount()
    {
        var (sut, db, bankContactId) = await CreateWithContactAsync();
        var owner = db.Contacts.First().OwnerUserId;
        var acc = await sut.CreateAsync(owner, "Sammelkonto", AccountType.Savings, null, bankContactId, SavingsPlanExpectation.Optional, true, true, CancellationToken.None);
        await sut.AddLinkedIbanAsync(acc.Id, owner, "DE11111111111111111111", CancellationToken.None);
        await sut.AddLinkedIbanAsync(acc.Id, owner, "DE22222222222222222222", CancellationToken.None);

        var ibans = await sut.GetLinkedIbansAsync(acc.Id, owner, CancellationToken.None);

        Assert.NotNull(ibans);
        Assert.Equal(2, ibans.Count);
        Assert.Contains("DE11111111111111111111", ibans);
        Assert.Contains("DE22222222222222222222", ibans);
    }

    /// <summary>
    /// Ensures the linked IBANs of a collection account are projected into the account DTO returned by
    /// <see cref="AccountService.GetAsync"/>, so callers do not need a separate round-trip to see them.
    /// </summary>
    [Fact]
    public async Task GetAsync_ShouldIncludeLinkedIbans_InAccountDto()
    {
        var (sut, db, bankContactId) = await CreateWithContactAsync();
        var owner = db.Contacts.First().OwnerUserId;
        var acc = await sut.CreateAsync(owner, "Sammelkonto", AccountType.Savings, null, bankContactId, SavingsPlanExpectation.Optional, true, true, CancellationToken.None);
        await sut.AddLinkedIbanAsync(acc.Id, owner, "DE99999999999999999999", CancellationToken.None);

        var dto = await sut.GetAsync(acc.Id, owner, CancellationToken.None);

        Assert.NotNull(dto);
        Assert.Single(dto!.LinkedIbans);
        Assert.Contains("DE99999999999999999999", dto.LinkedIbans);
    }
}
