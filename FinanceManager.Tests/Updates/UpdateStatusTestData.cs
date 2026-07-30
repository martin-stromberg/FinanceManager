using msTools.Updater;

namespace FinanceManager.Tests.Updates;

/// <summary>
/// Shared builder for <see cref="AutoUpdateStatusSnapshot"/> test fixtures, reused across the unit and
/// integration test projects to avoid duplicating the long positional constructor in multiple places.
/// </summary>
public static class UpdateStatusTestData
{
    /// <summary>
    /// Builds an <see cref="AutoUpdateStatusSnapshot"/> representing an in-progress installation of
    /// <paramref name="availableVersion"/> with an active lock.
    /// </summary>
    /// <param name="availableVersion">The version currently being installed.</param>
    /// <returns>An <see cref="AutoUpdateStatusSnapshot"/> with <see cref="AutoUpdateState.Installing"/> state.</returns>
    public static AutoUpdateStatusSnapshot InstallingSnapshot(string availableVersion)
        => new(
            AutoUpdateState.Installing,
            null,
            availableVersion,
            DateTimeOffset.UtcNow,
            null,
            null,
            null,
            null,
            true,
            DateTimeOffset.UtcNow);

    /// <summary>
    /// Builds an <see cref="AutoUpdateStatusSnapshot"/> representing a package ready to install.
    /// </summary>
    /// <param name="availableVersion">The version ready to be installed.</param>
    /// <param name="package">The package descriptor to attach as the last check result, or <see langword="null"/> if none is needed.</param>
    /// <returns>An <see cref="AutoUpdateStatusSnapshot"/> with <see cref="AutoUpdateState.ReadyToInstall"/> state.</returns>
    public static AutoUpdateStatusSnapshot ReadyToInstallSnapshot(string availableVersion, AutoUpdatePackageDescriptor? package = null)
        => new(
            AutoUpdateState.ReadyToInstall,
            "1.0.0",
            availableVersion,
            DateTimeOffset.UtcNow,
            package is null ? null : new AutoUpdateCheckResult(availableVersion, package, null, null),
            new AutoUpdateDownloadResult("release.zip", 10, true),
            null,
            null,
            false,
            null);
}
