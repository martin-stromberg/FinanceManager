using FinanceManager.Domain.Accounts;
using FinanceManager.Domain.Attachments;
using FinanceManager.Domain.Contacts;
using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Accounts;
using FinanceManager.Infrastructure.Contacts;
using FinanceManager.Infrastructure.Savings;
using FinanceManager.Infrastructure.Securities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Tests.Attachments;

/// <summary>
/// Verifies that attachments follow the lifecycle of the entity they are attached to: deleting a
/// contact, account, savings plan, or security must also delete every attachment stored against it, so
/// no orphaned attachment rows are left behind when the owning entity disappears. For bank contacts
/// specifically (which are indirectly owned through an account), also confirms the cascade only fires
/// once the last referencing account is gone.
/// </summary>
public sealed class AttachmentCleanupTests
{
    private static (AppDbContext db, SqliteConnection conn, Guid owner) CreateDb()
    {
        var conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options;
        var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        var owner = new FinanceManager.Domain.Users.User("owner", "hash", true);
        db.Users.Add(owner);
        db.SaveChanges();
        // ensure self contact exists for owner
        db.Contacts.Add(new Contact(owner.Id, "Self", ContactType.Self, null, null));
        db.SaveChanges();
        return (db, conn, owner.Id);
    }

    /// <summary>
    /// Verifies that deleting a contact removes only its own attachments and leaves attachments belonging
    /// to a different contact untouched.
    /// </summary>
    [Fact]
    public async Task DeleteContact_ShouldRemoveContactAttachments()
    {
        var (db, conn, owner) = CreateDb();
        var svc = new ContactService(db);
        var c1 = new Contact(owner, "Alpha", ContactType.Person, null, null);
        var c2 = new Contact(owner, "Beta", ContactType.Person, null, null);
        db.Contacts.AddRange(c1, c2);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        // add attachments for both contacts
        db.Attachments.Add(new Attachment(owner, AttachmentEntityKind.Contact, c1.Id, "a.txt", "text/plain", 1, null, null, new byte[] { 1 }, null));
        db.Attachments.Add(new Attachment(owner, AttachmentEntityKind.Contact, c2.Id, "b.txt", "text/plain", 1, null, null, new byte[] { 2 }, null));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, await db.Attachments.CountAsync(a => a.EntityKind == AttachmentEntityKind.Contact, cancellationToken: TestContext.Current.CancellationToken));

        var ok = await svc.DeleteAsync(c1.Id, owner, CancellationToken.None);
        Assert.True(ok);

