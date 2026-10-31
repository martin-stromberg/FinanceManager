using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Backups;
using FinanceManager.Infrastructure.Setup;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FinanceManager.Tests.Infrastructure;

/// <summary>
/// Covers the startup migration that moves legacy flat backup files into the per-user
/// storage directories introduced for backup isolation.
/// </summary>
public sealed class SchemaPatcherBackupMigrationTests
{
    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "FinanceManager.Tests";
        public string ContentRootPath { get; set; } = string.Empty;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
    }

    /// <summary>
    /// Verifies that a flat legacy file under <c>backups/</c> is moved into the owning user's
    /// subdirectory while its record's <c>StoragePath</c> stays resolvable.
    /// </summary>
    [Fact]
    public async Task RunPostMigrationPatches_MovesLegacyBackupFiles()
    {
        var temp = Path.Combine(Path.GetTempPath(), "fmtests", Guid.NewGuid().ToString());
        var backupsDir = Path.Combine(temp, "backups");
        Directory.CreateDirectory(backupsDir);

        var conn = new SqliteConnection($"Data Source={Path.Combine(temp, "test.db")}");
        await conn.OpenAsync(TestContext.Current.CancellationToken);
        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options;
            await using var db = new AppDbContext(options);
            db.Database.EnsureCreated();

            var userId = Guid.NewGuid();
            var legacyName = "legacy.zip";
            await File.WriteAllTextAsync(Path.Combine(backupsDir, legacyName), "zip", TestContext.Current.CancellationToken);
            var rec = new BackupRecord { OwnerUserId = userId, CreatedUtc = DateTime.UtcNow, FileName = legacyName, SizeBytes = 3, Source = "Upload", StoragePath = legacyName };
            db.Backups.Add(rec);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);

            var env = new TestHostEnvironment { ContentRootPath = temp };
            var sp = new ServiceCollection().AddSingleton<IHostEnvironment>(env).BuildServiceProvider();

            SchemaPatcher.RunPostMigrationPatches(sp, db, NullLogger.Instance);

            var movedPath = Path.Combine(backupsDir, userId.ToString("N"), legacyName);
            Assert.True(File.Exists(movedPath));
            Assert.False(File.Exists(Path.Combine(backupsDir, legacyName)));
            Assert.Equal(legacyName, db.Backups.Single(b => b.Id == rec.Id).StoragePath);
        }
        finally
        {
            await conn.DisposeAsync();
            try { Directory.Delete(temp, true); } catch { }
        }
    }

    /// <summary>
    /// Verifies that a name collision in the target user directory renames the moved file and
    /// updates the record's <c>StoragePath</c> instead of overwriting existing content.
    /// </summary>
    [Fact]
    public async Task RunPostMigrationPatches_RenamesOnCollision()
    {
        var temp = Path.Combine(Path.GetTempPath(), "fmtests", Guid.NewGuid().ToString());
        var backupsDir = Path.Combine(temp, "backups");
        Directory.CreateDirectory(backupsDir);

        var conn = new SqliteConnection($"Data Source={Path.Combine(temp, "test.db")}");
        await conn.OpenAsync(TestContext.Current.CancellationToken);
        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options;
            await using var db = new AppDbContext(options);
            db.Database.EnsureCreated();

            var userId = Guid.NewGuid();
            var userDir = Path.Combine(backupsDir, userId.ToString("N"));
            Directory.CreateDirectory(userDir);
            var name = "dup.zip";
            await File.WriteAllTextAsync(Path.Combine(userDir, name), "existing", TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(Path.Combine(backupsDir, name), "legacy", TestContext.Current.CancellationToken);
            var rec = new BackupRecord { OwnerUserId = userId, CreatedUtc = DateTime.UtcNow, FileName = name, SizeBytes = 6, Source = "Upload", StoragePath = name };
            db.Backups.Add(rec);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);

            var env = new TestHostEnvironment { ContentRootPath = temp };
            var sp = new ServiceCollection().AddSingleton<IHostEnvironment>(env).BuildServiceProvider();

            SchemaPatcher.RunPostMigrationPatches(sp, db, NullLogger.Instance);

            var updated = db.Backups.Single(b => b.Id == rec.Id);
            Assert.NotEqual(name, updated.StoragePath);
            Assert.True(File.Exists(Path.Combine(userDir, updated.StoragePath)));
            Assert.Equal("legacy", await File.ReadAllTextAsync(Path.Combine(userDir, updated.StoragePath), TestContext.Current.CancellationToken));
            Assert.Equal("existing", await File.ReadAllTextAsync(Path.Combine(userDir, name), TestContext.Current.CancellationToken));
        }
        finally
        {
            await conn.DisposeAsync();
            try { Directory.Delete(temp, true); } catch { }
        }
    }
}
