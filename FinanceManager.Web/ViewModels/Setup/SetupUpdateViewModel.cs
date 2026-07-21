using FinanceManager.Shared.Dtos.Update;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Web.ViewModels.Setup;

/// <summary>
/// View model backing the update setup tab. Loads and persists update settings, triggers update checks
/// and installs, and tracks install progress state for the UI.
/// </summary>
public sealed class SetupUpdateViewModel : BaseViewModel
{
    private readonly ILogger<SetupUpdateViewModel>? _logger;

    /// <summary>
    /// Creates a new instance of <see cref="SetupUpdateViewModel"/>.
    /// </summary>
    /// <param name="sp">Service provider used to resolve dependencies such as <see cref="FinanceManager.Shared.IApiClient"/> and logging.</param>
    public SetupUpdateViewModel(IServiceProvider sp) : base(sp)
    {
        _logger = sp.GetService<ILogger<SetupUpdateViewModel>>();
    }

    /// <summary>
    /// Currently loaded update settings, or <c>null</c> before <see cref="LoadAsync"/> has completed.
    /// </summary>
    public UpdateSettingsDto? Settings { get; private set; }

    /// <summary>
    /// Currently loaded update status, or <c>null</c> before <see cref="LoadAsync"/> has completed.
    /// </summary>
    public UpdateStatusDto? Status { get; private set; }

    /// <summary>
    /// Indicates that a setup action (save, check, install, reset lock) is in progress.
    /// Distinct from the base <see cref="BaseViewModel.Loading"/> flag by design: this view model has no
    /// separate initial-load phase, and other setup view models in this codebase (e.g.
    /// SetupAttachmentCategoriesViewModel) use the same convention of a dedicated <c>Busy</c> flag for
    /// action-in-progress state.
    /// </summary>
    public bool Busy { get; private set; }

    /// <summary>
    /// Indicates that an update install is currently in progress.
    /// </summary>
    public bool Installing { get; private set; }

    /// <summary>
    /// Localization key describing the current phase of an ongoing install (e.g. installing, waiting for restart).
    /// </summary>
    public string? InstallPhase { get; private set; }

    /// <summary>
    /// Loads the current update settings and status from the API.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous load operation.</returns>
    public Task LoadAsync(CancellationToken ct = default)
    {
        return RunBusyAsync(async ct =>
        {
            Settings = await ApiClient.Updates_GetSettingsAsync(ct);
            Status = await ApiClient.Updates_GetStatusAsync(ct);
        }, ct);
    }

    /// <summary>
    /// Persists the current <see cref="Settings"/> via the API and refreshes the status. No-op when <see cref="Settings"/> is <c>null</c>.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous save operation.</returns>
    public Task SaveAsync(CancellationToken ct = default)
    {
        if (Settings is null)
        {
            return Task.CompletedTask;
        }

        return RunBusyAsync(async ct =>
        {
            Settings = await ApiClient.Updates_UpdateSettingsAsync(new UpdateSettingsUpdateRequest(
                Settings.Enabled,
                Settings.CheckIntervalMinutes,
                Settings.RepositoryOwner,
                Settings.RepositoryName,
                Settings.ManifestAssetName,
                Settings.ScheduledInstallTime,
                Settings.ServiceName,
                Settings.ExecutablePath,
                Settings.WorkingDirectory,
                Settings.HealthTimeoutSeconds), ct);
            Status = await ApiClient.Updates_GetStatusAsync(ct);
        }, ct);
    }

    /// <summary>
    /// Triggers an immediate update check via the API and refreshes <see cref="Status"/> with the result.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous check operation.</returns>
    public Task CheckAsync(CancellationToken ct = default)
    {
        return RunBusyAsync(async ct =>
        {
            var result = await ApiClient.Updates_CheckAsync(ct);
            Status = result.Status;
        }, ct);
    }

    /// <summary>
    /// Starts installing the available update via the API and updates <see cref="Status"/> and <see cref="Installing"/>.
    /// </summary>
    /// <param name="confirmDowntime">Whether the caller confirmed the expected downtime.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous install-start operation.</returns>
    public Task StartInstallAsync(bool confirmDowntime, CancellationToken ct = default)
    {
        return RunBusyAsync(async ct =>
        {
            var installStatus = await ApiClient.Updates_StartInstallAsync(new UpdateStartRequest(confirmDowntime), ct);
            if (installStatus is not null)
            {
                Status = installStatus;
                Installing = installStatus.Status == UpdateStatusKind.Installing;
            }
        }, ct);
    }

    /// <summary>
    /// Resets the update lock via the API and refreshes <see cref="Status"/>.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous reset-lock operation.</returns>
    public Task ResetLockAsync(CancellationToken ct = default)
    {
        return RunBusyAsync(async ct =>
        {
            await ApiClient.Updates_ResetLockAsync(new UpdateLockResetRequest("Reset from setup UI"), ct);
            Status = await ApiClient.Updates_GetStatusAsync(ct);
        }, ct);
    }

    /// <summary>
    /// Replaces the in-memory <see cref="Settings"/> with the supplied values (used by form bindings before saving).
    /// </summary>
    /// <param name="settings">Updated settings values.</param>
    public void UpdateSettings(UpdateSettingsDto settings)
    {
        Settings = settings;
        RaiseStateChanged();
    }

    /// <summary>
    /// Marks the current install as having timed out while waiting for the application to become reachable again.
    /// </summary>
    public void MarkHealthTimeout()
    {
        SetError("Err_Update_HealthTimeout", "Die Anwendung wurde nach dem Update nicht innerhalb des erwarteten Zeitfensters erreichbar.");
        RaiseStateChanged();
    }

    /// <summary>
    /// Sets the localization key describing the current install phase and notifies subscribers.
    /// </summary>
    /// <param name="phase">Localization key for the current phase, or <c>null</c>.</param>
    public void SetInstallPhase(string? phase)
    {
        InstallPhase = phase;
        RaiseStateChanged();
    }

    private void BeginBusy()
    {
        Busy = true;
        SetError(null, null);
        RaiseStateChanged();
    }

    private async Task RunBusyAsync(Func<CancellationToken, Task> operation, CancellationToken ct)
    {
        BeginBusy();
        try
        {
            await operation(ct);
        }
        catch (Exception ex)
        {
            HandleException(ex);
        }
        finally
        {
            Busy = false;
            RaiseStateChanged();
        }
    }

    private void HandleException(Exception ex)
    {
        _logger?.LogError(ex, "Update setup operation failed.");
        SetError(ApiClient.LastErrorCode, ApiClient.LastError ?? ex.Message);
    }
}
