using System.Text;
using System.Text.Json;
using FinanceManager.Application.Aggregates;
using FinanceManager.Application.Statements;
using FinanceManager.Domain.Securities;
using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Backups;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FinanceManager.Tests.Infrastructure
{
    /// <summary>
    /// Covers backup and restore of the optional <c>CurrencyExchangeRates</c> payload
    /// section (global cross-user reference data): export includes all stored rates,
    /// import reseeds the pool deduplicated on (CurrencyCode, Date, Source), and backups
    /// predating the feature (no section) still import.
    /// </summary>
    public sealed class CurrencyExchangeRateBackupRestoreTests
    {
        private sealed class TestHostEnvironment : IHostEnvironment
        {
            public string EnvironmentName { get; set; } = "Development";
            public string ApplicationName { get; set; } = "FinanceManager.Tests";
            public string ContentRootPath { get; set; } = string.Empty;
            public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
        }

        private static AppDbContext CreateDb(out SqliteConnection conn)
        {
            conn = new SqliteConnection("DataSource=:memory:");
            conn.Open();
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options;
            var db = new AppDbContext(options);
            db.Database.EnsureCreated();
            return db;
        }

        private static BackupService CreateBackupService(AppDbContext db, string tempDir)
        {
            var backupsDir = Path.Combine(tempDir, "backups");
            Directory.CreateDirectory(backupsDir);
            var env = new TestHostEnvironment { ContentRootPath = tempDir };
            var services = new ServiceCollection().BuildServiceProvider();
            return new BackupService(db, env, NullLogger<BackupService>.Instance, services);
        }

        private static SetupImportService CreateImportService(AppDbContext db)
        {
            var aggregateMock = new Mock<IPostingAggregateService>();
            aggregateMock
                .Setup(x => x.RebuildForUserAsync(It.IsAny<Guid>(), It.IsAny<Action<int, int>>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            var statementDraftMock = new Mock<IStatementDraftService>();
            return new SetupImportService(db, statementDraftMock.Object, aggregateMock.Object, NullLogger<SetupImportService>.Instance);
        }

        private static async Task<string> ReadBackupNdjsonAsync(AppDbContext db, BackupService svc, Guid userId, string tempDir)
        {
            var dto = await svc.CreateAsync(userId, ct: CancellationToken.None);
            var rec = db.Backups.Single(b => b.Id == dto.Id);
            var full = Path.Combine(tempDir, "backups", userId.ToString("N"), rec.StoragePath);
            using var fs = File.OpenRead(full);
            using var zip = new System.IO.Compression.ZipArchive(fs, System.IO.Compression.ZipArchiveMode.Read, leaveOpen: false);
            var entry = zip.Entries[0];
            using var es = entry.Open();
            using var ms = new MemoryStream();
            es.CopyTo(ms);
            return Encoding.UTF8.GetString(ms.ToArray());
        }

        /// <summary>
        /// Export of a full backup contains the optional <c>CurrencyExchangeRates</c>
        /// section with the global rates (no owner filter — the pool is shared).
        /// </summary>
        [Fact]
        public async Task CreateAsync_Backup_ContainsCurrencyExchangeRatesSection()
        {
            await using var db = CreateDb(out var conn);
            var userId = Guid.NewGuid();
            db.CurrencyExchangeRates.Add(new CurrencyExchangeRate("USD", new DateTime(2026, 11, 20), 1.09m, "Frankfurter"));
            await db.SaveChangesAsync(CancellationToken.None);

            var temp = Path.Combine(Path.GetTempPath(), "fmtests", Guid.NewGuid().ToString());
            try
            {
                var svc = CreateBackupService(db, temp);
                var ndjson = await ReadBackupNdjsonAsync(db, svc, userId, temp);

                Assert.Contains("\"CurrencyExchangeRates\"", ndjson);
                Assert.Contains("\"RatePerEur\"", ndjson);
                Assert.Contains("USD", ndjson);
            }
            finally
            {
                try { Directory.Delete(temp, true); } catch { }
                conn.Dispose();
            }
        }

        /// <summary>
        /// Importing a payload with exchange rates reseeds the global pool; a row whose
        /// (CurrencyCode, Date, Source) already exists is not duplicated.
        /// </summary>
        [Fact]
        public async Task ImportAsync_RestoresRates_Deduplicated()
        {
            await using var db = CreateDb(out var conn);
            var userId = Guid.NewGuid();
            var createdUtc = new DateTime(2026, 11, 20, 8, 0, 0, DateTimeKind.Utc);
            db.CurrencyExchangeRates.Add(new CurrencyExchangeRate("USD", new DateTime(2026, 11, 20), 1.09m, "Frankfurter"));
            await db.SaveChangesAsync(CancellationToken.None);

            var data = new
            {
                CurrencyExchangeRates = new[]
                {
                    // same (cc, date, source) as the existing row -> skipped
                    new CurrencyExchangeRate.CurrencyExchangeRateBackupDto(Guid.NewGuid(), "USD", new DateTime(2026, 11, 20), 9.99m, "Frankfurter", createdUtc),
                    // new rows -> imported
                    new CurrencyExchangeRate.CurrencyExchangeRateBackupDto(Guid.NewGuid(), "USD", new DateTime(2026, 11, 21), 1.10m, "Frankfurter", createdUtc),
                    new CurrencyExchangeRate.CurrencyExchangeRateBackupDto(Guid.NewGuid(), "chf", new DateTime(2026, 11, 21), 0.94m, "Frankfurter", createdUtc)
                }
            };
            var payload = "{\"Type\":\"Backup\",\"Version\":3}\n" + JsonSerializer.Serialize(data);
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(payload));

            var svc = CreateImportService(db);
            await svc.ImportAsync(userId, stream, replaceExisting: false, CancellationToken.None);

            var rates = await db.CurrencyExchangeRates.OrderBy(r => r.Date).ThenBy(r => r.CurrencyCode).ToListAsync(CancellationToken.None);
            Assert.Equal(3, rates.Count);
            // Existing row kept (original rate, not the backup's 9.99).
            Assert.Equal(1.09m, rates[0].RatePerEur);
            // Lowercase backup code normalized to uppercase on import.
            Assert.Equal("CHF", rates[1].CurrencyCode);
            Assert.Equal(createdUtc, rates[1].CreatedUtc);
            Assert.Equal(1.10m, rates[2].RatePerEur);
            conn.Dispose();
        }

        /// <summary>
        /// A backup created before the FX feature (payload without the optional section)
        /// still imports cleanly.
        /// </summary>
        [Fact]
        public async Task ImportAsync_PayloadWithoutExchangeRateSection_StillWorks()
        {
            await using var db = CreateDb(out var conn);
            var userId = Guid.NewGuid();

            var data = new { Accounts = Array.Empty<object>() };
            var payload = "{\"Type\":\"Backup\",\"Version\":3}\n" + JsonSerializer.Serialize(data);
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(payload));

            var svc = CreateImportService(db);
            await svc.ImportAsync(userId, stream, replaceExisting: false, CancellationToken.None);

            Assert.Equal(0, await db.CurrencyExchangeRates.CountAsync(CancellationToken.None));
            conn.Dispose();
        }
    }
}
