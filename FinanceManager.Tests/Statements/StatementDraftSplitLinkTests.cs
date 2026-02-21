using FinanceManager.Application.Accounts;
using FinanceManager.Domain.Accounts;
using FinanceManager.Domain.Contacts;
using FinanceManager.Domain.Postings;
using FinanceManager.Domain.Statements;
using FinanceManager.Domain.Users;
using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Aggregates;
using FinanceManager.Infrastructure.Statements;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace FinanceManager.Tests.Statements;

public sealed class StatementDraftSplitLinkTests
{
    private static (StatementDraftService sut, AppDbContext db, SqliteConnection conn, Guid owner) Create()
    {
        var conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(conn)
            .Options;
        var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        var owner = new User("owner", "hash", true);
        db.Users.Add(owner);
        db.SaveChanges();
        var self = new Contact(owner.Id, "Ich", ContactType.Self, null, null);
        db.Contacts.Add(self);
        db.SaveChanges();

        var accountService = new TestAccountService();
        var sut = new StatementDraftService(db, new PostingAggregateService(db), accountService, null, null, NullLogger<StatementDraftService>.Instance, null);
        return (sut, db, conn, owner.Id);
    }

    private sealed class TestAccountService : IAccountService
    {
        public Task<AccountDto> CreateAsync(Guid ownerUserId, string name, AccountType type, string? iban, Guid bankContactId, SavingsPlanExpectation expectation, CancellationToken ct)
            => throw new NotImplementedException();

        public Task<AccountDto?> UpdateAsync(Guid id, Guid ownerUserId, string name, string? iban, Guid bankContactId, SavingsPlanExpectation expectation, CancellationToken ct)
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
    }

    /// <summary>
    /// Ensures split-draft postings use the parent booking date and child booking dates as valuta dates.
    /// </summary>
    [Fact]
    public async Task BookAsync_ShouldUseParentBookingDateAndChildBookingAsValuta_WhenSplitDraftsLinked()
    {
        var (sut, db, conn, owner) = Create();

        var bank = new Contact(owner, "Bank", ContactType.Bank, null, null);
        var intermediary = new Contact(owner, "CardProvider", ContactType.Organization, null, null, isPaymentIntermediary: true);
        var contactA = new Contact(owner, "Shop A", ContactType.Organization, null, null);
        var contactB = new Contact(owner, "Shop B", ContactType.Organization, null, null);
        var contactC = new Contact(owner, "Shop C", ContactType.Organization, null, null);
        db.Contacts.AddRange(bank, intermediary, contactA, contactB, contactC);
        await db.SaveChangesAsync();

        var account = new Account(owner, AccountType.Giro, "Giro", "DE12500105170648489890", bank.Id);
        db.Accounts.Add(account);
        await db.SaveChangesAsync();

        var childDraft = new StatementDraft(owner, "child.pdf", null, null);
        childDraft.SetUploadGroup(Guid.NewGuid());
        var parentDraft = new StatementDraft(owner, "parent.pdf", null, null);
        parentDraft.SetUploadGroup(Guid.NewGuid());
        parentDraft.SetDetectedAccount(account.Id);
        db.StatementDrafts.AddRange(childDraft, parentDraft);
        await db.SaveChangesAsync();

        var childEntry1 = childDraft.AddEntry(new DateTime(2026, 1, 10), -30m, "Child A", contactA.Name, new DateTime(2026, 1, 11), "EUR", null, false);
        childEntry1.MarkAccounted(contactA.Id);
        db.Entry(childEntry1).State = EntityState.Added;

        var childEntry2 = childDraft.AddEntry(new DateTime(2026, 1, 12), -40m, "Child B", contactB.Name, null, "EUR", null, false);
        childEntry2.MarkAccounted(contactB.Id);
        db.Entry(childEntry2).State = EntityState.Added;

        var childEntry3 = childDraft.AddEntry(new DateTime(2026, 1, 20), -50m, "Child C", contactC.Name, new DateTime(2026, 1, 21), "EUR", null, false);
        childEntry3.MarkAccounted(contactC.Id);
        db.Entry(childEntry3).State = EntityState.Added;

        var parentBookingDate = new DateTime(2026, 2, 5);
        var parentEntry = parentDraft.AddEntry(parentBookingDate, -120m, "Card Statement", intermediary.Name, parentBookingDate, "EUR", null, false);
        parentEntry.MarkAccounted(intermediary.Id);
        db.Entry(parentEntry).State = EntityState.Added;

        await db.SaveChangesAsync();

        await sut.SetEntrySplitDraftAsync(parentDraft.Id, parentEntry.Id, childDraft.Id, owner, CancellationToken.None);

        var result = await sut.BookAsync(parentDraft.Id, null, owner, forceWarnings: true, CancellationToken.None);
        Assert.True(result.Success);

        var childEntries = await db.StatementDraftEntries.AsNoTracking()
            .Where(e => e.DraftId == childDraft.Id)
            .ToListAsync();
        var childBookings = childEntries.ToDictionary(e => e.Id, e => e.BookingDate);

        var childPostings = await db.Postings.AsNoTracking()
            .Where(p => p.SourceId != Guid.Empty && childBookings.Keys.Contains(p.SourceId))
            .ToListAsync();

        var parentZeroPostings = await db.Postings.AsNoTracking()
            .Where(p => p.SourceId == parentEntry.Id && p.Amount == 0m)
            .ToListAsync();

        Assert.NotEmpty(childPostings);
        Assert.NotEmpty(parentZeroPostings);
        foreach (var posting in childPostings)
        {
            Assert.Equal(parentBookingDate.Date, posting.BookingDate.Date);
            Assert.Equal(childBookings[posting.SourceId].Date, posting.ValutaDate.Date);
        }

        foreach (var posting in parentZeroPostings)
        {
            Assert.Equal(parentEntry.Amount, posting.OriginalAmount);
        }

        conn.Dispose();
    }

