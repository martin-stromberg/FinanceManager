using FinanceManager.Shared.Dtos.Update;
using FinanceManager.Web.Services.Updates;
using FluentAssertions;
using Moq;
using msTools.Updater;

namespace FinanceManager.Tests.Updates;

public sealed class UpdateStatusMapperTests
{
    [Fact]
    public async Task MapAsync_WhenIdleSnapshotContainsLastCheckPackageWithoutAvailableVersion_DoesNotReportAvailableUpdate()
    {
        var mapper = CreateMapper();
        var package = new AutoUpdatePackageDescriptor(
            "1.21.0-RC.2",
            "windows",
            "win-x64",
            "release.zip",
            new Uri("https://example.invalid/release.zip"),
            "abc123",
            42);
        var checkResult = new AutoUpdateCheckResult(null, package, "Release notes", DateTimeOffset.UtcNow, true);
        var snapshot = new AutoUpdateStatusSnapshot(
            AutoUpdateState.Idle,
            "1.20.0",
            null,
            DateTimeOffset.UtcNow,
            checkResult,
            null,
            null,
            null,
            null,
            false,
            null);

        var result = await mapper.MapAsync(snapshot, TestContext.Current.CancellationToken);

        result.Status.Should().Be(UpdateStatusKind.NoUpdate);
        result.AvailableVersion.Should().BeNull();
        result.AvailableUpdate.Should().BeNull();
    }

    [Fact]
    public async Task MapAsync_WhenSnapshotStateIsUpdateAvailable_ReportsAvailableUpdate()
    {
        var mapper = CreateMapper();
        var package = new AutoUpdatePackageDescriptor(
            "1.21.0-RC.2",
            "windows",
            "win-x64",
            "release.zip",
            new Uri("https://example.invalid/release.zip"),
            "abc123",
            42);
        var checkResult = new AutoUpdateCheckResult("1.21.0-RC.2", package, "Release notes", DateTimeOffset.UtcNow, true);
        var snapshot = new AutoUpdateStatusSnapshot(
            AutoUpdateState.UpdateAvailable,
            "1.20.0",
            "1.21.0-RC.2",
            DateTimeOffset.UtcNow,
            checkResult,
            null,
            null,
            null,
            null,
            false,
            null);

        var result = await mapper.MapAsync(snapshot, TestContext.Current.CancellationToken);

        result.Status.Should().Be(UpdateStatusKind.Available);
        result.AvailableVersion.Should().Be("1.21.0-RC.2");
        result.AvailableUpdate.Should().NotBeNull();
        result.AvailableUpdate!.Version.Should().Be("1.21.0-RC.2");
    }

    private static UpdateStatusMapper CreateMapper()
    {
        var installedProvider = new Mock<IInstalledReleaseMetadataProvider>();
        installedProvider
            .Setup(p => p.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InstalledReleaseMetadataDto("1.20.0", null, null, null, "win-x64"));
        var platformResolver = new Mock<IAutoUpdatePlatformResolver>();
        platformResolver.SetupGet(p => p.CurrentRuntimeIdentifier).Returns("win-x64");
        var settingsStore = new Mock<IUpdateSettingsStore>();
        settingsStore
            .Setup(s => s.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UpdateSettingsDto(
                true,
                "owner",
                "repo",
                "update.json",
                new TimeOnly(20, 0),
                new TimeOnly(6, 0),
                null,
                null,
                null,
                "updates",
                120,
                true));
        return new UpdateStatusMapper(installedProvider.Object, platformResolver.Object, settingsStore.Object);
    }
}
