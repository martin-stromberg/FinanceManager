using FinanceManager.Application.Notifications;
using FinanceManager.Application.Securities.TrendReversal;
using FinanceManager.Domain.Notifications;
using FinanceManager.Domain.Securities;
using FinanceManager.Domain.Users;
using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Securities.TrendReversal;
using FinanceManager.Shared.Dtos.Securities;
using FinanceManager.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace FinanceManager.Tests.Securities;

/// <summary>
/// Tests for <see cref="TrendReversalService"/>: owner scoping of the statistics endpoint and the
/// once-per-status-per-cycle notification rules for weakened / likely reversing trends.
/// </summary>
public sealed class TrendReversalServiceTests
{
    private static readonly TrendReversalOptions Options = new()
    {
        ShortWindow = 3,
        LongWindow = 6,
        StructuralBreakDays = 2,
        MacdFastPeriod = 2,
        MacdSlowPeriod = 4,
        MacdSignalPeriod = 2,
        RsiPeriod = 3,
        MarketStructureLookbackDays = 30,
        SwingLookaround = 1,
        ReversalLikelyThreshold = 4
    };

    private sealed class Fixture
    {
        public AppDbContext Db { get; }
        public Mock<INotificationWriter> Notifier { get; } = new();
        public TrendReversalService Service { get; }
        public User User { get; }
        public Security Security { get; }

        public Fixture(bool notificationsEnabled = true)
        {
            var opts = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            Db = new AppDbContext(opts);

            User = new User("alice", "hash", false);
            TestEntityHelper.SetEntityId(User, Guid.NewGuid());
            User.SetTrendReversalNotificationsEnabled(notificationsEnabled);
            Db.Users.Add(User);

            Security = new Security(User.Id, "ETF X", "DE000A0", null, "ETFX", "EUR", null);
            Db.Securities.Add(Security);
            Db.SaveChanges();

            var localizer = new Mock<ITrendReversalLocalizer>();
            localizer.Setup(l => l.Format(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<object[]>()))
                .Returns((string key, string? _, object[] _) => key);

            Service = new TrendReversalService(Db, Notifier.Object, localizer.Object, Microsoft.Extensions.Options.Options.Create(Options), NullLogger<TrendReversalService>.Instance);
        }

        /// <summary>Replaces the security's price history with the given closes (one per day, oldest first).</summary>
        /// <param name="closes">Chronological closing prices, oldest first.</param>
        public async Task SetPricesAsync(params decimal[] closes)
        {
            Db.SecurityPrices.RemoveRange(Db.SecurityPrices.Where(p => p.SecurityId == Security.Id));
            var start = new DateTime(2024, 1, 1);
            for (var i = 0; i < closes.Length; i++)
            {
                Db.SecurityPrices.Add(new SecurityPrice(Security.Id, start.AddDays(i), closes[i]));
            }
            await Db.SaveChangesAsync();
            Db.ChangeTracker.Clear();
        }

        public async Task<TrendStatus> NotifiedStatusAsync()
            => await Db.Securities.AsNoTracking().Where(s => s.Id == Security.Id).Select(s => s.TrendReversalNotifiedStatus).SingleAsync();
    }

    // Series fixtures (short=3, long=6, structural break=2, threshold=4)
    // Monotone rise: no signal at all.
    private static readonly decimal[] IntactSeries = [100, 101, 102, 103, 104, 105, 106];
    // Single-day break of the long average plus bearish MACD crossover: score 3 -> weakened.
    private static readonly decimal[] WeakenedSeries = [90, 100, 110, 110, 110, 90];
    // Oscillating decline: death cross + structural line break + emerging downtrend: score >= 4.
    private static readonly decimal[] ReversalLikelySeries = [100, 105, 98, 102, 95, 100, 90, 96, 85];

