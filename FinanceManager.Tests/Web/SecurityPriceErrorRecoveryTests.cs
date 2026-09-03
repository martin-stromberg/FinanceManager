using FinanceManager.Application;
using FinanceManager.Application.Notifications;
using FinanceManager.Application.Securities;
using FinanceManager.Domain.Notifications;
using FinanceManager.Domain.Securities;
using FinanceManager.Domain.Users;
using FinanceManager.Infrastructure;
using FinanceManager.Shared.Dtos.Securities;
using FinanceManager.Web.Services;
using FinanceManager.Web;
using FinanceManager.Infrastructure.Securities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Reflection;
using FinanceManager.Infrastructure.Notifications;
using FinanceManager.Shared.Extensions;

namespace FinanceManager.Tests.Web;
/// <summary>
/// Tests for verifying that the security price retrieval worker and backfill executor correctly handle securities with price errors, including clearing the error state and creating new price entries, and that dismissing notifications does not clear the price error. These tests use an in-memory SQLite database to simulate the application data store and Moq to create test doubles for dependencies. The tests ensure that when a security has a price error, the worker and backfill executor attempt to retrieve prices, clear the error state, and create price entries as expected. Additionally, they verify that dismissing a notification related to a security price error does not inadvertently clear the error state on the security.
/// </summary>
public sealed class SecurityPriceErrorRecoveryTests
{
    /// <summary>
    /// Wraps a real or mocked <see cref="ISecurityPriceService"/> to count how many times
    /// <see cref="CreateAsync"/> and <see cref="ClearPriceErrorAsync"/> are invoked, while still
    /// delegating to the inner implementation for actual behavior. Lets tests assert that price
    /// recovery flows call these specific operations exactly once, not just that the end state
    /// looks correct.
    /// </summary>
    private sealed class SpySecurityPriceService : ISecurityPriceService
    {
        private readonly ISecurityPriceService _inner;

        /// <summary>
        /// Creates the spy around an inner service whose calls should be counted and forwarded.
        /// </summary>
        /// <param name="inner">The real implementation to delegate to after recording the call.</param>
        public SpySecurityPriceService(ISecurityPriceService inner)
        {
            _inner = inner;
        }

        /// <summary>Number of times <see cref="CreateAsync"/> has been invoked.</summary>
        public int CreateCallCount { get; private set; }

        /// <summary>Number of times <see cref="ClearPriceErrorAsync"/> has been invoked.</summary>
        public int ClearPriceErrorCallCount { get; private set; }

        /// <inheritdoc />
        public Task CreateAsync(Guid ownerUserId, Guid securityId, DateTime date, decimal close, CancellationToken ct)
        {
            CreateCallCount++;
            return _inner.CreateAsync(ownerUserId, securityId, date, close, ct);
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<SecurityPriceDto>> ListAsync(Guid ownerUserId, Guid securityId, int skip, int take, CancellationToken ct)
            => _inner.ListAsync(ownerUserId, securityId, skip, take, ct);

        /// <inheritdoc />
        public Task<DateTime?> GetLatestDateAsync(Guid ownerUserId, Guid securityId, CancellationToken ct)
            => _inner.GetLatestDateAsync(ownerUserId, securityId, ct);

        /// <inheritdoc />
        public Task<SecurityPriceImportResultDto> UpsertDailyPricesAsync(Guid ownerUserId, Guid securityId, IReadOnlyList<SecurityPriceImportItem> items, CancellationToken ct)
            => _inner.UpsertDailyPricesAsync(ownerUserId, securityId, items, ct);

        /// <inheritdoc />
        public Task SetPriceErrorAsync(Guid ownerUserId, Guid securityId, string message, CancellationToken ct)
            => _inner.SetPriceErrorAsync(ownerUserId, securityId, message, ct);

        /// <inheritdoc />
        public Task ClearPriceErrorAsync(Guid ownerUserId, Guid securityId, CancellationToken ct)
        {
            ClearPriceErrorCallCount++;
            return _inner.ClearPriceErrorAsync(ownerUserId, securityId, ct);
        }
    }

    /// <summary>
    /// Verifies that the background price-retrieval worker still processes a security that
    /// currently has a recorded price error: it must fetch a new price, clear the error flag, and
    /// persist the new price entry. This guards against a security getting permanently "stuck"
    /// with a stale error that would otherwise prevent it from ever being retried.
    /// </summary>
    [Fact]
    public async Task SecurityPriceWorker_ShouldProcessSecurityWhenPriceErrorExists_WhenRunExecutes()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var db = CreateDatabase(connection, out _, out var securityId);
        var priceService = CreatePriceService(db);
        var spyPriceService = new SpySecurityPriceService(priceService.Object);
        var provider = CreateServiceProvider(
            db,
            CreatePriceProvider(new[] { (DateTime.UtcNow.Date.ToPreviousWorkday(), 123.45m) }),
            CreateNotificationWriter(),
            CreateKeyResolver(),
            priceService: spyPriceService);

