using FinanceManager.Shared.Dtos.Update;
using SoftwareSchmiede.AutoUpdate;

namespace FinanceManager.Web.Services.Updates;

/// <summary>
/// Implements <see cref="IUpdateOrchestrator"/> on top of the <c>SoftwareSchmiede.AutoUpdate</c> library, mapping
/// between the library's status/result types and the existing DTOs in <c>FinanceManager.Shared.Dtos.Update</c> so
/// that <c>UpdateController</c>, <c>ApiClient</c>, <c>SetupUpdateViewModel</c> and <c>SetupUpdateTab.razor</c>
/// remain unchanged. Errors reported by the library as <see cref="AutoUpdateResult.Error"/> are re-thrown so the
/// controller's existing exception mapping continues to apply.
/// </summary>
public sealed class UpdateOrchestratorAdapter : IUpdateOrchestrator
{
    private readonly IAutoUpdateOrchestrator _orchestrator;
    private readonly IAutoUpdateCommandHandler _commandHandler;
    private readonly IAutoUpdateStatusProvider _statusProvider;
    private readonly IUpdateSettingsStore _settingsStore;
    private readonly IInstalledReleaseMetadataProvider _installedProvider;
    private readonly IAutoUpdatePlatformResolver _platformResolver;
    private readonly IAutoUpdatePackageStore _packageStore;
    private readonly AutoUpdateStatusService _statusService;
    private readonly AutoUpdateOptions _autoUpdateOptions;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateOrchestratorAdapter"/> class.
    /// </summary>
    /// <param name="orchestrator">The library orchestrator, used for status reads including restart reconciliation.</param>
    /// <param name="commandHandler">The library command handler, used for manually triggered check/install operations.</param>
    /// <param name="statusProvider">Used to read the status snapshot immediately after a command completes.</param>
    /// <param name="settingsStore">The host-specific settings store.</param>
    /// <param name="installedProvider">The host-specific installed release metadata provider.</param>
    /// <param name="platformResolver">Used to map the current platform onto <see cref="UpdateStatusDto.CurrentPlatform"/>.</param>
    /// <param name="packageStore">Used to inspect and reset the installation lock.</param>
    /// <param name="statusService">Used to persist a lock reset in the status snapshot.</param>
    /// <param name="autoUpdateOptions">The library's runtime-mutable options, used for the lock staleness threshold.</param>
    /// <param name="timeProvider">Used for the lock staleness comparison, so it can be controlled in tests.</param>
    public UpdateOrchestratorAdapter(
        IAutoUpdateOrchestrator orchestrator,
        IAutoUpdateCommandHandler commandHandler,
        IAutoUpdateStatusProvider statusProvider,
        IUpdateSettingsStore settingsStore,
        IInstalledReleaseMetadataProvider installedProvider,
        IAutoUpdatePlatformResolver platformResolver,
        IAutoUpdatePackageStore packageStore,
        AutoUpdateStatusService statusService,
        AutoUpdateOptions autoUpdateOptions,
        TimeProvider timeProvider)
    {
        _orchestrator = orchestrator;
        _commandHandler = commandHandler;
        _statusProvider = statusProvider;
        _settingsStore = settingsStore;
        _installedProvider = installedProvider;
        _platformResolver = platformResolver;
        _packageStore = packageStore;
        _statusService = statusService;
        _autoUpdateOptions = autoUpdateOptions;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public async Task<UpdateStatusDto> GetStatusAsync(CancellationToken ct = default)
    {
        var snapshot = await _orchestrator.GetStatusAsync(ct);
        return await MapToStatusDtoAsync(snapshot, ct);
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
        var result = await _commandHandler.CheckAsync(ct);
        var statusDto = await MapToStatusDtoAsync(_statusProvider.GetSnapshot(), ct);
        return new UpdateCheckResultDto(result.Outcome == AutoUpdateOutcome.Success, statusDto, result.Message);
    }

    /// <inheritdoc />
    public async Task<UpdateStatusDto> StartInstallAsync(bool confirmDowntime, CancellationToken ct = default)
    {
        var result = await _commandHandler.InstallAsync(confirmDowntime, ct);
        if (result.Outcome == AutoUpdateOutcome.Failed && result.Error is not null)
        {
            throw result.Error;
        }

        return await MapToStatusDtoAsync(_statusProvider.GetSnapshot(), ct);
    }

    /// <inheritdoc />
    public async Task ResetLockAsync(string? reason, CancellationToken ct = default)
    {
        var lockCreatedAt = await _packageStore.GetLockCreatedAtAsync(ct);
        if (!lockCreatedAt.HasValue)
        {
            throw new IOException("No update lock is active.");
        }

        var staleLockAge = TimeSpan.FromSeconds(_autoUpdateOptions.HealthTimeoutSeconds);
        if (_timeProvider.GetUtcNow() - lockCreatedAt.Value < staleLockAge)
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

    private async Task<UpdateStatusDto> MapToStatusDtoAsync(AutoUpdateStatusSnapshot snapshot, CancellationToken ct)
    {
        var installed = await _installedProvider.GetAsync(ct);
        var settings = await _settingsStore.GetAsync(ct);

        UpdateMetadataDto? availableUpdate = null;
        if (snapshot.LastCheckResult?.Package is { } package)
        {
            availableUpdate = new UpdateMetadataDto(
                snapshot.LastCheckResult.AvailableVersion ?? package.Version,
                snapshot.LastCheckResult.ReleaseNotes,
                snapshot.LastCheckResult.PublishedAt,
                settings.RepositoryOwner,
                settings.RepositoryName,
                new[]
                {
                    new UpdateAssetDto(package.Platform, package.RuntimeIdentifier, package.FileName, package.Uri.ToString(), package.Sha256, package.SizeBytes)
                });
        }

        return new UpdateStatusDto(
            MapState(snapshot.State),
            installed.Version,
            installed.PublishedAt,
            snapshot.AvailableVersion,
            _platformResolver.CurrentRuntimeIdentifier,
            snapshot.LastCheckedAt,
            snapshot.LastError,
            snapshot.LastDownloadResult is not null ? Path.GetFileName(snapshot.LastDownloadResult.LocalPath) : null,
            snapshot.IsLocked,
            snapshot.LockCreatedAt,
            settings.ScheduledInstallTime,
            availableUpdate);
    }

    private static UpdateStatusKind MapState(AutoUpdateState state) => state switch
    {
        AutoUpdateState.Idle => UpdateStatusKind.NoUpdate,
        AutoUpdateState.Checking => UpdateStatusKind.Checking,
        AutoUpdateState.UpdateAvailable => UpdateStatusKind.Available,
        AutoUpdateState.Downloading => UpdateStatusKind.Downloading,
        AutoUpdateState.ReadyToInstall => UpdateStatusKind.Ready,
        AutoUpdateState.Installing => UpdateStatusKind.Installing,
        AutoUpdateState.Success => UpdateStatusKind.NoUpdate,
        AutoUpdateState.Failed => UpdateStatusKind.Failed,
        AutoUpdateState.Disabled => UpdateStatusKind.NoUpdate,
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown auto-update state.")
    };
}
