using FinanceManager.Web.Services.Updates;
using FluentAssertions;
using SoftwareSchmiede.AutoUpdate;

namespace FinanceManager.Tests.Updates;

public sealed class InstalledReleaseMetadataProviderTests
{
    [Fact]
    public async Task InstalledReleaseMetadataProvider_DelegatesToInstalledVersionProvider()
    {
        var installed = new InstalledReleaseInfo("2.3.4", DateTimeOffset.Parse("2026-07-19T10:15:00+00:00"), "abc123", "FinanceManager", "win-x64");
        var provider = new InstalledReleaseMetadataProvider(new FixedInstalledVersionProvider(installed));

        var metadata = await provider.GetAsync();

        metadata.Version.Should().Be("2.3.4");
        metadata.CommitSha.Should().Be("abc123");
        metadata.Repository.Should().Be("FinanceManager");
        metadata.RuntimeIdentifier.Should().Be("win-x64");
    }

    private sealed class FixedInstalledVersionProvider : IInstalledVersionProvider
    {
        private readonly InstalledReleaseInfo _installed;

        public FixedInstalledVersionProvider(InstalledReleaseInfo installed)
        {
            _installed = installed;
        }

        public Task<InstalledReleaseInfo> GetAsync(CancellationToken ct = default) => Task.FromResult(_installed);
    }
}
