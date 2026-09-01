using FinanceManager.Domain.Users;
using FinanceManager.Infrastructure;
using FinanceManager.Web.Services;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace FinanceManager.Tests.Web;

/// <summary>
/// Tests for <see cref="AlphaVantageKeyResolver"/>, which resolves the AlphaVantage API key to use for a user:
/// their own protected key if set, a legacy plaintext key that gets transparently re-protected on read, or an
/// admin's shared key as a fallback — plus the failure mode when a stored protected value can no longer be
/// decrypted.
/// </summary>
public sealed class AlphaVantageKeyResolverTests
{
    /// <summary>
    /// Verifies that a personal key stored in protected form is returned in plaintext to the caller while
    /// remaining protected (not overwritten in plaintext) in the database.
    /// </summary>
    [Fact]
    public async Task GetForUserAsync_ProtectedPersonalKey_ShouldReturnPlaintext()
    {
        await using var db = CreateDbContext();
        var protector = CreateProtector();
        var user = new User("user", "hash", isAdmin: false);
        user.SetAlphaVantageKey(protector.Protect("personal-key"));
        db.Users.Add(user);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var resolver = CreateResolver(db, protector);

        var key = await resolver.GetForUserAsync(user.Id, CancellationToken.None);

        key.Should().Be("personal-key");
        db.Users.Single().AlphaVantageApiKey.Should().NotBe("personal-key");
    }

    /// <summary>
    /// Verifies that a personal key stored as legacy plaintext (from before key protection was introduced) is
    /// returned correctly and is transparently re-encrypted in the database on read, migrating it to the
    /// protected format without requiring any explicit user action.
    /// </summary>
    [Fact]
    public async Task GetForUserAsync_LegacyPlaintextPersonalKey_ShouldReturnAndReprotect()
    {
        await using var db = CreateDbContext();
        var protector = CreateProtector();
        var user = new User("user", "hash", isAdmin: false);
        user.SetAlphaVantageKey("legacy-key");
        db.Users.Add(user);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var resolver = CreateResolver(db, protector);

        var key = await resolver.GetForUserAsync(user.Id, CancellationToken.None);

        key.Should().Be("legacy-key");
        var stored = db.Users.Single().AlphaVantageApiKey;
        stored.Should().StartWith(DataProtectionAlphaVantageSecretProtector.ProtectedPrefix);
        stored.Should().NotBe("legacy-key");
        protector.Unprotect(stored).Should().Be("legacy-key");
    }

    /// <summary>
    /// Verifies that when a user has no personal AlphaVantage key configured, the resolver falls back to an
    /// admin's key that has been explicitly marked as shared — letting non-admin users use market data
    /// features without needing their own API key.
    /// </summary>
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
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var resolver = CreateResolver(db, protector);

        var key = await resolver.GetForUserAsync(user.Id, CancellationToken.None);

        key.Should().Be("shared-key");
    }

    /// <summary>
    /// Verifies that a stored key with the protected-value prefix but a payload that fails to decrypt (e.g.
    /// corrupted or protected under a different key ring) surfaces as a generic
    /// <see cref="AlphaVantageSecretProtectionException"/> rather than leaking decryption internals or the
    /// raw stored value in the error message.
    /// </summary>
    [Fact]
    public async Task GetForUserAsync_InvalidProtectedKey_ShouldThrowGenericException()
    {
        await using var db = CreateDbContext();
        var protector = CreateProtector();
        const string secretPayload = "secret-key";
        var user = new User("user", "hash", isAdmin: false);
        user.SetAlphaVantageKey(DataProtectionAlphaVantageSecretProtector.ProtectedPrefix + secretPayload);
        db.Users.Add(user);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
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
