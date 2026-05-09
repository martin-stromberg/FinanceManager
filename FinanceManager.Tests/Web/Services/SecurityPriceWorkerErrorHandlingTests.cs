using System.Reflection;
using FinanceManager.Application.Notifications;
using FinanceManager.Domain.Securities;
using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Notifications;
using FinanceManager.Web.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FinanceManager.Tests.Web.Services;

public sealed class SecurityPriceWorkerErrorHandlingTests
{
    [Fact]
    public async Task RunOnceAsync_InvalidSymbol_PersistsExpectedErrorFields_SanitizesProviderText_AndContinues()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var priceProvider = new ScriptedPriceProvider();
        var rawProviderMessage = "Invalid API call. Please retry for TIME_SERIES_DAILY.\u0000\u0001" + new string('x', 2100);
        priceProvider.Throws("BAD", PriceProviderErrorClass.InvalidSymbolOrFunction, rawProviderMessage);
        priceProvider.Returns("GOOD", (DateTime.UtcNow.Date.AddDays(-1), 123.45m));

        using var provider = BuildProvider(dbName, priceProvider);
        var ownerUserId = Guid.NewGuid();
        Guid badSecurityId;
        Guid goodSecurityId;

        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Securities.Add(new Security(ownerUserId, "Bad Security", "BAD-1", null, "BAD", "EUR", null));
            db.Securities.Add(new Security(ownerUserId, "Good Security", "GOOD-1", null, "GOOD", "EUR", null));
            await db.SaveChangesAsync();

