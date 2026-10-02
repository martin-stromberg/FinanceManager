using FinanceManager.Application.Notifications;
using FinanceManager.Application.Securities.GoldenCross;
using FinanceManager.Domain.Notifications;
using FinanceManager.Domain.Securities;
using FinanceManager.Domain.Users;
using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Securities.GoldenCross;
using FinanceManager.Shared.Dtos.Securities;
using FinanceManager.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace FinanceManager.Tests.Securities;

/// <summary>
/// Tests for <see cref="GoldenCrossService"/>: owner scoping of the statistics endpoint and the
/// once-per-cycle notification rules for approaching / reached golden crosses.
/// </summary>
public sealed class GoldenCrossServiceTests
{
    private static readonly GoldenCrossOptions Options = new() { ShortWindow = 3, LongWindow = 6, ApproachThresholdPercent = 3m };

    private sealed class Fixture
    {
        public AppDbContext Db { get; }
        public Mock<INotificationWriter> Notifier { get; } = new();
        public GoldenCrossService Service { get; }
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
            User.SetGoldenCrossNotificationsEnabled(notificationsEnabled);
            Db.Users.Add(User);

            Security = new Security(User.Id, "ETF X", "DE000A0", null, "ETFX", "EUR", null);
            Db.Securities.Add(Security);
            Db.SaveChanges();

            var localizer = new Mock<IGoldenCrossLocalizer>();
            localizer.Setup(l => l.Format(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<object[]>()))
                .Returns((string key, string? _, object[] _) => key);

            Service = new GoldenCrossService(Db, Notifier.Object, localizer.Object, Microsoft.Extensions.Options.Options.Create(Options), NullLogger<GoldenCrossService>.Instance);
        }

        /// <summary>Replaces the security's price history with the given closes (one per day, oldest first).</summary>
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

