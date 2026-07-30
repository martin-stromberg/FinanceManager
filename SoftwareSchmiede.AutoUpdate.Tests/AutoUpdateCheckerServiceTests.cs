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
    public async Task Execute_RunsUpdateWorkflow_WithinWindow()
    {
        var orchestrator = new Mock<IAutoUpdateOrchestrator>();
        orchestrator.Setup(o => o.RunUpdateAsync(It.IsAny<CancellationToken>()))
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

        orchestrator.Verify(o => o.RunUpdateAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task Execute_DoesNotRun_OutsideWindow()
    {
        var orchestrator = new Mock<IAutoUpdateOrchestrator>();
        orchestrator.Setup(o => o.RunUpdateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AutoUpdateResult(AutoUpdateOutcome.NoUpdate, AutoUpdateState.Idle, null, null));
        var timeProvider = new FakeTimeProvider(Monday10Am);
        var options = new AutoUpdateOptions
        {
            Enabled = true,
            SourceCheck = new SourceCheckOptions
            {
                Interval = 60,
                TimeRanges = new List<SourceCheckTimeRange> { new() { DayOfWeek = DayOfWeek.Tuesday, StartTime = TimeOnly.MinValue, EndTime = TimeOnly.MaxValue } }
            }
        };
        var service = new AutoUpdateCheckerService(orchestrator.Object, options, new SourceCheckWindowEvaluator(), timeProvider, NullLogger<AutoUpdateCheckerService>.Instance);

        await service.StartAsync(CancellationToken.None);
        await AssertStaysFalseAsync(() => orchestrator.Invocations.Count > 0);
        await service.StopAsync(CancellationToken.None);

        orchestrator.Verify(o => o.RunUpdateAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Execute_OnlyCallsRunUpdateAsync_NeverIndividualSteps()
    {
        var orchestrator = new Mock<IAutoUpdateOrchestrator>();
        orchestrator.Setup(o => o.RunUpdateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AutoUpdateResult(AutoUpdateOutcome.Success, AutoUpdateState.Installing, null, null));
        var timeProvider = new FakeTimeProvider(Monday10Am);
        var options = new AutoUpdateOptions { Enabled = true, SourceCheck = new SourceCheckOptions { Interval = 60 } };
        var service = new AutoUpdateCheckerService(orchestrator.Object, options, new SourceCheckWindowEvaluator(), timeProvider, NullLogger<AutoUpdateCheckerService>.Instance);

        await service.StartAsync(CancellationToken.None);
        await WaitForAsync(() => orchestrator.Invocations.Count > 0);
        await service.StopAsync(CancellationToken.None);

        orchestrator.Verify(o => o.CheckForUpdateAsync(It.IsAny<CancellationToken>()), Times.Never);
        orchestrator.Verify(o => o.DownloadAsync(It.IsAny<CancellationToken>()), Times.Never);
        orchestrator.Verify(o => o.InstallAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Execute_WhenRunThrows_ContinuesLoop()
    {
        var orchestrator = new Mock<IAutoUpdateOrchestrator>();
        orchestrator.Setup(o => o.RunUpdateAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("boom"));
        var timeProvider = new FakeTimeProvider(Monday10Am);
        var options = new AutoUpdateOptions { Enabled = true, SourceCheck = new SourceCheckOptions { Interval = 1 } };
        var service = new AutoUpdateCheckerService(orchestrator.Object, options, new SourceCheckWindowEvaluator(), timeProvider, NullLogger<AutoUpdateCheckerService>.Instance);

        await service.StartAsync(CancellationToken.None);
        await WaitForAsync(() => orchestrator.Invocations.Count > 0);
        // Give the loop real time to re-enter its Task.Delay wait state (and register its timer with the
        // FakeTimeProvider) before advancing the clock; the WaitForAsync polls below tolerate the remaining slack.
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
        orchestrator.Setup(o => o.RunUpdateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AutoUpdateResult(AutoUpdateOutcome.NoUpdate, AutoUpdateState.Idle, null, null));
        var timeProvider = new FakeTimeProvider(Monday10Am);
        var options = new AutoUpdateOptions { Enabled = true, SourceCheck = new SourceCheckOptions { Interval = 30 } };
        var service = new AutoUpdateCheckerService(orchestrator.Object, options, new SourceCheckWindowEvaluator(), timeProvider, NullLogger<AutoUpdateCheckerService>.Instance);

        await service.StartAsync(CancellationToken.None);
        await WaitForAsync(() => orchestrator.Invocations.Count > 0);
        orchestrator.Invocations.Count.Should().Be(1);
        // Give the loop real time to re-enter its Task.Delay wait state (and register its timer with the
        // FakeTimeProvider) before advancing the clock.
        await Task.Delay(100);

        timeProvider.Advance(TimeSpan.FromMinutes(29));
        await AssertStaysFalseAsync(() => orchestrator.Invocations.Count > 1);
        orchestrator.Invocations.Count.Should().Be(1);

        timeProvider.Advance(TimeSpan.FromMinutes(2));
        await WaitForAsync(() => orchestrator.Invocations.Count > 1);

        await service.StopAsync(CancellationToken.None);
        orchestrator.Invocations.Count.Should().Be(2);
    }
}
