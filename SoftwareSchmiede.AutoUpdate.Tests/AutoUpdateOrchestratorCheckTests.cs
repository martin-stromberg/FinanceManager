using FluentAssertions;
using SoftwareSchmiede.AutoUpdate;
using SoftwareSchmiede.AutoUpdate.Tests.TestSupport;

namespace SoftwareSchmiede.AutoUpdate.Tests;

public sealed class AutoUpdateOrchestratorCheckTests
{
    [Fact]
    public async Task Check_WhenNewerVersionAvailable_SetsUpdateAvailable()
    {
        using var ctx = new AutoUpdateTestContext();
        ctx.InstalledVersionProvider.Version = "1.0.0";
        ctx.CreateAvailablePackage("2.0.0");

        var result = await ctx.Orchestrator.CheckForUpdateAsync();

        result.Outcome.Should().Be(AutoUpdateOutcome.Success);
        result.State.Should().Be(AutoUpdateState.UpdateAvailable);
        var snapshot = ctx.StatusService.GetSnapshot();
        snapshot.AvailableVersion.Should().Be("2.0.0");
        snapshot.LastCheckResult.Should().NotBeNull();
    }

    [Fact]
    public async Task Check_WhenNoNewerVersion_ReturnsNoUpdate()
    {
        using var ctx = new AutoUpdateTestContext();
        ctx.InstalledVersionProvider.Version = "2.0.0";
        ctx.CreateAvailablePackage("2.0.0");

        var result = await ctx.Orchestrator.CheckForUpdateAsync();

        result.Outcome.Should().Be(AutoUpdateOutcome.NoUpdate);
        result.State.Should().Be(AutoUpdateState.Idle);
        ctx.StatusService.GetSnapshot().State.Should().NotBe(AutoUpdateState.UpdateAvailable);
    }

    [Fact]
    public async Task Check_WhenDisabled_ReturnsSkippedAndDisabledState()
    {
        using var ctx = new AutoUpdateTestContext();
        ctx.Options.Enabled = false;

        var result = await ctx.Orchestrator.CheckForUpdateAsync();

        result.Outcome.Should().Be(AutoUpdateOutcome.Skipped);
        result.State.Should().Be(AutoUpdateState.Disabled);
    }

    [Fact]
    public async Task Check_WhenSourceThrows_ReportsErrorAndFails()
    {
        using var ctx = new AutoUpdateTestContext();
        ctx.Source.ThrowOnCheck = true;
        AutoUpdateErrorEventArgs? captured = null;
        ctx.Events.ErrorOccured += (_, args) => captured = args;

        var result = await ctx.Orchestrator.CheckForUpdateAsync();

        result.Outcome.Should().Be(AutoUpdateOutcome.Failed);
        result.State.Should().Be(AutoUpdateState.Failed);
        captured.Should().NotBeNull();
        captured!.Phase.Should().Be("Check");
    }
}