    /// <summary>
    /// Ensures split-draft entries are not marked as already booked when the amount differs.
    /// </summary>
    [Fact]
    public async Task ClassifyAsync_ShouldKeepOpen_WhenSplitDraftEntryAmountDiffers()
    {
        var (sut, db, conn, owner) = Create();

        var bank = new Contact(owner, "Bank", ContactType.Bank, null, null);
        var intermediary = new Contact(owner, "CardProvider", ContactType.Organization, null, null, isPaymentIntermediary: true);
        var contactA = new Contact(owner, "Shop A", ContactType.Organization, null, null);
        var contactB = new Contact(owner, "Shop B", ContactType.Organization, null, null);
        var contactC = new Contact(owner, "Shop C", ContactType.Organization, null, null);
        db.Contacts.AddRange(bank, intermediary, contactA, contactB, contactC);
        await db.SaveChangesAsync();

        var account = new Account(owner, AccountType.Giro, "Giro", "DE12500105170648489890", bank.Id);
        db.Accounts.Add(account);
        await db.SaveChangesAsync();

        var childDraft = new StatementDraft(owner, "child.pdf", null, null);
        childDraft.SetUploadGroup(Guid.NewGuid());
        var parentDraft = new StatementDraft(owner, "parent.pdf", null, null);
        parentDraft.SetUploadGroup(Guid.NewGuid());
        parentDraft.SetDetectedAccount(account.Id);
        db.StatementDrafts.AddRange(childDraft, parentDraft);
        await db.SaveChangesAsync();

        var childEntry1 = childDraft.AddEntry(new DateTime(2026, 1, 10), -30m, "Child A", contactA.Name, new DateTime(2026, 1, 11), "EUR", null, false);
        childEntry1.MarkAccounted(contactA.Id);
        db.Entry(childEntry1).State = EntityState.Added;

        var childEntry2 = childDraft.AddEntry(new DateTime(2026, 1, 12), -40m, "Child B", contactB.Name, null, "EUR", null, false);
        childEntry2.MarkAccounted(contactB.Id);
        db.Entry(childEntry2).State = EntityState.Added;

        var childEntry3 = childDraft.AddEntry(new DateTime(2026, 1, 20), -50m, "Child C", contactC.Name, new DateTime(2026, 1, 21), "EUR", null, false);
        childEntry3.MarkAccounted(contactC.Id);
        db.Entry(childEntry3).State = EntityState.Added;

        var parentBookingDate = new DateTime(2026, 2, 5);
        var parentEntry = parentDraft.AddEntry(parentBookingDate, -120m, "Card Statement", intermediary.Name, parentBookingDate, "EUR", null, false);
        parentEntry.MarkAccounted(intermediary.Id);
        db.Entry(parentEntry).State = EntityState.Added;
        await db.SaveChangesAsync();

        await sut.SetEntrySplitDraftAsync(parentDraft.Id, parentEntry.Id, childDraft.Id, owner, CancellationToken.None);

        var booked = await sut.BookAsync(parentDraft.Id, null, owner, forceWarnings: true, CancellationToken.None);
        Assert.True(booked.Success);

        var secondParent = new StatementDraft(owner, "parent-2.pdf", null, null);
        secondParent.SetUploadGroup(Guid.NewGuid());
        secondParent.SetDetectedAccount(account.Id);
        db.StatementDrafts.Add(secondParent);
        await db.SaveChangesAsync();

        var secondEntry = secondParent.AddEntry(parentBookingDate, parentEntry.Amount - 1m, parentEntry.Subject, intermediary.Name, parentBookingDate, "EUR", null, false);
        secondEntry.MarkAccounted(intermediary.Id);
        db.Entry(secondEntry).State = EntityState.Added;
        await db.SaveChangesAsync();

        var classified = await sut.ClassifyAsync(secondParent.Id, null, owner, CancellationToken.None);
        Assert.NotNull(classified);

        var updatedEntry = await db.StatementDraftEntries.AsNoTracking()
            .FirstAsync(e => e.Id == secondEntry.Id);
        Assert.NotEqual(StatementDraftEntryStatus.AlreadyBooked, updatedEntry.Status);

        conn.Dispose();
    }

