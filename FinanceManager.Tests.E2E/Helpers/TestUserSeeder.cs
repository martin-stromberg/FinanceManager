using FinanceManager.Domain.Users;
using FinanceManager.Domain.Contacts;
using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Auth;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Tests.E2E;

public sealed class TestUserSeeder
{
    private readonly string _databasePath;

    public TestUserSeeder(string databasePath)
    {
        _databasePath = databasePath;
    }

    public async Task<User> EnsureUserAsync(string username, string password, bool isAdmin = false)
    {
        using var db = CreateContext();

        var existing = await db.Users.FirstOrDefaultAsync(u => u.UserName == username);
        if (existing != null)
        {
            await EnsureSelfContactInternalAsync(db, existing.Id, $"Self {username}");
            return existing;
        }

        var user = new User(username, new Pbkdf2IdentityPasswordHasher().Hash(password), isAdmin)
        {
            Id = Guid.NewGuid(),
            SecurityStamp = Guid.NewGuid().ToString("N"),
            ConcurrencyStamp = Guid.NewGuid().ToString("N"),
            NormalizedUserName = username.ToUpperInvariant(),
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();
        await EnsureSelfContactInternalAsync(db, user.Id, $"Self {username}");
        return user;
    }

    public async Task EnsureSelfContactAsync(Guid userId, string name)
    {
        using var db = CreateContext();
        await EnsureSelfContactInternalAsync(db, userId, name);
    }

    private static async Task EnsureSelfContactInternalAsync(AppDbContext db, Guid userId, string name)
    {
        var exists = await db.Contacts.AnyAsync(contact => contact.OwnerUserId == userId && contact.Type == ContactType.Self);
        if (exists)
        {
            return;
        }

        db.Contacts.Add(new Contact(userId, name, ContactType.Self, null));
        await db.SaveChangesAsync();
    }

    private AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={_databasePath}")
            .Options;

        return new AppDbContext(options);
    }
}
