using FinanceManager.Web.Services.Updates;
using FluentAssertions;
using msTools.Updater;

namespace FinanceManager.Tests.Updates;

/// <summary>
/// Covers <see cref="InstalledReleaseMetadataProvider"/>, the adapter that exposes the currently installed
/// release's version, commit, repository, and runtime identifier (as reported by the msTools.Updater's
/// <see cref="IInstalledVersionProvider"/>) to the rest of the application - e.g. for display in the update status
/// UI or diagnostic pages.
/// </summary>
public sealed class InstalledReleaseMetadataProviderTests
{
    /// <summary>
    /// Verifies that the provider maps every field of the underlying <see cref="IInstalledVersionProvider"/> result
    /// through unchanged - a thin adapter test guarding against a field being dropped or mismapped when a new field
    /// is added upstream.
    /// </summary>
    [Fact]
    public async Task InstalledReleaseMetadataProvider_DelegatesToInstalledVersionProvider()
    {
        var installed = new InstalledReleaseInfo("2.3.4", DateTimeOffset.Parse("2026-07-19T10:15:00+00:00"), "abc123", "FinanceManager", "win-x64");
        var provider = new InstalledReleaseMetadataProvider(new FixedInstalledVersionProvider(installed));

        var metadata = await provider.GetAsync(TestContext.Current.CancellationToken);

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
