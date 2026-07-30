using FinanceManager.Shared.Dtos.Update;
using SoftwareSchmiede.AutoUpdate;

namespace FinanceManager.Web.Services.Updates;

/// <summary>
/// Maps an <see cref="AutoUpdateStatusSnapshot"/> onto <see cref="UpdateStatusDto"/>, aggregating the installed
/// version, current settings and current platform the DTO also carries.
/// </summary>
public sealed class UpdateStatusMapper
{
    private readonly IInstalledReleaseMetadataProvider _installedProvider;
    private readonly IAutoUpdatePlatformResolver _platformResolver;
    private readonly IUpdateSettingsStore _settingsStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateStatusMapper"/> class.
    /// </summary>
    /// <param name="installedProvider">The host-specific installed release metadata provider.</param>
    /// <param name="platformResolver">Used to map the current platform onto <see cref="UpdateStatusDto.CurrentPlatform"/>.</param>
    /// <param name="settingsStore">The host-specific settings store.</param>
    public UpdateStatusMapper(IInstalledReleaseMetadataProvider installedProvider, IAutoUpdatePlatformResolver platformResolver, IUpdateSettingsStore settingsStore)
    {
        _installedProvider = installedProvider;
        _platformResolver = platformResolver;
        _settingsStore = settingsStore;
    }

    /// <summary>
    /// Maps <paramref name="snapshot"/> onto an <see cref="UpdateStatusDto"/>.
    /// </summary>
    /// <param name="snapshot">The status snapshot to map.</param>
    /// <param name="ct">A token to observe for cancellation requests.</param>
    /// <returns>The mapped status DTO.</returns>
    public async Task<UpdateStatusDto> MapAsync(AutoUpdateStatusSnapshot snapshot, CancellationToken ct = default)
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
