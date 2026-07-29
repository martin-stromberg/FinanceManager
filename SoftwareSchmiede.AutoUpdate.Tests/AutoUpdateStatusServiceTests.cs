using FluentAssertions;
using SoftwareSchmiede.AutoUpdate;
using SoftwareSchmiede.AutoUpdate.Tests.TestSupport;

namespace SoftwareSchmiede.AutoUpdate.Tests;

public sealed class AutoUpdateStatusServiceTests
{
    [Fact]
    public void GetSnapshot_ReturnsConsistentState()
    {
        using var ctx = new AutoUpdateTestContext();

        var snapshot = ctx.StatusService.GetSnapshot();

        snapshot.State.Should().Be(AutoUpdateState.Idle);
        snapshot.IsLocked.Should().BeFalse();
    }

    [Fact]
    public async Task Update_FromParallelThreads_KeepsLastWriteVisible()
    {
        using var ctx = new AutoUpdateTestContext();
        await ctx.StatusService.EnsureLoadedAsync();

        var writtenValues = Enumerable.Range(0, 25).Select(i => $"error-{i}").ToArray();
        await Task.WhenAll(writtenValues.Select(value => ctx.StatusService.UpdateAsync(s => s with { LastError = value })));

        var snapshot = ctx.StatusService.GetSnapshot();
        snapshot.LastError.Should().BeOneOf(writtenValues);
        var persisted = await ctx.StateStore.ReadAsync();
        persisted.Should().BeEquivalentTo(snapshot, "the persisted file must always reflect the last in-memory snapshot, even under concurrent writes");
    }

    [Fact]
    public async Task Snapshot_IsPersistedAndReloaded()
    {
        using var ctx = new AutoUpdateTestContext();
        await ctx.StatusService.EnsureLoadedAsync();
        await ctx.StatusService.UpdateAsync(s => s with { State = AutoUpdateState.Failed, LastError = "boom" });

        var reloaded = new AutoUpdateStatusService(ctx.StateStore, ctx.InstalledVersionProvider);
        await reloaded.EnsureLoadedAsync();

        reloaded.GetSnapshot().State.Should().Be(AutoUpdateState.Failed);
        reloaded.GetSnapshot().LastError.Should().Be("boom");
    }

    [Fact]
    public async Task Load_WithUnreadableStateFile_FallsBackToIdle()
    {
        using var ctx = new AutoUpdateTestContext();
        await ctx.PackageStore.EnsureAsync();
        var statusPath = Path.Combine(ctx.PackageStore.RootDirectory, "status.json");
        await File.WriteAllTextAsync(statusPath, "{ this is not valid json");

        var statusService = new AutoUpdateStatusService(ctx.StateStore, ctx.InstalledVersionProvider);
        await statusService.EnsureLoadedAsync();

        statusService.GetSnapshot().State.Should().Be(AutoUpdateState.Idle);
    }
}
