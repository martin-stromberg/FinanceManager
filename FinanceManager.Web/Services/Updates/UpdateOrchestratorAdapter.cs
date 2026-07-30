using FinanceManager.Shared.Dtos.Update;
using msTools.Updater;

namespace FinanceManager.Web.Services.Updates;

/// <summary>
/// Implements <see cref="IUpdateOrchestrator"/> on top of the <c>msTools.Updater</c> library, mapping
/// between the library's status/result types and the existing DTOs in <c>FinanceManager.Shared.Dtos.Update</c> so
/// that <c>UpdateController</c>, <c>ApiClient</c>, <c>SetupUpdateViewModel</c> and <c>SetupUpdateTab.razor</c>
/// remain unchanged. Errors reported by the library as <see cref="AutoUpdateResult.Error"/> are re-thrown so the
/// controller's existing exception mapping continues to apply. Status-to-DTO mapping is delegated to
/// <see cref="UpdateStatusMapper"/>; the lock-staleness decision is delegated to <see cref="IAutoUpdatePackageStore.IsLockStale"/>.
/// </summary>
public sealed class UpdateOrchestratorAdapter : IUpdateOrchestrator
{
    private readonly IAutoUpdateOrchestrator _orchestrator;
    private readonly AutoUpdateStatusService _statusService;
    private readonly IUpdateSettingsStore _settingsStore;
    private readonly IAutoUpdatePackageStore _packageStore;
    private readonly UpdateStatusMapper _statusMapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateOrchestratorAdapter"/> class.
    /// </summary>
    /// <param name="orchestrator">The library orchestrator, used for status reads (including restart reconciliation) and manually triggered check/install operations.</param>
    /// <param name="statusService">Used to read the status snapshot immediately after a command completes and to persist a lock reset.</param>
    /// <param name="settingsStore">The host-specific settings store.</param>
    /// <param name="packageStore">Used to inspect, reset and evaluate the staleness of the installation lock.</param>
    /// <param name="statusMapper">Used to map a status snapshot onto <see cref="UpdateStatusDto"/>.</param>
    public UpdateOrchestratorAdapter(
        IAutoUpdateOrchestrator orchestrator,
        AutoUpdateStatusService statusService,
        IUpdateSettingsStore settingsStore,
        IAutoUpdatePackageStore packageStore,
        UpdateStatusMapper statusMapper)
    {
        _orchestrator = orchestrator;
        _statusService = statusService;
        _settingsStore = settingsStore;
        _packageStore = packageStore;
        _statusMapper = statusMapper;
    }

    /// <inheritdoc />
    public async Task<UpdateStatusDto> GetStatusAsync(CancellationToken ct = default)
    {
        var snapshot = await _orchestrator.GetStatusAsync(ct);
        return await _statusMapper.MapAsync(snapshot, ct);
    }

    /// <inheritdoc />
    public Task<UpdateSettingsDto> GetSettingsAsync(CancellationToken ct = default)
        => _settingsStore.GetAsync(ct);

    /// <inheritdoc />
    public async Task<UpdateSettingsDto> SaveSettingsAsync(UpdateSettingsUpdateRequest request, CancellationToken ct = default)
    {
        var settings = await _settingsStore.SaveAsync(request, ct);
        _settingsStore.ApplyToOptions(settings);
        return settings;
    }

    /// <inheritdoc />
    public async Task<UpdateSettingsDto> ScheduleAsync(TimeOnly? scheduledInstallTime, CancellationToken ct = default)
    {
        var settings = await _settingsStore.SaveScheduleAsync(scheduledInstallTime, ct);
        _settingsStore.ApplyToOptions(settings);
        return settings;
    }

    /// <inheritdoc />
    public async Task<UpdateCheckResultDto> CheckAsync(CancellationToken ct = default)
    {
        var result = await _orchestrator.CheckForUpdateAsync(ct);
        var statusDto = await _statusMapper.MapAsync(_statusService.GetSnapshot(), ct);
        return new UpdateCheckResultDto(result.Outcome == AutoUpdateOutcome.Success, statusDto, result.Message);
    }

    /// <inheritdoc />
    public async Task<UpdateStatusDto> StartInstallAsync(bool confirmDowntime, CancellationToken ct = default)
    {
        var result = await _orchestrator.InstallAsync(confirmDowntime, ct);
        if (result.Outcome == AutoUpdateOutcome.Failed && result.Error is not null)
        {
            throw result.Error;
        }

        return await _statusMapper.MapAsync(_statusService.GetSnapshot(), ct);
    }

    /// <inheritdoc />
    public async Task ResetLockAsync(string? reason, CancellationToken ct = default)
    {
        var lockCreatedAt = await _packageStore.GetLockCreatedAtAsync(ct);
        if (!lockCreatedAt.HasValue)
        {
            throw new IOException("No update lock is active.");
        }

        if (!_packageStore.IsLockStale(lockCreatedAt.Value))
        {
            throw new IOException("The update lock is not old enough to be considered stale.");
        }

        await _packageStore.DeleteLockAsync(ct);
        await _statusService.UpdateAsync(s => s with
        {
            IsLocked = false,
            LockCreatedAt = null,
            LastError = string.IsNullOrWhiteSpace(reason) ? s.LastError : $"Lock reset: {reason}"
        }, ct);
    }
}
