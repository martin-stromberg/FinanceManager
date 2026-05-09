using FinanceManager.Application;
using FinanceManager.Application.Notifications;
using FinanceManager.Application.Securities;
using FinanceManager.Domain.Notifications;
using FinanceManager.Shared.Dtos.Securities;
using FinanceManager.Web.Services;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FinanceManager.Tests.Web.Services;

public sealed class SecurityPricesBackfillExecutorNotificationTests
{
    [Fact]
    public async Task ExecuteAsync_InvalidSymbol_PersistsPriceError_AndCreatesExpectedNotification()
    {
        var ownerUserId = Guid.NewGuid();
        var securityId = Guid.NewGuid();
        var security = new SecurityDto
        {
            Id = securityId,
            Name = "Bad Security",
            Identifier = "BAD-1",
            AlphaVantageCode = "BAD",
            IsActive = true,
            HasPriceError = false
        };

        var securityService = new Mock<ISecurityService>();
        securityService.Setup(x => x.ListAsync(ownerUserId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync([security]);

        var priceService = new Mock<ISecurityPriceService>();
        priceService.Setup(x => x.GetLatestDateAsync(ownerUserId, securityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DateTime?)null);

        string? persistedMessage = null;
        string? persistedErrorClass = null;
        priceService.Setup(x => x.SetPriceErrorAsync(ownerUserId, securityId, It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, Guid, string, string?, string?, CancellationToken>((_, _, message, errorClass, _, _) =>
            {
                persistedMessage = message;
                persistedErrorClass = errorClass;
            })
            .Returns(Task.CompletedTask);

        var priceProvider = new Mock<IPriceProvider>();
        priceProvider.Setup(x => x.GetDailyPricesAsync("BAD", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new PriceProviderException(PriceProviderErrorClass.InvalidSymbolOrFunction, "provider raw", "provider error"));

        var keyResolver = new Mock<IAlphaVantageKeyResolver>();
        keyResolver.Setup(x => x.GetSharedAsync(It.IsAny<CancellationToken>())).ReturnsAsync("shared-key");

        var notifier = new Mock<INotificationWriter>();
        var executor = CreateExecutor(securityService.Object, priceService.Object, priceProvider.Object, keyResolver.Object, notifier.Object);
        var context = CreateContext(ownerUserId);
        var expectedScheduledDate = DateTime.UtcNow.Date;

        await executor.ExecuteAsync(context, CancellationToken.None);

        persistedErrorClass.Should().Be("INVALID_SYMBOL_OR_FUNCTION");
        persistedMessage.Should().NotBeNullOrWhiteSpace();

        notifier.Verify(x => x.CreateForUserAsync(
                ownerUserId,
                "Kursabruf fehlgeschlagen",
                persistedMessage!,
                NotificationType.SystemAlert,
                NotificationTarget.HomePage,
                expectedScheduledDate,
                $"security:error:{securityId}",
                It.IsAny<CancellationToken>()),
            Times.Once);

        priceService.Verify(x => x.SetPriceErrorAsync(
                ownerUserId,
                securityId,
                persistedMessage!,
                "INVALID_SYMBOL_OR_FUNCTION",
                "provider raw",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_TransientNetwork_DoesNotCreateNotification()
    {
        var ownerUserId = Guid.NewGuid();
        var securityId = Guid.NewGuid();
        var security = new SecurityDto
        {
            Id = securityId,
            Name = "Transient Security",
            Identifier = "TR-1",
            AlphaVantageCode = "TRANSIENT",
            IsActive = true,
            HasPriceError = false
        };

        var securityService = new Mock<ISecurityService>();
        securityService.Setup(x => x.ListAsync(ownerUserId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync([security]);

        var priceService = new Mock<ISecurityPriceService>();
        priceService.Setup(x => x.GetLatestDateAsync(ownerUserId, securityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DateTime?)null);
        priceService.Setup(x => x.SetPriceErrorAsync(ownerUserId, securityId, It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var priceProvider = new Mock<IPriceProvider>();
        priceProvider.Setup(x => x.GetDailyPricesAsync("TRANSIENT", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new PriceProviderException(PriceProviderErrorClass.TransientNetwork, "provider raw", "provider error"));

        var keyResolver = new Mock<IAlphaVantageKeyResolver>();
        keyResolver.Setup(x => x.GetSharedAsync(It.IsAny<CancellationToken>())).ReturnsAsync("shared-key");

        var notifier = new Mock<INotificationWriter>();
        var executor = CreateExecutor(securityService.Object, priceService.Object, priceProvider.Object, keyResolver.Object, notifier.Object);
        var context = CreateContext(ownerUserId);

        await executor.ExecuteAsync(context, CancellationToken.None);

        notifier.Verify(x => x.CreateForUserAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<NotificationType>(), It.IsAny<NotificationTarget>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_RateLimit_DoesNotCreateNotification()
    {
        var ownerUserId = Guid.NewGuid();
        var securityId = Guid.NewGuid();
        var security = new SecurityDto
        {
            Id = securityId,
            Name = "RateLimit Security",
            Identifier = "RL-1",
            AlphaVantageCode = "RATE",
            IsActive = true,
            HasPriceError = false
        };

        var securityService = new Mock<ISecurityService>();
        securityService.Setup(x => x.ListAsync(ownerUserId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync([security]);

        var priceService = new Mock<ISecurityPriceService>();
        priceService.Setup(x => x.GetLatestDateAsync(ownerUserId, securityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DateTime?)null);

        var priceProvider = new Mock<IPriceProvider>();
        priceProvider.Setup(x => x.GetDailyPricesAsync("RATE", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new PriceProviderException(PriceProviderErrorClass.RateLimit, "provider raw", "provider error"));

        var keyResolver = new Mock<IAlphaVantageKeyResolver>();
        keyResolver.Setup(x => x.GetSharedAsync(It.IsAny<CancellationToken>())).ReturnsAsync("shared-key");

        var notifier = new Mock<INotificationWriter>();
        var executor = CreateExecutor(securityService.Object, priceService.Object, priceProvider.Object, keyResolver.Object, notifier.Object);
        var context = CreateContext(ownerUserId);

        var act = () => executor.ExecuteAsync(context, CancellationToken.None);
        await act.Should().ThrowAsync<PriceProviderException>();

        notifier.Verify(x => x.CreateForUserAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<NotificationType>(), It.IsAny<NotificationTarget>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        priceService.Verify(x => x.SetPriceErrorAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownProviderError_PersistsPriceError_AndCreatesExpectedNotification()
    {
        var ownerUserId = Guid.NewGuid();
        var securityId = Guid.NewGuid();
        var security = new SecurityDto
        {
            Id = securityId,
            Name = "Unknown Security",
            Identifier = "UNK-1",
            AlphaVantageCode = "UNKNOWN",
            IsActive = true,
            HasPriceError = false
        };

        var securityService = new Mock<ISecurityService>();
        securityService.Setup(x => x.ListAsync(ownerUserId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync([security]);

        var priceService = new Mock<ISecurityPriceService>();
        priceService.Setup(x => x.GetLatestDateAsync(ownerUserId, securityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DateTime?)null);

        string? persistedMessage = null;
        string? persistedErrorClass = null;
        priceService.Setup(x => x.SetPriceErrorAsync(ownerUserId, securityId, It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, Guid, string, string?, string?, CancellationToken>((_, _, message, errorClass, _, _) =>
            {
                persistedMessage = message;
                persistedErrorClass = errorClass;
            })
            .Returns(Task.CompletedTask);

        var priceProvider = new Mock<IPriceProvider>();
        priceProvider.Setup(x => x.GetDailyPricesAsync("UNKNOWN", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new PriceProviderException(PriceProviderErrorClass.UnknownProviderError, "provider raw", "provider error"));

        var keyResolver = new Mock<IAlphaVantageKeyResolver>();
        keyResolver.Setup(x => x.GetSharedAsync(It.IsAny<CancellationToken>())).ReturnsAsync("shared-key");

        var notifier = new Mock<INotificationWriter>();
        var executor = CreateExecutor(securityService.Object, priceService.Object, priceProvider.Object, keyResolver.Object, notifier.Object);
        var context = CreateContext(ownerUserId);
        var expectedScheduledDate = DateTime.UtcNow.Date;

        await executor.ExecuteAsync(context, CancellationToken.None);

        persistedErrorClass.Should().Be("UNKNOWN_PROVIDER_ERROR");
        persistedMessage.Should().NotBeNullOrWhiteSpace();
        persistedMessage.Should().Contain("externer Fehler");

        notifier.Verify(x => x.CreateForUserAsync(
                ownerUserId,
                "Kursabruf fehlgeschlagen",
                persistedMessage!,
                NotificationType.SystemAlert,
                NotificationTarget.HomePage,
                expectedScheduledDate,
                $"security:error:{securityId}",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenSetPriceErrorFailsInProviderErrorBranch_ContinuesWithNextSecurity_AndDoesNotThrow()
    {
        var ownerUserId = Guid.NewGuid();
        var badSecurityId = Guid.NewGuid();
        var goodSecurityId = Guid.NewGuid();

        var securityService = new Mock<ISecurityService>();
        securityService.Setup(x => x.ListAsync(ownerUserId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new SecurityDto
                {
                    Id = badSecurityId,
                    Name = "A Bad Security",
                    Identifier = "BAD-1",
                    AlphaVantageCode = "BAD",
                    IsActive = true,
                    HasPriceError = false
                },
                new SecurityDto
                {
                    Id = goodSecurityId,
                    Name = "B Good Security",
                    Identifier = "GOOD-1",
                    AlphaVantageCode = "GOOD",
                    IsActive = true,
                    HasPriceError = false
                }
            ]);

        var priceService = new Mock<ISecurityPriceService>();
        priceService.Setup(x => x.GetLatestDateAsync(ownerUserId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DateTime?)null);
        priceService.Setup(x => x.SetPriceErrorAsync(ownerUserId, badSecurityId, It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("set failed"));

        var calledSymbols = new List<string>();
        var priceProvider = new Mock<IPriceProvider>();
        priceProvider.Setup(x => x.GetDailyPricesAsync("BAD", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Callback(() => calledSymbols.Add("BAD"))
            .ThrowsAsync(new PriceProviderException(PriceProviderErrorClass.InvalidSymbolOrFunction, "provider raw", "provider error"));
        priceProvider.Setup(x => x.GetDailyPricesAsync("GOOD", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Callback(() => calledSymbols.Add("GOOD"))
            .ReturnsAsync([(new DateTime(2024, 1, 2), 123.45m)]);

        var keyResolver = new Mock<IAlphaVantageKeyResolver>();
        keyResolver.Setup(x => x.GetSharedAsync(It.IsAny<CancellationToken>())).ReturnsAsync("shared-key");

        var notifier = new Mock<INotificationWriter>();
        var executor = CreateExecutor(securityService.Object, priceService.Object, priceProvider.Object, keyResolver.Object, notifier.Object);
        var context = CreateContext(ownerUserId);

        var act = () => executor.ExecuteAsync(context, CancellationToken.None);
        await act.Should().NotThrowAsync();

        calledSymbols.Should().ContainInOrder("BAD", "GOOD");
        priceService.Verify(x => x.CreateAsync(ownerUserId, goodSecurityId, new DateTime(2024, 1, 2), 123.45m, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenUnexpectedExceptionOccursForSecurity_ContinuesWithRemainingSecurities()
    {
        var ownerUserId = Guid.NewGuid();
        var badSecurityId = Guid.NewGuid();
        var goodSecurityId = Guid.NewGuid();

        var securityService = new Mock<ISecurityService>();
        securityService.Setup(x => x.ListAsync(ownerUserId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new SecurityDto
                {
                    Id = badSecurityId,
                    Name = "A Broken Security",
                    Identifier = "BROKEN-1",
                    AlphaVantageCode = "BROKEN",
                    IsActive = true,
                    HasPriceError = false
                },
                new SecurityDto
                {
                    Id = goodSecurityId,
                    Name = "B Good Security",
                    Identifier = "GOOD-1",
                    AlphaVantageCode = "GOOD",
                    IsActive = true,
                    HasPriceError = false
                }
            ]);

        var priceService = new Mock<ISecurityPriceService>();
        priceService.Setup(x => x.GetLatestDateAsync(ownerUserId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DateTime?)null);

        var calledSymbols = new List<string>();
        var priceProvider = new Mock<IPriceProvider>();
        priceProvider.Setup(x => x.GetDailyPricesAsync("BROKEN", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Callback(() => calledSymbols.Add("BROKEN"))
            .ThrowsAsync(new Exception("unexpected provider failure"));
        priceProvider.Setup(x => x.GetDailyPricesAsync("GOOD", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Callback(() => calledSymbols.Add("GOOD"))
            .ReturnsAsync([(new DateTime(2024, 1, 3), 200.00m)]);

        var keyResolver = new Mock<IAlphaVantageKeyResolver>();
        keyResolver.Setup(x => x.GetSharedAsync(It.IsAny<CancellationToken>())).ReturnsAsync("shared-key");

        var notifier = new Mock<INotificationWriter>();
        var executor = CreateExecutor(securityService.Object, priceService.Object, priceProvider.Object, keyResolver.Object, notifier.Object);
        var context = CreateContext(ownerUserId);

        var act = () => executor.ExecuteAsync(context, CancellationToken.None);
        await act.Should().NotThrowAsync();

        calledSymbols.Should().ContainInOrder("BROKEN", "GOOD");
        priceService.Verify(x => x.CreateAsync(ownerUserId, goodSecurityId, new DateTime(2024, 1, 3), 200.00m, It.IsAny<CancellationToken>()), Times.Once);
    }

    private static SecurityPricesBackfillExecutor CreateExecutor(
        ISecurityService securityService,
        ISecurityPriceService priceService,
        IPriceProvider priceProvider,
        IAlphaVantageKeyResolver keyResolver,
        INotificationWriter notifier)
    {
        var services = new ServiceCollection();
        services.AddSingleton(securityService);
        services.AddSingleton(priceService);
        services.AddSingleton(priceProvider);
        services.AddSingleton(keyResolver);
        services.AddSingleton(notifier);
        var provider = services.BuildServiceProvider();
        return new SecurityPricesBackfillExecutor(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<SecurityPricesBackfillExecutor>.Instance,
            new PassthroughLocalizer<FinanceManager.Web.Pages>());
    }

    private static BackgroundTaskContext CreateContext(Guid ownerUserId)
        => new(Guid.NewGuid(), ownerUserId, null, (_, _, _, _, _) => { });

    private sealed class PassthroughLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name, false);
        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(name, arguments), false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Array.Empty<LocalizedString>();
        public IStringLocalizer WithCulture(System.Globalization.CultureInfo culture) => this;
    }
}