    /// <summary>
    /// Ensures split-draft duplicates are detected when re-importing the parent statement entry.
    /// </summary>
    [Fact]
    public async Task ClassifyAsync_ShouldMarkAlreadyBooked_WhenSplitDraftEntryReimported()
    {
        var (sut, db, conn, owner) = Create();

        var bank = new Contact(owner, "Bank", ContactType.Bank, null, null);
        var intermediary = new Contact(owner, "CardProvider", ContactType.Organization, null, null, isPaymentIntermediary: true);
        var contactA = new Contact(owner, "Shop A", ContactType.Organization, null, null);
        var contactB = new Contact(owner, "Shop B", ContactType.Organization, null, null);
        var contactC = new Contact(owner, "Shop C", ContactType.Organization, null, null);
        db.Contacts.AddRange(bank, intermediary, contactA, contactB, contactC);
        await db.SaveChangesAsync();

        var account = new Account(owner, AccountType.Giro, "Giro", "DE12500105170648489890", bank.Id);
        db.Accounts.Add(account);
        await db.SaveChangesAsync();

        var childDraft = new StatementDraft(owner, "child.pdf", null, null);
        childDraft.SetUploadGroup(Guid.NewGuid());
        var parentDraft = new StatementDraft(owner, "parent.pdf", null, null);
        parentDraft.SetUploadGroup(Guid.NewGuid());
        parentDraft.SetDetectedAccount(account.Id);
        db.StatementDrafts.AddRange(childDraft, parentDraft);
        await db.SaveChangesAsync();

        var childEntry1 = childDraft.AddEntry(new DateTime(2026, 1, 10), -30m, "Child A", contactA.Name, new DateTime(2026, 1, 11), "EUR", null, false);
        childEntry1.MarkAccounted(contactA.Id);
        db.Entry(childEntry1).State = EntityState.Added;

        var childEntry2 = childDraft.AddEntry(new DateTime(2026, 1, 12), -40m, "Child B", contactB.Name, null, "EUR", null, false);
        childEntry2.MarkAccounted(contactB.Id);
        db.Entry(childEntry2).State = EntityState.Added;

        var childEntry3 = childDraft.AddEntry(new DateTime(2026, 1, 20), -50m, "Child C", contactC.Name, new DateTime(2026, 1, 21), "EUR", null, false);
        childEntry3.MarkAccounted(contactC.Id);
        db.Entry(childEntry3).State = EntityState.Added;

        var parentBookingDate = new DateTime(2026, 2, 5);
        var parentEntry = parentDraft.AddEntry(parentBookingDate, -120m, "Card Statement", intermediary.Name, parentBookingDate, "EUR", null, false);
        parentEntry.MarkAccounted(intermediary.Id);
        db.Entry(parentEntry).State = EntityState.Added;
        await db.SaveChangesAsync();

        await sut.SetEntrySplitDraftAsync(parentDraft.Id, parentEntry.Id, childDraft.Id, owner, CancellationToken.None);

        var booked = await sut.BookAsync(parentDraft.Id, null, owner, forceWarnings: true, CancellationToken.None);
        Assert.True(booked.Success);

        var secondParent = new StatementDraft(owner, "parent-2.pdf", null, null);
        secondParent.SetUploadGroup(Guid.NewGuid());
        secondParent.SetDetectedAccount(account.Id);
        db.StatementDrafts.Add(secondParent);
        await db.SaveChangesAsync();

        var secondEntry = secondParent.AddEntry(parentBookingDate, parentEntry.Amount, parentEntry.Subject, intermediary.Name, parentBookingDate, "EUR", null, false);
        secondEntry.MarkAccounted(intermediary.Id);
        db.Entry(secondEntry).State = EntityState.Added;
        await db.SaveChangesAsync();

        var classified = await sut.ClassifyAsync(secondParent.Id, null, owner, CancellationToken.None);
        Assert.NotNull(classified);

        var updatedEntry = await db.StatementDraftEntries.AsNoTracking()
            .FirstAsync(e => e.Id == secondEntry.Id);
        Assert.Equal(StatementDraftEntryStatus.AlreadyBooked, updatedEntry.Status);

        conn.Dispose();
    }

