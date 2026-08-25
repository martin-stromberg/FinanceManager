using FinanceManager.Domain.Users;
using FinanceManager.Domain.Contacts;
using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Tests.E2E;

public sealed class TestUserSeeder
{
    private const string AdminRoleName = "Admin";

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
            if (isAdmin)
            {
                await EnsureAdminRoleAssignedAsync(db, existing.Id);
            }
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
        if (isAdmin)
        {
            await EnsureAdminRoleAssignedAsync(db, user.Id);
        }
        return user;
    }

    /// <summary>
    /// Grants the ASP.NET Identity "Admin" role to the given user. Login only includes the "Admin" claim when the
    /// user is a member of this Identity role (see <c>UserAuthService.LoginAsync</c>), so seeding
    /// <see cref="User.IsAdmin"/> alone is not sufficient to make admin-only UI (e.g. the setup update tab) visible.
    /// </summary>
    /// <param name="db">The database context used to read and persist the role assignment.</param>
    /// <param name="userId">Identifier of the user to grant the role to.</param>
    private static async Task EnsureAdminRoleAssignedAsync(AppDbContext db, Guid userId)
    {
        var role = await db.Roles.FirstOrDefaultAsync(r => r.Name == AdminRoleName);
        if (role == null)
        {
            role = new IdentityRole<Guid>
            {
                Id = Guid.NewGuid(),
                Name = AdminRoleName,
                NormalizedName = AdminRoleName.ToUpperInvariant(),
            };
            db.Roles.Add(role);
            await db.SaveChangesAsync();
        }

        var alreadyAssigned = await db.UserRoles.AnyAsync(ur => ur.UserId == userId && ur.RoleId == role.Id);
        if (!alreadyAssigned)
        {
            db.UserRoles.Add(new IdentityUserRole<Guid> { UserId = userId, RoleId = role.Id });
            await db.SaveChangesAsync();
        }
    }

    public async Task EnsureSelfContactAsync(Guid userId, string name)
    {
        using var db = CreateContext();
        await EnsureSelfContactInternalAsync(db, userId, name);
    }

    public async Task InvalidateSecurityStampAsync(string username)
    {
        using var db = CreateContext();
        var user = await db.Users.SingleAsync(u => u.UserName == username);
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        user.ConcurrencyStamp = Guid.NewGuid().ToString("N");
        await db.SaveChangesAsync();
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