        // c1 attachments removed, c2 remains
        Assert.Equal(0, await db.Attachments.CountAsync(a => a.EntityKind == AttachmentEntityKind.Contact && a.EntityId == c1.Id, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(1, await db.Attachments.CountAsync(a => a.EntityKind == AttachmentEntityKind.Contact && a.EntityId == c2.Id, cancellationToken: TestContext.Current.CancellationToken));

        conn.Dispose();
    }

    /// <summary>
    /// Verifies that deleting the last account of a bank contact removes both the account's own
    /// attachments and, because the bank contact itself is cascade-deleted, the bank contact's
    /// attachments as well - the attachment cleanup must follow the contact cascade, not stop at the
    /// account.
    /// </summary>
    [Fact]
    public async Task DeleteAccount_ShouldRemoveAccountAttachments_AndBankContactAttachments_WhenLastAccount()
    {
        var (db, conn, owner) = CreateDb();
        var bank = new Contact(owner, "Bank X", ContactType.Bank, null, null);
        db.Contacts.Add(bank);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var acc = new Account(owner, AccountType.Giro, "Konto A", "DE00", bank.Id);
        db.Accounts.Add(acc);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        // add attachments to account and bank contact
        db.Attachments.Add(new Attachment(owner, AttachmentEntityKind.Account, acc.Id, "acc.txt", "text/plain", 1, null, null, new byte[] { 1 }, null));
        db.Attachments.Add(new Attachment(owner, AttachmentEntityKind.Contact, bank.Id, "bank.txt", "text/plain", 1, null, null, new byte[] { 1 }, null));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var svc = new AccountService(db);
        var ok = await svc.DeleteAsync(acc.Id, owner, CancellationToken.None);
        Assert.True(ok);

        // account attachments removed
        Assert.Equal(0, await db.Attachments.CountAsync(a => a.EntityKind == AttachmentEntityKind.Account && a.EntityId == acc.Id, cancellationToken: TestContext.Current.CancellationToken));
        // bank contact removed -> its attachments removed too
        Assert.False(await db.Contacts.AnyAsync(c => c.Id == bank.Id, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(0, await db.Attachments.CountAsync(a => a.EntityKind == AttachmentEntityKind.Contact && a.EntityId == bank.Id, cancellationToken: TestContext.Current.CancellationToken));

        conn.Dispose();
    }

    /// <summary>
    /// Guards against over-eager cleanup: deleting one of two accounts sharing a bank contact must leave
    /// the bank contact and its attachments intact, and only deleting the remaining account should then
    /// trigger removal of the bank contact and its attachments.
    /// </summary>
    [Fact]
    public async Task DeleteAccount_ShouldNotRemoveBankContactAttachments_WhenAnotherAccountExists()
    {
        var (db, conn, owner) = CreateDb();
        var bank = new Contact(owner, "Bank Y", ContactType.Bank, null, null);
        db.Contacts.Add(bank);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var acc1 = new Account(owner, AccountType.Giro, "Konto A", "DE00", bank.Id);
        var acc2 = new Account(owner, AccountType.Savings, "Konto B", "DE01", bank.Id);
        db.Accounts.AddRange(acc1, acc2);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        // bank contact attachment
        db.Attachments.Add(new Attachment(owner, AttachmentEntityKind.Contact, bank.Id, "bankY.txt", "text/plain", 1, null, null, new byte[] { 1 }, null));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var svc = new AccountService(db);
        var ok = await svc.DeleteAsync(acc1.Id, owner, CancellationToken.None);
        Assert.True(ok);

        // bank contact still exists, its attachment remains
        Assert.True(await db.Contacts.AnyAsync(c => c.Id == bank.Id, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(1, await db.Attachments.CountAsync(a => a.EntityKind == AttachmentEntityKind.Contact && a.EntityId == bank.Id, cancellationToken: TestContext.Current.CancellationToken));

        // cleanup: delete second account, then bank contact attachment should be removed
        ok = await svc.DeleteAsync(acc2.Id, owner, CancellationToken.None);
        Assert.True(ok);
        Assert.False(await db.Contacts.AnyAsync(c => c.Id == bank.Id, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(0, await db.Attachments.CountAsync(a => a.EntityKind == AttachmentEntityKind.Contact && a.EntityId == bank.Id, cancellationToken: TestContext.Current.CancellationToken));

        conn.Dispose();
    }

    /// <summary>
    /// Verifies that deleting an archived savings plan removes the attachments stored against it.
    /// </summary>
    [Fact]
    public async Task DeleteSavingsPlan_ShouldRemovePlanAttachments()
    {
        var (db, conn, owner) = CreateDb();
        var svc = new SavingsPlanService(db);
        var dto = await svc.CreateAsync(owner, "Plan A", SavingsPlanType.OneTime, null, null, null, null, null, CancellationToken.None);
        // archive then add attachment and delete
        var archived = await svc.ArchiveAsync(dto.Id, owner, CancellationToken.None);
        Assert.True(archived);

        db.Attachments.Add(new Attachment(owner, AttachmentEntityKind.SavingsPlan, dto.Id, "sp.txt", "text/plain", 1, null, null, new byte[] { 1 }, null));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var ok = await svc.DeleteAsync(dto.Id, owner, CancellationToken.None);
        Assert.True(ok);
        Assert.Equal(0, await db.Attachments.CountAsync(a => a.EntityKind == AttachmentEntityKind.SavingsPlan && a.EntityId == dto.Id, cancellationToken: TestContext.Current.CancellationToken));
        conn.Dispose();
    }

    /// <summary>
    /// Verifies that deleting an archived security removes the attachments stored against it.
    /// </summary>
    [Fact]
    public async Task DeleteSecurity_ShouldRemoveSecurityAttachments()
    {
        var (db, conn, owner) = CreateDb();
        var svc = new SecurityService(db);
        var created = await svc.CreateAsync(owner, name: "SEC A", identifier: "ID123", description: null, alphaVantageCode: null, currencyCode: "EUR", categoryId: null, ct: CancellationToken.None);
        // archive before delete
        var archived = await svc.ArchiveAsync(created.Id, owner, CancellationToken.None);
        Assert.True(archived);

        db.Attachments.Add(new Attachment(owner, AttachmentEntityKind.Security, created.Id, "sec.txt", "text/plain", 1, null, null, new byte[] { 1 }, null));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var ok = await svc.DeleteAsync(created.Id, owner, CancellationToken.None);
        Assert.True(ok);
        Assert.Equal(0, await db.Attachments.CountAsync(a => a.EntityKind == AttachmentEntityKind.Security && a.EntityId == created.Id, cancellationToken: TestContext.Current.CancellationToken));
        conn.Dispose();
    }
}
