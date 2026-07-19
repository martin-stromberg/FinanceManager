using FinanceManager.Shared.Dtos.Update;
using FinanceManager.Web.Services.Updates;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;

namespace FinanceManager.Tests.Updates;

public sealed class UpdateServiceResolverTests
{
    [Fact]
    public void Resolve_UsesConfiguredServiceOverride()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var resolver = new UpdateServiceResolver(new TestEnvironment(root.FullName), new TestProbe());
            var settings = Settings(
                windowsServiceName: "FinanceManager",
                linuxServiceName: "financemanager.service");

            var target = resolver.Resolve(settings);

            target.ServiceName.Should().Be(OperatingSystem.IsWindows() ? "FinanceManager" : "financemanager.service");
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
            var resolver = new UpdateServiceResolver(new TestEnvironment(root.FullName), probe);

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
            var resolver = new UpdateServiceResolver(new TestEnvironment(root.FullName), new TestProbe());

            var act = () => resolver.Resolve(Settings(executablePath: exe));

            act.Should().Throw<InvalidOperationException>().WithMessage("*current application directory*");
        }
        finally
        {
            root.Delete(recursive: true);
            outside.Delete(recursive: true);
        }
    }

    private static UpdateSettingsDto Settings(string? windowsServiceName = null, string? linuxServiceName = null, string? executablePath = null)
        => new(false, 60, "martin-stromberg", "FinanceManager", "update.json", null, windowsServiceName, linuxServiceName, executablePath, "updates", 120);

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

    private sealed class TestEnvironment : IWebHostEnvironment
    {
        public TestEnvironment(string root)
        {
            ContentRootPath = root;
            WebRootPath = root;
        }

        public string ApplicationName { get; set; } = "Tests";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; }
        public string EnvironmentName { get; set; } = "Development";
        public string WebRootPath { get; set; }
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