        public async Task<GoldenCrossPhase> NotifiedPhaseAsync()
            => await Db.Securities.AsNoTracking().Where(s => s.Id == Security.Id).Select(s => s.GoldenCrossNotifiedPhase).SingleAsync();
    }

    // Phase fixtures (short=3, long=6, threshold=3%)
    private static readonly decimal[] FarSeries = [100, 100, 100, 80, 80, 80];
    private static readonly decimal[] ApproachingSeries = [102, 102, 102, 99, 99, 99];
    private static readonly decimal[] CrossedSeries = [90, 90, 90, 110, 110, 110];

    /// <summary>The statistics endpoint returns the analysis for the owning user.</summary>
    [Fact]
    public async Task GetAsync_ForOwner_ReturnsAnalysis()
    {
        var f = new Fixture();
        await f.SetPricesAsync(ApproachingSeries);

        var dto = await f.Service.GetAsync(f.Security.Id, f.User.Id, CancellationToken.None);

        dto.Should().NotBeNull();
        dto!.Phase.Should().Be(GoldenCrossPhaseDto.Approaching);
        dto.ShortWindow.Should().Be(3);
        dto.LongWindow.Should().Be(6);
        dto.ShortAverage.Should().Be(99m);
        dto.LongAverage.Should().Be(100.5m);
        dto.CurrencyCode.Should().Be("EUR");
    }

    /// <summary>The statistics endpoint does not leak data of other users' securities.</summary>
    [Fact]
    public async Task GetAsync_ForOtherUser_ReturnsNull()
    {
        var f = new Fixture();
        await f.SetPricesAsync(ApproachingSeries);

        var dto = await f.Service.GetAsync(f.Security.Id, Guid.NewGuid(), CancellationToken.None);

        dto.Should().BeNull();
    }

    /// <summary>Without enough history the DTO reports insufficient data instead of failing.</summary>
    [Fact]
    public async Task GetAsync_WithFewPrices_ReturnsInsufficientData()
    {
        var f = new Fixture();
        await f.SetPricesAsync(1, 2, 3);

        var dto = await f.Service.GetAsync(f.Security.Id, f.User.Id, CancellationToken.None);

        dto!.Phase.Should().Be(GoldenCrossPhaseDto.InsufficientData);
        dto.ShortAverage.Should().BeNull();
    }

    /// <summary>Approaching is notified once; re-evaluating in the same phase does not notify again.</summary>
    [Fact]
    public async Task EvaluateAndNotify_Approaching_NotifiesOnlyOncePerCycle()
    {
        var f = new Fixture();
        await f.SetPricesAsync(ApproachingSeries);

        var first = await f.Service.EvaluateAndNotifyAsync(f.Security.Id, CancellationToken.None);
        var second = await f.Service.EvaluateAndNotifyAsync(f.Security.Id, CancellationToken.None);

        first.Should().BeTrue();
        second.Should().BeFalse();
        (await f.NotifiedPhaseAsync()).Should().Be(GoldenCrossPhase.Approaching);
        f.Notifier.Verify(n => n.CreateForUserAsync(
            f.User.Id, "GoldenCross_Approaching_Title", "GoldenCross_Approaching_Message",
            NotificationType.EventDriven, NotificationTarget.HomePage, It.IsAny<DateTime>(),
            GoldenCrossService.TriggerKeyPrefix + f.Security.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Approaching followed by crossed yields exactly one notification per phase; crossed is not repeated.</summary>
    [Fact]
    public async Task EvaluateAndNotify_ApproachingThenCrossed_NotifiesEachPhaseOnce()
    {
        var f = new Fixture();
        await f.SetPricesAsync(ApproachingSeries);
        await f.Service.EvaluateAndNotifyAsync(f.Security.Id, CancellationToken.None);

        await f.SetPricesAsync(CrossedSeries);
        var crossed = await f.Service.EvaluateAndNotifyAsync(f.Security.Id, CancellationToken.None);
        var crossedAgain = await f.Service.EvaluateAndNotifyAsync(f.Security.Id, CancellationToken.None);

        crossed.Should().BeTrue();
        crossedAgain.Should().BeFalse();
        (await f.NotifiedPhaseAsync()).Should().Be(GoldenCrossPhase.Crossed);
        f.Notifier.Verify(n => n.CreateForUserAsync(
            f.User.Id, "GoldenCross_Approaching_Title", It.IsAny<string>(), It.IsAny<NotificationType>(), It.IsAny<NotificationTarget>(),
            It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        f.Notifier.Verify(n => n.CreateForUserAsync(
            f.User.Id, "GoldenCross_Crossed_Title", It.IsAny<string>(), It.IsAny<NotificationType>(), It.IsAny<NotificationTarget>(),
            It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Falling back from crossed to approaching within the same cycle does not notify again.</summary>
    [Fact]
    public async Task EvaluateAndNotify_CrossedThenApproaching_DoesNotNotifyAgain()
    {
        var f = new Fixture();
        await f.SetPricesAsync(CrossedSeries);
        await f.Service.EvaluateAndNotifyAsync(f.Security.Id, CancellationToken.None);

        await f.SetPricesAsync(ApproachingSeries);
        var result = await f.Service.EvaluateAndNotifyAsync(f.Security.Id, CancellationToken.None);

        result.Should().BeFalse();
        (await f.NotifiedPhaseAsync()).Should().Be(GoldenCrossPhase.Crossed);
        f.Notifier.Verify(n => n.CreateForUserAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<NotificationType>(), It.IsAny<NotificationTarget>(),
            It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Once the cross is far away again the cycle resets and a new approach is notified again.</summary>
    [Fact]
    public async Task EvaluateAndNotify_AfterReturningToFar_NotifiesNewCycle()
    {
        var f = new Fixture();
        await f.SetPricesAsync(CrossedSeries);
        await f.Service.EvaluateAndNotifyAsync(f.Security.Id, CancellationToken.None);

        await f.SetPricesAsync(FarSeries);
        var far = await f.Service.EvaluateAndNotifyAsync(f.Security.Id, CancellationToken.None);
        far.Should().BeFalse();
        (await f.NotifiedPhaseAsync()).Should().Be(GoldenCrossPhase.Far);

        await f.SetPricesAsync(ApproachingSeries);
        var approaching = await f.Service.EvaluateAndNotifyAsync(f.Security.Id, CancellationToken.None);

        approaching.Should().BeTrue();
        f.Notifier.Verify(n => n.CreateForUserAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<NotificationType>(), It.IsAny<NotificationTarget>(),
            It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    /// <summary>Far away securities never produce a notification.</summary>
    [Fact]
    public async Task EvaluateAndNotify_Far_DoesNotNotify()
    {
        var f = new Fixture();
        await f.SetPricesAsync(FarSeries);

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
        f.Notifier.VerifyNoOtherCalls();
    }

    /// <summary>When the user disabled golden cross notifications, the phase is still tracked but no notification is created.</summary>
    [Fact]
    public async Task EvaluateAndNotify_WhenDisabledByUser_TracksPhaseButDoesNotNotify()
    {
        var f = new Fixture(notificationsEnabled: false);
        await f.SetPricesAsync(CrossedSeries);

        var result = await f.Service.EvaluateAndNotifyAsync(f.Security.Id, CancellationToken.None);

        result.Should().BeFalse();
        (await f.NotifiedPhaseAsync()).Should().Be(GoldenCrossPhase.Crossed);
        f.Notifier.VerifyNoOtherCalls();
    }

    /// <summary>The user setting is positive and enabled by default.</summary>
    [Fact]
    public void User_GoldenCrossNotificationsEnabled_DefaultsToTrue()
    {
        var u = new User("bob", "hash", false);

        u.GoldenCrossNotificationsEnabled.Should().BeTrue();
        u.SetGoldenCrossNotificationsEnabled(false);
        u.GoldenCrossNotificationsEnabled.Should().BeFalse();
    }
}
