using System.Runtime.InteropServices;
using FinanceManager.Shared.Dtos.Update;
using FinanceManager.Web.Services.Updates;
using FluentAssertions;

namespace FinanceManager.Tests.Updates;

public sealed class UpdatePlatformResolverTests
{
    [Fact]
    public void UpdatePlatformResolver_SelectAsset_WhenWindows_SelectsWindowsRuntimeAsset()
    {
        var resolver = new UpdatePlatformResolver(platform => platform == OSPlatform.Windows, "ignored");
        var manifest = Manifest();

        var asset = resolver.SelectAsset(manifest);

        resolver.CurrentPlatform.Should().Be("windows");
        resolver.CurrentRuntimeIdentifier.Should().Be("win-x64");
        asset!.AssetName.Should().Be("windows.zip");
    }

    [Fact]
    public void UpdatePlatformResolver_SelectAsset_WhenLinux_SelectsLinuxRuntimeAsset()
    {
        var resolver = new UpdatePlatformResolver(platform => platform == OSPlatform.Linux, "ignored");
        var manifest = Manifest();

        var asset = resolver.SelectAsset(manifest);

        resolver.CurrentPlatform.Should().Be("linux");
        resolver.CurrentRuntimeIdentifier.Should().Be("linux-x64");
        asset!.AssetName.Should().Be("linux.zip");
    }

    private static UpdateMetadataDto Manifest()
        => new(
            "1.0.0",
            null,
            null,
            "owner",
            "repo",
            new[]
            {
                new UpdateAssetDto("linux", "linux-x64", "linux.zip", "https://example.test/linux.zip", "hash", 1),
                new UpdateAssetDto("windows", "win-x64", "windows.zip", "https://example.test/windows.zip", "hash", 1)
            });
}