        var worker = new SecurityPriceWorker(
            new TestScopeFactory(provider),
            Mock.Of<ILogger<SecurityPriceWorker>>(),
            Options.Create(new AlphaVantageQuotaOptions { MaxSymbolsPerRun = 10, RequestsPerMinute = 0 }));

        var method = typeof(SecurityPriceWorker).GetMethod("RunOnceAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var task = (Task)method!.Invoke(worker, new object[] { CancellationToken.None })!;
        await task;

        var security = db.Securities.Single(x => x.Id == securityId);
        Assert.Equal(1, spyPriceService.ClearPriceErrorCallCount);
        Assert.False(security.HasPriceError);
        Assert.Single(db.SecurityPrices.Where(x => x.SecurityId == securityId));
    }

    /// <summary>
    /// Verifies that the manual price-backfill background task also picks up securities that
    /// currently have a price error, fetching and persisting a price and clearing the error - the
    /// same guarantee as the scheduled worker, but exercised through the user-triggered backfill
    /// execution path instead.
    /// </summary>
    [Fact]
    public async Task SecurityPricesBackfillExecutor_ShouldIncludeSecurityWhenPriceErrorExists_WhenBackfillRuns()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var db = CreateDatabase(connection, out var ownerId, out var securityId);

        var securityService = new Mock<ISecurityService>();
        securityService
            .Setup(x => x.ListAsync(ownerId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new SecurityDto
                {
                    Id = securityId,
                    Name = "Test Security",
                    Identifier = "ISIN123",
                    AlphaVantageCode = "TEST",
                    CurrencyCode = "EUR",
                    IsActive = true,
                    HasPriceError = true
                }
            });

        var priceProvider = CreatePriceProvider(new[] { (DateTime.UtcNow.Date.ToPreviousWorkday(), 99.99m) });
        var priceService = new Mock<ISecurityPriceService>();
        priceService
            .Setup(x => x.GetLatestDateAsync(ownerId, securityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DateTime?)null);
        priceService
            .Setup(x => x.CreateAsync(ownerId, securityId, It.IsAny<DateTime>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        priceService
            .Setup(x => x.ClearPriceErrorAsync(ownerId, securityId, It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                var security = db.Securities.Single(x => x.Id == securityId);
                security.ClearPriceError();
                db.SaveChanges();
                return Task.CompletedTask;
            });
        var spyPriceService = new SpySecurityPriceService(priceService.Object);

        var localizer = CreateLocalizer();
        var provider = CreateServiceProvider(db, priceProvider, CreateNotificationWriter(), CreateKeyResolver(), securityService.Object, spyPriceService, localizer);

        var executor = new SecurityPricesBackfillExecutor(
            new TestScopeFactory(provider),
            Mock.Of<ILogger<SecurityPricesBackfillExecutor>>(),
            localizer);

        var context = new BackgroundTaskContext(Guid.NewGuid(), ownerId, null, (_, _, _, _, _) => { });

        await executor.ExecuteAsync(context, CancellationToken.None);

        Assert.Equal(1, spyPriceService.ClearPriceErrorCallCount);
        Assert.Equal(1, spyPriceService.CreateCallCount);

        var security = db.Securities.Single(x => x.Id == securityId);
        Assert.False(security.HasPriceError);
    }

    /// <summary>
    /// Verifies that dismissing a notification only marks it as seen and does not clear the security error.
    /// </summary>
    [Fact]
    public async Task NotificationService_ShouldNotClearPriceError_WhenDismissingSecurityErrorNotification()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var db = CreateDatabase(connection, out var ownerId, out var securityId);
        var notification = new Notification
        {
            OwnerUserId = ownerId,
            Title = "Kursabruf fehlgeschlagen",
            Message = "Invalid API call",
            Type = NotificationType.SystemAlert,
            Target = NotificationTarget.HomePage,
            ScheduledDateUtc = DateTime.UtcNow.Date,
            TriggerEventKey = $"security:error:{securityId}"
        };
        db.Notifications.Add(notification);
        db.SaveChanges();

        var service = new NotificationService(db);

        var dismissed = await service.DismissAsync(notification.Id, ownerId, CancellationToken.None);

        Assert.True(dismissed);

        var security = db.Securities.Single(x => x.Id == securityId);
        Assert.True(security.HasPriceError);

        var stored = db.Notifications.Single(x => x.Id == notification.Id);
        Assert.True(stored.IsDismissed);
    }

