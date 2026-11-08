using System.Text;
using System.Text.Json;
using FinanceManager.Application.Aggregates;
using FinanceManager.Application.Statements;
using FinanceManager.Domain.Accounts;
using FinanceManager.Domain.Postings;
using FinanceManager.Domain.Savings;
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
    /// Covers backup and restore of the new savings-plan data: the optional
    /// <c>SavingsPlanAccountLinks</c> payload section and the actual-balance report marker
    /// (<c>SavingsPlanSubType</c>) on plan postings must survive export and import
    /// (with id remapping for plans and accounts).
    /// </summary>
    public sealed class SavingsPlanLinkBackupRestoreTests
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
        /// Export of a full backup contains the optional <c>SavingsPlanAccountLinks</c> section and
        /// the actual-report marker on the posting.
        /// </summary>
        [Fact]
        public async Task CreateAsync_Backup_ContainsLinksAndActualReportMarker()
        {
            await using var db = CreateDb(out var conn);
            var userId = Guid.NewGuid();
            var plan = new SavingsPlan(userId, "Plan", SavingsPlanType.Open, null, null, null);
            var account = new Account(userId, AccountType.Savings, "Vertragskonto", null, Guid.NewGuid());
            db.SavingsPlans.Add(plan);
            db.Accounts.Add(account);
            db.SavingsPlanAccountLinks.Add(new SavingsPlanAccountLink(plan.Id, account.Id, userId));
            db.Postings.Add(new Posting(plan.Id, PostingKind.SavingsPlan, null, null, plan.Id, null, DateTime.UtcNow, 75.30m, "Istmeldung", null, null, null)
                .SetSavingsPlanSubType(SavingsPlanPostingSubType.ActualReport));
            await db.SaveChangesAsync(CancellationToken.None);

            var temp = Path.Combine(Path.GetTempPath(), "fmtests", Guid.NewGuid().ToString());
            try
            {
                var svc = CreateBackupService(db, temp);
                var ndjson = await ReadBackupNdjsonAsync(db, svc, userId, temp);

                Assert.Contains("\"SavingsPlanAccountLinks\"", ndjson);
                Assert.Contains("\"SavingsPlanSubType\":0", ndjson);
            }
            finally
            {
                try { Directory.Delete(temp, true); } catch { }
                conn.Dispose();
            }
        }

        /// <summary>
        /// Importing a payload with links and an actual-report posting restores the link with
        /// remapped ids and keeps the correction marker.
        /// </summary>
        [Fact]
        public async Task ImportAsync_RestoresLinksWithRemappedIdsAndMarker()
        {
            await using var db = CreateDb(out var conn);
            var userId = Guid.NewGuid();

            var oldPlanId = Guid.NewGuid();
            var oldAccountId = Guid.NewGuid();
            var oldPostingId = Guid.NewGuid();
            var data = new
            {
                SavingsPlans = new[]
                {
                    new { Id = oldPlanId, OwnerUserId = userId, Name = "Plan", Type = 2, TargetAmount = (decimal?)null, TargetDate = (DateTime?)null, Interval = (int?)null, IsActive = true, CreatedUtc = DateTime.UtcNow, ModifiedUtc = (DateTime?)null, ArchivedUtc = (DateTime?)null, CategoryId = (Guid?)null, ContractNumber = (string?)null, SymbolAttachmentId = (Guid?)null }
                },
                Accounts = new[]
                {
                    new { Id = oldAccountId, OwnerUserId = userId, Type = 1, Name = "Vertragskonto", Iban = (string?)null, CurrentBalance = 0m, BankContactId = Guid.NewGuid(), SymbolAttachmentId = (Guid?)null, SavingsPlanExpectation = 0, CreatedUtc = DateTime.UtcNow, ModifiedUtc = (DateTime?)null, SecurityProcessingEnabled = true, IsCollectionAccount = false }
                },
                SavingsPlanAccountLinks = new[]
                {
                    new { Id = Guid.NewGuid(), SavingsPlanId = oldPlanId, AccountId = oldAccountId, OwnerUserId = userId, CreatedUtc = DateTime.UtcNow, ModifiedUtc = (DateTime?)null }
                },
                Postings = new[]
                {
                    new { Id = oldPostingId, SourceId = oldPlanId, Kind = 2, AccountId = (Guid?)null, ContactId = (Guid?)null, SavingsPlanId = (Guid?)oldPlanId, SecurityId = (Guid?)null, BookingDate = DateTime.UtcNow, ValutaDate = DateTime.UtcNow, Amount = 75.30m, OriginalAmount = (decimal?)null, Subject = "Istmeldung", RecipientName = (string?)null, Description = (string?)null, SecuritySubType = (int?)null, Quantity = (decimal?)null, GroupId = (Guid?)null, ParentId = (Guid?)null, LinkedPostingId = (Guid?)null, ReversedByPostingId = (Guid?)null, ReversalForPostingId = (Guid?)null, ReversedByUserId = (Guid?)null, ReversedAtUtc = (DateTime?)null, IsPreliminary = false, SavingsPlanSubType = 0 }
                }
            };
            var payload = "{\"Type\":\"Backup\",\"Version\":3}\n" + JsonSerializer.Serialize(data);
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(payload));

            var aggregateMock = new Mock<IPostingAggregateService>();
            aggregateMock
                .Setup(x => x.RebuildForUserAsync(It.IsAny<Guid>(), It.IsAny<Action<int, int>>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            var statementDraftMock = new Mock<IStatementDraftService>();
            var svc = new SetupImportService(db, statementDraftMock.Object, aggregateMock.Object, NullLogger<SetupImportService>.Instance);

            await svc.ImportAsync(userId, stream, replaceExisting: false, CancellationToken.None);

            var link = await db.SavingsPlanAccountLinks.SingleAsync(CancellationToken.None);
            var plan = await db.SavingsPlans.SingleAsync(CancellationToken.None);
            var account = await db.Accounts.SingleAsync(a => a.Name == "Vertragskonto", CancellationToken.None);
            Assert.Equal(plan.Id, link.SavingsPlanId);
            Assert.Equal(account.Id, link.AccountId);
            Assert.Equal(userId, link.OwnerUserId);

            var posting = await db.Postings.SingleAsync(CancellationToken.None);
            Assert.Equal(SavingsPlanPostingSubType.ActualReport, posting.SavingsPlanSubType);
            Assert.Equal(plan.Id, posting.SavingsPlanId);
            conn.Dispose();
        }

        /// <summary>
        /// A backup created before the link feature (payload without the optional section) still
        /// imports cleanly.
        /// </summary>
        [Fact]
        public async Task ImportAsync_PayloadWithoutLinkSection_StillWorks()
        {
            await using var db = CreateDb(out var conn);
            var userId = Guid.NewGuid();

            var data = new
            {
                SavingsPlans = new[]
                {
                    new { Id = Guid.NewGuid(), OwnerUserId = userId, Name = "Plan", Type = 2, TargetAmount = (decimal?)null, TargetDate = (DateTime?)null, Interval = (int?)null, IsActive = true, CreatedUtc = DateTime.UtcNow, ModifiedUtc = (DateTime?)null, ArchivedUtc = (DateTime?)null, CategoryId = (Guid?)null, ContractNumber = (string?)null, SymbolAttachmentId = (Guid?)null }
                }
            };
            var payload = "{\"Type\":\"Backup\",\"Version\":3}\n" + JsonSerializer.Serialize(data);
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(payload));

            var aggregateMock = new Mock<IPostingAggregateService>();
            aggregateMock
                .Setup(x => x.RebuildForUserAsync(It.IsAny<Guid>(), It.IsAny<Action<int, int>>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            var statementDraftMock = new Mock<IStatementDraftService>();
            var svc = new SetupImportService(db, statementDraftMock.Object, aggregateMock.Object, NullLogger<SetupImportService>.Instance);

            await svc.ImportAsync(userId, stream, replaceExisting: false, CancellationToken.None);

            Assert.Equal(1, await db.SavingsPlans.CountAsync(CancellationToken.None));
            Assert.Equal(0, await db.SavingsPlanAccountLinks.CountAsync(CancellationToken.None));
            conn.Dispose();
        }
    }
}
