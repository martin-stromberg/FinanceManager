using FinanceManager.Domain.Users;
using FinanceManager.Infrastructure;
using FinanceManager.Web.Services;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace FinanceManager.Tests.Web;

public sealed class AlphaVantageKeyResolverTests
{
    [Fact]
    public async Task GetForUserAsync_ProtectedPersonalKey_ShouldReturnPlaintext()
    {
        await using var db = CreateDbContext();
        var protector = CreateProtector();
        var user = new User("user", "hash", isAdmin: false);
        user.SetAlphaVantageKey(protector.Protect("personal-key"));
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var resolver = CreateResolver(db, protector);

        var key = await resolver.GetForUserAsync(user.Id, CancellationToken.None);

        key.Should().Be("personal-key");
        db.Users.Single().AlphaVantageApiKey.Should().NotBe("personal-key");
    }

    [Fact]
    public async Task GetForUserAsync_LegacyPlaintextPersonalKey_ShouldReturnAndReprotect()
    {
        await using var db = CreateDbContext();
        var protector = CreateProtector();
        var user = new User("user", "hash", isAdmin: false);
        user.SetAlphaVantageKey("legacy-key");
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var resolver = CreateResolver(db, protector);

        var key = await resolver.GetForUserAsync(user.Id, CancellationToken.None);

        key.Should().Be("legacy-key");
        var stored = db.Users.Single().AlphaVantageApiKey;
        stored.Should().StartWith(DataProtectionAlphaVantageSecretProtector.ProtectedPrefix);
        stored.Should().NotBe("legacy-key");
        protector.Unprotect(stored).Should().Be("legacy-key");
    }

    [Fact]
    public async Task GetForUserAsync_WhenPersonalMissing_ShouldReturnProtectedSharedAdminKey()
    {
        await using var db = CreateDbContext();
        var protector = CreateProtector();
        var user = new User("user", "hash", isAdmin: false);
        var admin = new User("admin", "hash", isAdmin: true);
        admin.SetShareAlphaVantageKey(true);
        admin.SetAlphaVantageKey(protector.Protect("shared-key"));
        db.Users.AddRange(user, admin);
        await db.SaveChangesAsync();
        var resolver = CreateResolver(db, protector);

        var key = await resolver.GetForUserAsync(user.Id, CancellationToken.None);

        key.Should().Be("shared-key");
    }

    [Fact]
    public async Task GetForUserAsync_InvalidProtectedKey_ShouldThrowGenericException()
    {
        await using var db = CreateDbContext();
        var protector = CreateProtector();
        const string secretPayload = "secret-key";
        var user = new User("user", "hash", isAdmin: false);
        user.SetAlphaVantageKey(DataProtectionAlphaVantageSecretProtector.ProtectedPrefix + secretPayload);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var resolver = CreateResolver(db, protector);

        var act = () => resolver.GetForUserAsync(user.Id, CancellationToken.None);

        await act.Should().ThrowAsync<AlphaVantageSecretProtectionException>()
            .WithMessage("Stored AlphaVantage API key cannot be read.");
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new AppDbContext(options);
    }

    private static DataProtectionAlphaVantageSecretProtector CreateProtector()
        => new(DataProtectionProvider.Create("FinanceManager.Tests"));

    private static AlphaVantageKeyResolver CreateResolver(AppDbContext db, IAlphaVantageSecretProtector protector)
        => new(db, protector, NullLogger<AlphaVantageKeyResolver>.Instance);
}
