using SoftwareSchmiede.AutoUpdate;

namespace SoftwareSchmiede.AutoUpdate.Tests.TestSupport;

/// <summary>
/// Controllable <see cref="IAutoUpdateSource"/> test double: the available version, release payload and
/// failure behavior can all be set by the test, and calls are counted.
/// </summary>
public sealed class FakeAutoUpdateSource : IAutoUpdateSource
{
    public string? AvailableVersion { get; set; }

    public AutoUpdatePackageDescriptor? Package { get; set; }

    public string? ReleaseNotes { get; set; }

    public DateTimeOffset? PublishedAt { get; set; }

    public bool ThrowOnCheck { get; set; }

    public bool ThrowOnDownload { get; set; }

    public byte[] PackageContent { get; set; } = "content"u8.ToArray();

    public int CheckCallCount { get; private set; }

    public int DownloadCallCount { get; private set; }

    public Task<AutoUpdateCheckResult> CheckAsync(CancellationToken ct = default)
    {
        CheckCallCount++;
        if (ThrowOnCheck)
        {
            throw new InvalidOperationException("Simulated source check failure.");
        }

        return Task.FromResult(new AutoUpdateCheckResult(AvailableVersion, Package, ReleaseNotes, PublishedAt));
    }

    public async Task DownloadAsync(AutoUpdatePackageDescriptor package, string targetPath, long maxBytes, CancellationToken ct = default)
    {
        DownloadCallCount++;
        if (ThrowOnDownload)
        {
            throw new InvalidOperationException("Simulated source download failure.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        await File.WriteAllBytesAsync(targetPath, PackageContent, ct);
    }
}
