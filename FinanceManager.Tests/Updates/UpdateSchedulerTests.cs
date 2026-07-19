using FinanceManager.Shared.Dtos.Update;
using FinanceManager.Web.Services.Updates;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace FinanceManager.Tests.Updates;

public sealed class UpdateSchedulerTests
{
    [Fact]
    public void ShouldInstall_DoesNotRetrySameScheduleAfterAttemptMarker()
    {
        var scheduler = new UpdateScheduler(new EmptyScopeFactory(), TimeProvider.System, NullLogger<UpdateScheduler>.Instance);
        var scheduled = new TimeOnly(3, 0);
        var now = new DateTime(2026, 7, 19, 3, 1, 0);
        var status = new UpdateStatusDto(UpdateStatusKind.Ready, "1.0.0", null, "1.1.0", "win-x64", null, null, "release.zip", false, null, scheduled, null);

        scheduler.ShouldInstall(scheduled, status, now).Should().BeTrue();
        SetAttemptMarker(scheduler, DateOnly.FromDateTime(now), scheduled);

        scheduler.ShouldInstall(scheduled, status, now.AddMinutes(1)).Should().BeFalse();
        scheduler.ShouldInstall(scheduled, status, now.AddDays(1)).Should().BeTrue();
    }

    private static void SetAttemptMarker(UpdateScheduler scheduler, DateOnly date, TimeOnly time)
    {
        typeof(UpdateScheduler).GetField("_lastAttemptedDate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(scheduler, date);
        typeof(UpdateScheduler).GetField("_lastAttemptedTime", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(scheduler, time);
    }

    private sealed class EmptyScopeFactory : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => new EmptyScope();
    }

    private sealed class EmptyScope : IServiceScope
    {
        public IServiceProvider ServiceProvider { get; } = new ServiceCollection().BuildServiceProvider();
        public void Dispose()
        {
        }
    }
}
