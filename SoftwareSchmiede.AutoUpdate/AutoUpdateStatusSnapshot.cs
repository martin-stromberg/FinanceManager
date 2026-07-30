namespace SoftwareSchmiede.AutoUpdate;

/// <summary>
/// An immutable, self-consistent snapshot of the auto-update subsystem's current state, as maintained by
/// <see cref="AutoUpdateStatusService"/>.
/// </summary>
/// <param name="State">The current lifecycle state.</param>
/// <param name="InstalledVersion">The currently installed version.</param>
/// <param name="AvailableVersion">The most recently discovered available version, if any.</param>
/// <param name="LastCheckedAt">The timestamp of the most recent source check.</param>
/// <param name="LastCheckResult">The result of the most recent source check.</param>
/// <param name="LastDownloadResult">The result of the most recent download.</param>
/// <param name="LastInstallResult">The result of the most recent installation attempt.</param>
/// <param name="LastError">The message of the most recently reported error, if any.</param>
/// <param name="IsLocked">Whether an installation lock is currently active.</param>
/// <param name="LockCreatedAt">The timestamp the active installation lock was created at.</param>
public sealed record AutoUpdateStatusSnapshot(
    AutoUpdateState State,
    string? InstalledVersion,
    string? AvailableVersion,
    DateTimeOffset? LastCheckedAt,
    AutoUpdateCheckResult? LastCheckResult,
    AutoUpdateDownloadResult? LastDownloadResult,
    AutoUpdateInstallResult? LastInstallResult,
    string? LastError,
    bool IsLocked,
    DateTimeOffset? LockCreatedAt)
{
    /// <summary>
    /// Returns a fresh snapshot in the <see cref="AutoUpdateState.Idle"/> state with no history, used whenever
    /// no snapshot could be loaded from persistence.
    /// </summary>
    /// <param name="installedVersion">The currently installed version.</param>
    /// <returns>A fresh, idle snapshot with no history.</returns>
    public static AutoUpdateStatusSnapshot Idle(string? installedVersion)
        => new(AutoUpdateState.Idle, installedVersion, null, null, null, null, null, null, false, null);
}
