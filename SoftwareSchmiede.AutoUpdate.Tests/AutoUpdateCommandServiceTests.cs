using FluentAssertions;
using SoftwareSchmiede.AutoUpdate;
using SoftwareSchmiede.AutoUpdate.Tests.TestSupport;

namespace SoftwareSchmiede.AutoUpdate.Tests;

public sealed class AutoUpdateCommandServiceTests
{
    [Fact]
    public async Task Check_DelegatesToOrchestrator()
    {
        using var ctx = new AutoUpdateTestContext();
        var commandService = new AutoUpdateCommandService(ctx.Orchestrator);

        var result = await commandService.CheckAsync();

        result.Should().NotBeNull();
        ctx.Source.CheckCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Download_DelegatesToOrchestrator()
    {
        using var ctx = new AutoUpdateTestContext();
        ctx.InstalledVersionProvider.Version = "1.0.0";
        ctx.CreateAvailablePackage("2.0.0");
        var commandService = new AutoUpdateCommandService(ctx.Orchestrator);
        await commandService.CheckAsync();

        var result = await commandService.DownloadAsync();

        result.Outcome.Should().Be(AutoUpdateOutcome.Success);
        ctx.StatusService.GetSnapshot().State.Should().Be(AutoUpdateState.ReadyToInstall);
    }

    [Fact]
    public async Task Install_DelegatesToOrchestrator()
    {
        using var ctx = new AutoUpdateTestContext();
        var result = await new AutoUpdateCommandService(ctx.Orchestrator).InstallAsync(confirmDowntime: false);

        result.Outcome.Should().Be(AutoUpdateOutcome.Failed);
        result.Error.Should().BeOfType<ArgumentException>();
    }

    [Fact]
    public async Task Commands_UpdateStatusService()
    {
        using var ctx = new AutoUpdateTestContext();
        ctx.InstalledVersionProvider.Version = "1.0.0";
        ctx.CreateAvailablePackage("2.0.0");
        var commandService = new AutoUpdateCommandService(ctx.Orchestrator);

        await commandService.CheckAsync();

        ctx.StatusService.GetSnapshot().State.Should().Be(AutoUpdateState.UpdateAvailable);
    }

    [Fact]
    public async Task Commands_ParallelCalls_AreSerialized()
    {
        using var ctx = new AutoUpdateTestContext();
        var commandService = new AutoUpdateCommandService(ctx.Orchestrator);

        var results = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => commandService.CheckAsync()));

        results.Should().OnlyContain(result => result.Outcome == AutoUpdateOutcome.NoUpdate || result.Outcome == AutoUpdateOutcome.Success);
        ctx.Source.CheckCallCount.Should().Be(10);
    }
}
