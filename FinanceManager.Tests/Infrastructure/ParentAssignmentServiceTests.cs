using FinanceManager.Domain.Contacts;
using FinanceManager.Domain.Statements;
using FinanceManager.Domain.Users;
using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Common;
using FinanceManager.Shared.Dtos.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace FinanceManager.Tests.Infrastructure;

public sealed class ParentAssignmentServiceTests
{
    private const string StatementEntryParentKind = "statement-drafts/entries";

    private static AppDbContext CreateSqliteContext()
    {
        var conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(conn)
            .Options;
        var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    private static (User User, StatementDraft Draft, StatementDraftEntry Entry) SeedDraftWithEntry(AppDbContext db, string userName)
    {
        var user = new User(userName, "hash", false);
        db.Users.Add(user);
        db.SaveChanges();

        var draft = new StatementDraft(user.Id, "draft.csv", null, null);
        db.StatementDrafts.Add(draft);
        var entry = draft.AddEntry(DateTime.UtcNow.Date, 10m, "Entry");
        db.SaveChanges();
        return (user, draft, entry);
    }

    /// <summary>
    /// Verifies null parent input is rejected.
    /// </summary>
    [Fact]
    public async Task TryAssignAsync_ShouldReturnFalse_WhenParentIsNull()
    {
        using var db = CreateSqliteContext();
        var sut = new ParentAssignmentService(db, NullLogger<ParentAssignmentService>.Instance);

        var result = await sut.TryAssignAsync(Guid.NewGuid(), null, "contacts", Guid.NewGuid(), CancellationToken.None);

        Assert.False(result);
    }

    /// <summary>
    /// Verifies invalid parent kind or empty parent id is rejected.
    /// </summary>
    [Theory]
    [InlineData("", false)]
    [InlineData(" ", false)]
    [InlineData(StatementEntryParentKind, true)]
    public async Task TryAssignAsync_ShouldReturnFalse_WhenParentKindMissingOrParentIdEmpty(string parentKind, bool useEmptyParentId)
    {
        using var db = CreateSqliteContext();
        var sut = new ParentAssignmentService(db, NullLogger<ParentAssignmentService>.Instance);
        var parent = new ParentLinkRequest(parentKind, useEmptyParentId ? Guid.Empty : Guid.NewGuid(), "ContactId");

        var result = await sut.TryAssignAsync(Guid.NewGuid(), parent, "contacts", Guid.NewGuid(), CancellationToken.None);

        Assert.False(result);
    }

    /// <summary>
    /// Verifies invalid created context is rejected.
    /// </summary>
    [Theory]
    [InlineData("", false)]
    [InlineData(" ", false)]
    [InlineData("contacts", true)]
    public async Task TryAssignAsync_ShouldReturnFalse_WhenCreatedKindMissingOrCreatedIdEmpty(string createdKind, bool useEmptyCreatedId)
    {
        using var db = CreateSqliteContext();
        var sut = new ParentAssignmentService(db, NullLogger<ParentAssignmentService>.Instance);
        var parent = new ParentLinkRequest(StatementEntryParentKind, Guid.NewGuid(), "ContactId");

        var result = await sut.TryAssignAsync(
            Guid.NewGuid(),
            parent,
            createdKind,
            useEmptyCreatedId ? Guid.Empty : Guid.NewGuid(),
            CancellationToken.None);

        Assert.False(result);
    }

    /// <summary>
    /// Verifies unknown parent/created combinations are rejected.
    /// </summary>
    [Fact]
    public async Task TryAssignAsync_ShouldReturnFalse_WhenHandlerIsNotRegistered()
    {
        using var db = CreateSqliteContext();
        var sut = new ParentAssignmentService(db, NullLogger<ParentAssignmentService>.Instance);
        var parent = new ParentLinkRequest("not-registered/parent", Guid.NewGuid(), "AnyField");

        var result = await sut.TryAssignAsync(Guid.NewGuid(), parent, "contacts", Guid.NewGuid(), CancellationToken.None);

        Assert.False(result);
    }

    /// <summary>
    /// Verifies assignment fails if created contact does not belong to the owner.
    /// </summary>
    [Fact]
    public async Task AssignContactToStatementDraftEntryAsync_ShouldReturnFalse_WhenCreatedContactNotOwnedOrMissing()
    {
        using var db = CreateSqliteContext();
        var (owner, _, entry) = SeedDraftWithEntry(db, "owner");
        var foreignUser = new User("foreign", "hash", false);
        db.Users.Add(foreignUser);
        db.SaveChanges();

        var foreignContact = new Contact(foreignUser.Id, "Foreign Contact", ContactType.Other, null, null, false);
        db.Contacts.Add(foreignContact);
        db.SaveChanges();

        var sut = new ParentAssignmentService(db, NullLogger<ParentAssignmentService>.Instance);
        var parent = new ParentLinkRequest(StatementEntryParentKind, entry.Id, "ContactId");

        var result = await sut.TryAssignAsync(owner.Id, parent, "contacts", foreignContact.Id, CancellationToken.None);

        Assert.False(result);
    }

    /// <summary>
    /// Verifies assignment fails if parent entry belongs to another user's draft.
    /// </summary>
    [Fact]
    public async Task AssignContactToStatementDraftEntryAsync_ShouldReturnFalse_WhenDraftOwnershipCheckFails()
    {
        using var db = CreateSqliteContext();
        var owner = new User("owner", "hash", false);
        var foreignOwner = new User("foreign-owner", "hash", false);
        db.Users.AddRange(owner, foreignOwner);
        db.SaveChanges();

        var contact = new Contact(owner.Id, "Local Contact", ContactType.Other, null, null, false);
        db.Contacts.Add(contact);

        var foreignDraft = new StatementDraft(foreignOwner.Id, "foreign.csv", null, null);
        db.StatementDrafts.Add(foreignDraft);
        var foreignEntry = foreignDraft.AddEntry(DateTime.UtcNow.Date, 5m, "Foreign entry");
        db.SaveChanges();

        var sut = new ParentAssignmentService(db, NullLogger<ParentAssignmentService>.Instance);
        var parent = new ParentLinkRequest(StatementEntryParentKind, foreignEntry.Id, "ContactId");

        var result = await sut.TryAssignAsync(owner.Id, parent, "contacts", contact.Id, CancellationToken.None);

        Assert.False(result);
    }

    /// <summary>
    /// Verifies repeated assignment of the same contact is treated as idempotent no-op.
    /// </summary>
    [Fact]
    public async Task AssignContactToStatementDraftEntryAsync_ShouldReturnTrueWithoutRewrite_WhenContactAlreadyAssigned()
    {
        using var db = CreateSqliteContext();
        var (owner, _, entry) = SeedDraftWithEntry(db, "owner");

        var contact = new Contact(owner.Id, "Assigned Contact", ContactType.Other, null, null, false);
        db.Contacts.Add(contact);
        db.SaveChanges();

        entry.AssignContactWithoutAccounting(contact.Id);
        db.SaveChanges();

        var modifiedUtcBefore = entry.ModifiedUtc;
        var sut = new ParentAssignmentService(db, NullLogger<ParentAssignmentService>.Instance);
        var parent = new ParentLinkRequest(StatementEntryParentKind, entry.Id, "ContactId");

        var result = await sut.TryAssignAsync(owner.Id, parent, "contacts", contact.Id, CancellationToken.None);

        Assert.True(result);
        var reloaded = await db.StatementDraftEntries.AsNoTracking().SingleAsync(e => e.Id == entry.Id);
        Assert.Equal(contact.Id, reloaded.ContactId);
        Assert.Equal(modifiedUtcBefore, reloaded.ModifiedUtc);
    }
}
