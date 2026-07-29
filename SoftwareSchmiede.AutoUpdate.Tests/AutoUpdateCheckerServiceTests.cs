using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;
using SoftwareSchmiede.AutoUpdate;
using SoftwareSchmiede.AutoUpdate.Tests.TestSupport;
using static SoftwareSchmiede.AutoUpdate.Tests.TestSupport.AsyncTestWait;

namespace SoftwareSchmiede.AutoUpdate.Tests;

public sealed class AutoUpdateCheckerServiceTests
{
    private static readonly DateTimeOffset Monday10Am = new(2026, 7, 27, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Execute_TriggersCheckOnlyWithinWindow()
    {
        var orchestrator = new Mock<IAutoUpdateOrchestrator>();
        orchestrator.Setup(o => o.CheckForUpdateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AutoUpdateResult(AutoUpdateOutcome.NoUpdate, AutoUpdateState.Idle, null, null));
        var timeProvider = new FakeTimeProvider(Monday10Am);
        var options = new AutoUpdateOptions
        {
            Enabled = true,
            SourceCheck = new SourceCheckOptions
            {
                Interval = 60,
                TimeRanges = new List<SourceCheckTimeRange> { new() { DayOfWeek = DayOfWeek.Monday, StartTime = TimeOnly.MinValue, EndTime = TimeOnly.MaxValue } }
            }
        };
        var service = new AutoUpdateCheckerService(orchestrator.Object, options, new SourceCheckWindowEvaluator(), timeProvider, NullLogger<AutoUpdateCheckerService>.Instance);

        await service.StartAsync(CancellationToken.None);
        await WaitForAsync(() => orchestrator.Invocations.Count > 0);
        await service.StopAsync(CancellationToken.None);

        orchestrator.Verify(o => o.CheckForUpdateAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task Execute_NeverTriggersDownloadOrInstall()
    {
        var orchestrator = new Mock<IAutoUpdateOrchestrator>();
        orchestrator.Setup(o => o.CheckForUpdateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AutoUpdateResult(AutoUpdateOutcome.Success, AutoUpdateState.UpdateAvailable, null, null));
        var timeProvider = new FakeTimeProvider(Monday10Am);
        var options = new AutoUpdateOptions { Enabled = true, SourceCheck = new SourceCheckOptions { Interval = 60 } };
        var service = new AutoUpdateCheckerService(orchestrator.Object, options, new SourceCheckWindowEvaluator(), timeProvider, NullLogger<AutoUpdateCheckerService>.Instance);

        await service.StartAsync(CancellationToken.None);
        await WaitForAsync(() => orchestrator.Invocations.Count > 0);
        await service.StopAsync(CancellationToken.None);

        orchestrator.Verify(o => o.DownloadAsync(It.IsAny<CancellationToken>()), Times.Never);
        orchestrator.Verify(o => o.InstallAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        orchestrator.Verify(o => o.RunUpdateAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Execute_WhenCheckThrows_ContinuesLoop()
    {
        var orchestrator = new Mock<IAutoUpdateOrchestrator>();
        orchestrator.Setup(o => o.CheckForUpdateAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("boom"));
        var timeProvider = new FakeTimeProvider(Monday10Am);
        var options = new AutoUpdateOptions { Enabled = true, SourceCheck = new SourceCheckOptions { Interval = 1 } };
        var service = new AutoUpdateCheckerService(orchestrator.Object, options, new SourceCheckWindowEvaluator(), timeProvider, NullLogger<AutoUpdateCheckerService>.Instance);

        await service.StartAsync(CancellationToken.None);
        await WaitForAsync(() => orchestrator.Invocations.Count > 0);
        await Task.Delay(100);
        timeProvider.Advance(TimeSpan.FromMinutes(6));
        await WaitForAsync(() => orchestrator.Invocations.Count > 1);
        await service.StopAsync(CancellationToken.None);

        orchestrator.Invocations.Count.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task Execute_RespectsConfiguredInterval()
    {
        var orchestrator = new Mock<IAutoUpdateOrchestrator>();
        orchestrator.Setup(o => o.CheckForUpdateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AutoUpdateResult(AutoUpdateOutcome.NoUpdate, AutoUpdateState.Idle, null, null));
        var timeProvider = new FakeTimeProvider(Monday10Am);
        var options = new AutoUpdateOptions { Enabled = true, SourceCheck = new SourceCheckOptions { Interval = 30 } };
        var service = new AutoUpdateCheckerService(orchestrator.Object, options, new SourceCheckWindowEvaluator(), timeProvider, NullLogger<AutoUpdateCheckerService>.Instance);

        await service.StartAsync(CancellationToken.None);
        await WaitForAsync(() => orchestrator.Invocations.Count > 0);
        orchestrator.Invocations.Count.Should().Be(1);
        await Task.Delay(100);

        timeProvider.Advance(TimeSpan.FromMinutes(29));
        await Task.Delay(50);
        orchestrator.Invocations.Count.Should().Be(1);

        timeProvider.Advance(TimeSpan.FromMinutes(2));
        await WaitForAsync(() => orchestrator.Invocations.Count > 1);

        await service.StopAsync(CancellationToken.None);
        orchestrator.Invocations.Count.Should().Be(2);
    }
}
