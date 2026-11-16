using System.Text;
using System.Text.Json;
using FinanceManager.Application.Aggregates;
using FinanceManager.Application.Statements;
using FinanceManager.Domain.Accounts;
using FinanceManager.Domain.Reports;
using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Backups;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace FinanceManager.Tests.Infrastructure
{
    /// <summary>
    /// Covers backup and restore of the optional <c>ReportSuppressedExpectations</c> payload
    /// section: export includes the markers, import remaps the scoped favorite and the target
    /// entity to the newly imported ids, entries whose favorite or entity is not part of the
    /// backup are skipped, and backups predating the feature (no section) still import.
    /// </summary>
    public sealed class ReportSuppressedExpectationBackupRestoreTests
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
        /// Export of a full backup contains the optional <c>ReportSuppressedExpectations</c>
        /// section with the suppression identity fields.
        /// </summary>
        [Fact]
        public async Task CreateAsync_Backup_ContainsSuppressedExpectationsSection()
        {
            await using var db = CreateDb(out var conn);
            var userId = Guid.NewGuid();
            var favorite = new ReportFavorite(userId, "Fav", PostingKind.Bank, false, ReportInterval.Month, false, false, true, true, true, 12);
            db.ReportFavorites.Add(favorite);
            var account = new Account(userId, AccountType.Giro, "Festgeld", null, Guid.NewGuid());
            db.Accounts.Add(account);
            db.ReportSuppressedExpectations.Add(new ReportSuppressedExpectation(userId, favorite.Id, PostingKind.Bank, account.Id, new DateTime(2025, 5, 10)));
            await db.SaveChangesAsync(CancellationToken.None);

            var temp = Path.Combine(Path.GetTempPath(), "fmtests", Guid.NewGuid().ToString());
            try
            {
                var svc = CreateBackupService(db, temp);
                var ndjson = await ReadBackupNdjsonAsync(db, svc, userId, temp);

                Assert.Contains("\"ReportSuppressedExpectations\"", ndjson);
                Assert.Contains("\"PriorYearDate\"", ndjson);
                Assert.Contains(favorite.Id.ToString(), ndjson);
            }
            finally
            {
                try { Directory.Delete(temp, true); } catch { }
                conn.Dispose();
            }
        }

        /// <summary>
        /// Importing a payload with suppressions restores them bound to the remapped favorite
        /// and remapped entity id, while entries whose favorite or entity cannot be remapped
        /// are skipped.
        /// </summary>
        [Fact]
        public async Task ImportAsync_RestoresSuppressions_WithRemappedIds()
        {
            await using var db = CreateDb(out var conn);
            var userId = Guid.NewGuid();
            var oldFavoriteId = Guid.NewGuid();
            var oldAccountId = Guid.NewGuid();
            var suppressedAt = new DateTime(2026, 5, 3, 9, 30, 0, DateTimeKind.Utc);
            var priorYearDate = new DateTime(2025, 5, 10, 0, 0, 0, DateTimeKind.Utc);

            var data = new
            {
                Accounts = new[]
                {
                    new Account.AccountBackupDto(oldAccountId, userId, AccountType.Giro, "Festgeld", null, 0m, Guid.NewGuid(), null, SavingsPlanExpectation.None, DateTime.UtcNow, null)
                },
                ReportFavorites = new[]
                {
                    new ReportFavorite.ReportFavoriteBackupDto(oldFavoriteId, userId, "Fav", PostingKind.Bank, false, ReportInterval.Month, 12, false, false, true, true, true, null, null, null, null, null, null, null, null, null, null, false, FinanceManager.Shared.Dtos.Reports.SavingsPlanPostingSource.PlanPostings)
                },
                ReportSuppressedExpectations = new[]
                {
                    new ReportSuppressedExpectation.ReportSuppressedExpectationBackupDto(Guid.NewGuid(), userId, oldFavoriteId, PostingKind.Bank, oldAccountId, priorYearDate, suppressedAt, DateTime.UtcNow, null),
                    // Favorite not in backup -> skipped
                    new ReportSuppressedExpectation.ReportSuppressedExpectationBackupDto(Guid.NewGuid(), userId, Guid.NewGuid(), PostingKind.Bank, oldAccountId, priorYearDate, suppressedAt, DateTime.UtcNow, null),
                    // Entity not in backup -> skipped
                    new ReportSuppressedExpectation.ReportSuppressedExpectationBackupDto(Guid.NewGuid(), userId, oldFavoriteId, PostingKind.Bank, Guid.NewGuid(), priorYearDate, suppressedAt, DateTime.UtcNow, null)
                }
            };
            var payload = "{\"Type\":\"Backup\",\"Version\":3}\n" + JsonSerializer.Serialize(data);
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(payload));

            var svc = CreateImportService(db);
            await svc.ImportAsync(userId, stream, replaceExisting: false, CancellationToken.None);

            var favorite = await db.ReportFavorites.SingleAsync(CancellationToken.None);
            var account = await db.Accounts.SingleAsync(a => a.Name == "Festgeld", CancellationToken.None);
            var suppression = await db.ReportSuppressedExpectations.SingleAsync(CancellationToken.None);

            Assert.Equal(favorite.Id, suppression.ReportFavoriteId);
            Assert.NotEqual(oldFavoriteId, suppression.ReportFavoriteId);
            Assert.Equal(account.Id, suppression.EntityId);
            Assert.NotEqual(oldAccountId, suppression.EntityId);
            Assert.Equal(PostingKind.Bank, suppression.Kind);
            Assert.Equal(priorYearDate, suppression.PriorYearDate);
            Assert.Equal(suppressedAt, suppression.SuppressedAtUtc);
            Assert.Equal(userId, suppression.OwnerUserId);
            conn.Dispose();
        }

        /// <summary>
        /// A backup created before the suppression feature (payload without the optional
        /// section) still imports cleanly.
        /// </summary>
        [Fact]
        public async Task ImportAsync_PayloadWithoutSuppressionSection_StillWorks()
        {
            await using var db = CreateDb(out var conn);
            var userId = Guid.NewGuid();

            var data = new
            {
                ReportFavorites = new[]
                {
                    new ReportFavorite.ReportFavoriteBackupDto(Guid.NewGuid(), userId, "Fav", PostingKind.Bank, false, ReportInterval.Month, 12, false, false, true, true, true, null, null, null, null, null, null, null, null, null, null, false, FinanceManager.Shared.Dtos.Reports.SavingsPlanPostingSource.PlanPostings)
                }
            };
            var payload = "{\"Type\":\"Backup\",\"Version\":3}\n" + JsonSerializer.Serialize(data);
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(payload));

            var svc = CreateImportService(db);
            await svc.ImportAsync(userId, stream, replaceExisting: false, CancellationToken.None);

            Assert.Equal(1, await db.ReportFavorites.CountAsync(CancellationToken.None));
            Assert.Equal(0, await db.ReportSuppressedExpectations.CountAsync(CancellationToken.None));
            conn.Dispose();
        }
    }
}
