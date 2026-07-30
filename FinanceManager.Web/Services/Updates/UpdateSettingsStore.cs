using System.Text.Json;
using FinanceManager.Shared.Dtos.Update;
using Microsoft.Extensions.Options;
using SoftwareSchmiede.AutoUpdate;

namespace FinanceManager.Web.Services.Updates;

/// <summary>
/// Default <see cref="IUpdateSettingsStore"/> implementation. Persists settings as JSON alongside the auto-update
/// library's package directory and mirrors runtime-relevant fields into the library's <see cref="AutoUpdateOptions"/>.
/// </summary>
public sealed class UpdateSettingsStore : IUpdateSettingsStore
{
    private readonly UpdateOptions _webOptions;
    private readonly AutoUpdateOptions _autoUpdateOptions;
    private readonly IAutoUpdatePackageStore _packageStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateSettingsStore"/> class.
    /// </summary>
    /// <param name="webOptions">The host-specific update options, used for repository and manifest defaults.</param>
    /// <param name="autoUpdateOptions">The auto-update library's runtime-mutable options, used for shared defaults.</param>
    /// <param name="packageStore">Used to resolve the path of the settings file.</param>
    public UpdateSettingsStore(IOptions<UpdateOptions> webOptions, AutoUpdateOptions autoUpdateOptions, IAutoUpdatePackageStore packageStore)
    {
        _webOptions = webOptions.Value;
        _autoUpdateOptions = autoUpdateOptions;
        _packageStore = packageStore;
    }

    private string SettingsPath => Path.Combine(_packageStore.RootDirectory, "settings.json");

    /// <inheritdoc />
    public async Task<UpdateSettingsDto> GetAsync(CancellationToken ct = default)
    {
        await _packageStore.EnsureAsync(ct);
        return await ReadSettingsAsync(ct) ?? Defaults();
    }

    /// <inheritdoc />
    public async Task<UpdateSettingsDto> SaveAsync(UpdateSettingsUpdateRequest request, CancellationToken ct = default)
    {
        var dto = Build(request);
        await _packageStore.EnsureAsync(ct);
        await WriteAtomicAsync(dto, ct);
        return dto;
    }

    /// <inheritdoc />
    public async Task<UpdateSettingsDto> SaveScheduleAsync(TimeOnly? scheduledInstallTime, CancellationToken ct = default)
    {
        var current = await GetAsync(ct);
        var updated = current with { ScheduledInstallTime = scheduledInstallTime };
        await WriteAtomicAsync(updated, ct);
        return updated;
    }

    /// <inheritdoc />
    public void ApplyToOptions(UpdateSettingsDto settings)
        => AutoUpdateOptionsMapper.ApplySettings(_autoUpdateOptions, settings);

    private UpdateSettingsDto Defaults()
    {
        var raw = AutoUpdateOptionsMapper.ToSettingsDto(
            _autoUpdateOptions,
            _webOptions.RepositoryOwner,
            _webOptions.RepositoryName,
            _webOptions.ManifestAssetName);

        return Build(new UpdateSettingsUpdateRequest(
            raw.Enabled,
            raw.CheckIntervalMinutes,
            raw.RepositoryOwner,
            raw.RepositoryName,
            raw.ManifestAssetName,
            raw.ScheduledInstallTime,
            raw.ServiceName,
            raw.ExecutablePath,
            raw.WorkingDirectory,
            raw.HealthTimeoutSeconds));
    }

    /// <summary>
    /// Normalizes and clamps <paramref name="request"/> into a persistable <see cref="UpdateSettingsDto"/>: blank
    /// repository/manifest values fall back to the host defaults, the check interval and health timeout are
    /// clamped to their valid ranges, and optional string fields are trimmed to <see langword="null"/>.
    /// </summary>
    /// <param name="request">The settings to normalize.</param>
    /// <returns>The normalized settings.</returns>
    private UpdateSettingsDto Build(UpdateSettingsUpdateRequest request)
        => new(
            request.Enabled,
            Math.Clamp(request.CheckIntervalMinutes, 1, 24 * 60),
            NormalizeRepositoryPart(request.RepositoryOwner, _webOptions.RepositoryOwner),
            NormalizeRepositoryPart(request.RepositoryName, _webOptions.RepositoryName),
            string.IsNullOrWhiteSpace(request.ManifestAssetName) ? _webOptions.ManifestAssetName : request.ManifestAssetName.Trim(),
            request.ScheduledInstallTime,
            TrimToNull(request.ServiceName),
            TrimToNull(request.ExecutablePath),
            NormalizeWorkingDirectory(request.WorkingDirectory),
            Math.Clamp(request.HealthTimeoutSeconds, AutoUpdateOptions.MinHealthTimeoutSeconds, AutoUpdateOptions.MaxHealthTimeoutSeconds));

    private async Task<UpdateSettingsDto?> ReadSettingsAsync(CancellationToken ct)
    {
        if (!File.Exists(SettingsPath))
        {
            return null;
        }

        await using var stream = File.OpenRead(SettingsPath);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        if (document.RootElement.TryGetProperty("windowsServiceName", out _) ||
            document.RootElement.TryGetProperty("linuxServiceName", out _))
        {
            var legacy = document.Deserialize<LegacyUpdateSettingsDto>(JsonFileStore.JsonOptions);
            if (legacy is null)
            {
                return null;
            }

            var legacyServiceName = TrimToNull(legacy.ServiceName) ?? TrimToNull(legacy.WindowsServiceName) ?? TrimToNull(legacy.LinuxServiceName);
            return Build(new UpdateSettingsUpdateRequest(
                legacy.Enabled,
                legacy.CheckIntervalMinutes,
                legacy.RepositoryOwner,
                legacy.RepositoryName,
                legacy.ManifestAssetName,
                legacy.ScheduledInstallTime,
                legacyServiceName,
                legacy.ExecutablePath,
                legacy.WorkingDirectory,
                legacy.HealthTimeoutSeconds));
        }

        return document.Deserialize<UpdateSettingsDto>(JsonFileStore.JsonOptions);
    }

    private Task WriteAtomicAsync(UpdateSettingsDto settings, CancellationToken ct)
        => JsonFileStore.WriteAtomicAsync(SettingsPath, settings, ct);

    private static string NormalizeRepositoryPart(string? value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static string? TrimToNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private string NormalizeWorkingDirectory(string? value)
    {
        var path = string.IsNullOrWhiteSpace(value) ? _webOptions.WorkingDirectory : value.Trim();
        if (path.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
        {
            throw new InvalidOperationException("Working directory contains invalid path characters.");
        }

        return path;
    }

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
