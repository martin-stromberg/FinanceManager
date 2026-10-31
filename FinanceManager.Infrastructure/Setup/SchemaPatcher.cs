using FinanceManager.Application;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Infrastructure.Setup;

/// <summary>
/// Runtime safety patcher that ensures schema/runtime consistency after applying migrations.
/// Also triggers long-running background work that cannot/should not be executed inside migrations
/// (for example: rebuilding aggregates).
/// </summary>
public static class SchemaPatcher
{
    /// <summary>
    /// Apply runtime fixes after migrations have been applied. This is executed from ProgramExtensions after
    /// calling <c>db.Database.Migrate()</c>. It may enqueue background tasks to rebuild aggregates if needed.
    /// </summary>
    /// <param name="serviceProvider">The service provider used to resolve required services.</param>
    /// <param name="db">The application database context.</param>
    /// <param name="logger">The logger for patch diagnostics.</param>
    public static void RunPostMigrationPatches(IServiceProvider serviceProvider, AppDbContext db, ILogger logger)
    {
        // Run synchronously but perform async DB operations internally
        RunPostMigrationPatchesAsync(serviceProvider, db, logger).GetAwaiter().GetResult();
    }

    private static async Task RunPostMigrationPatchesAsync(IServiceProvider serviceProvider, AppDbContext db, ILogger logger)
    {
        if (serviceProvider == null) return;
        try
        {
            await EnsureAspNetUsersColumnsAsync(db, logger);
            await MigrateBackupStorageAsync(serviceProvider, db, logger);

            var taskManager = serviceProvider.GetService(typeof(IBackgroundTaskManager)) as IBackgroundTaskManager;
            if (taskManager == null)
            {
                logger.LogInformation("BackgroundTaskManager not available; skipping post-migration enqueue.");
                return;
            }

            // Check whether PostingAggregates has any Valuta aggregates already. If the column is not present
            // querying may throw (older DB without migration). In that case skip.
            bool hasAnyValuta = false;
            try
            {
                // If table empty or column absent this may throw for SQLite; catch below
                hasAnyValuta = await db.PostingAggregates.AsNoTracking().AnyAsync(a => a.DateKind != 0);
            }
            catch (SqliteException ex)
            {
                logger.LogInformation(ex, "PostingAggregates.DateKind column not present or query failed; skipping enqueue.");
                return;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Unable to query PostingAggregates for DateKind; skipping enqueue.");
                return;
            }

            if (hasAnyValuta)
            {
                logger.LogInformation("Valuta aggregates appear to exist already; no rebuild enqueued.");
                return;
            }

            // Enqueue a rebuild task per user so background runner will rebuild aggregates including Valuta
            var userIds = await db.Users.AsNoTracking().Select(u => u.Id).ToListAsync();
            if (userIds.Count == 0)
            {
                logger.LogInformation("No users found; skipping aggregate rebuild enqueue.");
                return;
            }

            foreach (var uid in userIds)
            {
                try
                {
                    var info = taskManager.Enqueue(BackgroundTaskType.RebuildAggregates, uid, null, allowDuplicate: false);
                    logger.LogInformation("Enqueued RebuildAggregates task {TaskId} for user {UserId}", info.Id, uid);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to enqueue rebuild aggregates for user {UserId}", uid);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Post-migration patching failed");
        }
    }

    /// <summary>
    /// Moves legacy backup files stored flat under the shared <c>backups</c> directory into the
    /// per-user subdirectory of their record's owner. Renames on collision and updates the
    /// record's <c>StoragePath</c> accordingly. Idempotent and non-fatal.
    /// </summary>
    /// <param name="serviceProvider">The service provider used to resolve the backup service.</param>
    /// <param name="db">The application database context.</param>
    /// <param name="logger">The logger for migration diagnostics.</param>
    private static async Task MigrateBackupStorageAsync(IServiceProvider serviceProvider, AppDbContext db, ILogger logger)
    {
        try
        {
            var env = serviceProvider.GetService(typeof(IHostEnvironment)) as IHostEnvironment;
            if (env == null || string.IsNullOrWhiteSpace(env.ContentRootPath))
            {
                return;
            }

            var backupsRoot = Path.Combine(env.ContentRootPath, "backups");
            if (!Directory.Exists(backupsRoot))
            {
                return;
            }

            var records = await db.Backups.ToListAsync();
            var changed = false;
            foreach (var record in records)
            {
                try
                {
                    var storagePath = record.StoragePath;
                    if (string.IsNullOrWhiteSpace(storagePath)
                        || !string.Equals(storagePath, Path.GetFileName(storagePath), StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var legacyPath = Path.Combine(backupsRoot, storagePath);
                    if (!File.Exists(legacyPath))
                    {
                        continue;
                    }

                    var userDir = Path.Combine(backupsRoot, record.OwnerUserId.ToString("N"));
                    Directory.CreateDirectory(userDir);

                    var targetName = storagePath;
                    var targetPath = Path.Combine(userDir, targetName);
                    if (File.Exists(targetPath))
                    {
                        targetName = $"{Path.GetFileNameWithoutExtension(storagePath)}-{Guid.NewGuid().ToString("N")[..8]}{Path.GetExtension(storagePath)}";
                        targetPath = Path.Combine(userDir, targetName);
                    }

                    File.Move(legacyPath, targetPath);
                    if (!string.Equals(targetName, storagePath, StringComparison.Ordinal))
                    {
                        record.StoragePath = targetName;
                        changed = true;
                    }

                    logger.LogInformation("Migrated backup file {StoragePath} for record {BackupId} into owner directory {OwnerUserId}", storagePath, record.Id, record.OwnerUserId);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to migrate backup file for record {BackupId}", record.Id);
                }
            }

            if (changed)
            {
                await db.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Backup storage migration failed; continuing startup");
        }
    }

    private static async Task EnsureAspNetUsersColumnsAsync(AppDbContext db, ILogger logger)
    {
        try
        {
            var columnAdditions = new[]
            {
                ("ImportSplitMode", "ALTER TABLE \"AspNetUsers\" ADD COLUMN \"ImportSplitMode\" INTEGER NOT NULL DEFAULT 2;"),
                ("ImportMaxEntriesPerDraft", "ALTER TABLE \"AspNetUsers\" ADD COLUMN \"ImportMaxEntriesPerDraft\" INTEGER NOT NULL DEFAULT 250;"),
                ("ImportMinEntriesPerDraft", "ALTER TABLE \"AspNetUsers\" ADD COLUMN \"ImportMinEntriesPerDraft\" INTEGER NOT NULL DEFAULT 8;"),
                ("ImportMonthlySplitThreshold", "ALTER TABLE \"AspNetUsers\" ADD COLUMN \"ImportMonthlySplitThreshold\" INTEGER;"),
                ("BenchmarkSecurityId", "ALTER TABLE \"AspNetUsers\" ADD COLUMN \"BenchmarkSecurityId\" TEXT NULL;"),
                ("RiskFreeRate", "ALTER TABLE \"AspNetUsers\" ADD COLUMN \"RiskFreeRate\" TEXT NOT NULL DEFAULT '0.0';"),
                ("ShowSharpeRatio", "ALTER TABLE \"AspNetUsers\" ADD COLUMN \"ShowSharpeRatio\" INTEGER NOT NULL DEFAULT 0;"),
                ("SymbolAttachmentId", "ALTER TABLE \"AspNetUsers\" ADD COLUMN \"SymbolAttachmentId\" TEXT NULL;"),
                ("MassImportDialogPolicy", "ALTER TABLE \"AspNetUsers\" ADD COLUMN \"MassImportDialogPolicy\" INTEGER NOT NULL DEFAULT 1;"),
                ("KnownContactAutoCreateEnabled", "ALTER TABLE \"AspNetUsers\" ADD COLUMN \"KnownContactAutoCreateEnabled\" INTEGER NOT NULL DEFAULT 1;")
            };

            foreach (var (columnName, sql) in columnAdditions)
            {
                try
                {
                    await db.Database.ExecuteSqlRawAsync(sql);
                    logger.LogInformation("Added missing column {Column} to AspNetUsers", columnName);
                }
                catch (SqliteException ex) when (ex.Message.Contains("duplicate column name", StringComparison.OrdinalIgnoreCase))
                {
                    logger.LogInformation("Column {Column} already exists on AspNetUsers", columnName);
                }
            }

            // Ensure the monthly split threshold has a value for rows that were added before the migration.
            try
            {
                await db.Database.ExecuteSqlRawAsync(
                    "UPDATE \"AspNetUsers\" SET \"ImportMonthlySplitThreshold\" = \"ImportMaxEntriesPerDraft\" WHERE \"ImportMonthlySplitThreshold\" IS NULL;");
            }
            catch (SqliteException ex)
            {
                logger.LogWarning(ex, "Could not initialize ImportMonthlySplitThreshold on AspNetUsers");
            }

            // Add missing index for BenchmarkSecurityId (idempotent).
            try
            {
                await db.Database.ExecuteSqlRawAsync(
                    "CREATE INDEX IF NOT EXISTS \"IX_AspNetUsers_BenchmarkSecurityId\" ON \"AspNetUsers\" (\"BenchmarkSecurityId\");");
            }
            catch (SqliteException ex)
            {
                logger.LogWarning(ex, "Could not create index IX_AspNetUsers_BenchmarkSecurityId");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to ensure AspNetUsers columns");
            throw;
        }
    }
}
