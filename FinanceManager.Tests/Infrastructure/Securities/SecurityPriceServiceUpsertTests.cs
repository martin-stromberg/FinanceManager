using FinanceManager.Domain.Securities;
using FinanceManager.Domain.Users;
using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Securities;
using FinanceManager.Shared.Dtos.Securities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace FinanceManager.Tests.Infrastructure.Securities;

public sealed class SecurityPriceServiceUpsertTests
{
    /// <summary>
    /// Verifies that upsert inserts new rows and updates changed rows while keeping unchanged rows untouched.
    /// </summary>
    [Fact]
    public async Task UpsertDailyPricesAsync_ShouldInsertUpdateAndKeepUnchanged_WhenMixedInputIsProvided()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        await using var db = new AppDbContext(options);
        var owner = new User("owner", "hash", true);
        db.Users.Add(owner);
        var security = new Security(owner.Id, "ETF", "ISIN123", null, "ETF", "EUR", null);
        db.Securities.Add(security);
        db.SecurityPrices.Add(new SecurityPrice(security.Id, new DateTime(2026, 7, 1), 10.10m));
        db.SecurityPrices.Add(new SecurityPrice(security.Id, new DateTime(2026, 7, 2), 10.20m));
        await db.SaveChangesAsync();

        var sut = new SecurityPriceService(db, Mock.Of<ILogger<SecurityPriceService>>());
        var items = new List<SecurityPriceImportItem>
        {
            new(new DateTime(2026, 7, 1), 11.10m, 3), // update
            new(new DateTime(2026, 7, 2), 10.20m, 4), // unchanged
            new(new DateTime(2026, 7, 3), 10.30m, 5)  // insert
        };

        var result = await sut.UpsertDailyPricesAsync(owner.Id, security.Id, items, CancellationToken.None);

        Assert.Equal(1, result.Inserted);
        Assert.Equal(1, result.Updated);
        Assert.Equal(1, result.Unchanged);

        var stored = await db.SecurityPrices
            .Where(x => x.SecurityId == security.Id)
            .OrderBy(x => x.Date)
            .ToListAsync();

        Assert.Equal(3, stored.Count);
        Assert.Equal(11.10m, stored[0].Close);
        Assert.Equal(10.20m, stored[1].Close);
        Assert.Equal(10.30m, stored[2].Close);
    }

    /// <summary>
    /// Verifies that duplicate dates use the last value in the provided list.
    /// </summary>
    [Fact]
    public async Task UpsertDailyPricesAsync_ShouldApplyLastValue_WhenDuplicateDatesAreProvided()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        await using var db = new AppDbContext(options);
        var owner = new User("owner", "hash", true);
        db.Users.Add(owner);
        var security = new Security(owner.Id, "ETF", "ISIN123", null, "ETF", "EUR", null);
        db.Securities.Add(security);
        await db.SaveChangesAsync();

        var sut = new SecurityPriceService(db, Mock.Of<ILogger<SecurityPriceService>>());
        var items = new List<SecurityPriceImportItem>
        {
            new(new DateTime(2026, 7, 1), 10.00m, 3),
            new(new DateTime(2026, 7, 1), 12.00m, 4)
        };

        var result = await sut.UpsertDailyPricesAsync(owner.Id, security.Id, items, CancellationToken.None);
        var stored = await db.SecurityPrices.SingleAsync(x => x.SecurityId == security.Id && x.Date == new DateTime(2026, 7, 1));

        Assert.Equal(1, result.Inserted);
        Assert.Equal(12.00m, stored.Close);
    }

    /// <summary>
    /// Verifies that upsert throws when the requested security is not owned by the given user.
    /// </summary>
    [Fact]
    public async Task UpsertDailyPricesAsync_ShouldThrow_WhenSecurityIsNotOwnedByUser()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        await using var db = new AppDbContext(options);
        var owner = new User("owner", "hash", true);
        var foreignUser = new User("foreign", "hash", true);
        db.Users.Add(owner);
        db.Users.Add(foreignUser);
        var security = new Security(owner.Id, "ETF", "ISIN123", null, "ETF", "EUR", null);
        db.Securities.Add(security);
        await db.SaveChangesAsync();

        var sut = new SecurityPriceService(db, Mock.Of<ILogger<SecurityPriceService>>());
        var items = new List<SecurityPriceImportItem> { new(new DateTime(2026, 7, 1), 10.00m, 3) };

        var act = async () => await sut.UpsertDailyPricesAsync(foreignUser.Id, security.Id, items, CancellationToken.None);

        await Assert.ThrowsAsync<ArgumentException>(act);
    }

    /// <summary>
    /// Verifies that upsert returns zero counters and does not persist when no items are supplied.
    /// </summary>
    [Fact]
    public async Task UpsertDailyPricesAsync_ShouldReturnZeroCounters_WhenItemsAreEmpty()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        await using var db = new AppDbContext(options);
        var owner = new User("owner", "hash", true);
        db.Users.Add(owner);
        var security = new Security(owner.Id, "ETF", "ISIN123", null, "ETF", "EUR", null);
        db.Securities.Add(security);
        await db.SaveChangesAsync();

        var sut = new SecurityPriceService(db, Mock.Of<ILogger<SecurityPriceService>>());

        var result = await sut.UpsertDailyPricesAsync(owner.Id, security.Id, Array.Empty<SecurityPriceImportItem>(), CancellationToken.None);

        Assert.Equal(0, result.Inserted);
        Assert.Equal(0, result.Updated);
        Assert.Equal(0, result.Unchanged);
        Assert.Equal(0, result.Skipped);
        Assert.Empty(result.Errors);
        Assert.Empty(await db.SecurityPrices.ToListAsync());
    }

    /// <summary>
    /// Verifies that upsert rejects negative close values and does not write partial data.
    /// </summary>
    [Fact]
    public async Task UpsertDailyPricesAsync_ShouldThrow_WhenCloseIsNegative()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        await using var db = new AppDbContext(options);
        var owner = new User("owner", "hash", true);
        db.Users.Add(owner);
        var security = new Security(owner.Id, "ETF", "ISIN123", null, "ETF", "EUR", null);
        db.Securities.Add(security);
        await db.SaveChangesAsync();

        var sut = new SecurityPriceService(db, Mock.Of<ILogger<SecurityPriceService>>());
        var items = new List<SecurityPriceImportItem>
        {
            new(new DateTime(2026, 7, 1), 10.00m, 3),
            new(new DateTime(2026, 7, 2), -1.00m, 4)
        };

        var act = async () => await sut.UpsertDailyPricesAsync(owner.Id, security.Id, items, CancellationToken.None);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(act);
        Assert.Empty(await db.SecurityPrices.ToListAsync());
    }
}