    /// <summary>The statistics endpoint returns the analysis for the owning user.</summary>
    [Fact]
    public async Task GetAsync_ForOwner_ReturnsDto()
    {
        var f = new Fixture();
        await f.SetPricesAsync(ReversalLikelySeries);

        var dto = await f.Service.GetAsync(f.Security.Id, f.User.Id, CancellationToken.None);

        dto.Should().NotBeNull();
        dto!.Status.Should().Be(TrendStatusDto.ReversalLikely);
        dto.ShortWindow.Should().Be(3);
        dto.LongWindow.Should().Be(6);
        dto.CurrencyCode.Should().Be("EUR");
        dto.Signals.Should().NotBeNullOrEmpty();
        dto.SignalScore.Should().BeGreaterThanOrEqualTo(4);
    }

    /// <summary>The statistics endpoint does not leak data of other users' securities.</summary>
    [Fact]
    public async Task GetAsync_ForOtherUser_ReturnsNull()
    {
        var f = new Fixture();
        await f.SetPricesAsync(ReversalLikelySeries);

        var dto = await f.Service.GetAsync(f.Security.Id, Guid.NewGuid(), CancellationToken.None);

        dto.Should().BeNull();
    }

    /// <summary>Without enough history the DTO reports insufficient data instead of failing.</summary>
    [Fact]
    public async Task GetAsync_InsufficientData_ReturnsStatus()
    {
        var f = new Fixture();
        await f.SetPricesAsync(1, 2, 3);

        var dto = await f.Service.GetAsync(f.Security.Id, f.User.Id, CancellationToken.None);

        dto!.Status.Should().Be(TrendStatusDto.InsufficientData);
        dto.ShortAverage.Should().BeNull();
    }

