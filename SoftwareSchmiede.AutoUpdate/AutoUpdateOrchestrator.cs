namespace SoftwareSchmiede.AutoUpdate;

/// <summary>
/// Default <see cref="IAutoUpdateOrchestrator"/> implementation. Coordinates checking, downloading and
/// installing update packages, raises the lifecycle events in the documented order, persists status through
/// <see cref="AutoUpdateStatusService"/> and reports every error via <see cref="IAutoUpdateEventAggregator.ErrorOccured"/>
/// instead of throwing. Registered as a singleton; all Check/Download/Install operations are serialized through
/// an internal semaphore.
/// </summary>
public sealed class AutoUpdateOrchestrator : IAutoUpdateOrchestrator, IDisposable
{
    private readonly AutoUpdateOptions _options;
    private readonly IAutoUpdateEventAggregator _events;
    private readonly AutoUpdateStatusService _statusService;
    private readonly IAutoUpdatePackageStore _packageStore;
    private readonly IAutoUpdatePackageValidator _validator;
    private readonly IInstalledVersionProvider _installedVersionProvider;
    private readonly IAutoUpdateInstaller _installer;
    private readonly IAutoUpdateHostTerminator _hostTerminator;
    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    /// <summary>
    /// Initializes a new instance of the <see cref="AutoUpdateOrchestrator"/> class.
    /// </summary>
    /// <param name="options">The runtime-mutable auto-update options, including the configured source.</param>
    /// <param name="events">Used to raise the lifecycle events of the update workflow.</param>
    /// <param name="statusService">Used to read and persist the current status snapshot.</param>
    /// <param name="packageStore">Used to resolve package paths and manage the installation lock.</param>
    /// <param name="validator">Used to compare versions and validate downloaded packages.</param>
    /// <param name="installedVersionProvider">Used to determine the currently installed version.</param>
    /// <param name="installer">Used to prepare and start the installation.</param>
    /// <param name="hostTerminator">Used to stop the host application after the installation script starts, if configured.</param>
    /// <param name="timeProvider">Used to time-stamp status transitions.</param>
    public AutoUpdateOrchestrator(
        AutoUpdateOptions options,
        IAutoUpdateEventAggregator events,
        AutoUpdateStatusService statusService,
        IAutoUpdatePackageStore packageStore,
        IAutoUpdatePackageValidator validator,
        IInstalledVersionProvider installedVersionProvider,
        IAutoUpdateInstaller installer,
        IAutoUpdateHostTerminator hostTerminator,
        TimeProvider timeProvider)
    {
        _options = options;
        _events = events;
        _statusService = statusService;
        _packageStore = packageStore;
        _validator = validator;
        _installedVersionProvider = installedVersionProvider;
        _installer = installer;
        _hostTerminator = hostTerminator;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Releases the internal serialization semaphore.
    /// </summary>
    public void Dispose() => _semaphore.Dispose();

    /// <inheritdoc />
    public async Task<AutoUpdateResult> RunUpdateAsync(CancellationToken ct = default)
    {
        await _semaphore.WaitAsync(ct);
        try
        {
            await _statusService.EnsureLoadedAsync(ct);

            var checkResult = await CheckCoreAsync(ct);
            if (checkResult.Outcome != AutoUpdateOutcome.Success)
            {
                return checkResult;
            }

            if (!_options.EnableAutomaticDownload)
            {
                return new AutoUpdateResult(AutoUpdateOutcome.Skipped, checkResult.State, "Automatic download is disabled.", null);
            }

            var downloadResult = await DownloadCoreAsync(ct);
            if (downloadResult.Outcome != AutoUpdateOutcome.Success)
            {
                return downloadResult;
            }

            if (!_options.EnableAutomaticInstallation)
            {
                return new AutoUpdateResult(AutoUpdateOutcome.Skipped, downloadResult.State, "Automatic installation is disabled.", null);
            }

            return await InstallCoreAsync(ct);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <inheritdoc />
    public async Task<AutoUpdateResult> CheckForUpdateAsync(CancellationToken ct = default)
    {
        await _semaphore.WaitAsync(ct);
        try
        {
            await _statusService.EnsureLoadedAsync(ct);
            return await CheckCoreAsync(ct);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <inheritdoc />
    public async Task<AutoUpdateResult> DownloadAsync(CancellationToken ct = default)
    {
        await _semaphore.WaitAsync(ct);
        try
        {
            await _statusService.EnsureLoadedAsync(ct);
            return await DownloadCoreAsync(ct);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <inheritdoc />
    public async Task<AutoUpdateResult> InstallAsync(bool confirmDowntime, CancellationToken ct = default)
    {
        if (!confirmDowntime)
        {
            var argumentException = new ArgumentException("Downtime confirmation is required.", nameof(confirmDowntime));
            return new AutoUpdateResult(AutoUpdateOutcome.Failed, _statusService.GetSnapshot().State, argumentException.Message, argumentException);
        }

        await _semaphore.WaitAsync(ct);
        try
        {
            await _statusService.EnsureLoadedAsync(ct);
            return await InstallCoreAsync(ct);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <inheritdoc />
    public async Task<AutoUpdateStatusSnapshot> GetStatusAsync(CancellationToken ct = default)
    {
        await _semaphore.WaitAsync(ct);
        try
        {
            await _statusService.EnsureLoadedAsync(ct);
            return await ReconcileAfterRestartAsync(ct);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task<AutoUpdateResult> CheckCoreAsync(CancellationToken ct)
    {
        if (!_options.Enabled)
        {
            await _statusService.UpdateAsync(s => s with { State = AutoUpdateState.Disabled }, ct);
            return new AutoUpdateResult(AutoUpdateOutcome.Skipped, AutoUpdateState.Disabled, "Auto-update is disabled.", null);
        }

        if (_events.RaiseBeforeCheckSource(this))
        {
            await _statusService.UpdateAsync(s => s with { State = AutoUpdateState.Idle }, ct);
            return new AutoUpdateResult(AutoUpdateOutcome.Canceled, AutoUpdateState.Idle, "Source check canceled.", null);
        }

        await _statusService.UpdateAsync(s => s with { State = AutoUpdateState.Checking }, ct);

        try
        {
            var source = RequireSource();
            var checkResult = await source.CheckAsync(ct);
            var installed = await _installedVersionProvider.GetAsync(ct);
            var isNewer = checkResult.AvailableVersion is not null && _validator.IsNewerVersion(installed.Version, checkResult.AvailableVersion);
            var now = _timeProvider.GetUtcNow();

            if (!isNewer)
            {
                await _statusService.UpdateAsync(s => s with
                {
                    State = AutoUpdateState.Idle,
                    LastCheckedAt = now,
                    LastCheckResult = checkResult,
                    AvailableVersion = null
                }, ct);
                return new AutoUpdateResult(AutoUpdateOutcome.NoUpdate, AutoUpdateState.Idle, "No newer update is available.", null);
            }

            await _statusService.UpdateAsync(s => s with
            {
                State = AutoUpdateState.UpdateAvailable,
                LastCheckedAt = now,
                LastCheckResult = checkResult,
                AvailableVersion = checkResult.AvailableVersion
            }, ct);
            return new AutoUpdateResult(AutoUpdateOutcome.Success, AutoUpdateState.UpdateAvailable, "A newer version is available.", null);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return CanceledResult("Check");
        }
        catch (Exception ex)
        {
            return await FailAsync(ex, "Check", ct);
        }
    }

    private async Task<AutoUpdateResult> DownloadCoreAsync(CancellationToken ct)
    {
        var snapshot = _statusService.GetSnapshot();
        var package = snapshot.LastCheckResult?.Package;
        if (package is null)
        {
            var notFound = new InvalidOperationException("No update package is available to download.");
            return new AutoUpdateResult(AutoUpdateOutcome.Failed, snapshot.State, notFound.Message, notFound);
        }

        if (_events.RaiseBeforeDownload(this, package.Uri))
        {
            await _statusService.UpdateAsync(s => s with { State = AutoUpdateState.UpdateAvailable }, ct);
            return new AutoUpdateResult(AutoUpdateOutcome.Canceled, AutoUpdateState.UpdateAvailable, "Download canceled.", null);
        }

        try
        {
            await _statusService.UpdateAsync(s => s with { State = AutoUpdateState.Downloading }, ct);
            await _packageStore.EnsureAsync(ct);
            var targetPath = _packageStore.PendingAssetPath(package.FileName);
            var source = RequireSource();
            await source.DownloadAsync(package, targetPath, _options.MaxAssetBytes, ct);
            await _validator.ValidateDownloadedPackageAsync(package, targetPath, _options.MaxAssetBytes, ct);

            var downloadResult = new AutoUpdateDownloadResult(targetPath, new FileInfo(targetPath).Length, true);
            await _statusService.UpdateAsync(s => s with
            {
                State = AutoUpdateState.ReadyToInstall,
                LastDownloadResult = downloadResult
            }, ct);
            return new AutoUpdateResult(AutoUpdateOutcome.Success, AutoUpdateState.ReadyToInstall, "Update package is ready to install.", null);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return CanceledResult("Download");
        }
        catch (Exception ex)
        {
            return await FailAsync(ex, "Download", ct);
        }
    }

    private async Task<AutoUpdateResult> InstallCoreAsync(CancellationToken ct)
    {
        var snapshot = _statusService.GetSnapshot();
        var package = snapshot.LastCheckResult?.Package;
        if (snapshot.State != AutoUpdateState.ReadyToInstall || snapshot.LastDownloadResult is null || package is null)
        {
            var notReady = new FileNotFoundException("No update package is ready to install.");
            return new AutoUpdateResult(AutoUpdateOutcome.Failed, snapshot.State, notReady.Message, notReady);
        }

        var packageFile = new FileInfo(snapshot.LastDownloadResult.LocalPath);
        if (_events.RaiseBeforeInstall(this, packageFile))
        {
            return new AutoUpdateResult(AutoUpdateOutcome.Canceled, AutoUpdateState.ReadyToInstall, "Installation canceled.", null);
        }

        if (!await _packageStore.TryCreateLockAsync(ct))
        {
            var locked = new IOException("An update lock is already active.");
            return new AutoUpdateResult(AutoUpdateOutcome.Failed, snapshot.State, locked.Message, locked);
        }

        try
        {
            var scriptPath = await _installer.PrepareAsync(package, packageFile.FullName, ct);
            var scriptFile = new FileInfo(scriptPath);

            if (_events.RaiseBeforeStartUpdateScript(this, scriptFile))
            {
                await _packageStore.DeleteLockAsync(ct);
                return new AutoUpdateResult(AutoUpdateOutcome.Canceled, AutoUpdateState.ReadyToInstall, "Installation script start canceled.", null);
            }

            var lockCreatedAt = await _packageStore.GetLockCreatedAtAsync(ct);
            var startedAt = _timeProvider.GetUtcNow();
            var installResult = new AutoUpdateInstallResult(package.Version, scriptPath, startedAt);
            await _statusService.UpdateAsync(s => s with
            {
                State = AutoUpdateState.Installing,
                LastInstallResult = installResult,
                LastError = null,
                IsLocked = true,
                LockCreatedAt = lockCreatedAt
            }, ct);

            _installer.Start(scriptPath);
            _events.RaiseAfterStartUpdateScript(this);

            if (_options.StopHostAfterScriptStart)
            {
                _hostTerminator.StopApplication();
            }

            return new AutoUpdateResult(AutoUpdateOutcome.Success, AutoUpdateState.Installing, "Installation started.", null);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            await _packageStore.DeleteLockAsync(CancellationToken.None);
            return CanceledResult("Install");
        }
        catch (Exception ex)
        {
            await _packageStore.DeleteLockAsync(CancellationToken.None);
            return await FailAsync(ex, "Install", ct);
        }
    }

    private async Task<AutoUpdateStatusSnapshot> ReconcileAfterRestartAsync(CancellationToken ct)
    {
        var snapshot = _statusService.GetSnapshot();
        if (snapshot.State != AutoUpdateState.Installing)
        {
            return snapshot;
        }

        var lockCreatedAt = await _packageStore.GetLockCreatedAtAsync(ct);
        if (lockCreatedAt.HasValue)
        {
            return snapshot;
        }

        var installed = await _installedVersionProvider.GetAsync(ct);
        if (!string.IsNullOrWhiteSpace(snapshot.AvailableVersion) &&
            string.Equals(installed.Version, snapshot.AvailableVersion, StringComparison.Ordinal))
        {
            return await _statusService.UpdateAsync(s => s with
            {
                State = AutoUpdateState.Success,
                AvailableVersion = null,
                LastCheckResult = null,
                LastDownloadResult = null,
                LastError = null,
                IsLocked = false,
                LockCreatedAt = null
            }, ct);
        }

        return await _statusService.UpdateAsync(s => s with
        {
            State = AutoUpdateState.Failed,
            LastError = $"Installed version '{installed.Version}' does not match the expected version '{snapshot.AvailableVersion}' after the update process finished.",
            IsLocked = false,
            LockCreatedAt = null
        }, ct);
    }

    private async Task<AutoUpdateResult> FailAsync(Exception ex, string phase, CancellationToken ct)
    {
        _events.RaiseErrorOccured(this, ex, phase);
        await _statusService.UpdateAsync(s => s with { State = AutoUpdateState.Failed, LastError = ex.Message }, ct);
        return new AutoUpdateResult(AutoUpdateOutcome.Failed, AutoUpdateState.Failed, ex.Message, ex);
    }

    private AutoUpdateResult CanceledResult(string phase)
    {
        var state = _statusService.GetSnapshot().State;
        return new AutoUpdateResult(AutoUpdateOutcome.Canceled, state, $"{phase} canceled.", null);
    }

    private IAutoUpdateSource RequireSource()
        => _options.Source ?? throw new InvalidOperationException("No update source is configured.");
}