    /// <summary>
    /// Builds an in-memory SQLite-backed <see cref="AppDbContext"/> seeded with one owner user and
    /// one security that already has a price error set, mirroring the starting state each test in
    /// this class needs before it exercises the recovery paths.
    /// </summary>
    /// <param name="connection">The open in-memory SQLite connection to build the context on.</param>
    /// <param name="ownerId">Receives the id of the seeded owner user.</param>
    /// <param name="securityId">Receives the id of the seeded security with a price error.</param>
    /// <returns>The seeded database context.</returns>
    private static AppDbContext CreateDatabase(SqliteConnection connection, out Guid ownerId, out Guid securityId)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        var db = new AppDbContext(options);
        db.Database.EnsureCreated();

        var owner = new User("owner", "hash", true);
        db.Users.Add(owner);
        db.SaveChanges();

        var security = new Security(owner.Id, "Test Security", "ISIN123", null, "TEST", "EUR", null);
        security.SetPriceError("Invalid API call");
        db.Securities.Add(security);
        db.SaveChanges();

        ownerId = owner.Id;
        securityId = security.Id;
        return db;
    }

    /// <summary>
    /// Assembles a DI container holding the shared database plus the given collaborator mocks, so
    /// the worker/executor under test can resolve everything it needs via
    /// <see cref="IServiceScopeFactory"/> just as it would in production.
    /// </summary>
    /// <param name="db">The shared database context all registered services should operate on.</param>
    /// <param name="priceProvider">Mock price provider returning canned daily price data.</param>
    /// <param name="notificationWriter">Mock notification writer, not asserted on in these tests but required for construction.</param>
    /// <param name="keyResolver">Mock AlphaVantage key resolver returning a fixed shared key.</param>
    /// <param name="securityService">Optional security service override; defaults to an unconfigured mock.</param>
    /// <param name="priceService">Optional price service override; defaults to an unconfigured mock.</param>
    /// <param name="localizer">Optional localizer override; defaults to <see cref="CreateLocalizer"/>.</param>
    /// <returns>A built service provider suitable for constructing a test scope factory.</returns>
    private static IServiceProvider CreateServiceProvider(
        AppDbContext db,
        Mock<IPriceProvider> priceProvider,
        Mock<INotificationWriter> notificationWriter,
        Mock<IAlphaVantageKeyResolver> keyResolver,
        ISecurityService? securityService = null,
        ISecurityPriceService? priceService = null,
        IStringLocalizer<Pages>? localizer = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(db);
        services.AddSingleton(priceProvider.Object);
        services.AddSingleton(notificationWriter.Object);
        services.AddSingleton(keyResolver.Object);
        services.AddSingleton(securityService ?? Mock.Of<ISecurityService>());
        services.AddSingleton(priceService ?? Mock.Of<ISecurityPriceService>());
        services.AddSingleton(localizer ?? CreateLocalizer());
        return services.BuildServiceProvider();
    }

    private static Mock<IPriceProvider> CreatePriceProvider(IReadOnlyList<(DateTime date, decimal close)> data)
    {
        var mock = new Mock<IPriceProvider>();
        mock.Setup(x => x.GetDailyPricesAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(data);
        return mock;
    }

    private static Mock<INotificationWriter> CreateNotificationWriter()
        => new Mock<INotificationWriter>();

    private static Mock<ISecurityPriceService> CreatePriceService(AppDbContext db)
    {
        var mock = new Mock<ISecurityPriceService>();
        mock
            .Setup(x => x.ClearPriceErrorAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns((Guid ownerUserId, Guid securityId, CancellationToken _) =>
            {
                var security = db.Securities.Single(x => x.Id == securityId && x.OwnerUserId == ownerUserId);
                security.ClearPriceError();
                db.SaveChanges();
                return Task.CompletedTask;
            });
        return mock;
    }

    private static Mock<IAlphaVantageKeyResolver> CreateKeyResolver()
    {
        var mock = new Mock<IAlphaVantageKeyResolver>();
        mock.Setup(x => x.GetSharedAsync(It.IsAny<CancellationToken>())).ReturnsAsync("shared-key");
        return mock;
    }

    private static IStringLocalizer<Pages> CreateLocalizer()
    {
        var mock = new Mock<IStringLocalizer<Pages>>();
        mock.Setup(x => x[It.IsAny<string>()]).Returns((string key) => new LocalizedString(key, key));
        return mock.Object;
    }

    private sealed class TestScopeFactory : IServiceScopeFactory
    {
        private readonly IServiceProvider _provider;

        public TestScopeFactory(IServiceProvider provider)
        {
            _provider = provider;
        }

        public IServiceScope CreateScope()
        {
            return new TestScope(_provider);
        }
    }

    private sealed class TestScope : IServiceScope
    {
        public IServiceProvider ServiceProvider { get; }

        public TestScope(IServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;
        }

        public void Dispose()
        {
        }
    }
}
