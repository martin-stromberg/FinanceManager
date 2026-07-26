using FinanceManager.Shared.Dtos.Update;
using FinanceManager.Web.Services.Updates;
using FluentAssertions;
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
            var env = new TestWebHostEnvironment(root.FullName);
            var store = new UpdateFileStore(env, Options.Create(new UpdateOptions { WorkingDirectory = Path.Combine(root.FullName, "updates") }));
            var generator = new UpdateScriptGenerator(env, store);
            var settings = new UpdateSettingsDto(false, 60, "martin-stromberg", "FinanceManager", "update.json", null, "FinanceManager", null, "updates", 120);
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

}
