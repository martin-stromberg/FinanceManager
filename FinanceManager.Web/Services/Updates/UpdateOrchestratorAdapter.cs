using FinanceManager.Shared.Dtos.Update;
using Microsoft.Extensions.Logging;
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
    private readonly ILogger<UpdateOrchestratorAdapter> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateOrchestratorAdapter"/> class.
    /// </summary>
    /// <param name="orchestrator">The library orchestrator, used for status reads (including restart reconciliation) and manually triggered check/install operations.</param>
    /// <param name="statusService">Used to read the status snapshot immediately after a command completes and to persist a lock reset.</param>
    /// <param name="settingsStore">The host-specific settings store.</param>
    /// <param name="packageStore">Used to inspect, reset and evaluate the staleness of the installation lock.</param>
    /// <param name="statusMapper">Used to map a status snapshot onto <see cref="UpdateStatusDto"/>.</param>
    /// <param name="logger">Used to log a warning when the installation lock is not cleaned up after a successful installation.</param>
    public UpdateOrchestratorAdapter(
        IAutoUpdateOrchestrator orchestrator,
        AutoUpdateStatusService statusService,
        IUpdateSettingsStore settingsStore,
        IAutoUpdatePackageStore packageStore,
        UpdateStatusMapper statusMapper,
        ILogger<UpdateOrchestratorAdapter> logger)
    {
        _orchestrator = orchestrator;
        _statusService = statusService;
        _settingsStore = settingsStore;
        _packageStore = packageStore;
        _statusMapper = statusMapper;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<UpdateStatusDto> GetStatusAsync(CancellationToken ct = default)
    {
        await ReconcileLockStatusAsync(ct: ct);
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
        try
        {
            await ReconcileLockStatusAsync(ct: ct);
            var result = await _orchestrator.CheckForUpdateAsync(ct);
            var statusDto = await _statusMapper.MapAsync(_statusService.GetSnapshot(), ct);
            var message = UpdateErrorMessageMapper.Map(result.Message);
            if (result.Outcome == AutoUpdateOutcome.Failed
                && result.Error is not null
                && UpdateErrorMessageMapper.IsGithubRateLimit(result.Error.ToString()))
            {
                message = UpdateErrorMessageMapper.Map(result.Error);
                statusDto = statusDto with { LastError = message };
            }

            return new UpdateCheckResultDto(result.Outcome == AutoUpdateOutcome.Success, statusDto, message);
        }
        catch (Exception ex) when (UpdateErrorMessageMapper.IsGithubRateLimit(ex.ToString()))
        {
            var message = UpdateErrorMessageMapper.GithubRateLimitMessage;
            var statusDto = await _statusMapper.MapAsync(_statusService.GetSnapshot(), ct);
            return new UpdateCheckResultDto(false, statusDto with { LastError = message }, message);
        }
    }

    /// <inheritdoc />
    public async Task<UpdateStatusDto> StartInstallAsync(bool confirmDowntime, CancellationToken ct = default)
    {
        var result = await _orchestrator.InstallAsync(confirmDowntime, ct);
        if (result.Outcome == AutoUpdateOutcome.Failed && result.Error is not null)
        {
            throw result.Error;
        }

        if (result.Outcome != AutoUpdateOutcome.Failed)
        {
            await ReconcileLockStatusAsync(
                LogLevel.Warning,
                "Failed to validate lock cleanup after installation.",
                warnIfStillLocked: true,
                ct: ct);
        }
        else
        {
            await ReconcileLockStatusAsync(ct: ct);
        }

        return await _statusMapper.MapAsync(_statusService.GetSnapshot(), ct);
    }

    /// <inheritdoc />
    public async Task ResetLockAsync(string? reason, CancellationToken ct = default)
    {
        DateTimeOffset? lockCreatedAt = null;
        var statusUpdateStarted = false;
        try
        {
            lockCreatedAt = await _packageStore.GetLockCreatedAtAsync(ct);
            if (!lockCreatedAt.HasValue)
            {
                throw CreateResetException(
                    UpdateLockResetFailureKind.NoLock,
                    UpdateLockResetFailureSource.FinanceManager,
                    "No update lock is active.");
            }

            if (!_packageStore.IsLockStale(lockCreatedAt.Value))
            {
                throw CreateResetException(
                    UpdateLockResetFailureKind.LockNotStale,
                    UpdateLockResetFailureSource.FinanceManager,
                    "The update lock is not old enough to be considered stale.",
                    lockCreatedAt);
            }

            var deleted = await DeleteLockOrThrowAsync(lockCreatedAt.Value, ct);
            if (!deleted)
            {
                throw CreateResetException(
                    UpdateLockResetFailureKind.LockDeleteFailed,
                    UpdateLockResetFailureSource.FinanceManager,
                    "The update lock could not be deleted.",
                    lockCreatedAt);
            }

            statusUpdateStarted = true;
            await _statusService.UpdateAsync(s => s with
            {
                IsLocked = false,
                LockCreatedAt = null,
                LastError = string.IsNullOrWhiteSpace(reason) ? s.LastError : $"Lock reset: {reason}"
            }, ct);
        }
        catch (UpdateLockResetException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw CreateResetException(
                UpdateLockResetFailureKind.ResetFailed,
                statusUpdateStarted ? UpdateLockResetFailureSource.FinanceManager : UpdateLockResetFailureSource.Updater,
                "The update lock reset failed.",
                lockCreatedAt,
                ex);
        }
    }

    private async Task<bool> DeleteLockOrThrowAsync(DateTimeOffset lockCreatedAt, CancellationToken ct)
    {
        try
        {
            return await _packageStore.DeleteLockAsync(ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw CreateResetException(
                UpdateLockResetFailureKind.LockDeleteFailed,
                UpdateLockResetFailureSource.Updater,
                "The update lock could not be deleted.",
                lockCreatedAt,
                ex);
        }
    }

    private UpdateLockResetException CreateResetException(
        UpdateLockResetFailureKind kind,
        UpdateLockResetFailureSource failureSource,
        string message,
        DateTimeOffset? lockCreatedAt = null,
        Exception? innerException = null)
        => new(kind, failureSource, message, lockCreatedAt, _packageStore.LockPath, innerException);

    private async Task ReconcileLockStatusAsync(
        LogLevel failureLogLevel = LogLevel.Debug,
        string failureLogMessage = "Failed to reconcile lock status against the file system.",
        bool warnIfStillLocked = false,
        CancellationToken ct = default)
    {
        var (succeeded, lockCreatedAt) = await TryGetLockCreatedAtAsync(
            failureLogLevel, failureLogMessage, ct);
        if (succeeded)
        {
            if (warnIfStillLocked && lockCreatedAt.HasValue)
            {
                _logger.LogWarning("Lock was not cleaned up after installation. LockCreatedAt: {LockCreatedAt}", lockCreatedAt);
            }

            await ReconcileLockStatusCacheAsync(lockCreatedAt, ct);
        }
    }

    private async Task ReconcileLockStatusCacheAsync(DateTimeOffset? lockCreatedAt, CancellationToken ct)
    {
        var snapshot = _statusService.GetSnapshot();
        if (snapshot.IsLocked && !lockCreatedAt.HasValue)
        {
            await _statusService.UpdateAsync(s => s with { IsLocked = false, LockCreatedAt = null }, ct);
            _logger.LogDebug("Lock status cache reconciled: cleared stale lock entry not present on disk.");
        }
    }

    private async Task<(bool Succeeded, DateTimeOffset? LockCreatedAt)> TryGetLockCreatedAtAsync(
        LogLevel failureLogLevel, string failureLogMessage, CancellationToken ct)
    {
        try
        {
            var lockCreatedAt = await _packageStore.GetLockCreatedAtAsync(ct);
            return (true, lockCreatedAt);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.Log(failureLogLevel, ex, failureLogMessage);
            return (false, null);
        }
    }
}
