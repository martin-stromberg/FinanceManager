namespace SoftwareSchmiede.AutoUpdate;

/// <summary>
/// Thread-safe status service holding the current <see cref="AutoUpdateStatusSnapshot"/> behind a lock and
/// persisting every mutation through <see cref="IAutoUpdateStateStore"/>. The persisted snapshot is loaded lazily
/// on first access.
/// </summary>
public sealed class AutoUpdateStatusService : IAutoUpdateStatusProvider
{
    private readonly IAutoUpdateStateStore _stateStore;
    private readonly IInstalledVersionProvider _installedVersionProvider;
    private readonly Lock _gate = new();
    private readonly SemaphoreSlim _loadGate = new(1, 1);
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private AutoUpdateStatusSnapshot _snapshot = AutoUpdateStatusSnapshot.Idle(null);
    private bool _loaded;

    /// <summary>
    /// Initializes a new instance of the <see cref="AutoUpdateStatusService"/> class.
    /// </summary>
    /// <param name="stateStore">Used to persist and reload the status snapshot.</param>
    /// <param name="installedVersionProvider">Used to determine the installed version for a fresh snapshot.</param>
    public AutoUpdateStatusService(IAutoUpdateStateStore stateStore, IInstalledVersionProvider installedVersionProvider)
    {
        _stateStore = stateStore;
        _installedVersionProvider = installedVersionProvider;
    }

    /// <inheritdoc />
    public AutoUpdateStatusSnapshot GetSnapshot()
    {
        lock (_gate)
        {
            return _snapshot;
        }
    }

    /// <summary>
    /// Ensures that the persisted snapshot has been loaded. Subsequent calls are no-ops. Safe to call from
    /// multiple threads concurrently.
    /// </summary>
    /// <param name="ct">A token to observe for cancellation requests.</param>
    /// <returns>A task that completes once the snapshot has been loaded.</returns>
    public async Task EnsureLoadedAsync(CancellationToken ct = default)
    {
        if (Volatile.Read(ref _loaded))
        {
            return;
        }

        await _loadGate.WaitAsync(ct);
        try
        {
            if (_loaded)
            {
                return;
            }

            var installed = await _installedVersionProvider.GetAsync(ct);
            var persisted = await _stateStore.ReadAsync(ct);
            lock (_gate)
            {
                _snapshot = persisted ?? AutoUpdateStatusSnapshot.Idle(installed.Version);
            }

            Volatile.Write(ref _loaded, true);
        }
        finally
        {
            _loadGate.Release();
        }
    }

    /// <summary>
    /// Atomically replaces the current snapshot using <paramref name="mutate"/> and persists the result. Calls
    /// are serialized (including the persistence step) so that concurrent callers cannot race each other's writes
    /// to the underlying <see cref="IAutoUpdateStateStore"/>, and so the persisted file always reflects the most
    /// recently computed in-memory snapshot.
    /// </summary>
    /// <param name="mutate">Computes the new snapshot from the current one. Invoked while holding an internal lock; must not block.</param>
    /// <param name="ct">A token to observe for cancellation requests.</param>
    /// <returns>The updated snapshot.</returns>
    public async Task<AutoUpdateStatusSnapshot> UpdateAsync(Func<AutoUpdateStatusSnapshot, AutoUpdateStatusSnapshot> mutate, CancellationToken ct = default)
    {
        await _writeGate.WaitAsync(ct);
        try
        {
            AutoUpdateStatusSnapshot updated;
            lock (_gate)
            {
                updated = mutate(_snapshot);
                _snapshot = updated;
            }

            await _stateStore.WriteAsync(updated, ct);
            return updated;
        }
        finally
        {
            _writeGate.Release();
        }
    }
}
