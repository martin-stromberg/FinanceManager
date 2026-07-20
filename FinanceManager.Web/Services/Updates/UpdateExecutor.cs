#pragma warning disable CS1591
using FinanceManager.Shared.Dtos.Update;
using Microsoft.Extensions.Options;

namespace FinanceManager.Web.Services.Updates;

public sealed class UpdateExecutor : IUpdateExecutor
{
    private readonly IUpdateFileStore _fileStore;
    private readonly IUpdateServiceResolver _serviceResolver;
    private readonly IUpdateScriptGenerator _scriptGenerator;
    private readonly IUpdateProcessRunner _processRunner;
    private readonly IUpdateHostTerminator _hostTerminator;
    private readonly IUpdateValidator _validator;
    private readonly UpdateOptions _options;

    public UpdateExecutor(
        IUpdateFileStore fileStore,
        IUpdateServiceResolver serviceResolver,
        IUpdateScriptGenerator scriptGenerator,
        IUpdateProcessRunner processRunner,
        IUpdateHostTerminator hostTerminator,
        IUpdateValidator validator,
        IOptions<UpdateOptions> options)
    {
        _fileStore = fileStore;
        _serviceResolver = serviceResolver;
        _scriptGenerator = scriptGenerator;
        _processRunner = processRunner;
        _hostTerminator = hostTerminator;
        _validator = validator;
        _options = options.Value;
    }

    public bool IsInstallRunning { get; private set; }

    public async Task<UpdateStatusDto> StartAsync(UpdateSettingsDto settings, UpdateStatusDto status, CancellationToken ct = default)
    {
        if (IsInstallRunning)
        {
            throw new InvalidOperationException("An update installation is already running.");
        }

        var asset = status.AvailableUpdate?.Assets.FirstOrDefault(a => a.AssetName == status.DownloadedAssetName)
            ?? status.AvailableUpdate?.Assets.FirstOrDefault();
        if (asset is null || string.IsNullOrWhiteSpace(status.DownloadedAssetName))
        {
            throw new FileNotFoundException("No downloaded update package is ready.");
        }

        if (!await _fileStore.TryCreateLockAsync(ct))
        {
            throw new IOException("An update lock is already active.");
        }

        UpdateStatusDto? installing = null;
        try
        {
            IsInstallRunning = true;
            var zipPath = _fileStore.PendingAssetPath(status.DownloadedAssetName);
            await _validator.ValidateDownloadedAssetAsync(asset, zipPath, _options.MaxAssetBytes, ct);
            var target = _serviceResolver.Resolve(settings);
            var scriptPath = await _scriptGenerator.GenerateAsync(asset, zipPath, settings, target, ct);
            installing = status with
            {
                Status = UpdateStatusKind.Installing,
                IsLocked = true,
                LockCreatedAt = await _fileStore.GetLockCreatedAtAsync(ct),
                LastError = null
            };
            await _fileStore.WriteStatusAsync(installing, ct);
            _processRunner.StartScript(scriptPath);
            _hostTerminator.StopApplication();
            return installing;
        }
        catch (Exception ex)
        {
            IsInstallRunning = false;
            await _fileStore.DeleteLockAsync(CancellationToken.None);
            var failed = (installing ?? status) with
            {
                Status = UpdateStatusKind.Failed,
                IsLocked = false,
                LockCreatedAt = null,
                LastError = ex.Message
            };
            await _fileStore.WriteStatusAsync(failed, CancellationToken.None);

            throw;
        }
    }
}
#pragma warning restore CS1591
