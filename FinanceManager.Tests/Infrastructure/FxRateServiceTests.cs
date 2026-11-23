using FinanceManager.Application.Securities;
using FinanceManager.Domain.Securities;
using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Securities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FinanceManager.Tests.Infrastructure;

/// <summary>
/// Tests of <see cref="FxRateService"/>: EUR shortcut without provider calls, persisted
/// "latest stored rate on/before the requested date" hits, lazy provider fill + persist,
/// provider-failure fallbacks (earliest stored rate, then null) and normalization.
/// </summary>
public sealed class FxRateServiceTests
{
    private static AppDbContext CreateDb(out SqliteConnection conn)
    {
        conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options;
        var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    private static FxRateService NewService(AppDbContext db, IFxRateProvider provider)
        => new(db, provider, NullLogger<FxRateService>.Instance);

    /// <summary>
    /// EUR resolves to a synthetic rate of 1 ("Fixed") without touching provider or store.
    /// </summary>
    [Fact]
    public async Task GetRateAsync_Eur_ReturnsOne_WithoutProviderCall()
    {
        await using var db = CreateDb(out var conn);
        var provider = new Mock<IFxRateProvider>(MockBehavior.Strict);
        var svc = NewService(db, provider.Object);

        var rate = await svc.GetRateAsync("eur", new DateTime(2026, 11, 20), CancellationToken.None);

        Assert.NotNull(rate);
        Assert.Equal(1m, rate.RatePerEur);
        Assert.Equal(new DateTime(2026, 11, 20), rate.Date);
        conn.Dispose();
    }

    /// <summary>
    /// A stored rate on or before the requested date is served directly — the newest one
    /// wins and the provider is never asked.
    /// </summary>
    [Fact]
    public async Task GetRateAsync_StoredRate_ServedWithoutProviderCall()
    {
        await using var db = CreateDb(out var conn);
        db.CurrencyExchangeRates.Add(new CurrencyExchangeRate("USD", new DateTime(2026, 11, 10), 1.10m, "Frankfurter"));
        db.CurrencyExchangeRates.Add(new CurrencyExchangeRate("USD", new DateTime(2026, 11, 18), 1.09m, "Frankfurter"));
        db.CurrencyExchangeRates.Add(new CurrencyExchangeRate("USD", new DateTime(2026, 11, 25), 1.20m, "Frankfurter"));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var provider = new Mock<IFxRateProvider>(MockBehavior.Strict);
        var svc = NewService(db, provider.Object);

        var rate = await svc.GetRateAsync("USD", new DateTime(2026, 11, 20), CancellationToken.None);

        Assert.NotNull(rate);
        Assert.Equal(1.09m, rate.RatePerEur);
        Assert.Equal(new DateTime(2026, 11, 18), rate.Date);
        Assert.Equal("Frankfurter", rate.Source);
        conn.Dispose();
    }

    /// <summary>
    /// Without a suitable stored rate the provider is asked once and the answer is
    /// persisted — a second call for the same date is a pure cache hit.
    /// </summary>
    [Fact]
    public async Task GetRateAsync_NoStoredRate_FetchesProviderOnce_AndPersists()
    {
        await using var db = CreateDb(out var conn);
        var requested = new DateTime(2026, 11, 20);
        var provider = new Mock<IFxRateProvider>();
        provider.Setup(p => p.GetRateAsync("USD", requested, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FxRate(new DateTime(2026, 11, 19), 1.09m, "Frankfurter"));
        var svc = NewService(db, provider.Object);

        var rate = await svc.GetRateAsync("USD", requested, CancellationToken.None);

        Assert.NotNull(rate);
        Assert.Equal(1.09m, rate.RatePerEur);
        var stored = await db.CurrencyExchangeRates.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal("USD", stored.CurrencyCode);
        Assert.Equal(new DateTime(2026, 11, 19), stored.Date);
        Assert.Equal(1.09m, stored.RatePerEur);
        Assert.Equal("Frankfurter", stored.Source);

        // Second lookup: stored rate (provider date <= requested) serves without provider.
        var again = await svc.GetRateAsync("USD", requested, CancellationToken.None);
        Assert.Equal(1.09m, again!.RatePerEur);
        provider.Verify(p => p.GetRateAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
        provider.Verify(p => p.GetLatestRateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        conn.Dispose();
    }

    /// <summary>
    /// When the provider cannot serve a rate and no stored rate on/before the date exists,
    /// the earliest stored rate acts as last-resort fallback — the service never throws.
    /// </summary>
    [Fact]
    public async Task GetRateAsync_ProviderFails_FallsBackToEarliestStored()
    {
        await using var db = CreateDb(out var conn);
        db.CurrencyExchangeRates.Add(new CurrencyExchangeRate("USD", new DateTime(2026, 12, 1), 1.05m, "Frankfurter"));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var provider = new Mock<IFxRateProvider>();
        provider.Setup(p => p.GetRateAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((FxRate?)null);
        var svc = NewService(db, provider.Object);

        var rate = await svc.GetRateAsync("USD", new DateTime(2026, 11, 20), CancellationToken.None);

        Assert.NotNull(rate);
        Assert.Equal(1.05m, rate.RatePerEur);
        Assert.Equal(new DateTime(2026, 12, 1), rate.Date);
        conn.Dispose();
    }

    /// <summary>
    /// A provider throwing an exception is treated like "no rate": stored fallback applies
    /// and nothing propagates to the caller.
    /// </summary>
    [Fact]
    public async Task GetRateAsync_ProviderThrows_FallsBackToStored()
    {
        await using var db = CreateDb(out var conn);
        db.CurrencyExchangeRates.Add(new CurrencyExchangeRate("USD", new DateTime(2026, 12, 1), 1.05m, "Frankfurter"));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var provider = new Mock<IFxRateProvider>();
        provider.Setup(p => p.GetRateAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("offline"));
        var svc = NewService(db, provider.Object);

        var rate = await svc.GetRateAsync("USD", new DateTime(2026, 11, 20), CancellationToken.None);

        Assert.NotNull(rate);
        Assert.Equal(1.05m, rate.RatePerEur);
        conn.Dispose();
    }

    /// <summary>
    /// With no stored rate and no provider answer the result is <c>null</c> — the caller
    /// decides how to present the missing valuation.
    /// </summary>
    [Fact]
    public async Task GetRateAsync_ProviderFails_AndNothingStored_ReturnsNull()
    {
        await using var db = CreateDb(out var conn);
        var provider = new Mock<IFxRateProvider>();
        provider.Setup(p => p.GetRateAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((FxRate?)null);
        var svc = NewService(db, provider.Object);

        var rate = await svc.GetRateAsync("USD", new DateTime(2026, 11, 20), CancellationToken.None);

        Assert.Null(rate);
        Assert.Equal(0, await db.CurrencyExchangeRates.CountAsync(TestContext.Current.CancellationToken));
        conn.Dispose();
    }
}
