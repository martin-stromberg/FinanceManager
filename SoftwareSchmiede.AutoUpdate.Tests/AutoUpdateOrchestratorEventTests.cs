using FluentAssertions;
using SoftwareSchmiede.AutoUpdate;
using SoftwareSchmiede.AutoUpdate.Tests.TestSupport;

namespace SoftwareSchmiede.AutoUpdate.Tests;

public sealed class AutoUpdateOrchestratorEventTests
{
    [Fact]
    public async Task Run_WhenBeforeCheckSourceCanceled_StopsImmediately()
    {
        using var ctx = new AutoUpdateTestContext();
        ctx.Events.BeforeCheckSource += (_, args) => args.Cancel = true;

        var result = await ctx.Orchestrator.RunUpdateAsync();

        result.Outcome.Should().Be(AutoUpdateOutcome.Canceled);
        ctx.Source.CheckCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Run_WhenBeforeDownloadCanceled_DoesNotDownload()
    {
        using var ctx = new AutoUpdateTestContext();
        ctx.InstalledVersionProvider.Version = "1.0.0";
        ctx.CreateAvailablePackage("2.0.0");
        ctx.Events.BeforeDownload += (_, args) => args.Cancel = true;

        var result = await ctx.Orchestrator.RunUpdateAsync();

        result.Outcome.Should().Be(AutoUpdateOutcome.Canceled);
        ctx.Source.DownloadCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Run_WhenBeforeInstallCanceled_DoesNotInstall()
    {
        using var ctx = new AutoUpdateTestContext();
        ctx.InstalledVersionProvider.Version = "1.0.0";
        ctx.CreateAvailablePackage("2.0.0");
        ctx.Events.BeforeInstall += (_, args) => args.Cancel = true;

        var result = await ctx.Orchestrator.RunUpdateAsync();

        result.Outcome.Should().Be(AutoUpdateOutcome.Canceled);
        ctx.ProcessRunner.StartScriptCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Run_WhenBeforeStartUpdateScriptCanceled_ReleasesLock()
    {
        using var ctx = new AutoUpdateTestContext();
        ctx.InstalledVersionProvider.Version = "1.0.0";
        ctx.CreateAvailablePackage("2.0.0");
        ctx.Events.BeforeStartUpdateScript += (_, args) => args.Cancel = true;

        var result = await ctx.Orchestrator.RunUpdateAsync();

        result.Outcome.Should().Be(AutoUpdateOutcome.Canceled);
        (await ctx.PackageStore.GetLockCreatedAtAsync()).Should().BeNull();
    }

    [Fact]
    public async Task Run_RaisesEventsInDocumentedOrder()
    {
        using var ctx = new AutoUpdateTestContext();
        ctx.InstalledVersionProvider.Version = "1.0.0";
        ctx.CreateAvailablePackage("2.0.0");
        var order = new List<string>();
        ctx.Events.BeforeCheckSource += (_, _) => order.Add("BeforeCheckSource");
        ctx.Events.BeforeDownload += (_, _) => order.Add("BeforeDownload");
        ctx.Events.BeforeInstall += (_, _) => order.Add("BeforeInstall");
        ctx.Events.BeforeStartUpdateScript += (_, _) => order.Add("BeforeStartUpdateScript");
        ctx.Events.AfterStartUpdateScript += (_, _) => order.Add("AfterStartUpdateScript");

        await ctx.Orchestrator.RunUpdateAsync();

        order.Should().Equal("BeforeCheckSource", "BeforeDownload", "BeforeInstall", "BeforeStartUpdateScript", "AfterStartUpdateScript");
    }
}
