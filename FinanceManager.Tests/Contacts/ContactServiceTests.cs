using FinanceManager.Domain.Contacts;
using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Contacts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceManager.Tests.Contacts;

/// <summary>
/// Covers <see cref="ContactService"/>'s CRUD operations, in particular the special handling of the
/// "Self" contact (a user's own identity, which must stay unique and immutable in type) and the
/// per-owner isolation that keeps one user's contacts invisible to another.
/// </summary>
public sealed class ContactServiceTests
{
    private static (ContactService sut, AppDbContext db) Create()
    {
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        var sp = services.BuildServiceProvider();
        var db = sp.GetRequiredService<AppDbContext>();
        var sut = new ContactService(db);
        return (sut, db);
    }

    /// <summary>Verifies the baseline path: creating a contact with valid data persists it and returns a DTO with the given name and type.</summary>
    [Fact]
    public async Task CreateAsync_ShouldCreateContact_WhenValid()
    {
        var (sut, db) = Create();
        var owner = Guid.NewGuid();

        var dto = await sut.CreateAsync(owner, "Alice", ContactType.Person, null, null, null, CancellationToken.None);

        Assert.Equal("Alice", dto.Name);
        Assert.Equal(ContactType.Person, dto.Type);
        Assert.Equal(1, db.Contacts.Count());
    }

    /// <summary>Ensures a caller cannot create an additional contact of type "Self" - each owner may only have exactly one, created implicitly by the system.</summary>
    [Fact]
    public async Task CreateAsync_ShouldThrow_WhenTypeSelf()
    {
        var (sut, _) = Create();
        var owner = Guid.NewGuid();
        Func<Task> act = () => sut.CreateAsync(owner, "Me", ContactType.Self, null, null, null, CancellationToken.None);
        var ex = await Assert.ThrowsAsync<ArgumentException>(act);
        Assert.Contains("Self", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Verifies that name and type can both be changed on a regular (non-Self) contact via UpdateAsync.</summary>
    [Fact]
    public async Task UpdateAsync_ShouldUpdateNameAndType_WhenNotSelf()
    {
        var (sut, db) = Create();
        var owner = Guid.NewGuid();
        var created = await sut.CreateAsync(owner, "BankX", ContactType.Organization, null, null, null, CancellationToken.None);

        var updated = await sut.UpdateAsync(created.Id, owner, "BankY", ContactType.Bank, null, null, null, CancellationToken.None);

        Assert.NotNull(updated);
        Assert.Equal("BankY", updated!.Name);
        Assert.Equal(ContactType.Bank, updated.Type);
    }

    /// <summary>Ensures the Self contact's type is locked - it can be renamed but must not be turned into a regular contact type.</summary>
    [Fact]
    public async Task UpdateAsync_ShouldThrow_WhenChangingFromSelf()
    {
        var (sut, db) = Create();
        var owner = Guid.NewGuid();
        var self = new Contact(owner, "Me", ContactType.Self, null);
        db.Contacts.Add(self);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        Func<Task> act = () => sut.UpdateAsync(self.Id, owner, "Me2", ContactType.Person, null, null, null, CancellationToken.None);
        var ex = await Assert.ThrowsAsync<ArgumentException>(act);
        Assert.Contains("Self", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Ensures a regular contact cannot be retyped into "Self" via update, keeping Self a system-assigned, non-transferable type rather than something an owner can grant to any contact.</summary>
    [Fact]
    public async Task UpdateAsync_ShouldThrow_WhenChangingToSelf()
    {
        var (sut, db) = Create();
        var owner = Guid.NewGuid();
        var created = await sut.CreateAsync(owner, "Org", ContactType.Organization, null, null, null, CancellationToken.None);

        Func<Task> act = () => sut.UpdateAsync(created.Id, owner, "Org", ContactType.Self, null, null, null, CancellationToken.None);
        var ex = await Assert.ThrowsAsync<ArgumentException>(act);
        Assert.Contains("Self", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Verifies that a regular contact can be deleted and is removed from the database afterwards.</summary>
    [Fact]
    public async Task DeleteAsync_ShouldDelete_WhenNotSelf()
    {
        var (sut, db) = Create();
        var owner = Guid.NewGuid();
        var c = await sut.CreateAsync(owner, "Temp", ContactType.Other, null, null, null, CancellationToken.None);

        var ok = await sut.DeleteAsync(c.Id, owner, CancellationToken.None);

        Assert.True(ok);
        Assert.Equal(0, db.Contacts.Count());
    }

    /// <summary>Ensures the Self contact cannot be deleted, since it represents the owner's own identity and other records (e.g. transfers) rely on it always existing.</summary>
    [Fact]
    public async Task DeleteAsync_ShouldThrow_WhenSelf()
    {
        var (sut, db) = Create();
        var owner = Guid.NewGuid();
        var self = new Contact(owner, "Me", ContactType.Self, null);
        db.Contacts.Add(self);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        Func<Task> act = () => sut.DeleteAsync(self.Id, owner, CancellationToken.None);
        var ex = await Assert.ThrowsAsync<ArgumentException>(act);
        Assert.Contains("Self", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Verifies that ListAsync scopes results strictly to the requesting owner - contacts belonging to other owners must never leak into the list, even when they share the same database.</summary>
    [Fact]
    public async Task ListAsync_ShouldReturnOnlyOwnersContacts()
    {
        var (sut, db) = Create();
        var owner1 = Guid.NewGuid();
        var owner2 = Guid.NewGuid();
        db.Contacts.AddRange(
            new Contact(owner1, "A", ContactType.Person, null),
            new Contact(owner2, "B", ContactType.Person, null),
            new Contact(owner1, "C", ContactType.Person, null));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var list = await sut.ListAsync(owner1, 0, 50, null, null, CancellationToken.None);

        Assert.Equal(2, list.Count);
        Assert.Equal(new[] { "A", "C" }, list.Select(c => c.Name).ToArray());
    }

    /// <summary>Ensures GetAsync enforces owner isolation by returning null instead of a contact record when the requesting owner does not match the contact's actual owner - a regression guard against ID-based cross-tenant lookups.</summary>
    [Fact]
    public async Task GetAsync_ShouldReturnNull_WhenOtherOwner()
    {
        var (sut, db) = Create();
        var owner1 = Guid.NewGuid();
        var owner2 = Guid.NewGuid();
        var c = new Contact(owner1, "A", ContactType.Person, null);
        db.Contacts.Add(c);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var dto = await sut.GetAsync(c.Id, owner2, CancellationToken.None);
        Assert.Null(dto);
    }
}
