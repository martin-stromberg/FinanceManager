using FinanceManager.Application.Backups;
using FinanceManager.Application.Statements;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace FinanceManager.Infrastructure.Backups;

/// <summary>
/// Service responsible for creating, uploading, listing and applying user backups.
/// Backups are stored as ZIP files containing an NDJSON payload and a record is persisted in the database.
/// </summary>
public sealed class BackupService : IBackupService
{
    private readonly AppDbContext _db;
    private readonly IHostEnvironment _env;
    private readonly ILogger<BackupService> _logger;
    private readonly IServiceProvider _services;
    private readonly BackupSecurityOptions _securityOptions;

    private static readonly string[] RequiredDataProperties =
    [
        "Accounts",
        "Contacts",
        "ContactCategories",
        "AliasNames",
        "SavingsPlanCategories",
        "SavingsPlans",
        "SecurityCategories",
        "Securities",
        "SecurityPrices",
        "StatementImports",
        "StatementEntries",
        "Postings",
        "StatementDrafts",
        "StatementDraftEntries",
        "ReportFavorites",
        "HomeKpis",
        "AttachmentCategories",
        "Attachments",
        "Notifications",
        "AccountShares",
        "BudgetCategories",
        "BudgetPurposes",
        "BudgetRules",
        "BudgetOverrides"
    ];

    /// <summary>
    /// Initializes a new instance of the <see cref="BackupService"/> class.
    /// </summary>
    /// <param name="db">Database context used to persist backup metadata.</param>
    /// <param name="env">Host environment used to resolve the content root for file storage.</param>
    /// <param name="logger">Logger instance for diagnostic messages.</param>
    /// <param name="services">Service provider used to create scoped services when applying backups.</param>
    /// <param name="securityOptions">Security limits for backup validation.</param>
    public BackupService(
        AppDbContext db,
        IHostEnvironment env,
        ILogger<BackupService> logger,
        IServiceProvider services,
        IOptions<BackupSecurityOptions>? securityOptions = null)
    {
        _db = db;
        _env = env;
        _logger = logger;
        _services = services;
        _securityOptions = securityOptions?.Value ?? new BackupSecurityOptions();
    }

    /// <summary>
    /// Resolves the root folder where backup files are stored on the host file system and ensures it exists.
    /// </summary>
    /// <returns>Absolute path to the backups directory.</returns>
    private string GetRoot()
    {
        var root = Path.Combine(_env.ContentRootPath, "backups");
        if (!Directory.Exists(root)) Directory.CreateDirectory(root);
        return root;
    }

    /// <summary>
    /// Creates a backup for the specified user and writes a ZIP file containing an NDJSON payload.
    /// A database record describing the backup is created and returned.
    /// </summary>
    /// <param name="userId">Owner user identifier for whom the backup is created.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="BackupDto"/> describing the created backup.</returns>
    public async Task<BackupDto> CreateAsync(Guid userId, CancellationToken ct)
    {
        // Build backup content (NDJSON with meta + data)
        var meta = new { Type = "Backup", Version = 3 };
        var data = await BuildBackupDataAsync(userId, ct);
        var ndjson = JsonSerializer.Serialize(meta) + "\n" + JsonSerializer.Serialize(data);
        var ndjsonBytes = new UTF8Encoding(false).GetBytes(ndjson);

        var backupName = $"backup-{DateTime.UtcNow:yyyyMMddHHmmss}";
        var fileName = $"{backupName}.zip";
        var dataFileName = $"{backupName}.ndjson";
        var path = Path.Combine(GetRoot(), fileName);

        // Write zip with single entry backup.ndjson
        await using (var fs = File.Create(path))
        using (var zip = new ZipArchive(fs, ZipArchiveMode.Create, leaveOpen: false))
        {
            var entry = zip.CreateEntry(dataFileName, CompressionLevel.Optimal);
            await using var es = entry.Open();
            await es.WriteAsync(ndjsonBytes, 0, ndjsonBytes.Length, ct);
        }

        var size = new FileInfo(path).Length;
        var rec = new BackupRecord
        {
            OwnerUserId = userId,
            CreatedUtc = DateTime.UtcNow,
            FileName = fileName,
            SizeBytes = size,
            Source = "System",
            StoragePath = fileName
        };
        _db.Backups.Add(rec);
        await _db.SaveChangesAsync(ct);
        return Map(rec);
    }

