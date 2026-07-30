using FluentAssertions;
using SoftwareSchmiede.AutoUpdate;
using SoftwareSchmiede.AutoUpdate.Tests.TestSupport;

namespace SoftwareSchmiede.AutoUpdate.Tests;

public sealed class AutoUpdateScriptGeneratorTests
{
    [Fact]
    public async Task Generate_OnWindows_WritesPowerShellScript()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("Windows-only test.");
        }

        var dir = Directory.CreateTempSubdirectory();
        try
        {
            var generator = CreateGenerator(dir.FullName);
            var target = new AutoUpdateInstallationTarget("windows", "TestService", null);
            var package = BuildPackage(dir.FullName);

            var scriptPath = await generator.GenerateAsync(package, Path.Combine(dir.FullName, "app.zip"), target);

            scriptPath.Should().EndWith(".ps1");
            File.Exists(scriptPath).Should().BeTrue();
            (await File.ReadAllTextAsync(scriptPath)).Should().Contain("Stop-Service");
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task Generate_OnLinux_WritesShellScriptWithUnixLineEndings()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Skip("Linux-only test.");
        }

        var dir = Directory.CreateTempSubdirectory();
        try
        {
            var generator = CreateGenerator(dir.FullName);
            var target = new AutoUpdateInstallationTarget("linux", "test.service", null);
            var package = BuildPackage(dir.FullName);

            var scriptPath = await generator.GenerateAsync(package, Path.Combine(dir.FullName, "app.zip"), target);

            scriptPath.Should().EndWith(".sh");
            var content = await File.ReadAllTextAsync(scriptPath);
            content.Should().NotContain("\r\n");
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task Generate_WithoutTarget_Throws()
    {
        if (!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux())
        {
            Assert.Skip("Windows- or Linux-only test.");
        }

        var dir = Directory.CreateTempSubdirectory();
        try
        {
            var generator = CreateGenerator(dir.FullName);
            var target = OperatingSystem.IsWindows()
                ? new AutoUpdateInstallationTarget("windows", null, null)
                : new AutoUpdateInstallationTarget("linux", null, null);
            var package = BuildPackage(dir.FullName);

            var act = () => generator.GenerateAsync(package, Path.Combine(dir.FullName, "app.zip"), target);

            await act.Should().ThrowAsync<InvalidOperationException>();
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    private static AutoUpdateScriptGenerator CreateGenerator(string root)
    {
        var environment = new TestAutoUpdateEnvironment(root);
        var options = new AutoUpdateOptions { DownloadPath = "updates" };
        var packageStore = new FileSystemAutoUpdatePackageStore(environment, options, TimeProvider.System);
        return new AutoUpdateScriptGenerator(environment, packageStore);
    }

    private static AutoUpdatePackageDescriptor BuildPackage(string root)
        => new("1.0.0", "windows", "win-x64", "app.zip", new Uri(Path.Combine(root, "app.zip")), new string('a', 64), 4);
}
