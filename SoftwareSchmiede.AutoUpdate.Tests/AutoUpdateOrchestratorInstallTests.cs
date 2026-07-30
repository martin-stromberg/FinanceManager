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

    [Fact]
    public async Task Install_WhenCanceledAndLockDeletionFails_ReportsError()
    {
        using var ctx = new AutoUpdateTestContext();
        ctx.InstalledVersionProvider.Version = "1.0.0";
        ctx.CreateAvailablePackage("2.0.0");
        await ctx.Orchestrator.CheckForUpdateAsync();
        await ctx.Orchestrator.DownloadAsync();

        var failingStore = new FailingDeleteLockPackageStore(ctx.PackageStore);
        var orchestrator = new AutoUpdateOrchestrator(
            ctx.Options, ctx.Events, ctx.StatusService, failingStore, ctx.Validator,
            ctx.InstalledVersionProvider, ctx.Installer, ctx.HostTerminator, ctx.TimeProvider);
        AutoUpdateErrorEventArgs? captured = null;
        ctx.Events.ErrorOccurred += (_, args) => captured = args;
        ctx.Events.BeforeStartUpdateScript += (_, args) => args.Cancel = true;

        var result = await orchestrator.InstallAsync(confirmDowntime: true);

        result.Outcome.Should().Be(AutoUpdateOutcome.Canceled);
        captured.Should().NotBeNull();
        captured!.Error.Should().BeOfType<IOException>();
        captured.Phase.Should().Be("Install");
    }

    private sealed class FailingDeleteLockPackageStore : IAutoUpdatePackageStore
    {
        private readonly IAutoUpdatePackageStore _inner;

        public FailingDeleteLockPackageStore(IAutoUpdatePackageStore inner) => _inner = inner;

        public string RootDirectory => _inner.RootDirectory;
        public string PendingDirectory => _inner.PendingDirectory;
        public string StagingDirectory => _inner.StagingDirectory;
        public string LockPath => _inner.LockPath;
        public string LogPath => _inner.LogPath;
        public string ScriptPath(string extension) => _inner.ScriptPath(extension);
        public string PendingAssetPath(string fileName) => _inner.PendingAssetPath(fileName);
        public Task EnsureAsync(CancellationToken ct = default) => _inner.EnsureAsync(ct);
        public Task<DateTimeOffset?> GetLockCreatedAtAsync(CancellationToken ct = default) => _inner.GetLockCreatedAtAsync(ct);
        public Task<bool> TryCreateLockAsync(CancellationToken ct = default) => _inner.TryCreateLockAsync(ct);
        public Task<bool> DeleteLockAsync(CancellationToken ct = default) => Task.FromResult(false);
        public bool IsLockStale(DateTimeOffset lockCreatedAt) => _inner.IsLockStale(lockCreatedAt);
    }
}