    /// <summary>
    /// Uploads a user-provided ZIP backup file after validating its container and NDJSON payload.
    /// A database record will be created referencing the stored file.
    /// </summary>
    /// <param name="userId">Owner user identifier.</param>
    /// <param name="stream">Stream containing the uploaded file content.</param>
    /// <param name="fileName">Original file name provided by the client.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="BackupDto"/> describing the stored upload.</returns>
    /// <exception cref="FileLoadException">Thrown when a backup with the target file name already exists for the user.</exception>
    public async Task<BackupDto> UploadAsync(Guid userId, Stream stream, string fileName, CancellationToken ct)
    {
        var safeName = Path.GetFileName(fileName);
        var isZip = safeName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
        if (!isZip)
        {
            _logger.LogWarning("BackupRestoreAudit Operation={Operation} Result={Result} Reason={Reason} UserId={UserId} FileName={FileName}", "Upload", "Rejected", "UnsupportedFormat", userId, safeName);
            throw new BackupValidationException("Err_Backup_UnsupportedFormat", "Only ZIP backup files are supported.");
        }

        var targetName = safeName;
        var target = Path.Combine(GetRoot(), targetName);

        if (await _db.Backups.AnyAsync(b => b.OwnerUserId == userId && b.FileName == targetName, ct))
            throw new FileLoadException("Eine Sicherung mit diesem Dateinamen existiert bereits.");

        await using var uploadCopy = new MemoryStream();
        await CopyBoundedAsync(stream, uploadCopy, _securityOptions.MaxUploadBytes, ct);
        uploadCopy.Position = 0;
        var validation = await ValidateAndReadBackupAsync(uploadCopy, BackupValidationPurpose.Upload, ct);

        uploadCopy.Position = 0;
        await using (var fs = File.Create(target))
        {
            await uploadCopy.CopyToAsync(fs, ct);
        }

        var size = new FileInfo(target).Length;
        var rec = new BackupRecord
        {
            OwnerUserId = userId,
            CreatedUtc = DateTime.UtcNow,
            FileName = Path.GetFileName(target),
            SizeBytes = size,
            Source = "Upload",
            StoragePath = Path.GetFileName(target)
        };
        _db.Backups.Add(rec);
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation(
            "BackupRestoreAudit Operation={Operation} Result={Result} UserId={UserId} FileName={FileName} EntryName={EntryName} CompressedBytes={CompressedBytes} UncompressedBytes={UncompressedBytes} Version={Version}",
            "Upload",
            "Succeeded",
            userId,
            rec.FileName,
            validation.EntryName,
            validation.CompressedBytes,
            validation.UncompressedBytes,
            validation.Version);
        return Map(rec);
    }

    /// <summary>
    /// Lists all backup records for the specified user ordered by creation time descending.
    /// </summary>
    /// <param name="userId">Owner user identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of <see cref="BackupDto"/> instances.</returns>
    public async Task<IReadOnlyList<BackupDto>> ListAsync(Guid userId, CancellationToken ct)
    {
        return await _db.Backups.AsNoTracking()
            .Where(b => b.OwnerUserId == userId)
            .OrderByDescending(b => b.CreatedUtc)
            .Select(b => Map(b))
            .ToListAsync(ct);
    }

