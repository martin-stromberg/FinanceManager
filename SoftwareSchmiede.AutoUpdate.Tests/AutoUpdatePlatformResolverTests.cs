using System.Runtime.InteropServices;
using FluentAssertions;
using SoftwareSchmiede.AutoUpdate;

namespace SoftwareSchmiede.AutoUpdate.Tests;

public sealed class AutoUpdatePlatformResolverTests
{
    [Fact]
    public void SelectPackage_MatchesRuntimeIdentifier()
    {
        var resolver = new AutoUpdatePlatformResolver(p => p == OSPlatform.Windows, "win-x64");
        var release = new AutoUpdateReleaseInfo("1.0.0", null, null, new[]
        {
            new AutoUpdatePackageDescriptor("1.0.0", "windows", "win-x64", "win.zip", new Uri("https://example.test/win.zip"), new string('a', 64), 10),
            new AutoUpdatePackageDescriptor("1.0.0", "linux", "linux-x64", "linux.zip", new Uri("https://example.test/linux.zip"), new string('a', 64), 10)
        });

        var selected = resolver.SelectPackage(release);

        selected.Should().NotBeNull();
        selected!.FileName.Should().Be("win.zip");
    }
}
