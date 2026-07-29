namespace SoftwareSchmiede.AutoUpdate;

/// <summary>
/// Persists and reloads the auto-update status snapshot so it survives a process restart triggered by the
/// installation script.
/// </summary>
public interface IAutoUpdateStateStore
{
    /// <summary>
    /// Reads the persisted status snapshot.
    /// </summary>
    /// <param name="ct">A token to observe for cancellation requests.</param>
    /// <returns>The persisted snapshot, or <see langword="null"/> if none exists or it could not be read.</returns>
    Task<AutoUpdateStatusSnapshot?> ReadAsync(CancellationToken ct = default);

    /// <summary>
    /// Atomically writes the given status snapshot.
    /// </summary>
    /// <param name="snapshot">The snapshot to persist.</param>
    /// <param name="ct">A token to observe for cancellation requests.</param>
    /// <returns>A task that completes once the snapshot has been written.</returns>
    Task WriteAsync(AutoUpdateStatusSnapshot snapshot, CancellationToken ct = default);
}
