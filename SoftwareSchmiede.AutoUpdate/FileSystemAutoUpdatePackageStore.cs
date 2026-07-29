using System.Globalization;

namespace SoftwareSchmiede.AutoUpdate;

/// <summary>
/// File-system-backed <see cref="IAutoUpdatePackageStore"/> implementation rooted at
/// <see cref="AutoUpdateOptions.DownloadPath"/>, relative to <see cref="IAutoUpdateEnvironment.ApplicationDirectory"/>.
/// </summary>
public sealed class FileSystemAutoUpdatePackageStore : IAutoUpdatePackageStore
{
    private readonly IAutoUpdateEnvironment _environment;
    private readonly AutoUpdateOptions _options;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileSystemAutoUpdatePackageStore"/> class.
    /// </summary>
    /// <param name="environment">Provides the application's root directory.</param>
    /// <param name="options">The runtime-mutable auto-update options.</param>
    /// <param name="timeProvider">Used for the lock timestamp, so lock aging can be controlled in tests.</param>
    public FileSystemAutoUpdatePackageStore(IAutoUpdateEnvironment environment, AutoUpdateOptions options, TimeProvider timeProvider)
    {
        _environment = environment;
        _options = options;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public string RootDirectory => ResolveFullPath(string.IsNullOrWhiteSpace(_options.DownloadPath) ? AutoUpdateOptions.DefaultDownloadPath : _options.DownloadPath);

    /// <inheritdoc />
    public string PendingDirectory => Path.Combine(RootDirectory, "pending");

    /// <inheritdoc />
    public string StagingDirectory => Path.Combine(RootDirectory, "staging");

    /// <inheritdoc />
    public string LockPath => Path.Combine(RootDirectory, "update.lock");

    /// <inheritdoc />
    public string LogPath => Path.Combine(RootDirectory, "update.log");

    /// <inheritdoc />
    public string ScriptPath(string extension) => Path.Combine(PendingDirectory, $"update.{extension.TrimStart('.')}");

    /// <inheritdoc />
    public string PendingAssetPath(string fileName)
    {
        var safeName = Path.GetFileName(fileName);
        if (!string.Equals(fileName, safeName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Package file name must not contain path segments.");
        }

        return Path.Combine(PendingDirectory, safeName);
    }

    /// <inheritdoc />
    public Task EnsureAsync(CancellationToken ct = default)
    {
        Directory.CreateDirectory(RootDirectory);
        Directory.CreateDirectory(PendingDirectory);
        Directory.CreateDirectory(StagingDirectory);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
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
            // Fall back to the file's last write time below.
        }

        return File.GetLastWriteTimeUtc(LockPath);
    }

    /// <inheritdoc />
    public async Task<bool> TryCreateLockAsync(CancellationToken ct = default)
    {
        await EnsureAsync(ct);
        try
        {
            await using var stream = new FileStream(LockPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await using var writer = new StreamWriter(stream);
            await writer.WriteLineAsync(_timeProvider.GetUtcNow().ToString("O").AsMemory(), ct);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public Task<bool> DeleteLockAsync(CancellationToken ct = default)
    {
        if (!File.Exists(LockPath))
        {
            return Task.FromResult(false);
        }

        try
        {
            File.Delete(LockPath);
            return Task.FromResult(true);
        }
        catch (IOException)
        {
            return Task.FromResult(false);
        }
    }

    private string ResolveFullPath(string configuredPath)
    {
        var root = _environment.ApplicationDirectory;
        return Path.IsPathRooted(configuredPath)
            ? Path.GetFullPath(configuredPath)
            : Path.GetFullPath(Path.Combine(root, configuredPath));
    }
}
