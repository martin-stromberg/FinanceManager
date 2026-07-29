using FluentAssertions;
using SoftwareSchmiede.AutoUpdate;
using SoftwareSchmiede.AutoUpdate.Tests.TestSupport;

namespace SoftwareSchmiede.AutoUpdate.Tests;

public sealed class AutoUpdateOrchestratorDownloadTests
{
    [Fact]
    public async Task Run_WhenAutomaticDownloadDisabled_StopsAfterCheck()
    {
        using var ctx = new AutoUpdateTestContext();
        ctx.Options.EnableAutomaticDownload = false;
        ctx.InstalledVersionProvider.Version = "1.0.0";
        ctx.CreateAvailablePackage("2.0.0");

        var result = await ctx.Orchestrator.RunUpdateAsync();

        result.Outcome.Should().Be(AutoUpdateOutcome.Skipped);
        ctx.Source.DownloadCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Run_DownloadsAndValidatesPackage()
    {
        using var ctx = new AutoUpdateTestContext();
        ctx.Options.EnableAutomaticInstallation = false;
        ctx.InstalledVersionProvider.Version = "1.0.0";
        ctx.CreateAvailablePackage("2.0.0");

        var result = await ctx.Orchestrator.RunUpdateAsync();

        result.Outcome.Should().Be(AutoUpdateOutcome.Skipped);
        var snapshot = ctx.StatusService.GetSnapshot();
        snapshot.State.Should().Be(AutoUpdateState.ReadyToInstall);
        snapshot.LastDownloadResult.Should().NotBeNull();
        snapshot.LastDownloadResult!.ChecksumValid.Should().BeTrue();
    }

    [Fact]
    public async Task Run_WhenChecksumMismatch_Fails()
    {
        using var ctx = new AutoUpdateTestContext();
        ctx.InstalledVersionProvider.Version = "1.0.0";
        var package = ctx.CreateAvailablePackage("2.0.0");
        ctx.Source.Package = package with { Sha256 = new string('0', 64) };

        var result = await ctx.Orchestrator.RunUpdateAsync();

        result.Outcome.Should().Be(AutoUpdateOutcome.Failed);
        ctx.StatusService.GetSnapshot().State.Should().Be(AutoUpdateState.Failed);
    }

    [Fact]
    public async Task Run_WhenPackageExceedsMaxBytes_Fails()
    {
        using var ctx = new AutoUpdateTestContext();
        ctx.Options.MaxAssetBytes = 5;
        ctx.InstalledVersionProvider.Version = "1.0.0";
        ctx.CreateAvailablePackage("2.0.0");

        var result = await ctx.Orchestrator.RunUpdateAsync();

        result.Outcome.Should().Be(AutoUpdateOutcome.Failed);
        ctx.StatusService.GetSnapshot().State.Should().Be(AutoUpdateState.Failed);
    }
}
