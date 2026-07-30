using FluentAssertions;
using SoftwareSchmiede.AutoUpdate;
using SoftwareSchmiede.AutoUpdate.Tests.TestSupport;

namespace SoftwareSchmiede.AutoUpdate.Tests;

public sealed class AutoUpdateServiceResolverTests
{
    [Fact]
    public void Resolve_WithConfiguredServiceName_ReturnsServiceTarget()
    {
        var environment = new TestAutoUpdateEnvironment(Path.GetTempPath());
        var options = new AutoUpdateOptions { ServiceName = "MyService" };
        var resolver = new AutoUpdateServiceResolver(environment, new NoOpServiceProbe(), options);

        var target = resolver.Resolve();

        target.ServiceName.Should().Be("MyService");
    }

    [Fact]
    public void Resolve_WithoutServiceOrExecutable_Throws()
    {
        var environment = new TestAutoUpdateEnvironment(Path.GetTempPath());
        var options = new AutoUpdateOptions();
        var resolver = new AutoUpdateServiceResolver(environment, new NoOpServiceProbe(), options);

        var act = () => resolver.Resolve();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Resolve_WithInvalidServiceName_Throws()
    {
        var environment = new TestAutoUpdateEnvironment(Path.GetTempPath());
        var options = new AutoUpdateOptions { ServiceName = "invalid/name" };
        var resolver = new AutoUpdateServiceResolver(environment, new NoOpServiceProbe(), options);

        var act = () => resolver.Resolve();

        act.Should().Throw<InvalidOperationException>();
    }

    private sealed class NoOpServiceProbe : IAutoUpdateServiceProbe
    {
        public IReadOnlyList<string> FindWindowsServicesForCurrentProcess() => Array.Empty<string>();

        public IReadOnlyList<string> FindLinuxServicesForCurrentProcess() => Array.Empty<string>();
    }
}
