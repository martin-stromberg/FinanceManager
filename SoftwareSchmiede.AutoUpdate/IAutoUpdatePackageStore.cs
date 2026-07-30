namespace SoftwareSchmiede.AutoUpdate;

/// <summary>
/// Manages the on-disk directory layout used to stage, validate and lock update packages.
/// </summary>
public interface IAutoUpdatePackageStore
{
    /// <summary>
    /// Gets the root directory update packages, status and lock files are stored in.
    /// </summary>
    string RootDirectory { get; }

    /// <summary>
    /// Gets the directory downloaded packages are stored in before installation.
    /// </summary>
    string PendingDirectory { get; }

    /// <summary>
    /// Gets the directory a package is extracted into during installation.
    /// </summary>
    string StagingDirectory { get; }

    /// <summary>
    /// Gets the path of the installation lock file.
    /// </summary>
    string LockPath { get; }

    /// <summary>
    /// Gets the path of the installation log file.
    /// </summary>
    string LogPath { get; }

    /// <summary>
    /// Builds the path of the generated installation script with the given file extension.
    /// </summary>
    /// <param name="extension">The script file extension, with or without a leading dot.</param>
    /// <returns>The full path of the installation script.</returns>
    string ScriptPath(string extension);

    /// <summary>
    /// Builds the path a downloaded package with the given file name should be stored at.
    /// </summary>
    /// <param name="fileName">The file name of the package. Must not contain path segments.</param>
    /// <returns>The full path of the pending package file.</returns>
    string PendingAssetPath(string fileName);

    /// <summary>
    /// Ensures that <see cref="RootDirectory"/>, <see cref="PendingDirectory"/> and <see cref="StagingDirectory"/>
    /// exist.
    /// </summary>
    /// <param name="ct">A token to observe for cancellation requests.</param>
    /// <returns>A task that completes once the directories exist.</returns>
    Task EnsureAsync(CancellationToken ct = default);

    /// <summary>
    /// Reads the creation timestamp of the active installation lock, if any.
    /// </summary>
    /// <param name="ct">A token to observe for cancellation requests.</param>
    /// <returns>The lock's creation timestamp, or <see langword="null"/> if no lock is active.</returns>
    Task<DateTimeOffset?> GetLockCreatedAtAsync(CancellationToken ct = default);

    /// <summary>
    /// Atomically creates the installation lock file if none exists yet.
    /// </summary>
    /// <param name="ct">A token to observe for cancellation requests.</param>
    /// <returns><see langword="true"/> if the lock was created; <see langword="false"/> if it already existed.</returns>
    Task<bool> TryCreateLockAsync(CancellationToken ct = default);

    /// <summary>
    /// Deletes the installation lock file, if any.
    /// </summary>
    /// <param name="ct">A token to observe for cancellation requests.</param>
    /// <returns><see langword="true"/> if a lock file was deleted; <see langword="false"/> if none existed.</returns>
    Task<bool> DeleteLockAsync(CancellationToken ct = default);

    /// <summary>
    /// Determines whether a lock created at <paramref name="lockCreatedAt"/> is older than
    /// <see cref="AutoUpdateOptions.HealthTimeoutSeconds"/> and can therefore be considered stale (e.g. left behind
    /// by an installation that started but never finished restarting the application).
    /// </summary>
    /// <param name="lockCreatedAt">The lock's creation timestamp, as returned by <see cref="GetLockCreatedAtAsync"/>.</param>
    /// <returns><see langword="true"/> if the lock is old enough to be considered stale; otherwise <see langword="false"/>.</returns>
    bool IsLockStale(DateTimeOffset lockCreatedAt);
}