    /// <summary>
    /// Deletes a backup record and removes the underlying file from storage when possible.
    /// </summary>
    /// <param name="userId">Owner user identifier.</param>
    /// <param name="id">Identifier of the backup record to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><c>true</c> when the backup existed and was removed; otherwise <c>false</c>.</returns>
    public async Task<bool> DeleteAsync(Guid userId, Guid id, CancellationToken ct)
    {
        var rec = await _db.Backups.FirstOrDefaultAsync(b => b.Id == id && b.OwnerUserId == userId, ct);
        if (rec == null) return false;
        var full = Path.Combine(GetRoot(), rec.StoragePath);
        try { if (File.Exists(full)) File.Delete(full); } catch { }
        _db.Backups.Remove(rec);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>
    /// Opens a read-only stream for downloading the backup file referenced by the specified record.
    /// </summary>
    /// <param name="userId">Owner user identifier.</param>
    /// <param name="id">Backup record identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A read-only <see cref="Stream"/> for the file, or <c>null</c> when not found.</returns>
    public async Task<Stream?> OpenDownloadAsync(Guid userId, Guid id, CancellationToken ct)
    {
        var rec = await _db.Backups.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id && b.OwnerUserId == userId, ct);
        if (rec == null) return null;
        var full = Path.Combine(GetRoot(), rec.StoragePath);
        if (!File.Exists(full)) return null;
        return File.OpenRead(full);
    }

