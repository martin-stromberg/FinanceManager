#pragma warning disable CS1591
namespace FinanceManager.Shared.Dtos.Update;

public enum UpdateStatusKind
{
    NoUpdate = 0,
    Checking = 1,
    Available = 2,
    Downloading = 3,
    Ready = 4,
    Installing = 5,
    Failed = 6
}

public sealed record UpdateMetadataDto(
    string Version,
    string? ReleaseNotes,
    DateTimeOffset? PublishedAt,
    string RepositoryOwner,
    string RepositoryName,
    IReadOnlyList<UpdateAssetDto> Assets);

public sealed record UpdateAssetDto(
    string Platform,
    string RuntimeIdentifier,
    string AssetName,
    string AssetUrl,
    string Sha256,
    long SizeBytes);

public sealed record InstalledReleaseMetadataDto(
    string? Version,
    DateTimeOffset? PublishedAt,
    string? CommitSha,
    string? Repository,
    string? RuntimeIdentifier);

public sealed record UpdateStatusDto(
    UpdateStatusKind Status,
    string? InstalledVersion,
    DateTimeOffset? InstalledReleasePublishedAt,
    string? AvailableVersion,
    string CurrentPlatform,
    DateTimeOffset? LastCheckedAt,
    string? LastError,
    string? DownloadedAssetName,
    bool IsLocked,
    DateTimeOffset? LockCreatedAt,
    TimeOnly? ScheduledInstallTime,
    UpdateMetadataDto? AvailableUpdate);

public sealed record UpdateSettingsDto(
    bool Enabled,
    int CheckIntervalMinutes,
    string RepositoryOwner,
    string RepositoryName,
    string ManifestAssetName,
    TimeOnly? ScheduledInstallTime,
    string? ServiceName,
    string? ExecutablePath,
    string WorkingDirectory,
    int HealthTimeoutSeconds);

public sealed record UpdateSettingsUpdateRequest(
    bool Enabled,
    int CheckIntervalMinutes,
    string? RepositoryOwner,
    string? RepositoryName,
    string? ManifestAssetName,
    TimeOnly? ScheduledInstallTime,
    string? ServiceName,
    string? ExecutablePath,
    string? WorkingDirectory,
    int HealthTimeoutSeconds);

public sealed record UpdateScheduleRequest(TimeOnly? ScheduledInstallTime);

public sealed record UpdateStartRequest(bool ConfirmDowntime);

public sealed record UpdateLockResetRequest(string? Reason);

public sealed record UpdateCheckResultDto(
    bool UpdateAvailable,
    UpdateStatusDto Status,
    string? Message);
#pragma warning restore CS1591
