using FinanceManager.Web.Services.Updates;
using FluentAssertions;

namespace FinanceManager.Tests.Updates;

public sealed class InstalledReleaseMetadataProviderTests
{
    [Fact]
    public async Task InstalledReleaseMetadataProvider_WhenMetadataFileExists_ReadsInstalledRelease()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(root.FullName, "release-metadata.json"),
                """
                {
                  "version": "2.3.4",
                  "publishedAt": "2026-07-19T10:15:00+00:00",
                  "commitSha": "abc123",
                  "repository": "FinanceManager",
                  "runtimeIdentifier": "win-x64"
                }
                """);
            var provider = new InstalledReleaseMetadataProvider(new TestWebHostEnvironment(root.FullName));

            var metadata = await provider.GetAsync();

            metadata.Version.Should().Be("2.3.4");
            metadata.CommitSha.Should().Be("abc123");
            metadata.Repository.Should().Be("FinanceManager");
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }
}