    /// <summary>
    /// Gets a single backup metadata entry for the specified user.
    /// </summary>
    /// <param name="userId">Owner user identifier.</param>
    /// <param name="id">Backup identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The backup metadata or <c>null</c> when it is not found.</returns>
    public async Task<BackupDto?> GetAsync(Guid userId, Guid id, CancellationToken ct)
    {
        var rec = await _db.Backups.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id && b.OwnerUserId == userId, ct);
        return rec == null ? null : Map(rec);
    }

    /// <summary>
    /// Applies the specified backup for the user by reading the NDJSON payload and invoking the importer.
    /// Progress updates reported by the importer are forwarded to the provided callback.
    /// </summary>
    /// <param name="userId">Owner user identifier.</param>
    /// <param name="id">Identifier of the backup record to apply.</param>
    /// <param name="confirmationText">Confirmation text that must match the stored backup file name.</param>
    /// <param name="confirmationAlreadyValidated">True when the caller already validated the confirmation before enqueueing a background restore.</param>
    /// <param name="progressCallback">Callback that receives progress updates. Parameters: stepDescription, step, total, subStep, subTotal.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A typed result describing whether the restore succeeded or why it was rejected.</returns>
    public async Task<BackupApplyResult> ApplyAsync(
        Guid userId,
        Guid id,
        string? confirmationText,
        bool confirmationAlreadyValidated,
        Action<string, int, int, int, int> progressCallback,
        CancellationToken ct)
    {
        var rec = await _db.Backups.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id && b.OwnerUserId == userId, ct);
        if (rec == null) return BackupApplyResult.NotFound("Backup not found.");

        if (!string.Equals(confirmationText, rec.FileName, StringComparison.Ordinal))
        {
            _logger.LogWarning("BackupRestoreAudit Operation={Operation} Result={Result} Reason={Reason} UserId={UserId} BackupId={BackupId} FileName={FileName}", "Restore", "Rejected", confirmationAlreadyValidated ? "StaleConfirmation" : "ConfirmationRequired", userId, id, rec.FileName);
            return BackupApplyResult.ConfirmationRequired("Restore confirmation must match the backup file name.");
        }

        var full = Path.Combine(GetRoot(), rec.StoragePath);
        if (!File.Exists(full)) return BackupApplyResult.NotFound("Backup file not found.");

        BackupValidationResult validation;
        try
        {
            validation = await ValidateAndReadBackupAsync(full, BackupValidationPurpose.Restore, ct);
        }
        catch (BackupValidationException ex)
        {
            _logger.LogWarning("BackupRestoreAudit Operation={Operation} Result={Result} Reason={Reason} UserId={UserId} BackupId={BackupId} FileName={FileName}", "Restore", "Rejected", ex.Code, userId, id, rec.FileName);
            return BackupApplyResult.InvalidBackup(ex.Message);
        }

        // Resolve services scoped to this operation
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var draftSvc = scope.ServiceProvider.GetRequiredService<IStatementDraftService>();
        var aggSvc = scope.ServiceProvider.GetRequiredService<FinanceManager.Application.Aggregates.IPostingAggregateService>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<SetupImportService>>();
        var reportCache = scope.ServiceProvider.GetService<FinanceManager.Application.Budget.IReportCacheService>();
        var importerLegacy = new SetupImportService(db, draftSvc, aggSvc, logger);

        // propagate nested progress: main step 1 = reading, step 2 = importing (with subprogress)
        importerLegacy.ProgressChanged += (sender, e) =>
        {
            progressCallback(e.StepDescription, e.Step, e.Total, e.SubStep, e.SubTotal);
        };
        try
        {
            await importerLegacy.ImportAsync(userId, validation.Stream, replaceExisting: true, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "BackupRestoreAudit Operation={Operation} Result={Result} Reason={Reason} UserId={UserId} BackupId={BackupId} FileName={FileName}", "Restore", "Failed", "ImportFailed", userId, id, rec.FileName);
            return BackupApplyResult.ImportFailed(ex.Message);
        }

        if (reportCache != null)
        {
            await reportCache.MarkAllReportCacheEntriesForUpdateAsync(userId, ct);
            reportCache.EnqueueBudgetReportCacheRefresh(userId);
        }
        _logger.LogInformation(
            "BackupRestoreAudit Operation={Operation} Result={Result} UserId={UserId} BackupId={BackupId} FileName={FileName} EntryName={EntryName} CompressedBytes={CompressedBytes} UncompressedBytes={UncompressedBytes} Version={Version}",
            "Restore",
            "Succeeded",
            userId,
            id,
            rec.FileName,
            validation.EntryName,
            validation.CompressedBytes,
            validation.UncompressedBytes,
            validation.Version);
        return BackupApplyResult.Succeeded();
    }

    private async Task<BackupValidationResult> ValidateAndReadBackupAsync(string filePath, BackupValidationPurpose purpose, CancellationToken ct)
    {
        await using var zipFs = File.OpenRead(filePath);
        return await ValidateAndReadBackupAsync(zipFs, purpose, ct);
    }

    private async Task<BackupValidationResult> ValidateAndReadBackupAsync(Stream zipStream, BackupValidationPurpose purpose, CancellationToken ct)
    {
        if (zipStream.CanSeek && zipStream.Length > _securityOptions.MaxCompressedZipBytes)
        {
            throw new BackupValidationException("Err_Backup_TooLarge", "Backup ZIP exceeds the configured compressed size limit.");
        }

        ZipArchive zip;
        try
        {
            zip = new ZipArchive(zipStream, ZipArchiveMode.Read, leaveOpen: true);
        }
        catch (InvalidDataException ex)
        {
            throw new BackupValidationException("Err_Backup_InvalidZip", "Backup must be a valid ZIP file.", ex);
        }

        using (zip)
        {
            if (zip.Entries.Count == 0)
            {
                throw new BackupValidationException("Err_Backup_EmptyZip", "Backup ZIP does not contain an NDJSON entry.");
            }

            if (zip.Entries.Count > _securityOptions.MaxZipEntries)
            {
                throw new BackupValidationException("Err_Backup_TooManyEntries", "Backup ZIP contains too many entries.");
            }

            var entry = zip.Entries.Single();
            ValidateEntry(entry);

            var outMs = new MemoryStream();
            await using (var es = entry.Open())
            {
                await CopyBoundedAsync(es, outMs, _securityOptions.MaxUncompressedNdjsonBytes, ct);
            }

            if (outMs.Length != entry.Length)
            {
                throw new BackupValidationException("Err_Backup_SizeMismatch", "Backup entry size does not match ZIP metadata.");
            }

            outMs.Position = 0;
            var version = await ValidateNdjsonSchemaAsync(outMs, ct);
            outMs.Position = 0;

            return new BackupValidationResult(outMs, entry.FullName, entry.CompressedLength, outMs.Length, version);
        }
    }

    private void ValidateEntry(ZipArchiveEntry entry)
    {
        if (!IsAllowedEntryName(entry.FullName))
        {
            throw new BackupValidationException("Err_Backup_UnexpectedEntryName", "Backup ZIP contains an unexpected entry name.");
        }

        if (entry.CompressedLength < 0 || entry.Length < 0)
        {
            throw new BackupValidationException("Err_Backup_InvalidZipMetadata", "Backup ZIP contains invalid entry size metadata.");
        }

        if (entry.CompressedLength > _securityOptions.MaxCompressedZipBytes)
        {
            throw new BackupValidationException("Err_Backup_TooLarge", "Backup entry exceeds the configured compressed size limit.");
        }

        if (entry.Length <= 0)
        {
            throw new BackupValidationException("Err_Backup_EmptyPayload", "Backup NDJSON payload is empty.");
        }

        if (entry.Length > _securityOptions.MaxUncompressedNdjsonBytes)
        {
            throw new BackupValidationException("Err_Backup_UncompressedTooLarge", "Backup entry exceeds the configured uncompressed size limit.");
        }

        if (entry.CompressedLength == 0 || entry.Length / (double)entry.CompressedLength > _securityOptions.MaxCompressionRatio)
        {
            throw new BackupValidationException("Err_Backup_CompressionRatio", "Backup compression ratio exceeds the configured limit.");
        }
    }

    private bool IsAllowedEntryName(string entryName)
    {
        if (Path.GetFileName(entryName) != entryName)
        {
            return false;
        }

        if (_securityOptions.AllowedEntryNames.Any(n => string.Equals(n, entryName, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return _securityOptions.AllowedEntryPrefixes.Any(prefix =>
            entryName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
            entryName.EndsWith(".ndjson", StringComparison.OrdinalIgnoreCase));
    }

    private async Task<int> ValidateNdjsonSchemaAsync(MemoryStream ndjson, CancellationToken ct)
    {
        using var reader = new StreamReader(ndjson, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var metaLine = await reader.ReadLineAsync(ct);
        if (string.IsNullOrWhiteSpace(metaLine))
        {
            throw new BackupValidationException("Err_Backup_MissingMeta", "Backup NDJSON metadata is missing.");
        }

        int version;
        try
        {
            using var metaDoc = JsonDocument.Parse(metaLine);
            if (metaDoc.RootElement.ValueKind != JsonValueKind.Object ||
                !metaDoc.RootElement.TryGetProperty("Type", out var typeEl) ||
                !string.Equals(typeEl.GetString(), "Backup", StringComparison.Ordinal) ||
                !metaDoc.RootElement.TryGetProperty("Version", out var versionEl) ||
                versionEl.ValueKind != JsonValueKind.Number ||
                !versionEl.TryGetInt32(out version))
            {
                throw new BackupValidationException("Err_Backup_InvalidMeta", "Backup NDJSON metadata is invalid.");
            }
        }
        catch (JsonException ex)
        {
            throw new BackupValidationException("Err_Backup_InvalidMeta", "Backup NDJSON metadata is invalid.", ex);
        }

        if (!_securityOptions.AllowedBackupVersions.Contains(version))
        {
            throw new BackupValidationException("Err_Backup_UnsupportedVersion", "Backup version is not supported.");
        }

        var dataLine = await reader.ReadLineAsync(ct);
        if (string.IsNullOrWhiteSpace(dataLine))
        {
            throw new BackupValidationException("Err_Backup_MissingData", "Backup NDJSON data object is missing.");
        }

        try
        {
            using var dataDoc = JsonDocument.Parse(dataLine);
            if (dataDoc.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new BackupValidationException("Err_Backup_InvalidData", "Backup NDJSON data must be a JSON object.");
            }

            foreach (var propertyName in RequiredDataProperties)
            {
                if (!dataDoc.RootElement.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
                {
                    throw new BackupValidationException("Err_Backup_InvalidData", $"Backup data is missing array property '{propertyName}'.");
                }
            }
        }
        catch (JsonException ex)
        {
            throw new BackupValidationException("Err_Backup_InvalidData", "Backup NDJSON data is invalid.", ex);
        }

        return version;
    }

    private static async Task CopyBoundedAsync(Stream source, Stream destination, long maxBytes, CancellationToken ct)
    {
        var buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, ct)) > 0)
        {
            total += read;
            if (total > maxBytes)
            {
                throw new BackupValidationException("Err_Backup_TooLarge", "Backup exceeds the configured size limit.");
            }

            await destination.WriteAsync(buffer.AsMemory(0, read), ct);
        }
    }

    private sealed record BackupValidationResult(MemoryStream Stream, string EntryName, long CompressedBytes, long UncompressedBytes, int Version);

    private enum BackupValidationPurpose
    {
        Upload,
        Restore
    }

    /// <summary>
    /// Builds the in-memory object graph that will be serialized into the NDJSON backup payload.
    /// The returned object contains only the minimal required properties for restore operations.
    /// </summary>
    /// <param name="userId">Owner user identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>An anonymous object representing the backup payload.</returns>
    private async Task<object> BuildBackupDataAsync(Guid userId, CancellationToken ct)
    {
        // Load entities and map via ToBackupDto() where available to preserve domain mapping logic.

        // Contact categories
        var contactCategoryEntities = await _db.ContactCategories.AsNoTracking()
            .Where(c => c.OwnerUserId == userId)
            .ToListAsync(ct);
        var contactCategories = contactCategoryEntities.Select(c => c.ToBackupDto()).ToList();

        // Contacts
        var contactEntities = await _db.Contacts.AsNoTracking()
            .Where(c => c.OwnerUserId == userId)
            .ToListAsync(ct);
        var contacts = contactEntities.Select(c => c.ToBackupDto()).ToList();

        // Alias names (only those referencing loaded contacts)
        var contactIds = contactEntities.Select(c => c.Id).ToList();
        var aliasEntities = await _db.AliasNames.AsNoTracking()
            .Where(a => contactIds.Contains(a.ContactId))
            .ToListAsync(ct);
        var aliasNames = aliasEntities.Select(a => a.ToBackupDto()).ToList();

        // Security categories & securities
        var securityCategoryEntities = await _db.SecurityCategories.AsNoTracking()
            .Where(sc => sc.OwnerUserId == userId)
            .ToListAsync(ct);
        var securityCategories = securityCategoryEntities.Select(sc => sc.ToBackupDto()).ToList();

        var securityEntities = await _db.Securities.AsNoTracking()
            .Where(s => s.OwnerUserId == userId)
            .ToListAsync(ct);
        var securities = securityEntities.Select(s => s.ToBackupDto()).ToList();

        var securityIds = securityEntities.Select(s => s.Id).ToList();
        var securityPriceEntities = await _db.SecurityPrices.AsNoTracking()
            .Where(p => securityIds.Contains(p.SecurityId))
            .ToListAsync(ct);
        var securityPrices = securityPriceEntities.Select(p => p.ToBackupDto()).ToList();

        // Savings plan categories & plans
        var savingsPlanCategoryEntities = await _db.SavingsPlanCategories.AsNoTracking()
            .Where(spc => spc.OwnerUserId == userId)
            .ToListAsync(ct);
        var savingsPlanCategories = savingsPlanCategoryEntities.Select(spc => spc.ToBackupDto()).ToList();

        var savingsPlanEntities = await _db.SavingsPlans.AsNoTracking()
            .Where(sp => sp.OwnerUserId == userId)
            .ToListAsync(ct);
        var savingsPlans = savingsPlanEntities.Select(sp => sp.ToBackupDto()).ToList();

        // Accounts
        var accountEntities = await _db.Accounts.AsNoTracking()
            .Where(a => a.OwnerUserId == userId)
            .ToListAsync(ct);
        var accounts = accountEntities.Select(a => a.ToBackupDto()).ToList();

        // Statement imports and entries for accounts owned by user
        var importAccountIds = accountEntities.Select(a => a.Id).ToList();
        var statementImportEntities = await _db.StatementImports.AsNoTracking()
            .Where(i => importAccountIds.Contains(i.AccountId))
            .ToListAsync(ct);
        var statementImports = statementImportEntities.Select(i => i.ToBackupDto()).ToList();

        var importIds = statementImportEntities.Select(i => i.Id).ToList();
        var statementEntryEntities = await _db.StatementEntries.AsNoTracking()
            .Where(e => importIds.Contains(e.StatementImportId))
            .ToListAsync(ct);
        var statementEntries = statementEntryEntities.Select(e => e.ToBackupDto()).ToList();

        // Postings referencing user entities
        var savingsPlanIds = savingsPlanEntities.Select(s => s.Id).ToList();
        var postingEntities = await _db.Postings.AsNoTracking()
            .Where(p => (p.AccountId != null && importAccountIds.Contains(p.AccountId.Value))
                     || (p.ContactId != null && contactIds.Contains(p.ContactId.Value))
                     || (p.SavingsPlanId != null && savingsPlanIds.Contains(p.SavingsPlanId.Value))
                     || (p.SecurityId != null && securityIds.Contains(p.SecurityId.Value)))
            .ToListAsync(ct);
        var postings = postingEntities.Select(p => p.ToBackupDto()).ToList();

        // Statement drafts + entries
        var draftEntities = await _db.StatementDrafts.AsNoTracking()
            .Where(d => d.OwnerUserId == userId)
            .ToListAsync(ct);
        var drafts = draftEntities.Select(d => d.ToBackupDto()).ToList();

        var draftIds = draftEntities.Select(d => d.Id).ToList();
        var draftEntryEntities = await _db.StatementDraftEntries.AsNoTracking()
            .Where(e => draftIds.Contains(e.DraftId))
            .ToListAsync(ct);
        var draftEntries = draftEntryEntities.Select(e => e.ToBackupDto()).ToList();

        // Report favorites & Home KPIs
        var reportFavoriteEntities = await _db.ReportFavorites.AsNoTracking()
            .Where(r => r.OwnerUserId == userId)
            .ToListAsync(ct);
        var reportFavorites = reportFavoriteEntities.Select(r => r.ToBackupDto()).ToList();

        var homeKpiEntities = await _db.HomeKpis.AsNoTracking()
            .Where(h => h.OwnerUserId == userId)
            .ToListAsync(ct);
        var homeKpis = homeKpiEntities.Select(h => h.ToBackupDto()).ToList();

        // Attachment categories & attachments
        var attachmentCategoryEntities = await _db.AttachmentCategories.AsNoTracking()
            .Where(ac => ac.OwnerUserId == userId)
            .ToListAsync(ct);
        var attachmentCategories = attachmentCategoryEntities.Select(ac => ac.ToBackupDto()).ToList();

        var attachmentEntities = await _db.Attachments.AsNoTracking()
            .Where(a => a.OwnerUserId == userId)
            .ToListAsync(ct);
        var attachments = attachmentEntities.Select(a => a.ToBackupDto()).ToList();

        // Notifications
        var notificationEntities = await _db.Notifications.AsNoTracking()
            .Where(n => n.OwnerUserId == userId)
            .ToListAsync(ct);
        var notifications = notificationEntities.Select(n => n.ToBackupDto()).ToList();

        // Account shares for accounts owned by the user
        var accountShareEntities = await _db.AccountShares.AsNoTracking()
            .Where(s => importAccountIds.Contains(s.AccountId) || s.UserId == userId)
            .ToListAsync(ct);
        var accountShares = accountShareEntities.Select(s => s.ToBackupDto()).ToList();

        // Budget categories
        var budgetCategoryEntities = await _db.BudgetCategories.AsNoTracking()
            .Where(c => c.OwnerUserId == userId)
            .ToListAsync(ct);
        var budgetCategories = budgetCategoryEntities.Select(c => c.ToBackupDto()).ToList();

        // Budget purposes, rules, overrides
        var budgetPurposeEntities = await _db.BudgetPurposes.AsNoTracking()
            .Where(b => b.OwnerUserId == userId)
            .ToListAsync(ct);
        var budgetPurposes = budgetPurposeEntities.Select(b => b.ToBackupDto()).ToList();

        var budgetPurposeIds = budgetPurposeEntities.Select(b => b.Id).ToList();

        var budgetRuleEntities = await _db.BudgetRules.AsNoTracking()
            .Where(r => r.OwnerUserId == userId && r.BudgetPurposeId != null && budgetPurposeIds.Contains(r.BudgetPurposeId.Value))
            .ToListAsync(ct);
        var budgetRules = budgetRuleEntities.Select(r => r.ToBackupDto()).ToList();

        var budgetOverrideEntities = await _db.BudgetOverrides.AsNoTracking()
            .Where(o => o.OwnerUserId == userId && budgetPurposeIds.Contains(o.BudgetPurposeId))
            .ToListAsync(ct);
        var budgetOverrides = budgetOverrideEntities.Select(o => o.ToBackupDto()).ToList();

        return new
        {
            Accounts = accounts,
            Contacts = contacts,
            ContactCategories = contactCategories,
            AliasNames = aliasNames,
            SavingsPlanCategories = savingsPlanCategories,
            SavingsPlans = savingsPlans,
            SecurityCategories = securityCategories,
            Securities = securities,
            SecurityPrices = securityPrices,
            StatementImports = statementImports,
            StatementEntries = statementEntries,
            Postings = postings,
            StatementDrafts = drafts,
            StatementDraftEntries = draftEntries,
            ReportFavorites = reportFavorites,
            HomeKpis = homeKpis,
            AttachmentCategories = attachmentCategories,
            Attachments = attachments,
            Notifications = notifications,
            AccountShares = accountShares,
            BudgetCategories = budgetCategories,
            BudgetPurposes = budgetPurposes,
            BudgetRules = budgetRules,
            BudgetOverrides = budgetOverrides
        };
    }

    /// <summary>
    /// Maps a persistent <see cref="BackupRecord"/> to a DTO used by the application layer.
    /// </summary>
    /// <param name="r">The backup record to map.</param>
    /// <returns>A <see cref="BackupDto"/> instance.</returns>
    private static BackupDto Map(BackupRecord r) => new BackupDto
    {
        Id = r.Id,
        CreatedUtc = r.CreatedUtc,
        FileName = r.FileName,
        SizeBytes = r.SizeBytes,
        Source = r.Source
    };
}

/// <summary>
/// Security limits used when validating uploaded or stored backup ZIP containers.
/// </summary>
public sealed class BackupSecurityOptions
{
    /// <summary>Configuration section path for backup security limits.</summary>
    public const string SectionName = "Backups:Security";

    /// <summary>Maximum accepted upload body size in bytes.</summary>
    public long MaxUploadBytes { get; set; } = 100L * 1024L * 1024L;
    /// <summary>Maximum accepted compressed ZIP size in bytes.</summary>
    public long MaxCompressedZipBytes { get; set; } = 100L * 1024L * 1024L;
    /// <summary>Maximum accepted uncompressed NDJSON payload size in bytes.</summary>
    public long MaxUncompressedNdjsonBytes { get; set; } = 250L * 1024L * 1024L;
    /// <summary>Maximum number of entries accepted in a backup ZIP.</summary>
    public int MaxZipEntries { get; set; } = 1;
    /// <summary>Maximum allowed uncompressed-to-compressed size ratio.</summary>
    public double MaxCompressionRatio { get; set; } = 25d;
    /// <summary>Exact allowed ZIP entry names.</summary>
    public string[] AllowedEntryNames { get; set; } = ["backup.ndjson"];
    /// <summary>Allowed ZIP entry prefixes; entries must still end in <c>.ndjson</c>.</summary>
    public string[] AllowedEntryPrefixes { get; set; } = ["backup-"];
    /// <summary>Supported backup metadata versions.</summary>
    public int[] AllowedBackupVersions { get; set; } = [3];
}
