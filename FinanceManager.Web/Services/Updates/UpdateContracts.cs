using FinanceManager.Shared.Dtos.Update;

namespace FinanceManager.Web.Services.Updates;

/// <summary>
/// Persists and reloads the update settings configured through the setup UI.
/// </summary>
public interface IUpdateSettingsStore
{
    /// <summary>
    /// Reads the current update settings, applying defaults on first access.
    /// </summary>
    /// <param name="ct">A token to observe for cancellation requests.</param>
    /// <returns>The current update settings.</returns>
    Task<UpdateSettingsDto> GetAsync(CancellationToken ct = default);

    /// <summary>
    /// Saves the update settings submitted from the setup UI.
    /// </summary>
    /// <param name="request">The settings to save.</param>
    /// <param name="ct">A token to observe for cancellation requests.</param>
    /// <returns>The saved settings.</returns>
    Task<UpdateSettingsDto> SaveAsync(UpdateSettingsUpdateRequest request, CancellationToken ct = default);

    /// <summary>
    /// Saves only the scheduled installation time, keeping all other settings unchanged.
    /// </summary>
    /// <param name="scheduledInstallTime">The new scheduled installation time, or <see langword="null"/> to clear it.</param>
    /// <param name="ct">A token to observe for cancellation requests.</param>
    /// <returns>The saved settings.</returns>
    Task<UpdateSettingsDto> SaveScheduleAsync(TimeOnly? scheduledInstallTime, CancellationToken ct = default);

    /// <summary>
    /// Transfers the given settings into the runtime-mutable auto-update library options, so that changes made
    /// through the setup UI take effect immediately.
    /// </summary>
    /// <param name="settings">The settings to apply.</param>
    void ApplyToOptions(UpdateSettingsDto settings);
}

/// <summary>
/// Provides the metadata of the currently installed release, as displayed in the application menu.
/// </summary>
public interface IInstalledReleaseMetadataProvider
{
    /// <summary>
    /// Reads the metadata of the currently installed release.
    /// </summary>
    /// <param name="ct">A token to observe for cancellation requests.</param>
    /// <returns>The installed release metadata.</returns>
    Task<InstalledReleaseMetadataDto> GetAsync(CancellationToken ct = default);
}

/// <summary>
/// Coordinates the self-update workflow for the REST API and setup UI. Implemented by
/// <see cref="UpdateOrchestratorAdapter"/> on top of the <c>SoftwareSchmiede.AutoUpdate</c> library.
/// </summary>
public interface IUpdateOrchestrator
{
    /// <summary>
    /// Gets the current update status.
    /// </summary>
    /// <param name="ct">A token to observe for cancellation requests.</param>
    /// <returns>The current update status.</returns>
    Task<UpdateStatusDto> GetStatusAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets the current update settings.
    /// </summary>
    /// <param name="ct">A token to observe for cancellation requests.</param>
    /// <returns>The current update settings.</returns>
    Task<UpdateSettingsDto> GetSettingsAsync(CancellationToken ct = default);

    /// <summary>
    /// Saves updated settings submitted from the setup UI.
    /// </summary>
    /// <param name="request">The settings to save.</param>
    /// <param name="ct">A token to observe for cancellation requests.</param>
    /// <returns>The saved settings.</returns>
    Task<UpdateSettingsDto> SaveSettingsAsync(UpdateSettingsUpdateRequest request, CancellationToken ct = default);

    /// <summary>
    /// Sets or clears the scheduled installation time.
    /// </summary>
    /// <param name="scheduledInstallTime">The new scheduled installation time, or <see langword="null"/> to clear it.</param>
    /// <param name="ct">A token to observe for cancellation requests.</param>
    /// <returns>The saved settings.</returns>
    Task<UpdateSettingsDto> ScheduleAsync(TimeOnly? scheduledInstallTime, CancellationToken ct = default);

    /// <summary>
    /// Manually triggers a source check.
    /// </summary>
    /// <param name="ct">A token to observe for cancellation requests.</param>
    /// <returns>The result of the check.</returns>
    Task<UpdateCheckResultDto> CheckAsync(CancellationToken ct = default);

    /// <summary>
    /// Manually triggers installation of a downloaded update package.
    /// </summary>
    /// <param name="confirmDowntime">Must be <see langword="true"/> to acknowledge that installation restarts the application.</param>
    /// <param name="ct">A token to observe for cancellation requests.</param>
    /// <returns>The status after installation was started.</returns>
    Task<UpdateStatusDto> StartInstallAsync(bool confirmDowntime, CancellationToken ct = default);

    /// <summary>
    /// Resets a stale installation lock.
    /// </summary>
    /// <param name="reason">An optional explanation recorded alongside the reset.</param>
    /// <param name="ct">A token to observe for cancellation requests.</param>
    /// <returns>A task that completes once the lock has been reset.</returns>
    Task ResetLockAsync(string? reason, CancellationToken ct = default);
}
