using FinanceManager.Application.Notifications;
using FinanceManager.Domain.Notifications;
using FinanceManager.Domain.Users;
using FinanceManager.Infrastructure;
using FinanceManager.Tests.TestHelpers;
using FinanceManager.Web;
using FinanceManager.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;

namespace FinanceManager.Tests.Notifications;

/// <summary>
/// Covers <see cref="MonthlyReminderJob"/>'s daily-run scheduling logic over a full calendar year: since the
/// job is invoked once per day (not just on the target day), it must recognize the last business day of each
/// month exactly once and stay idempotent for every other day, so a user with monthly reminders enabled ends
/// up with precisely one notification per month rather than duplicates or gaps.
/// </summary>
public sealed class MonthlyReminderSchedulerTests
{
    /// <summary>Runs the job for every single day of 2024 and asserts exactly 12 notifications were created, each dated on that month's last business day as computed by <see cref="BusinessDayCalculator"/> - verifying both the trigger condition and the idempotent re-run behavior across a full year in one pass.</summary>
    [Fact]
    public async Task Run_ForFullYear2024_ShouldCreateNotifications_OnEachLastBusinessDay()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        var sp = services.BuildServiceProvider();
        var db = sp.GetRequiredService<AppDbContext>();

        var userId = Guid.NewGuid();
        var u = new User("alice", "hash", false);
        TestEntityHelper.SetEntityId(u, userId);
        u.SetNotificationSettings(monthlyReminderEnabled: true);
        db.Users.Add(u);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var start = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2024, 12, 31, 12, 0, 0, DateTimeKind.Utc);

        // Localizer mock for job
        var loc = new Mock<IStringLocalizer<Pages>>();
        loc.Setup(l => l["MonthlyReminder_Title"]).Returns(new LocalizedString("MonthlyReminder_Title", "Monatsabschluss"));
        loc.Setup(l => l["MonthlyReminder_Message"]).Returns(new LocalizedString("MonthlyReminder_Message", "Es ist der letzte Werktag des Monats."));

        // Holiday provider mock (no holidays)
        var holidays = new Mock<IHolidayProvider>();
        holidays.Setup(h => h.IsPublicHoliday(It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<string?>())).Returns(false);

        var resolver = new Mock<IHolidayProviderResolver>();
        resolver.Setup(r => r.Resolve(It.IsAny<HolidayProviderKind>())).Returns(holidays.Object);

        var job = new MonthlyReminderJob(loc.Object, resolver.Object);

        // Act: iterate all days and run job (idempotent)
        var day = start;
        while (day <= end)
        {
            await job.RunAsync(db, day, CancellationToken.None);
            day = day.AddDays(1);
        }

        // Assert: exactly 12 notifications (one per month)
        var notes = await db.Notifications.AsNoTracking().Where(n => n.OwnerUserId == userId).OrderBy(n => n.ScheduledDateUtc).ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(12, notes.Count);

        // Check dates equal last business day of each month in 2024
        for (int m = 1; m <= 12; m++)
        {
            var expected = BusinessDayCalculator.GetLastBusinessDayUtc(new DateTime(2024, m, 15, 0, 0, 0, DateTimeKind.Utc));
            Assert.Equal(expected, notes[m - 1].ScheduledDateUtc);
        }
    }
}
