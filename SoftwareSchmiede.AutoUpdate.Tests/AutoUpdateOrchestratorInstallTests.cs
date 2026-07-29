using FluentAssertions;
using SoftwareSchmiede.AutoUpdate;
using SoftwareSchmiede.AutoUpdate.Tests.TestSupport;

namespace SoftwareSchmiede.AutoUpdate.Tests;

public sealed class AutoUpdateOrchestratorInstallTests
{
    [Fact]
    public async Task Install_WhenAutomaticInstallationDisabled_StopsAfterDownload()
    {
        using var ctx = new AutoUpdateTestContext();
        ctx.Options.EnableAutomaticInstallation = false;
        ctx.InstalledVersionProvider.Version = "1.0.0";
        ctx.CreateAvailablePackage("2.0.0");

        var result = await ctx.Orchestrator.RunUpdateAsync();

        result.Outcome.Should().Be(AutoUpdateOutcome.Skipped);
        ctx.ProcessRunner.StartScriptCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Install_GeneratesScriptAndStartsIt()
    {
        using var ctx = new AutoUpdateTestContext();
        ctx.InstalledVersionProvider.Version = "1.0.0";
        ctx.CreateAvailablePackage("2.0.0");

        var result = await ctx.Orchestrator.RunUpdateAsync();

        result.Outcome.Should().Be(AutoUpdateOutcome.Success);
        result.State.Should().Be(AutoUpdateState.Installing);
        ctx.ProcessRunner.StartScriptCallCount.Should().Be(1);
        ctx.ProcessRunner.LastScriptPath.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Install_WhenLockActive_Fails()
    {
        using var ctx = new AutoUpdateTestContext();
        ctx.InstalledVersionProvider.Version = "1.0.0";
        ctx.CreateAvailablePackage("2.0.0");
        await ctx.Orchestrator.CheckForUpdateAsync();
        await ctx.Orchestrator.DownloadAsync();
        await ctx.PackageStore.TryCreateLockAsync();

        var result = await ctx.Orchestrator.InstallAsync(confirmDowntime: true);

        result.Outcome.Should().Be(AutoUpdateOutcome.Failed);
        result.Error.Should().BeOfType<IOException>();
    }

    [Fact]
    public async Task Install_WithoutConfirmDowntime_Fails()
    {
        using var ctx = new AutoUpdateTestContext();

        var result = await ctx.Orchestrator.InstallAsync(confirmDowntime: false);

        result.Outcome.Should().Be(AutoUpdateOutcome.Failed);
        result.Error.Should().BeOfType<ArgumentException>();
    }

    [Fact]
    public async Task Install_WhenStopHostAfterScriptStart_TerminatesHost()
    {
        using var ctx = new AutoUpdateTestContext();
        ctx.Options.StopHostAfterScriptStart = true;
        ctx.InstalledVersionProvider.Version = "1.0.0";
        ctx.CreateAvailablePackage("2.0.0");

        await ctx.Orchestrator.RunUpdateAsync();

        ctx.HostTerminator.StopApplicationCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Install_PersistsInstallingStateBeforeScriptStart()
    {
        using var ctx = new AutoUpdateTestContext();
        ctx.InstalledVersionProvider.Version = "1.0.0";
        ctx.CreateAvailablePackage("2.0.0");

        await ctx.Orchestrator.RunUpdateAsync();

        var reloaded = new AutoUpdateStatusService(ctx.StateStore, ctx.InstalledVersionProvider);
        await reloaded.EnsureLoadedAsync();

        reloaded.GetSnapshot().State.Should().Be(AutoUpdateState.Installing);
    }

    [Fact]
    public async Task Reconcile_AfterRestart_WhenVersionMatches_SetsSuccess()
    {
        using var ctx = new AutoUpdateTestContext();
        ctx.InstalledVersionProvider.Version = "1.0.0";
        ctx.CreateAvailablePackage("2.0.0");
        await ctx.Orchestrator.RunUpdateAsync();
        await ctx.PackageStore.DeleteLockAsync();
        ctx.InstalledVersionProvider.Version = "2.0.0";

        var status = await ctx.Orchestrator.GetStatusAsync();

        status.State.Should().Be(AutoUpdateState.Success);
    }

    [Fact]
    public async Task Reconcile_AfterRestart_WhenVersionDiffers_SetsFailed()
    {
        using var ctx = new AutoUpdateTestContext();
        ctx.InstalledVersionProvider.Version = "1.0.0";
        ctx.CreateAvailablePackage("2.0.0");
        await ctx.Orchestrator.RunUpdateAsync();
        await ctx.PackageStore.DeleteLockAsync();

        var status = await ctx.Orchestrator.GetStatusAsync();

        status.State.Should().Be(AutoUpdateState.Failed);
    }
}
