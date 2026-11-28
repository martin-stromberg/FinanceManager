using FinanceManager.Domain.Securities;
using FinanceManager.Domain.Users;
using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Securities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace FinanceManager.Tests.Infrastructure.Securities;

/// <summary>
/// Verifies the batch lookup of <see cref="SecurityPriceService.GetLatestPricesAsync"/>: for each
/// requested security the price row with the maximum date is returned, securities without any
/// stored price are absent from the result, foreign-owned or unknown security ids are silently
/// excluded, a <c>null</c> id set is rejected, and an empty id set short-circuits to an empty
/// dictionary.
/// </summary>
public sealed class SecurityPriceServiceLatestPricesTests
{
    private static AppDbContext CreateDbContext()
        => new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);

    private static User SeedOwner(AppDbContext db, string userName = "owner")
    {
        var owner = new User(userName, "hash", true);
        db.Users.Add(owner);
        return owner;
    }

    private static SecurityPriceService CreateSut(AppDbContext db)
        => new(db, Mock.Of<ILogger<SecurityPriceService>>());

    /// <summary>
    /// For each requested security the price with the maximum stored date is returned,
    /// including the matching close value.
    /// </summary>
    [Fact]
    public async Task GetLatestPricesAsync_ReturnsLatestPricePerSecurity()
    {
        await using var db = CreateDbContext();
        var owner = SeedOwner(db);
        var securityA = new Security(owner.Id, "ETF A", "ISIN-A", null, "A", "EUR", null);
        var securityB = new Security(owner.Id, "ETF B", "ISIN-B", null, "B", "EUR", null);
        db.Securities.Add(securityA);
        db.Securities.Add(securityB);
        db.SecurityPrices.Add(new SecurityPrice(securityA.Id, new DateTime(2026, 7, 1), 10.00m));
        db.SecurityPrices.Add(new SecurityPrice(securityA.Id, new DateTime(2026, 7, 3), 12.50m));
        db.SecurityPrices.Add(new SecurityPrice(securityA.Id, new DateTime(2026, 7, 2), 11.00m));
        db.SecurityPrices.Add(new SecurityPrice(securityB.Id, new DateTime(2026, 7, 1), 55.00m));
        db.SecurityPrices.Add(new SecurityPrice(securityB.Id, new DateTime(2026, 7, 4), 57.75m));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var sut = CreateSut(db);

        var result = await sut.GetLatestPricesAsync(owner.Id, new[] { securityA.Id, securityB.Id }, CancellationToken.None);

        Assert.Equal(2, result.Count);
        var latestA = result[securityA.Id];
        Assert.Equal(new DateTime(2026, 7, 3), latestA.Date);
        Assert.Equal(12.50m, latestA.Close);
        var latestB = result[securityB.Id];
        Assert.Equal(new DateTime(2026, 7, 4), latestB.Date);
        Assert.Equal(57.75m, latestB.Close);
    }

    /// <summary>
    /// A security without any stored <see cref="SecurityPrice"/> is absent from the result
    /// dictionary (the caller renders <c>null</c> for it).
    /// </summary>
    [Fact]
    public async Task GetLatestPricesAsync_OmitsSecuritiesWithoutPrices()
    {
        await using var db = CreateDbContext();
        var owner = SeedOwner(db);
        var withPrice = new Security(owner.Id, "ETF A", "ISIN-A", null, "A", "EUR", null);
        var withoutPrice = new Security(owner.Id, "ETF B", "ISIN-B", null, "B", "EUR", null);
        db.Securities.Add(withPrice);
        db.Securities.Add(withoutPrice);
        db.SecurityPrices.Add(new SecurityPrice(withPrice.Id, new DateTime(2026, 7, 1), 10.00m));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var sut = CreateSut(db);

        var result = await sut.GetLatestPricesAsync(owner.Id, new[] { withPrice.Id, withoutPrice.Id }, CancellationToken.None);

        Assert.Single(result);
        Assert.True(result.ContainsKey(withPrice.Id));
        Assert.False(result.ContainsKey(withoutPrice.Id));
    }

    /// <summary>
    /// Prices of securities that are not owned by the given user are silently excluded —
    /// both when the foreign id is requested under the real owner and when an id owned by
    /// the user is requested under a foreign owner id.
    /// </summary>
    [Fact]
    public async Task GetLatestPricesAsync_ExcludesForeignOwnedSecurities()
    {
        await using var db = CreateDbContext();
        var owner = SeedOwner(db);
        var foreign = SeedOwner(db, "foreign");
        var own = new Security(owner.Id, "ETF A", "ISIN-A", null, "A", "EUR", null);
        var other = new Security(foreign.Id, "ETF B", "ISIN-B", null, "B", "EUR", null);
        db.Securities.Add(own);
        db.Securities.Add(other);
        db.SecurityPrices.Add(new SecurityPrice(own.Id, new DateTime(2026, 7, 1), 10.00m));
        db.SecurityPrices.Add(new SecurityPrice(other.Id, new DateTime(2026, 7, 2), 20.00m));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var sut = CreateSut(db);

        var result = await sut.GetLatestPricesAsync(owner.Id, new[] { own.Id, other.Id }, CancellationToken.None);

        Assert.Single(result);
        Assert.True(result.ContainsKey(own.Id));

        var wrongOwner = await sut.GetLatestPricesAsync(foreign.Id, new[] { own.Id }, CancellationToken.None);
        Assert.Empty(wrongOwner);
    }

    /// <summary>
    /// An empty id set returns an empty dictionary without touching the database.
    /// </summary>
    [Fact]
    public async Task GetLatestPricesAsync_EmptyIds_ReturnsEmpty()
    {
        await using var db = CreateDbContext();
        var owner = SeedOwner(db);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var sut = CreateSut(db);

        var result = await sut.GetLatestPricesAsync(owner.Id, Array.Empty<Guid>(), CancellationToken.None);

        Assert.Empty(result);
    }

    /// <summary>
    /// A <c>null</c> id set violates the non-nullable contract and is rejected with an
    /// <see cref="ArgumentNullException"/> — consistent with
    /// <see cref="SecurityPriceService.UpsertDailyPricesAsync"/>.
    /// </summary>
    [Fact]
    public async Task GetLatestPricesAsync_NullIds_Throws()
    {
        await using var db = CreateDbContext();
        var owner = SeedOwner(db);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var sut = CreateSut(db);

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => sut.GetLatestPricesAsync(owner.Id, null!, CancellationToken.None));
    }
}
