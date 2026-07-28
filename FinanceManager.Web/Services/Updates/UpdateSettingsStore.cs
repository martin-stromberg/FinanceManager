#pragma warning disable CS1591
using System.Text.Json;
using FinanceManager.Shared.Dtos.Update;
using Microsoft.Extensions.Options;

namespace FinanceManager.Web.Services.Updates;

public sealed class UpdateSettingsStore : IUpdateSettingsStore
{
    private const string FixedRepositoryOwner = "martin-stromberg";
    private const string FixedRepositoryName = "FinanceManager";
    private const string FixedManifestAssetName = "update.json";
    private const string FixedWorkingDirectory = "updates";

    private readonly UpdateOptions _options;
    private readonly IUpdateFileStore _fileStore;

    public UpdateSettingsStore(IOptions<UpdateOptions> options, IUpdateFileStore fileStore)
    {
        _options = options.Value;
        _fileStore = fileStore;
    }

    public async Task<UpdateSettingsDto> GetAsync(CancellationToken ct = default)
    {
        await _fileStore.EnsureAsync(ct);
        var settings = await ReadSettingsAsync(ct) ?? Defaults();
        _fileStore.UseWorkingDirectory(settings.WorkingDirectory);
        await _fileStore.EnsureAsync(ct);
        return settings;
    }

    public async Task<UpdateSettingsDto> SaveAsync(UpdateSettingsUpdateRequest request, CancellationToken ct = default)
    {
        var dto = Normalize(request);
        _fileStore.UseWorkingDirectory(dto.WorkingDirectory);
        await _fileStore.EnsureAsync(ct);
        await JsonFileStore.WriteAtomicAsync(_fileStore.SettingsPath, dto, ct);
        return dto;
    }

    public async Task<UpdateSettingsDto> SaveScheduleAsync(TimeOnly? scheduledInstallTime, CancellationToken ct = default)
    {
        var current = await GetAsync(ct);
        var updated = current with { ScheduledInstallTime = scheduledInstallTime };
        _fileStore.UseWorkingDirectory(updated.WorkingDirectory);
        await JsonFileStore.WriteAtomicAsync(_fileStore.SettingsPath, updated, ct);
        return updated;
    }

    private UpdateSettingsDto Defaults()
        => new(
            _options.Enabled,
            Math.Max(1, _options.CheckIntervalMinutes),
            FixedRepositoryOwner,
            FixedRepositoryName,
            FixedManifestAssetName,
            null,
            TrimToNull(_options.ServiceName),
            TrimToNull(_options.ExecutablePath),
            FixedWorkingDirectory,
            NormalizeHealthTimeout());

    private UpdateSettingsDto Normalize(UpdateSettingsUpdateRequest request)
        => new(
            request.Enabled,
            Math.Clamp(request.CheckIntervalMinutes, 1, 24 * 60),
            FixedRepositoryOwner,
            FixedRepositoryName,
            FixedManifestAssetName,
            request.ScheduledInstallTime,
            TrimToNull(request.ServiceName),
            null,
            FixedWorkingDirectory,
            NormalizeHealthTimeout());

    private async Task<UpdateSettingsDto?> ReadSettingsAsync(CancellationToken ct)
    {
        if (!File.Exists(_fileStore.SettingsPath))
        {
            return null;
        }

        await using var stream = File.OpenRead(_fileStore.SettingsPath);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        if (document.RootElement.TryGetProperty("windowsServiceName", out _) ||
            document.RootElement.TryGetProperty("linuxServiceName", out _))
        {
            var legacy = document.Deserialize<LegacyUpdateSettingsDto>(JsonFileStore.JsonOptions);
            if (legacy is null)
            {
                return null;
            }

            return new UpdateSettingsDto(
                legacy.Enabled,
                Math.Clamp(legacy.CheckIntervalMinutes, 1, 24 * 60),
                FixedRepositoryOwner,
                FixedRepositoryName,
                FixedManifestAssetName,
                legacy.ScheduledInstallTime,
                TrimToNull(legacy.ServiceName) ?? TrimToNull(legacy.WindowsServiceName) ?? TrimToNull(legacy.LinuxServiceName),
                TrimToNull(legacy.ExecutablePath),
                FixedWorkingDirectory,
                NormalizeHealthTimeout());
        }

        var settings = document.Deserialize<UpdateSettingsDto>(JsonFileStore.JsonOptions);
        return settings is null
            ? null
            : settings with
            {
                RepositoryOwner = FixedRepositoryOwner,
                RepositoryName = FixedRepositoryName,
                ManifestAssetName = FixedManifestAssetName,
                WorkingDirectory = FixedWorkingDirectory,
                HealthTimeoutSeconds = NormalizeHealthTimeout()
            };
    }

    private static string? TrimToNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private int NormalizeHealthTimeout()
        => Math.Clamp(_options.HealthTimeoutSeconds <= 0 ? 120 : _options.HealthTimeoutSeconds, 10, 600);

    private sealed record LegacyUpdateSettingsDto(
        bool Enabled,
        int CheckIntervalMinutes,
        string? RepositoryOwner,
        string? RepositoryName,
        string? ManifestAssetName,
        TimeOnly? ScheduledInstallTime,
        string? ServiceName,
        string? WindowsServiceName,
        string? LinuxServiceName,
        string? ExecutablePath,
        string? WorkingDirectory,
        int HealthTimeoutSeconds);
}
#pragma warning restore CS1591
