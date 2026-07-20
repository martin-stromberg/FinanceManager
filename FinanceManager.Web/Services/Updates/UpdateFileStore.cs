#pragma warning disable CS1591
using System.Globalization;
using FinanceManager.Shared.Dtos.Update;
using Microsoft.Extensions.Options;

namespace FinanceManager.Web.Services.Updates;

public sealed class UpdateFileStore : IUpdateFileStore
{
    private readonly IWebHostEnvironment _environment;
    private readonly UpdateOptions _options;
    // Intentionally resolved once at construction time (before any UseWorkingDirectory override) and never
    // recomputed: settings.json persists the working directory itself, so its own location must stay fixed
    // at the originally configured directory. Otherwise, after a process restart, the singleton would not
    // know where to look for the persisted working directory before having read it.
    private readonly string _settingsDirectory;
    private string? _workingDirectory;

    public UpdateFileStore(IWebHostEnvironment environment, IOptions<UpdateOptions> options)
    {
        _environment = environment;
        _options = options.Value;
        _settingsDirectory = ResolveFullPath(ResolveConfiguredWorkingDirectory());
    }

    public string RootDirectory => ResolveFullPath(ResolveConfiguredWorkingDirectory());
    public string PendingDirectory => Path.Combine(RootDirectory, "pending");
    public string StagingDirectory => Path.Combine(RootDirectory, "staging");
    public string SettingsPath => Path.Combine(_settingsDirectory, "settings.json");
    public string StatusPath => Path.Combine(RootDirectory, "status.json");
    public string LockPath => Path.Combine(RootDirectory, "update.lock");

    public string ScriptPath(string extension) => Path.Combine(PendingDirectory, $"update.{extension.TrimStart('.')}");

    public void UseWorkingDirectory(string workingDirectory)
    {
        if (string.IsNullOrWhiteSpace(workingDirectory))
        {
            throw new InvalidOperationException("Working directory must not be empty.");
        }

        _workingDirectory = workingDirectory.Trim();
    }

    public string PendingAssetPath(string assetName)
    {
        var safeName = Path.GetFileName(assetName);
        if (!string.Equals(assetName, safeName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Asset name must not contain path segments.");
        }

        return Path.Combine(PendingDirectory, safeName);
    }

    public Task EnsureAsync(CancellationToken ct = default)
    {
        Directory.CreateDirectory(RootDirectory);
        Directory.CreateDirectory(PendingDirectory);
        Directory.CreateDirectory(StagingDirectory);
        return Task.CompletedTask;
    }

    public Task<UpdateStatusDto?> ReadStatusAsync(CancellationToken ct = default)
        => JsonFileStore.ReadAsync<UpdateStatusDto>(StatusPath, ct);

    public async Task WriteStatusAsync(UpdateStatusDto status, CancellationToken ct = default)
    {
        await EnsureAsync(ct);
        await JsonFileStore.WriteAtomicAsync(StatusPath, status, ct);
    }

    public async Task<DateTimeOffset?> GetLockCreatedAtAsync(CancellationToken ct = default)
    {
        if (!File.Exists(LockPath))
        {
            return null;
        }

        try
        {
            using var reader = new StreamReader(LockPath);
            var firstLine = await reader.ReadLineAsync(ct);
            if (!string.IsNullOrWhiteSpace(firstLine)
                && DateTimeOffset.TryParse(firstLine, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
            {
                return parsed;
            }
        }
        catch (IOException)
        {
        }

        return File.GetLastWriteTimeUtc(LockPath);
    }

    public async Task<bool> TryCreateLockAsync(CancellationToken ct = default)
    {
        await EnsureAsync(ct);
        try
        {
            await using var stream = new FileStream(LockPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await using var writer = new StreamWriter(stream);
            await writer.WriteLineAsync(DateTimeOffset.UtcNow.ToString("O").AsMemory(), ct);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
    }

    public Task<bool> DeleteLockAsync(CancellationToken ct = default)
    {
        if (!File.Exists(LockPath))
        {
            return Task.FromResult(false);
        }

        File.Delete(LockPath);
        return Task.FromResult(true);
    }

    private string ResolveConfiguredWorkingDirectory()
    {
        if (!string.IsNullOrWhiteSpace(_workingDirectory))
        {
            return _workingDirectory;
        }

        return string.IsNullOrWhiteSpace(_options.WorkingDirectory) ? "updates" : _options.WorkingDirectory;
    }

    private string ResolveFullPath(string configuredPath)
    {
        var root = _environment.ContentRootPath;
        var candidate = Path.IsPathRooted(configuredPath)
            ? Path.GetFullPath(configuredPath)
            : Path.GetFullPath(Path.Combine(root, configuredPath));
        return candidate;
    }
}
#pragma warning restore CS1591
