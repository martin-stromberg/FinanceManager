using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace SoftwareSchmiede.AutoUpdate;

/// <summary>
/// Persists the <see cref="AutoUpdateStatusSnapshot"/> atomically as <c>status.json</c> under the package
/// store's root directory. Unreadable or foreign-schema files are treated as absent rather than as an error, so
/// that a status file written by an older version of the application does not prevent startup.
/// </summary>
public sealed class FileSystemAutoUpdateStateStore : IAutoUpdateStateStore
{
    private readonly IAutoUpdatePackageStore _packageStore;
    private readonly ILogger<FileSystemAutoUpdateStateStore> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileSystemAutoUpdateStateStore"/> class.
    /// </summary>
    /// <param name="packageStore">Provides the root directory the status file is stored in.</param>
    /// <param name="logger">Used to log a warning when a foreign-schema status file is encountered.</param>
    public FileSystemAutoUpdateStateStore(IAutoUpdatePackageStore packageStore, ILogger<FileSystemAutoUpdateStateStore>? logger = null)
    {
        _packageStore = packageStore;
        _logger = logger ?? NullLogger<FileSystemAutoUpdateStateStore>.Instance;
    }

    private string StatusPath => Path.Combine(_packageStore.RootDirectory, "status.json");

    /// <inheritdoc />
    public async Task<AutoUpdateStatusSnapshot?> ReadAsync(CancellationToken ct = default)
    {
        try
        {
            return await JsonFileStore.ReadAsync<AutoUpdateStatusSnapshot>(StatusPath, ct);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "The status file at {StatusPath} could not be parsed with the current schema; falling back to an idle state.", StatusPath);
            return null;
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "The status file at {StatusPath} could not be read; falling back to an idle state.", StatusPath);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task WriteAsync(AutoUpdateStatusSnapshot snapshot, CancellationToken ct = default)
    {
        await _packageStore.EnsureAsync(ct);
        await JsonFileStore.WriteAtomicAsync(StatusPath, snapshot, ct);
    }
}
