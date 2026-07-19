using FinanceManager.Shared.Dtos.Update;
using FinanceManager.Web.Services.Updates;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;

namespace FinanceManager.Tests.Updates;

public sealed class UpdateScriptGeneratorTests
{
    [Fact]
    public async Task GenerateAsync_CreatesScriptForCurrentPlatform()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var env = new TestEnvironment(root.FullName);
            var store = new UpdateFileStore(env, Options.Create(new UpdateOptions { WorkingDirectory = Path.Combine(root.FullName, "updates") }));
            var generator = new UpdateScriptGenerator(env, store);
            var settings = new UpdateSettingsDto(false, 60, "martin-stromberg", "FinanceManager", "update.json", null, "FinanceManager", "financemanager", null, "updates", 120);
            var asset = new UpdateAssetDto("current", "current", "release.zip", "https://example.test/release.zip", "abc", 1);

            var target = OperatingSystem.IsWindows()
                ? new UpdateInstallationTarget("windows", "FinanceManager", null)
                : new UpdateInstallationTarget("linux", "financemanager", null);

            var scriptPath = await generator.GenerateAsync(asset, Path.Combine(root.FullName, "release.zip"), settings, target);

            File.Exists(scriptPath).Should().BeTrue();
            var content = await File.ReadAllTextAsync(scriptPath);
            content.Should().Contain("release.zip");
            content.Should().Match(c => c.Contains("FinanceManager", StringComparison.Ordinal) || c.Contains("financemanager", StringComparison.Ordinal));
        }
        finally
        {
            root.Delete(recursive: true);
        }
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
