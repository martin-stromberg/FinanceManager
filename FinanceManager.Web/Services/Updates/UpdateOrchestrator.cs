#pragma warning disable CS1591
using FinanceManager.Shared.Dtos.Update;
using Microsoft.Extensions.Options;

namespace FinanceManager.Web.Services.Updates;

public sealed class UpdateOrchestrator : IUpdateOrchestrator
{
    private readonly IUpdateSettingsStore _settingsStore;
    private readonly IInstalledReleaseMetadataProvider _installedProvider;
    private readonly IUpdateManifestClient _manifestClient;
    private readonly IUpdatePlatformResolver _platformResolver;
    private readonly IUpdateFileStore _fileStore;
    private readonly IUpdateValidator _validator;
    private readonly IUpdateExecutor _executor;
    private readonly UpdateOptions _options;

    public UpdateOrchestrator(
        IUpdateSettingsStore settingsStore,
        IInstalledReleaseMetadataProvider installedProvider,
        IUpdateManifestClient manifestClient,
        IUpdatePlatformResolver platformResolver,
        IUpdateFileStore fileStore,
        IUpdateValidator validator,
        IUpdateExecutor executor,
        IOptions<UpdateOptions> options)
    {
        _settingsStore = settingsStore;
        _installedProvider = installedProvider;
        _manifestClient = manifestClient;
        _platformResolver = platformResolver;
        _fileStore = fileStore;
        _validator = validator;
        _executor = executor;
        _options = options.Value;
    }

    public async Task<UpdateStatusDto> GetStatusAsync(CancellationToken ct = default)
    {
        var installed = await _installedProvider.GetAsync(ct);
        await _settingsStore.GetAsync(ct);
        var stored = await _fileStore.ReadStatusAsync(ct);
        return await WithRuntimeStateAsync(stored ?? EmptyStatus(installed), installed, ct);
    }

    public Task<UpdateSettingsDto> GetSettingsAsync(CancellationToken ct = default)
        => _settingsStore.GetAsync(ct);

    public Task<UpdateSettingsDto> SaveSettingsAsync(UpdateSettingsUpdateRequest request, CancellationToken ct = default)
        => _settingsStore.SaveAsync(request, ct);

    public Task<UpdateSettingsDto> ScheduleAsync(TimeOnly? scheduledInstallTime, CancellationToken ct = default)
        => _settingsStore.SaveScheduleAsync(scheduledInstallTime, ct);

    public async Task<UpdateCheckResultDto> CheckAsync(CancellationToken ct = default)
    {
        var settings = await _settingsStore.GetAsync(ct);
        var installed = await _installedProvider.GetAsync(ct);
        var checking = EmptyStatus(installed) with { Status = UpdateStatusKind.Checking, LastCheckedAt = DateTimeOffset.UtcNow };
        await _fileStore.WriteStatusAsync(checking, ct);

        try
        {
            var manifest = await _manifestClient.GetManifestAsync(settings, ct);
            _validator.ValidateManifest(manifest, settings, _platformResolver.CurrentPlatform);
            var asset = _platformResolver.SelectAsset(manifest)
                ?? throw new InvalidOperationException($"No update asset for runtime '{_platformResolver.CurrentRuntimeIdentifier}'.");
            var isNewer = _validator.IsNewerVersion(installed.Version, manifest.Version);
            var status = checking with
            {
                Status = isNewer ? UpdateStatusKind.Downloading : UpdateStatusKind.NoUpdate,
                AvailableVersion = isNewer ? manifest.Version : null,
                AvailableUpdate = isNewer ? manifest : null,
                LastError = isNewer ? null : string.IsNullOrWhiteSpace(installed.Version) ? "Installed version is unknown; automatic update is disabled." : null
            };

            if (isNewer)
            {
                var targetPath = _fileStore.PendingAssetPath(asset.AssetName);
                await _manifestClient.DownloadAssetAsync(asset, targetPath, _options.MaxAssetBytes, ct);
                await _validator.ValidateDownloadedAssetAsync(asset, targetPath, _options.MaxAssetBytes, ct);
                status = status with { Status = UpdateStatusKind.Ready, DownloadedAssetName = asset.AssetName };
            }

            await _fileStore.WriteStatusAsync(status, ct);
            return new UpdateCheckResultDto(isNewer, await WithRuntimeStateAsync(status, installed, ct), isNewer ? "Update package is ready." : "No newer update is available.");
        }
        catch (Exception ex)
        {
            var failed = checking with { Status = UpdateStatusKind.Failed, LastError = ex.Message };
            await _fileStore.WriteStatusAsync(failed, ct);
            return new UpdateCheckResultDto(false, await WithRuntimeStateAsync(failed, installed, ct), ex.Message);
        }
    }

    public async Task<UpdateStatusDto> StartInstallAsync(bool confirmDowntime, CancellationToken ct = default)
    {
        if (!confirmDowntime)
        {
            throw new ArgumentException("Downtime confirmation is required.", nameof(confirmDowntime));
        }

        var settings = await _settingsStore.GetAsync(ct);
        var status = await GetStatusAsync(ct);
        if (status.IsLocked || status.Status == UpdateStatusKind.Installing)
        {
            throw new IOException("An update lock is active.");
        }

        if (status.Status != UpdateStatusKind.Ready || string.IsNullOrWhiteSpace(status.DownloadedAssetName))
        {
            throw new FileNotFoundException("No ready update package is available.");
        }

        return await _executor.StartAsync(settings, status, ct);
    }

    public async Task ResetLockAsync(string? reason, CancellationToken ct = default)
    {
        if (_executor.IsInstallRunning)
        {
            throw new IOException("The current process still owns an update installation.");
        }

        await _fileStore.DeleteLockAsync(ct);
        var status = await GetStatusAsync(ct);
        await _fileStore.WriteStatusAsync(status with { IsLocked = false, LockCreatedAt = null, LastError = string.IsNullOrWhiteSpace(reason) ? status.LastError : $"Lock reset: {reason}" }, ct);
    }

    private UpdateStatusDto EmptyStatus(InstalledReleaseMetadataDto installed)
        => new(
            UpdateStatusKind.NoUpdate,
            installed.Version,
            installed.PublishedAt,
            null,
            _platformResolver.CurrentRuntimeIdentifier,
            null,
            null,
            null,
            false,
            null,
            null,
            null);

    private async Task<UpdateStatusDto> WithRuntimeStateAsync(UpdateStatusDto status, InstalledReleaseMetadataDto installed, CancellationToken ct)
    {
        var settings = await _settingsStore.GetAsync(ct);
        var lockCreatedAt = await _fileStore.GetLockCreatedAtAsync(ct);
        return status with
        {
            InstalledVersion = installed.Version,
            InstalledReleasePublishedAt = installed.PublishedAt,
            CurrentPlatform = _platformResolver.CurrentRuntimeIdentifier,
            IsLocked = lockCreatedAt.HasValue,
            LockCreatedAt = lockCreatedAt,
            ScheduledInstallTime = settings.ScheduledInstallTime
        };
    }
}
#pragma warning restore CS1591
