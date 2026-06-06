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
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Reflection;
using FinanceManager.Infrastructure.Notifications;

namespace FinanceManager.Tests.Web;

public sealed class SecurityPriceErrorRecoveryTests
{
    [Fact]
    public async Task SecurityPriceWorker_ShouldProcessSecurityWhenPriceErrorExists_WhenRunExecutes()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var db = CreateDatabase(connection, out _, out var securityId);
        var priceService = CreatePriceService(db);
        var provider = CreateServiceProvider(
            db,
            CreatePriceProvider(new[] { (DateTime.UtcNow.Date.AddDays(-1), 123.45m) }),
            CreateNotificationWriter(),
            CreateKeyResolver(),
            priceService: priceService.Object);

        var worker = new SecurityPriceWorker(
            new TestScopeFactory(provider),
            Mock.Of<ILogger<SecurityPriceWorker>>(),
            Options.Create(new AlphaVantageQuotaOptions { MaxSymbolsPerRun = 10, RequestsPerMinute = 0 }));

        var method = typeof(SecurityPriceWorker).GetMethod("RunOnceAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var task = (Task)method!.Invoke(worker, new object[] { CancellationToken.None })!;
        await task;

        var security = db.Securities.Single(x => x.Id == securityId);
        Assert.False(security.HasPriceError);
        Assert.Single(db.SecurityPrices.Where(x => x.SecurityId == securityId));
    }

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

        var priceProvider = CreatePriceProvider(new[] { (DateTime.UtcNow.Date.AddDays(-1), 99.99m) });
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

        var localizer = CreateLocalizer();
        var provider = CreateServiceProvider(db, priceProvider, CreateNotificationWriter(), CreateKeyResolver(), securityService.Object, priceService.Object, localizer);

        var executor = new SecurityPricesBackfillExecutor(
            new TestScopeFactory(provider),
            Mock.Of<ILogger<SecurityPricesBackfillExecutor>>(),
            localizer);

        var context = new BackgroundTaskContext(Guid.NewGuid(), ownerId, null, (_, _, _, _, _) => { });

        await executor.ExecuteAsync(context, CancellationToken.None);

        priceService.Verify(x => x.CreateAsync(ownerId, securityId, It.IsAny<DateTime>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()), Times.Once);

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
