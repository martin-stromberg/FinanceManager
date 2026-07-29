using FluentAssertions;
using SoftwareSchmiede.AutoUpdate;
using SoftwareSchmiede.AutoUpdate.Tests.TestSupport;

namespace SoftwareSchmiede.AutoUpdate.Tests;

public sealed class FileSystemAutoUpdateStateStoreTests
{
    [Fact]
    public async Task Read_Write_RoundTrips()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            var packageStore = new FileSystemAutoUpdatePackageStore(new TestAutoUpdateEnvironment(dir.FullName), new AutoUpdateOptions { DownloadPath = "updates" }, TimeProvider.System);
            var stateStore = new FileSystemAutoUpdateStateStore(packageStore);
            var snapshot = new AutoUpdateStatusSnapshot(AutoUpdateState.Failed, "1.0.0", "2.0.0", DateTimeOffset.UtcNow, null, null, null, "boom", false, null);

            await stateStore.WriteAsync(snapshot);
            var reloaded = await stateStore.ReadAsync();

            reloaded.Should().Be(snapshot);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }
}
