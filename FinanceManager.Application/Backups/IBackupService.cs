namespace FinanceManager.Application.Backups;

/// <summary>
/// Service interface for creating, listing, uploading and applying user backups.
/// </summary>
public interface IBackupService
{
    /// <summary>
    /// Creates a new backup snapshot for the specified user.
    /// </summary>
    Task<BackupDto> CreateAsync(Guid userId, CancellationToken ct);

    /// <summary>
    /// Lists available backups for the specified user.
    /// </summary>
    Task<IReadOnlyList<BackupDto>> ListAsync(Guid userId, CancellationToken ct);

    /// <summary>
    /// Deletes a backup for the user.
    /// </summary>
    Task<bool> DeleteAsync(Guid userId, Guid id, CancellationToken ct);

    /// <summary>
    /// Opens a read stream for downloading a backup or null when not found.
    /// </summary>
    Task<Stream?> OpenDownloadAsync(Guid userId, Guid id, CancellationToken ct);

    /// <summary>
    /// Applies a backup synchronously and reports progress through a callback.
    /// </summary>
    /// <param name="progressCallback">Callback receiving message and progress values.</param>
    Task<bool> ApplyAsync(Guid userId, Guid id, Action<string, int, int, int, int> progressCallback, CancellationToken ct);

    /// <summary>
    /// Uploads a backup file and returns created metadata DTO.
    /// </summary>
    Task<BackupDto> UploadAsync(Guid userId, Stream stream, string fileName, CancellationToken ct);
}


