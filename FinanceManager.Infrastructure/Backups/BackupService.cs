using FinanceManager.Application.Backups;
using FinanceManager.Application.Statements;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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

    /// <summary>
    /// Initializes a new instance of the <see cref="BackupService"/> class.
    /// </summary>
    /// <param name="db">Database context used to persist backup metadata.</param>
    /// <param name="env">Host environment used to resolve the content root for file storage.</param>
    /// <param name="logger">Logger instance for diagnostic messages.</param>
    /// <param name="services">Service provider used to create scoped services when applying backups.</param>
    public BackupService(AppDbContext db, IHostEnvironment env, ILogger<BackupService> logger, IServiceProvider services)
    {
        _db = db; _env = env; _logger = logger; _services = services;
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
    /// Uploads a user-provided backup file. If the file is not a ZIP it will be wrapped into a ZIP containing the NDJSON payload.
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
        var targetName = isZip ? safeName : Path.ChangeExtension($"upload-{DateTime.UtcNow:yyyyMMddHHmmss}", ".zip");
        var target = Path.Combine(GetRoot(), targetName);

        if (await _db.Backups.AnyAsync(b => b.OwnerUserId == userId && b.FileName == targetName, ct))
            throw new FileLoadException("Eine Sicherung mit diesem Dateinamen existiert bereits.");

        if (isZip)
        {
            await using var fs = File.Create(target);
            await stream.CopyToAsync(fs, ct);
        }
        else
        {
            // Wrap uploaded content (assumed NDJSON) into a zip
            using var zipFs = File.Create(target);
            using var zip = new ZipArchive(zipFs, ZipArchiveMode.Create, leaveOpen: false);
            var entry = zip.CreateEntry("backup.ndjson", CompressionLevel.Optimal);
            await using var es = entry.Open();
            await stream.CopyToAsync(es, ct);
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
    /// Applies the specified backup for the user by reading the NDJSON payload and invoking the importer.
    /// Progress updates reported by the importer are forwarded to the provided callback.
    /// </summary>
    /// <param name="userId">Owner user identifier.</param>
    /// <param name="id">Identifier of the backup record to apply.</param>
    /// <param name="progressCallback">Callback that receives progress updates. Parameters: stepDescription, step, total, subStep, subTotal.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><c>true</c> when the backup was applied; otherwise <c>false</c> when the record or file was missing or reading failed.</returns>
    public async Task<bool> ApplyAsync(Guid userId, Guid id, Action<string, int, int, int, int> progressCallback, CancellationToken ct)
    {
        var rec = await _db.Backups.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id && b.OwnerUserId == userId, ct);
        if (rec == null) return false;
        var full = Path.Combine(GetRoot(), rec.StoragePath);
        if (!File.Exists(full)) return false;

        // Extract NDJSON from zip or read directly if file is NDJSON
        var (ok, ndjson) = await ReadNdjsonAsync(full, ct);
        if (!ok || ndjson == null) { return false; }

        // Read first line for meta/version
        ndjson.Position = 0;
        using var sr = new StreamReader(ndjson, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var metaLine = await sr.ReadLineAsync();
        if (string.IsNullOrWhiteSpace(metaLine)) { return false; }
        using var metaDoc = JsonDocument.Parse(metaLine);
        var versionProp = metaDoc.RootElement.TryGetProperty("Version", out var vEl) ? vEl.GetInt32() : 3;
        ndjson.Position = 0;

        // Resolve services scoped to this operation
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var draftSvc = scope.ServiceProvider.GetRequiredService<IStatementDraftService>();
        var aggSvc = scope.ServiceProvider.GetRequiredService<FinanceManager.Application.Aggregates.IPostingAggregateService>();
        var importerLegacy = new SetupImportService(db, draftSvc, aggSvc);

        // propagate nested progress: main step 1 = reading, step 2 = importing (with subprogress)
        importerLegacy.ProgressChanged += (sender, e) =>
        {
            progressCallback(e.StepDescription, e.Step, e.Total, e.SubStep, e.SubTotal);
        };
        await importerLegacy.ImportAsync(userId, ndjson, replaceExisting: true, ct);
        return true;
    }

    /// <summary>
    /// Reads the NDJSON content from either an NDJSON file or a ZIP containing an NDJSON entry.
    /// </summary>
    /// <param name="filePath">Full path to the backup file.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Tuple indicating success and the memory stream containing the NDJSON payload when successful.</returns>
    private static async Task<(bool ok, MemoryStream? content)> ReadNdjsonAsync(string filePath, CancellationToken ct)
    {
        var isZip = filePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
        if (!isZip)
        {
            // Assume NDJSON file
            var ms = new MemoryStream();
            await using (var fs = File.OpenRead(filePath))
            {
                await fs.CopyToAsync(ms, ct);
            }
            ms.Position = 0;
            return (true, ms);
        }

        await using var zipFs = File.OpenRead(filePath);
        using var zip = new ZipArchive(zipFs, ZipArchiveMode.Read, leaveOpen: false);
        var entry = zip.Entries.FirstOrDefault(e => e.FullName.EndsWith(".ndjson", StringComparison.OrdinalIgnoreCase))
                    ?? zip.Entries.FirstOrDefault();
        if (entry == null) { return (false, null); }
        await using var es = entry.Open();
        var outMs = new MemoryStream();
        await es.CopyToAsync(outMs, ct);
        outMs.Position = 0;
        return (true, outMs);
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
        // Master data
        var contactCategories = await _db.ContactCategories.AsNoTracking()
            .Where(c => c.OwnerUserId == userId)
            .Select(c => new { c.Id, c.Name, c.OwnerUserId, c.CreatedUtc, c.ModifiedUtc })
            .ToListAsync(ct);

        var contacts = await _db.Contacts.AsNoTracking()
            .Where(c => c.OwnerUserId == userId)
            .Select(c => new { c.Id, c.OwnerUserId, c.Name, c.Type, c.CategoryId, c.Description, IsPaymentIntermediary = c.IsPaymentIntermediary, c.CreatedUtc, c.ModifiedUtc })
            .ToListAsync(ct);

        var aliasNames = await _db.AliasNames.AsNoTracking()
            .Where(a => _db.Contacts.Any(c => c.Id == a.ContactId && c.OwnerUserId == userId))
            .Select(a => new { a.Id, a.ContactId, a.Pattern, a.CreatedUtc, a.ModifiedUtc })
            .ToListAsync(ct);

        var securityCategories = await _db.SecurityCategories.AsNoTracking()
            .Where(sc => sc.OwnerUserId == userId)
            .Select(sc => new { sc.Id, sc.OwnerUserId, sc.Name })
            .ToListAsync(ct);

        var securities = await _db.Securities.AsNoTracking()
            .Where(s => s.OwnerUserId == userId)
            .Select(s => new { s.Id, s.OwnerUserId, s.Name, s.Identifier, s.Description, s.AlphaVantageCode, s.CurrencyCode, s.CategoryId, s.IsActive, s.CreatedUtc, s.ArchivedUtc })
            .ToListAsync(ct);

        var securityIds = securities.Select(s => s.Id).ToList();
        var securityPrices = await _db.SecurityPrices.AsNoTracking()
            .Where(p => securityIds.Contains(p.SecurityId))
            .Select(p => new { p.Id, p.SecurityId, p.Date, p.Close, p.CreatedUtc })
            .ToListAsync(ct);

        var savingsPlanCategories = await _db.SavingsPlanCategories.AsNoTracking()
            .Where(spc => spc.OwnerUserId == userId)
            .Select(spc => new { spc.Id, spc.OwnerUserId, spc.Name })
            .ToListAsync(ct);

        var savingsPlans = await _db.SavingsPlans.AsNoTracking()
            .Where(sp => sp.OwnerUserId == userId)
            .Select(sp => new { sp.Id, sp.OwnerUserId, sp.Name, sp.Type, sp.TargetAmount, sp.TargetDate, sp.Interval, sp.IsActive, sp.CreatedUtc, sp.ArchivedUtc, sp.CategoryId, sp.ContractNumber })
            .ToListAsync(ct);

        var accounts = await _db.Accounts.AsNoTracking()
            .Where(a => a.OwnerUserId == userId)
            .Select(a => new { a.Id, a.OwnerUserId, a.Name, a.Type, a.Iban, a.CurrentBalance, a.BankContactId, a.CreatedUtc, a.ModifiedUtc })
            .ToListAsync(ct);

        // Statements (imports + entries)
        var importAccountIds = accounts.Select(a => a.Id).ToList();
        var statementImports = await _db.StatementImports.AsNoTracking()
            .Where(i => importAccountIds.Contains(i.AccountId))
            .Select(i => new { i.Id, i.AccountId, i.Format, i.ImportedAtUtc, i.OriginalFileName, i.TotalEntries, i.CreatedUtc, i.ModifiedUtc })
            .ToListAsync(ct);
        var importIds = statementImports.Select(i => i.Id).ToList();
        var statementEntries = await _db.StatementEntries.AsNoTracking()
            .Where(e => importIds.Contains(e.StatementImportId))
            .Select(e => new { e.Id, e.StatementImportId, e.BookingDate, e.ValutaDate, e.Amount, e.CurrencyCode, e.Subject, e.RecipientName, e.BookingDescription, e.ContactId, e.Status, e.RawHash, e.IsAnnounced, e.IsCostNeutral, e.SavingsPlanId, e.SecurityTransactionId, e.CreatedUtc, e.ModifiedUtc })
            .ToListAsync(ct);

        // Postings
        var contactIds = contacts.Select(c => c.Id).ToList();
        var savingsPlanIds = savingsPlans.Select(s => s.Id).ToList();
        var postingQuery = _db.Postings.AsNoTracking()
            .Where(p => (p.AccountId != null && importAccountIds.Contains(p.AccountId.Value))
                     || (p.ContactId != null && contactIds.Contains(p.ContactId.Value))
                     || (p.SavingsPlanId != null && savingsPlanIds.Contains(p.SavingsPlanId.Value))
                     || (p.SecurityId != null && securityIds.Contains(p.SecurityId.Value)));
        var postings = await postingQuery
            .Select(p => new { p.Id, p.SourceId, p.Kind, p.AccountId, p.ContactId, p.SavingsPlanId, p.SecurityId, p.BookingDate, p.Amount, p.Subject, p.RecipientName, p.Description, p.GroupId, SecuritySubType = p.SecuritySubType, Quantity = p.Quantity, p.CreatedUtc, p.ModifiedUtc })
            .ToListAsync(ct);

        // Drafts
        var drafts = await _db.StatementDrafts.AsNoTracking()
            .Where(d => d.OwnerUserId == userId)
            .Select(d => new { d.Id, d.OwnerUserId, d.AccountName, d.Description, d.DetectedAccountId, d.OriginalFileName, d.Status, d.CreatedUtc, d.ModifiedUtc })
            .ToListAsync(ct);
        var draftIds = drafts.Select(d => d.Id).ToList();
        var draftEntries = await _db.StatementDraftEntries.AsNoTracking()
            .Where(e => draftIds.Contains(e.DraftId))
            .Select(e => new { e.Id, e.DraftId, e.BookingDate, e.ValutaDate, e.Amount, e.CurrencyCode, e.Subject, e.RecipientName, e.BookingDescription, e.IsAnnounced, e.IsCostNeutral, e.Status, e.ContactId, e.SavingsPlanId, e.ArchiveSavingsPlanOnBooking, e.SplitDraftId, e.SecurityId, e.SecurityTransactionType, e.SecurityQuantity, e.SecurityFeeAmount, e.SecurityTaxAmount, e.CreatedUtc, e.ModifiedUtc })
            .ToListAsync(ct);

        // Favorites & Home KPIs (new in v3)
        var reportFavorites = await _db.ReportFavorites.AsNoTracking()
            .Where(r => r.OwnerUserId == userId)
            .Select(r => new
            {
                r.Id,
                r.OwnerUserId,
                r.Name,
                r.PostingKind,
                r.IncludeCategory,
                r.Interval,
                r.Take,
                r.ComparePrevious,
                r.CompareYear,
                r.ShowChart,
                r.Expandable,
                r.CreatedUtc,
                r.ModifiedUtc,
                r.PostingKindsCsv,
                r.AccountIdsCsv,
                r.ContactIdsCsv,
                r.SavingsPlanIdsCsv,
                r.SecurityIdsCsv,
                r.ContactCategoryIdsCsv,
                r.SavingsPlanCategoryIdsCsv,
                r.SecurityCategoryIdsCsv
            })
            .ToListAsync(ct);

        var homeKpis = await _db.HomeKpis.AsNoTracking()
            .Where(h => h.OwnerUserId == userId)
            .Select(h => new
            {
                h.Id,
                h.OwnerUserId,
                h.Kind,
                h.ReportFavoriteId,
                h.DisplayMode,
                h.SortOrder,
                h.Title,
                h.PredefinedType,
                h.CreatedUtc,
                h.ModifiedUtc
            })
            .ToListAsync(ct);

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
            HomeKpis = homeKpis
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
