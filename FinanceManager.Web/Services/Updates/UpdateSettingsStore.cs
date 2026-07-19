#pragma warning disable CS1591
using FinanceManager.Shared.Dtos.Update;
using Microsoft.Extensions.Options;

namespace FinanceManager.Web.Services.Updates;

public sealed class UpdateSettingsStore : IUpdateSettingsStore
{
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
        var settings = await JsonFileStore.ReadAsync<UpdateSettingsDto>(_fileStore.SettingsPath, ct) ?? Defaults();
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
            NormalizeRepositoryPart(_options.RepositoryOwner, "martin-stromberg"),
            NormalizeRepositoryPart(_options.RepositoryName, "FinanceManager"),
            string.IsNullOrWhiteSpace(_options.ManifestAssetName) ? "update.json" : _options.ManifestAssetName.Trim(),
            null,
            TrimToNull(_options.WindowsServiceName),
            TrimToNull(_options.LinuxServiceName),
            TrimToNull(_options.ExecutablePath),
            NormalizeWorkingDirectory(_options.WorkingDirectory),
            Math.Clamp(_options.HealthTimeoutSeconds, 10, 600));

    private UpdateSettingsDto Normalize(UpdateSettingsUpdateRequest request)
        => new(
            request.Enabled,
            Math.Clamp(request.CheckIntervalMinutes, 1, 24 * 60),
            NormalizeRepositoryPart(request.RepositoryOwner, "martin-stromberg"),
            NormalizeRepositoryPart(request.RepositoryName, "FinanceManager"),
            string.IsNullOrWhiteSpace(request.ManifestAssetName) ? "update.json" : request.ManifestAssetName.Trim(),
            request.ScheduledInstallTime,
            TrimToNull(request.WindowsServiceName),
            TrimToNull(request.LinuxServiceName),
            TrimToNull(request.ExecutablePath),
            NormalizeWorkingDirectory(request.WorkingDirectory),
            Math.Clamp(request.HealthTimeoutSeconds, 10, 600));

    private static string NormalizeRepositoryPart(string? value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static string? TrimToNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string NormalizeWorkingDirectory(string? value)
    {
        var path = string.IsNullOrWhiteSpace(value) ? "updates" : value.Trim();
        if (path.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
        {
            throw new InvalidOperationException("Working directory contains invalid path characters.");
        }

        return path;
    }
}
#pragma warning restore CS1591