            badSecurityId = await db.Securities.Where(x => x.AlphaVantageCode == "BAD").Select(x => x.Id).SingleAsync();
            goodSecurityId = await db.Securities.Where(x => x.AlphaVantageCode == "GOOD").Select(x => x.Id).SingleAsync();
        }

        var worker = CreateWorker(provider);
        var beforeRun = DateTime.UtcNow;
        await InvokeRunOnceAsync(worker);
        var afterRun = DateTime.UtcNow;

        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var bad = await db.Securities.FirstAsync(x => x.Id == badSecurityId);
            var good = await db.Securities.FirstAsync(x => x.Id == goodSecurityId);
            var notifications = await db.Notifications.Where(n => n.OwnerUserId == ownerUserId).ToListAsync();
            var insertedGoodPrice = await db.SecurityPrices.FirstOrDefaultAsync(x => x.SecurityId == goodSecurityId);

            priceProvider.CalledSymbols.Should().HaveCount(2);
            priceProvider.CalledSymbols.Should().Contain("BAD");
            priceProvider.CalledSymbols.Should().Contain("GOOD");

            bad.HasPriceError.Should().BeTrue();
            bad.PriceErrorClass.Should().Be("INVALID_SYMBOL_OR_FUNCTION");
            bad.PriceErrorMessage.Should().Contain("Symbol prüfen");
            bad.PriceErrorSinceUtc.Should().NotBeNull();
            bad.PriceErrorSinceUtc.Should().BeOnOrAfter(beforeRun);
            bad.PriceErrorSinceUtc.Should().BeOnOrBefore(afterRun);
            bad.PriceErrorProviderMessage.Should().NotBeNull();
            bad.PriceErrorProviderMessage!.Length.Should().Be(2000);
            bad.PriceErrorProviderMessage.Should().Contain("Invalid API call");
            bad.PriceErrorProviderMessage.Should().NotContain("\0");
            bad.PriceErrorProviderMessage.Should().NotContain("\u0001");

            notifications.Should().HaveCount(1);
            notifications[0].Title.Should().Be("Kursabruf fehlgeschlagen");
            notifications[0].Message.Should().Contain("Symbol prüfen");
            notifications[0].Message.Should().NotContain("Invalid API call");

            good.HasPriceError.Should().BeFalse();
            insertedGoodPrice.Should().NotBeNull();
            insertedGoodPrice!.Close.Should().Be(123.45m);
        }
    }

    [Fact]
    public async Task RunOnceAsync_UnknownProviderError_ContinuesWithNextSecurity_AndPersistsError()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var priceProvider = new ScriptedPriceProvider();
        priceProvider.Throws("UNKNOWN", PriceProviderErrorClass.UnknownProviderError, "Provider backend unavailable");
        priceProvider.Returns("GOOD", (DateTime.UtcNow.Date.AddDays(-1), 222.22m));

        using var provider = BuildProvider(dbName, priceProvider);
        var ownerUserId = Guid.NewGuid();
        Guid unknownSecurityId;
        Guid goodSecurityId;

        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Securities.Add(new Security(ownerUserId, "Unknown Security", "UNK-1", null, "UNKNOWN", "EUR", null));
            db.Securities.Add(new Security(ownerUserId, "Good Security", "GOOD-1", null, "GOOD", "EUR", null));
            await db.SaveChangesAsync();

            unknownSecurityId = await db.Securities.Where(x => x.AlphaVantageCode == "UNKNOWN").Select(x => x.Id).SingleAsync();
            goodSecurityId = await db.Securities.Where(x => x.AlphaVantageCode == "GOOD").Select(x => x.Id).SingleAsync();
            db.SecurityPrices.Add(new SecurityPrice(goodSecurityId, DateTime.UtcNow.Date.AddDays(-30), 100m));
            await db.SaveChangesAsync();
        }

        var worker = CreateWorker(provider);
        await InvokeRunOnceAsync(worker);

        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var unknown = await db.Securities.FirstAsync(x => x.Id == unknownSecurityId);
            var goodPrices = await db.SecurityPrices.Where(x => x.SecurityId == goodSecurityId).OrderBy(x => x.Date).ToListAsync();
            var notifications = await db.Notifications.Where(n => n.OwnerUserId == ownerUserId).ToListAsync();

            priceProvider.CalledSymbols.Should().ContainInOrder("UNKNOWN", "GOOD");
            unknown.HasPriceError.Should().BeTrue();
            unknown.PriceErrorClass.Should().Be("UNKNOWN_PROVIDER_ERROR");
            unknown.PriceErrorMessage.Should().Contain("externer Fehler");

            goodPrices.Should().HaveCount(2);
            goodPrices.Last().Close.Should().Be(222.22m);
            notifications.Should().HaveCount(1);
        }
    }

    [Fact]
    public async Task RunOnceAsync_TransientNetwork_DoesNotPersistError_AndContinuesWithNextSecurity()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var priceProvider = new ScriptedPriceProvider();
        priceProvider.Throws("TRANSIENT", PriceProviderErrorClass.TransientNetwork, "Temporary network timeout");
        priceProvider.Returns("GOOD", (DateTime.UtcNow.Date.AddDays(-1), 150m));

        using var provider = BuildProvider(dbName, priceProvider);
        var ownerUserId = Guid.NewGuid();
        Guid transientSecurityId;
        Guid goodSecurityId;

        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Securities.Add(new Security(ownerUserId, "Transient Security", "TR-1", null, "TRANSIENT", "EUR", null));
            db.Securities.Add(new Security(ownerUserId, "Good Security", "GOOD-1", null, "GOOD", "EUR", null));
            await db.SaveChangesAsync();

            transientSecurityId = await db.Securities.Where(x => x.AlphaVantageCode == "TRANSIENT").Select(x => x.Id).SingleAsync();
            goodSecurityId = await db.Securities.Where(x => x.AlphaVantageCode == "GOOD").Select(x => x.Id).SingleAsync();
            db.SecurityPrices.Add(new SecurityPrice(goodSecurityId, DateTime.UtcNow.Date.AddDays(-20), 100m));
            await db.SaveChangesAsync();
        }

        var worker = CreateWorker(provider);
        await InvokeRunOnceAsync(worker);

        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var transient = await db.Securities.FirstAsync(x => x.Id == transientSecurityId);
            var goodPrices = await db.SecurityPrices.Where(x => x.SecurityId == goodSecurityId).OrderBy(x => x.Date).ToListAsync();
            var notifications = await db.Notifications.Where(n => n.OwnerUserId == ownerUserId).ToListAsync();

            priceProvider.CalledSymbols.Should().ContainInOrder("TRANSIENT", "GOOD");

            transient.HasPriceError.Should().BeFalse();
            transient.PriceErrorClass.Should().BeNull();
            transient.PriceErrorMessage.Should().BeNull();
            transient.PriceErrorProviderMessage.Should().BeNull();
            transient.PriceErrorSinceUtc.Should().BeNull();

            goodPrices.Should().HaveCount(2);
            goodPrices.Last().Close.Should().Be(150m);
            notifications.Should().BeEmpty();
        }
    }

    [Fact]
    public async Task RunOnceAsync_RateLimit_StopsProcessingRemainingSecurities()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var priceProvider = new ScriptedPriceProvider();
        priceProvider.Throws("RL-1", PriceProviderErrorClass.RateLimit, "rate limit");
        priceProvider.Throws("RL-2", PriceProviderErrorClass.RateLimit, "rate limit");
        priceProvider.Throws("RL-3", PriceProviderErrorClass.RateLimit, "rate limit");

        using var provider = BuildProvider(dbName, priceProvider);
        var ownerUserId = Guid.NewGuid();

        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Securities.Add(new Security(ownerUserId, "Rate Limit 1", "RL1", null, "RL-1", "EUR", null));
            db.Securities.Add(new Security(ownerUserId, "Rate Limit 2", "RL2", null, "RL-2", "EUR", null));
            db.Securities.Add(new Security(ownerUserId, "Rate Limit 3", "RL3", null, "RL-3", "EUR", null));
            await db.SaveChangesAsync();
        }

        var worker = CreateWorker(provider);
        await InvokeRunOnceAsync(worker);

        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var securities = await db.Securities.ToListAsync();
            var prices = await db.SecurityPrices.ToListAsync();
            var notifications = await db.Notifications.Where(n => n.OwnerUserId == ownerUserId).ToListAsync();

            priceProvider.CalledSymbols.Should().HaveCount(1);
            prices.Should().BeEmpty();
            notifications.Should().BeEmpty();
            securities.Should().OnlyContain(x => x.HasPriceError == false);
        }
    }

    private static ServiceProvider BuildProvider(string dbName, ScriptedPriceProvider priceProvider)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(dbName));
        services.AddScoped<INotificationWriter, NotificationWriter>();
        services.AddScoped<IAlphaVantageKeyResolver, StubAlphaVantageKeyResolver>();
        services.AddSingleton<IPriceProvider>(priceProvider);
        return services.BuildServiceProvider();
    }

    private static SecurityPriceWorker CreateWorker(ServiceProvider provider)
        => new(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<SecurityPriceWorker>.Instance,
            Options.Create(new AlphaVantageQuotaOptions { MaxSymbolsPerRun = 10, RequestsPerMinute = 0 }));

    private static async Task InvokeRunOnceAsync(SecurityPriceWorker worker)
    {
        var method = typeof(SecurityPriceWorker).GetMethod("RunOnceAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        method.Should().NotBeNull();
        var task = (Task?)method!.Invoke(worker, new object[] { CancellationToken.None });
        task.Should().NotBeNull();
        await task!;
    }

    private sealed class StubAlphaVantageKeyResolver : IAlphaVantageKeyResolver
    {
        public Task<string?> GetForUserAsync(Guid userId, CancellationToken ct) => Task.FromResult<string?>("shared-key");
        public Task<string?> GetSharedAsync(CancellationToken ct) => Task.FromResult<string?>("shared-key");
    }

    private sealed class ScriptedPriceProvider : IPriceProvider
    {
        private readonly Dictionary<string, Func<IReadOnlyList<(DateTime date, decimal close)>>> _scripts = new(StringComparer.OrdinalIgnoreCase);

        public List<string> CalledSymbols { get; } = [];

        public void Returns(string symbol, params (DateTime date, decimal close)[] prices)
            => _scripts[symbol] = () => prices.ToList();

        public void Throws(string symbol, PriceProviderErrorClass errorClass, string providerRawMessage, string message = "Provider error")
            => _scripts[symbol] = () => throw new PriceProviderException(errorClass, providerRawMessage, message);

        public Task<IReadOnlyList<(DateTime date, decimal close)>> GetDailyPricesAsync(string symbol, DateTime startDateExclusive, DateTime endDateInclusive, CancellationToken ct)
        {
            CalledSymbols.Add(symbol);
            if (_scripts.TryGetValue(symbol, out var response))
            {
                return Task.FromResult(response());
            }

            return Task.FromResult<IReadOnlyList<(DateTime date, decimal close)>>(
                [(DateTime.UtcNow.Date.AddDays(-1), 100m)]);
        }
    }
}
