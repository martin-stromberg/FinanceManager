#pragma warning disable CS1591
using FinanceManager.Shared.Dtos.Update;

namespace FinanceManager.Web.Services.Updates;

public interface IUpdateSettingsStore
{
    Task<UpdateSettingsDto> GetAsync(CancellationToken ct = default);
    Task<UpdateSettingsDto> SaveAsync(UpdateSettingsUpdateRequest request, CancellationToken ct = default);
    Task<UpdateSettingsDto> SaveScheduleAsync(TimeOnly? scheduledInstallTime, CancellationToken ct = default);
}

public interface IInstalledReleaseMetadataProvider
{
    Task<InstalledReleaseMetadataDto> GetAsync(CancellationToken ct = default);
}

public interface IUpdateManifestClient
{
    Task<UpdateMetadataDto> GetManifestAsync(UpdateSettingsDto settings, CancellationToken ct = default);
    Task DownloadAssetAsync(UpdateAssetDto asset, string targetPath, long maxBytes, CancellationToken ct = default);
}

public interface IUpdatePlatformResolver
{
    string CurrentRuntimeIdentifier { get; }
    string CurrentPlatform { get; }
    UpdateAssetDto? SelectAsset(UpdateMetadataDto manifest);
}

public interface IUpdateFileStore
{
    string RootDirectory { get; }
    string PendingDirectory { get; }
    string StagingDirectory { get; }
    string SettingsPath { get; }
    string StatusPath { get; }
    string LockPath { get; }
    string ScriptPath(string extension);
    string PendingAssetPath(string assetName);
    void UseWorkingDirectory(string workingDirectory);
    Task EnsureAsync(CancellationToken ct = default);
    Task<UpdateStatusDto?> ReadStatusAsync(CancellationToken ct = default);
    Task WriteStatusAsync(UpdateStatusDto status, CancellationToken ct = default);
    Task<DateTimeOffset?> GetLockCreatedAtAsync(CancellationToken ct = default);
    Task<bool> TryCreateLockAsync(CancellationToken ct = default);
    Task<bool> DeleteLockAsync(CancellationToken ct = default);
}

public interface IUpdateValidator
{
    bool IsNewerVersion(string? installedVersion, string availableVersion);
    void ValidateManifest(UpdateMetadataDto manifest, UpdateSettingsDto settings, string currentPlatform);
    Task ValidateDownloadedAssetAsync(UpdateAssetDto asset, string path, long maxBytes, CancellationToken ct = default);
}

public interface IUpdateScriptGenerator
{
    Task<string> GenerateAsync(UpdateAssetDto asset, string zipPath, UpdateSettingsDto settings, UpdateInstallationTarget target, CancellationToken ct = default);
}

public sealed record UpdateInstallationTarget(
    string Platform,
    string? ServiceName,
    string? ExecutablePath);

public interface IUpdateServiceResolver
{
    UpdateInstallationTarget Resolve(UpdateSettingsDto settings);
}

public interface IUpdateServiceProbe
{
    IReadOnlyList<string> FindWindowsServicesForCurrentProcess();
    IReadOnlyList<string> FindLinuxServicesForCurrentProcess();
}

public interface IUpdateProcessRunner
{
    void StartScript(string scriptPath);
}

public interface IUpdateHostTerminator
{
    void StopApplication();
}

public interface IUpdateExecutor
{
    Task<UpdateStatusDto> StartAsync(UpdateSettingsDto settings, UpdateStatusDto status, CancellationToken ct = default);
    bool IsInstallRunning { get; }
}

public interface IUpdateOrchestrator
{
    Task<UpdateStatusDto> GetStatusAsync(CancellationToken ct = default);
    Task<UpdateSettingsDto> GetSettingsAsync(CancellationToken ct = default);
    Task<UpdateSettingsDto> SaveSettingsAsync(UpdateSettingsUpdateRequest request, CancellationToken ct = default);
    Task<UpdateSettingsDto> ScheduleAsync(TimeOnly? scheduledInstallTime, CancellationToken ct = default);
    Task<UpdateCheckResultDto> CheckAsync(CancellationToken ct = default);
    Task<UpdateStatusDto> StartInstallAsync(bool confirmDowntime, CancellationToken ct = default);
    Task ResetLockAsync(string? reason, CancellationToken ct = default);
}
#pragma warning restore CS1591
