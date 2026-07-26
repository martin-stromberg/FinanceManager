using FinanceManager.Shared.Dtos.Update;
using FinanceManager.Web.Services.Updates;
using FluentAssertions;

namespace FinanceManager.Tests.Updates;

public sealed class UpdateServiceResolverTests
{
    [Fact]
    public void Resolve_UsesConfiguredServiceOverride()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var resolver = new UpdateServiceResolver(new TestWebHostEnvironment(root.FullName), new TestProbe());
            var settings = Settings(serviceName: "FinanceManagerService");

            var target = resolver.Resolve(settings);

            target.ServiceName.Should().Be("FinanceManagerService");
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Fact]
    public void Resolve_RejectsAmbiguousBestEffortDetection()
    {
        if (!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux())
        {
            return;
        }

        var root = Directory.CreateTempSubdirectory();
        try
        {
            var probe = OperatingSystem.IsWindows()
                ? new TestProbe(windows: new[] { "one", "two" })
                : new TestProbe(linux: new[] { "one.service", "two.service" });
            var resolver = new UpdateServiceResolver(new TestWebHostEnvironment(root.FullName), probe);

            var act = () => resolver.Resolve(Settings());

            act.Should().Throw<InvalidOperationException>().WithMessage("*Multiple*");
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Fact]
    public void Resolve_WindowsExecutableOverrideMustBeInsideApplicationRoot()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var root = Directory.CreateTempSubdirectory();
        var outside = Directory.CreateTempSubdirectory();
        try
        {
            var exe = Path.Combine(outside.FullName, "FinanceManager.exe");
            File.WriteAllText(exe, string.Empty);
            var resolver = new UpdateServiceResolver(new TestWebHostEnvironment(root.FullName), new TestProbe());

            var act = () => resolver.Resolve(Settings(executablePath: exe));

            act.Should().Throw<InvalidOperationException>().WithMessage("*current application directory*");
        }
        finally
        {
            root.Delete(recursive: true);
            outside.Delete(recursive: true);
        }
    }

    private static UpdateSettingsDto Settings(string? serviceName = null, string? executablePath = null)
        => new(false, 60, "martin-stromberg", "FinanceManager", "update.json", null, serviceName, executablePath, "updates", 120);

    private sealed class TestProbe : IUpdateServiceProbe
    {
        private readonly IReadOnlyList<string> _windows;
        private readonly IReadOnlyList<string> _linux;

        public TestProbe(IReadOnlyList<string>? windows = null, IReadOnlyList<string>? linux = null)
        {
            _windows = windows ?? Array.Empty<string>();
            _linux = linux ?? Array.Empty<string>();
        }

        public IReadOnlyList<string> FindWindowsServicesForCurrentProcess() => _windows;
        public IReadOnlyList<string> FindLinuxServicesForCurrentProcess() => _linux;
    }

}
