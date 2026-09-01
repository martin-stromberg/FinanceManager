using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Contacts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace FinanceManager.Tests.Infrastructure;

/// <summary>
/// Guards the alias-name behavior of <see cref="ContactService"/>: preventing case-insensitive duplicate
/// aliases on a single contact, enforcing per-owner isolation when listing aliases, and confirming that
/// alias data is deduplicated and reassigned correctly when two contacts are merged.
/// </summary>
public sealed class ContactServiceAliasTests
{
    private static AppDbContext CreateDb()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(opts);
    }

    private static async Task<(ContactService svc, AppDbContext db, Guid userId, Guid contactId)> SeedAsync()
    {
        var db = CreateDb();
        var userId = Guid.NewGuid();
        var c = new FinanceManager.Domain.Contacts.Contact(userId, "Source", ContactType.Person, null, null, false);
        db.Contacts.Add(c);
        await db.SaveChangesAsync();
        return (new ContactService(db), db, userId, c.Id);
    }

    /// <summary>
    /// Verifies that <see cref="ContactService.AddAliasAsync"/> rejects a new alias pattern that only differs
    /// in casing from an alias the contact already has (e.g. "Foo" vs "foo"). Alias matching is used to
    /// auto-attribute imported statement entries to contacts, so two case-variant aliases for the same
    /// contact would be redundant and could mask genuinely new alias patterns from being noticed.
    /// </summary>
    [Fact]
    public async Task AddAliasAsync_ShouldPreventDuplicate_ForSameContact_CaseInsensitive()
    {
        var (svc, db, user, contact) = await SeedAsync();

        await svc.AddAliasAsync(contact, user, "Foo", CancellationToken.None);
        await Assert.ThrowsAsync<ArgumentException>(async () => await svc.AddAliasAsync(contact, user, "foo", CancellationToken.None));
    }

    /// <summary>
    /// Verifies that <see cref="ContactService.ListAliases"/> refuses to return aliases for a contact that
    /// belongs to a different owner, even though the caller supplies a valid contact id. This is a
    /// tenant-isolation guard: without it, one user could enumerate another user's alias configuration by
    /// guessing or observing contact identifiers.
    /// </summary>
    [Fact]
    public async Task ListAliases_ShouldReject_WhenContactDoesNotBelongToOwner()
    {
        var db = CreateDb();
        var owner = Guid.NewGuid();
        var otherOwner = Guid.NewGuid();
        var contact = new FinanceManager.Domain.Contacts.Contact(owner, "SourceName", ContactType.Person, null, null, false);
        db.Contacts.Add(contact);
        db.AliasNames.Add(new FinanceManager.Domain.Contacts.AliasName(contact.Id, "Alias"));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var svc = new ContactService(db);

        await Assert.ThrowsAsync<ArgumentException>(async () => await svc.ListAliases(contact.Id, otherOwner, CancellationToken.None));
    }

    /// <summary>
    /// Verifies that merging two contacts whose alias lists collide case-insensitively (target has "x",
    /// source has "X") does not leave duplicate alias rows behind: the merged target ends up with exactly
    /// one alias equal to "x" ignoring case, all remaining aliases are pairwise distinct, and the source
    /// contact itself is deleted once its data has been folded into the target.
    /// </summary>
    [Fact]
    public async Task MergeAsync_ShouldNotCreateDuplicateAliases_AndReassign()
    {
        var db = CreateDb();
        var user = Guid.NewGuid();
        var source = new FinanceManager.Domain.Contacts.Contact(user, "SourceName", ContactType.Person, null, null, false);
        var target = new FinanceManager.Domain.Contacts.Contact(user, "TargetName", ContactType.Person, null, null, false);
        db.Contacts.AddRange(source, target);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        // target has alias "x"; source has alias "X" (case-insensitive duplicate)
        db.AliasNames.Add(new FinanceManager.Domain.Contacts.AliasName(target.Id, "x"));
        db.AliasNames.Add(new FinanceManager.Domain.Contacts.AliasName(source.Id, "X"));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var svc = new ContactService(db);
        var result = await svc.MergeAsync(user, source.Id, target.Id, CancellationToken.None);

        Assert.Equal(target.Id, result.Id);

        var aliases = await db.AliasNames.AsNoTracking().Where(a => a.ContactId == target.Id).ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
        // Assert: exactly one alias with value "x" ignoring case remains
        Assert.Equal(1, aliases.Select(a => a.Pattern.ToLowerInvariant()).Count(p => p == "x"));
        // And no case-insensitive duplicates overall
        var distinct = aliases.Select(a => a.Pattern.ToLowerInvariant()).Distinct().Count();
        Assert.Equal(distinct, aliases.Count);
        // Source contact removed
        Assert.Null(await db.Contacts.FindAsync(new object?[] { source.Id }, TestContext.Current.CancellationToken));
    }
}
