using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;
using SoftwareSchmiede.AutoUpdate;
using SoftwareSchmiede.AutoUpdate.Tests.TestSupport;
using static SoftwareSchmiede.AutoUpdate.Tests.TestSupport.AsyncTestWait;

namespace SoftwareSchmiede.AutoUpdate.Tests;

public sealed class AutoUpdateSchedulerServiceTests
{
    [Fact]
    public async Task Execute_AtScheduledTime_TriggersInstall()
    {
        var statusProvider = new Mock<IAutoUpdateStatusProvider>();
        statusProvider.Setup(s => s.GetSnapshot())
            .Returns(new AutoUpdateStatusSnapshot(AutoUpdateState.ReadyToInstall, "1.0.0", "2.0.0", null, null, null, null, null, false, null));
        var commandService = new Mock<IAutoUpdateCommandHandler>();
        commandService.Setup(c => c.InstallAsync(true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AutoUpdateResult(AutoUpdateOutcome.Success, AutoUpdateState.Installing, null, null));
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 7, 27, 21, 59, 30, TimeSpan.Zero));
        var options = new AutoUpdateOptions { ScheduledInstallTime = new TimeOnly(22, 0) };
        var service = new AutoUpdateSchedulerService(commandService.Object, statusProvider.Object, options, timeProvider, NullLogger<AutoUpdateSchedulerService>.Instance);

        await service.StartAsync(CancellationToken.None);
        await Task.Delay(100);
        timeProvider.Advance(TimeSpan.FromMinutes(1));
        await WaitForAsync(() => commandService.Invocations.Count > 0);
        await service.StopAsync(CancellationToken.None);

        commandService.Verify(c => c.InstallAsync(true, It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task Execute_WhenNotReady_DoesNotInstall()
    {
        var statusProvider = new Mock<IAutoUpdateStatusProvider>();
        statusProvider.Setup(s => s.GetSnapshot())
            .Returns(new AutoUpdateStatusSnapshot(AutoUpdateState.Idle, "1.0.0", null, null, null, null, null, null, false, null));
        var commandService = new Mock<IAutoUpdateCommandHandler>();
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 7, 27, 22, 1, 0, TimeSpan.Zero));
        var options = new AutoUpdateOptions { ScheduledInstallTime = new TimeOnly(22, 0) };
        var service = new AutoUpdateSchedulerService(commandService.Object, statusProvider.Object, options, timeProvider, NullLogger<AutoUpdateSchedulerService>.Instance);

        await service.StartAsync(CancellationToken.None);
        await Task.Delay(100);
        await service.StopAsync(CancellationToken.None);

        commandService.Verify(c => c.InstallAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Execute_SameScheduleTwice_InstallsOnce()
    {
        var statusProvider = new Mock<IAutoUpdateStatusProvider>();
        statusProvider.Setup(s => s.GetSnapshot())
            .Returns(new AutoUpdateStatusSnapshot(AutoUpdateState.ReadyToInstall, "1.0.0", "2.0.0", null, null, null, null, null, false, null));
        var commandService = new Mock<IAutoUpdateCommandHandler>();
        commandService.Setup(c => c.InstallAsync(true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AutoUpdateResult(AutoUpdateOutcome.Success, AutoUpdateState.Installing, null, null));
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 7, 27, 22, 0, 0, TimeSpan.Zero));
        var options = new AutoUpdateOptions { ScheduledInstallTime = new TimeOnly(22, 0) };
        var service = new AutoUpdateSchedulerService(commandService.Object, statusProvider.Object, options, timeProvider, NullLogger<AutoUpdateSchedulerService>.Instance);

        await service.StartAsync(CancellationToken.None);
        await WaitForAsync(() => commandService.Invocations.Count > 0);
        timeProvider.Advance(TimeSpan.FromMinutes(1));
        await Task.Delay(100);
        await service.StopAsync(CancellationToken.None);

        commandService.Verify(c => c.InstallAsync(true, It.IsAny<CancellationToken>()), Times.Once);
    }
}