    [Fact]
    public async Task SetEntrySplitDraftAsync_ShouldLink_WhenUploadGroupIdDiffers()
    {
        var (sut, db, conn, owner) = Create();

        var parent = new StatementDraft(owner, "parent.pdf", null, null);
        parent.SetUploadGroup(Guid.NewGuid());
        db.StatementDrafts.Add(parent);
        var child = new StatementDraft(owner, "child.pdf", null, null);
        child.SetUploadGroup(Guid.NewGuid()); // different group -> should be allowed now
        db.StatementDrafts.Add(child);
        await db.SaveChangesAsync();

        var intermediary = new Contact(owner, "PayService", ContactType.Organization, null, null, isPaymentIntermediary: true);
        db.Contacts.Add(intermediary);
        await db.SaveChangesAsync();

        var pEntry = parent.AddEntry(DateTime.Today, 100m, "Root", intermediary.Name, DateTime.Today, "EUR", null, false);
        db.Entry(pEntry).State = EntityState.Added;
        pEntry.MarkAccounted(intermediary.Id);
        await db.SaveChangesAsync();

        // Act
        var result = await sut.SetEntrySplitDraftAsync(parent.Id, pEntry.Id, child.Id, owner, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        var updated = await db.StatementDraftEntries.FirstAsync(e => e.Id == pEntry.Id);
        Assert.Equal(child.Id, updated.SplitDraftId);

        conn.Dispose();
    }

    [Fact]
    public async Task SetEntrySplitDraftAsync_ShouldThrow_WhenUploadGroupIdSame()
    {
        var (sut, db, conn, owner) = Create();

        var uploadId = Guid.NewGuid();
        var parent = new StatementDraft(owner, "parent.pdf", null, null);
        parent.SetUploadGroup(uploadId);
        db.StatementDrafts.Add(parent);
        var child = new StatementDraft(owner, "child.pdf", null, null);
        child.SetUploadGroup(uploadId); // same group -> should now fail
        db.StatementDrafts.Add(child);
        await db.SaveChangesAsync();

        var intermediary = new Contact(owner, "PayService", ContactType.Organization, null, null, isPaymentIntermediary: true);
        db.Contacts.Add(intermediary);
        await db.SaveChangesAsync();

        var pEntry = parent.AddEntry(DateTime.Today, 100m, "Root", intermediary.Name, DateTime.Today, "EUR", null, false);
        db.Entry(pEntry).State = EntityState.Added;
        pEntry.MarkAccounted(intermediary.Id);
        await db.SaveChangesAsync();

        var act = async () => await sut.SetEntrySplitDraftAsync(parent.Id, pEntry.Id, child.Id, owner, CancellationToken.None);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(act);
        Assert.Contains("must NOT originate from the same upload", ex.Message, StringComparison.OrdinalIgnoreCase);

        conn.Dispose();
    }
}
