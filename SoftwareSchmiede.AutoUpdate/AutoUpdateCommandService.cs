namespace SoftwareSchmiede.AutoUpdate;

/// <summary>
/// UI-agnostic, thin facade over <see cref="IAutoUpdateOrchestrator"/> for manually triggered update operations.
/// Contains no update logic of its own; thread-safety is inherited from the orchestrator's internal
/// serialization.
/// </summary>
/// <remarks>
/// Deliberate interface segregation, not an accidental middle man: <see cref="IAutoUpdateCommandHandler"/> exposes
/// only the subset of <see cref="IAutoUpdateOrchestrator"/> meant for manual, UI-triggered operations
/// (check/download/install), while <see cref="IAutoUpdateOrchestrator"/> additionally covers status reads and the
/// full automated workflow used by the background services. Consumers that only need manual triggering (e.g. a
/// setup UI adapter) depend on the narrower contract instead of the full orchestrator surface.
/// </remarks>
public sealed class AutoUpdateCommandService : IAutoUpdateCommandHandler
{
    private readonly IAutoUpdateOrchestrator _orchestrator;

    /// <summary>
    /// Initializes a new instance of the <see cref="AutoUpdateCommandService"/> class.
    /// </summary>
    /// <param name="orchestrator">The orchestrator all operations are delegated to.</param>
    public AutoUpdateCommandService(IAutoUpdateOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    /// <inheritdoc />
    public Task<AutoUpdateResult> CheckAsync(CancellationToken ct = default)
        => _orchestrator.CheckForUpdateAsync(ct);

    /// <inheritdoc />
    public Task<AutoUpdateResult> DownloadAsync(CancellationToken ct = default)
        => _orchestrator.DownloadAsync(ct);

    /// <inheritdoc />
    public Task<AutoUpdateResult> InstallAsync(bool confirmDowntime, CancellationToken ct = default)
        => _orchestrator.InstallAsync(confirmDowntime, ct);
}