    /// <summary>A weakened trend is notified once; re-evaluating the same status does not notify again.</summary>
    [Fact]
    public async Task EvaluateAndNotify_Weakened_NotifiesOnlyOncePerCycle()
    {
        var f = new Fixture();
        await f.SetPricesAsync(WeakenedSeries);

        var first = await f.Service.EvaluateAndNotifyAsync(f.Security.Id, CancellationToken.None);
        var second = await f.Service.EvaluateAndNotifyAsync(f.Security.Id, CancellationToken.None);

        first.Should().BeTrue();
        second.Should().BeFalse();
        (await f.NotifiedStatusAsync()).Should().Be(TrendStatus.Weakened);
        f.Notifier.Verify(n => n.CreateForUserAsync(
            f.User.Id, "TrendReversal_Weakened_Title", "TrendReversal_Weakened_Message",
            NotificationType.EventDriven, NotificationTarget.HomePage, It.IsAny<DateTime>(),
            TrendReversalService.TriggerKeyPrefix + f.Security.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Weakened followed by reversal-likely yields exactly one notification per status.</summary>
    [Fact]
    public async Task EvaluateAndNotify_WeakenedThenReversalLikely_NotifiesEachStatusOnce()
    {
        var f = new Fixture();
        await f.SetPricesAsync(WeakenedSeries);
        await f.Service.EvaluateAndNotifyAsync(f.Security.Id, CancellationToken.None);

        await f.SetPricesAsync(ReversalLikelySeries);
        var likely = await f.Service.EvaluateAndNotifyAsync(f.Security.Id, CancellationToken.None);
        var likelyAgain = await f.Service.EvaluateAndNotifyAsync(f.Security.Id, CancellationToken.None);

        likely.Should().BeTrue();
        likelyAgain.Should().BeFalse();
        (await f.NotifiedStatusAsync()).Should().Be(TrendStatus.ReversalLikely);
        f.Notifier.Verify(n => n.CreateForUserAsync(
            f.User.Id, "TrendReversal_Weakened_Title", It.IsAny<string>(), It.IsAny<NotificationType>(), It.IsAny<NotificationTarget>(),
            It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        f.Notifier.Verify(n => n.CreateForUserAsync(
            f.User.Id, "TrendReversal_ReversalLikely_Title", It.IsAny<string>(), It.IsAny<NotificationType>(), It.IsAny<NotificationTarget>(),
            It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Falling back from reversal-likely to weakened within the same cycle does not notify again.</summary>
    [Fact]
    public async Task EvaluateAndNotify_ReversalLikelyThenWeakened_DoesNotNotifyAgain()
    {
        var f = new Fixture();
        await f.SetPricesAsync(ReversalLikelySeries);
        await f.Service.EvaluateAndNotifyAsync(f.Security.Id, CancellationToken.None);

        await f.SetPricesAsync(WeakenedSeries);
        var result = await f.Service.EvaluateAndNotifyAsync(f.Security.Id, CancellationToken.None);

        result.Should().BeFalse();
        (await f.NotifiedStatusAsync()).Should().Be(TrendStatus.ReversalLikely);
        f.Notifier.Verify(n => n.CreateForUserAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<NotificationType>(), It.IsAny<NotificationTarget>(),
            It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Once the trend is intact again the cycle resets and a new weakening is notified again.</summary>
    [Fact]
    public async Task EvaluateAndNotify_AfterReturningToIntact_NotifiesNewCycle()
    {
        var f = new Fixture();
        await f.SetPricesAsync(ReversalLikelySeries);
        await f.Service.EvaluateAndNotifyAsync(f.Security.Id, CancellationToken.None);

        await f.SetPricesAsync(IntactSeries);
        var intact = await f.Service.EvaluateAndNotifyAsync(f.Security.Id, CancellationToken.None);
        intact.Should().BeFalse();
        (await f.NotifiedStatusAsync()).Should().Be(TrendStatus.Intact);

        await f.SetPricesAsync(WeakenedSeries);
        var weakened = await f.Service.EvaluateAndNotifyAsync(f.Security.Id, CancellationToken.None);

        weakened.Should().BeTrue();
        f.Notifier.Verify(n => n.CreateForUserAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<NotificationType>(), It.IsAny<NotificationTarget>(),
            It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    /// <summary>Intact trends never produce a notification.</summary>
    [Fact]
    public async Task EvaluateAndNotify_Intact_DoesNotNotify()
    {
        var f = new Fixture();
        await f.SetPricesAsync(IntactSeries);

        var result = await f.Service.EvaluateAndNotifyAsync(f.Security.Id, CancellationToken.None);

        result.Should().BeFalse();
        f.Notifier.VerifyNoOtherCalls();
    }

    /// <summary>With insufficient data nothing is notified and the cycle state is untouched.</summary>
    [Fact]
    public async Task EvaluateAndNotify_InsufficientData_DoesNothing()
    {
        var f = new Fixture();
        await f.SetPricesAsync(1, 2);

        var result = await f.Service.EvaluateAndNotifyAsync(f.Security.Id, CancellationToken.None);

        result.Should().BeFalse();
        (await f.NotifiedStatusAsync()).Should().Be(TrendStatus.Intact);
        f.Notifier.VerifyNoOtherCalls();
    }

    /// <summary>When the user disabled trend reversal notifications, the status is still tracked but no notification is created.</summary>
    [Fact]
    public async Task EvaluateAndNotify_WhenDisabledByUser_TracksStatusButDoesNotNotify()
    {
        var f = new Fixture(notificationsEnabled: false);
        await f.SetPricesAsync(ReversalLikelySeries);

        var result = await f.Service.EvaluateAndNotifyAsync(f.Security.Id, CancellationToken.None);

        result.Should().BeFalse();
        (await f.NotifiedStatusAsync()).Should().Be(TrendStatus.ReversalLikely);
        f.Notifier.VerifyNoOtherCalls();
    }

    /// <summary>The user setting is positive and enabled by default.</summary>
    [Fact]
    public void User_TrendReversalNotificationsEnabled_DefaultsToTrue()
    {
        var u = new User("bob", "hash", false);

        u.TrendReversalNotificationsEnabled.Should().BeTrue();
        u.SetTrendReversalNotificationsEnabled(false);
        u.TrendReversalNotificationsEnabled.Should().BeFalse();
    }
}
