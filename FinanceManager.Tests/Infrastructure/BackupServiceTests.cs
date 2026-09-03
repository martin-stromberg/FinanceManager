using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FinanceManager.Domain.Budget;
using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Backups;
using FinanceManager.Shared.Dtos.Budget;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace FinanceManager.Tests.Infrastructure
{
    /// <summary>
    /// Covers <see cref="BackupService"/> end to end: creating a backup zip and its database record, restoring an
    /// uploaded backup only after it passes format/version/size validation (the guard against malformed or
    /// maliciously crafted archives, including zip-bomb style uncompressed-size limits), and the plain CRUD
    /// operations (list, download, delete) that manage previously created backups.
    /// </summary>
    public class BackupServiceTests
    {
        private sealed class TestHostEnvironment : IHostEnvironment
        {
            public string EnvironmentName { get; set; } = "Development";
            public string ApplicationName { get; set; } = "FinanceManager.Tests";
            public string ContentRootPath { get; set; } = string.Empty;
            public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
        }

        /// <summary>
        /// Verifies that creating a backup writes a zip file to disk containing an NDJSON entry whose first line is
        /// the expected "Backup" header, and that a matching <see cref="BackupRecord"/> is persisted pointing at
        /// that file - the baseline contract every restore test below depends on being correct.
        /// </summary>
        [Fact]
        public async Task CreateAsync_CreatesZipAndPersistRecord()
        {
            var dbName = Guid.NewGuid().ToString();
            var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(dbName).Options;
            await using var db = new AppDbContext(options);

            var temp = Path.Combine(Path.GetTempPath(), "fmtests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(temp);
            var backupsDir = Path.Combine(temp, "backups");
            Directory.CreateDirectory(backupsDir);
            var env = new TestHostEnvironment { ContentRootPath = temp };
            var services = new ServiceCollection().BuildServiceProvider();
            var logger = NullLogger<BackupService>.Instance;

            var svc = new BackupService(db, env, logger, services);
            var userId = Guid.NewGuid();

            var dto = await svc.CreateAsync(userId, CancellationToken.None);

            Assert.NotNull(dto);
            // record persisted
            var rec = db.Backups.FirstOrDefault(b => b.Id == dto.Id);
            Assert.NotNull(rec);
            var full = Path.Combine(backupsDir, rec.StoragePath);
            Assert.True(File.Exists(full));

            // Check zip contains ndjson
            using var fs = File.OpenRead(full);
            using var zip = new ZipArchive(fs, ZipArchiveMode.Read, leaveOpen: false);
            var entry = zip.Entries.FirstOrDefault();
            Assert.NotNull(entry);
            using var es = entry.Open();
            using var ms = new MemoryStream();
            es.CopyTo(ms);
            ms.Position = 0;
            var content = Encoding.UTF8.GetString(ms.ToArray());
            Assert.StartsWith("{\"Type\":\"Backup\"", content);

            // cleanup
            try { Directory.Delete(temp, true); } catch { }
        }

        /// <summary>
        /// Verifies that uploading a raw NDJSON payload (not wrapped in a zip) is rejected with
        /// "Err_Backup_UnsupportedFormat" - restore only ever accepts the zip container format produced by
        /// <see cref="BackupService.CreateAsync"/>, so a bare data file must fail fast with a clear error rather than
        /// being partially parsed.
        /// </summary>
        [Fact]
        public async Task UploadAsync_NonZip_IsRejected()
        {
            var dbName = Guid.NewGuid().ToString();
            var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(dbName).Options;
            await using var db = new AppDbContext(options);

            var temp = Path.Combine(Path.GetTempPath(), "fmtests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(temp);
            var backupsDir = Path.Combine(temp, "backups");
            Directory.CreateDirectory(backupsDir);
            var env = new TestHostEnvironment { ContentRootPath = temp };
            var services = new ServiceCollection().BuildServiceProvider();
            var logger = NullLogger<BackupService>.Instance;

            var svc = new BackupService(db, env, logger, services);
            var userId = Guid.NewGuid();

            var ndjson = CreateValidNdjson();
            await using var msIn = new MemoryStream(Encoding.UTF8.GetBytes(ndjson));

            var ex = await Assert.ThrowsAsync<FinanceManager.Application.Backups.BackupValidationException>(
                () => svc.UploadAsync(userId, msIn, "upload.ndjson", CancellationToken.None));
            Assert.Equal("Err_Backup_UnsupportedFormat", ex.Code);

            try { Directory.Delete(temp, true); } catch { }
        }

        /// <summary>
        /// Verifies that a well-formed backup zip is accepted, stored under the caller-supplied file name, and
        /// written to the backups directory - the positive counterpart to the validation-rejection tests below.
        /// </summary>
        [Fact]
        public async Task UploadAsync_ValidZip_Persists()
        {
            var dbName = Guid.NewGuid().ToString();
            var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(dbName).Options;
            await using var db = new AppDbContext(options);

            var temp = Path.Combine(Path.GetTempPath(), "fmtests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(temp);
            var backupsDir = Path.Combine(temp, "backups");
            Directory.CreateDirectory(backupsDir);
            var env = new TestHostEnvironment { ContentRootPath = temp };
            var services = new ServiceCollection().BuildServiceProvider();
            var logger = NullLogger<BackupService>.Instance;

            var svc = new BackupService(db, env, logger, services);
            var userId = Guid.NewGuid();

            await using var zip = CreateZip(("backup.ndjson", CreateValidNdjson()));
            var dto = await svc.UploadAsync(userId, zip, "custom.zip", CancellationToken.None);

            Assert.Equal("custom.zip", dto.FileName);
            Assert.True(File.Exists(Path.Combine(backupsDir, "custom.zip")));

            try { Directory.Delete(temp, true); } catch { }
        }

        /// <summary>
        /// Verifies that a zip whose single entry does not use the expected "*.ndjson" naming is rejected with
        /// "Err_Backup_UnexpectedEntryName" - restore trusts the entry it reads, so an unexpected name is treated as
        /// a sign the archive was not produced by this application (or was tampered with) rather than being read
        /// speculatively.
        /// </summary>
        /// <param name="entryName">The non-conforming zip entry name to test.</param>
        /// <param name="expectedCode">The <c>BackupValidationException.Code</c> expected for that entry name.</param>
        [Theory]
        [InlineData("notes.txt", "Err_Backup_UnexpectedEntryName")]
        [InlineData("backup.txt", "Err_Backup_UnexpectedEntryName")]
        public async Task UploadAsync_UnexpectedEntryName_IsRejected(string entryName, string expectedCode)
        {
            var svc = CreateService(out var db, out var temp);
            await using (db)
            {
                await using var zip = CreateZip((entryName, CreateValidNdjson()));
                var ex = await Assert.ThrowsAsync<FinanceManager.Application.Backups.BackupValidationException>(
                    () => svc.UploadAsync(Guid.NewGuid(), zip, "custom.zip", CancellationToken.None));
                Assert.Equal(expectedCode, ex.Code);
            }

            try { Directory.Delete(temp, true); } catch { }
        }

        /// <summary>
        /// Verifies that a zip containing more than one entry is rejected with "Err_Backup_TooManyEntries" - a
        /// genuine backup always contains exactly one NDJSON entry, so multiple entries indicate either a corrupted
        /// export or a crafted archive trying to smuggle extra content past restore.
        /// </summary>
        [Fact]
        public async Task UploadAsync_MultipleEntries_IsRejected()
        {
            var svc = CreateService(out var db, out var temp);
            await using (db)
            {
                await using var zip = CreateZip(("backup.ndjson", CreateValidNdjson()), ("backup-2.ndjson", CreateValidNdjson()));
                var ex = await Assert.ThrowsAsync<FinanceManager.Application.Backups.BackupValidationException>(
                    () => svc.UploadAsync(Guid.NewGuid(), zip, "custom.zip", CancellationToken.None));
                Assert.Equal("Err_Backup_TooManyEntries", ex.Code);
            }

            try { Directory.Delete(temp, true); } catch { }
        }

        /// <summary>
        /// Verifies that a backup written with a newer/unsupported format version is rejected with
        /// "Err_Backup_UnsupportedVersion" rather than being partially imported - restoring data written by a
        /// format version the current code does not understand would silently drop or misinterpret fields.
        /// </summary>
        [Fact]
        public async Task UploadAsync_UnsupportedVersion_IsRejected()
        {
            var svc = CreateService(out var db, out var temp);
            await using (db)
            {
                await using var zip = CreateZip(("backup.ndjson", CreateValidNdjson(version: 2)));
                var ex = await Assert.ThrowsAsync<FinanceManager.Application.Backups.BackupValidationException>(
                    () => svc.UploadAsync(Guid.NewGuid(), zip, "custom.zip", CancellationToken.None));
                Assert.Equal("Err_Backup_UnsupportedVersion", ex.Code);
            }

            try { Directory.Delete(temp, true); } catch { }
        }

        /// <summary>
        /// Verifies that restore enforces <see cref="BackupSecurityOptions.MaxUncompressedNdjsonBytes"/> and rejects
        /// an entry that decompresses beyond the configured limit with "Err_Backup_UncompressedTooLarge" - the
        /// zip-bomb guard that stops a small, highly compressible upload from exhausting memory/disk when expanded.
        /// </summary>
        [Fact]
        public async Task UploadAsync_UncompressedLimit_IsRejected()
        {
            var svc = CreateService(
                out var db,
                out var temp,
                new BackupSecurityOptions { MaxUncompressedNdjsonBytes = 10, MaxCompressionRatio = 1000 });
            await using (db)
            {
                await using var zip = CreateZip(("backup.ndjson", CreateValidNdjson()));
                var ex = await Assert.ThrowsAsync<FinanceManager.Application.Backups.BackupValidationException>(
                    () => svc.UploadAsync(Guid.NewGuid(), zip, "custom.zip", CancellationToken.None));
                Assert.Equal("Err_Backup_UncompressedTooLarge", ex.Code);
            }

            try { Directory.Delete(temp, true); } catch { }
        }

        /// <summary>
        /// Verifies that <see cref="BackupService.ListAsync"/> surfaces a backup record even when its file was
        /// placed on disk out of band from a normal <see cref="BackupService.CreateAsync"/> call - i.e. listing
        /// reflects the database record, not a directory scan.
        /// </summary>
        [Fact]
        public async Task ListAsync_ReturnsPersistedBackups()
        {
            var dbName = Guid.NewGuid().ToString();
            var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(dbName).Options;
            await using var db = new AppDbContext(options);

            var temp = Path.Combine(Path.GetTempPath(), "fmtests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(temp);
            var backupsDir = Path.Combine(temp, "backups");
            Directory.CreateDirectory(backupsDir);
            var env = new TestHostEnvironment { ContentRootPath = temp };
            var services = new ServiceCollection().BuildServiceProvider();
            var logger = NullLogger<BackupService>.Instance;

            var svc = new BackupService(db, env, logger, services);
            var userId = Guid.NewGuid();

            // create a manual file and record
            var filename = "manual.zip";
            var filepath = Path.Combine(temp, "backups", filename);
            using (var fs = File.Create(filepath)) { }
            var rec = new BackupRecord { OwnerUserId = userId, CreatedUtc = DateTime.UtcNow, FileName = filename, SizeBytes = new FileInfo(filepath).Length, Source = "Test", StoragePath = filename };
            db.Backups.Add(rec);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);

            var list = await svc.ListAsync(userId, CancellationToken.None);
            Assert.Contains(list, x => x.Id == rec.Id);

            try { Directory.Delete(temp, true); } catch { }
        }

        /// <summary>
        /// Verifies that deleting a backup removes both the file on disk and its database record - leaving either
        /// one behind would either waste disk space or expose a listing entry whose download would 404.
        /// </summary>
        [Fact]
        public async Task DeleteAsync_RemovesFileAndRecord()
        {
            var dbName = Guid.NewGuid().ToString();
            var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(dbName).Options;
            await using var db = new AppDbContext(options);

            var temp = Path.Combine(Path.GetTempPath(), "fmtests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(temp);
            var backupsDir = Path.Combine(temp, "backups");
            Directory.CreateDirectory(backupsDir);
            var env = new TestHostEnvironment { ContentRootPath = temp };
            var services = new ServiceCollection().BuildServiceProvider();
            var logger = NullLogger<BackupService>.Instance;

            var svc = new BackupService(db, env, logger, services);
            var userId = Guid.NewGuid();

            var filename = "todelete.zip";
            var filepath = Path.Combine(temp, "backups", filename);
            await using (var fs = File.Create(filepath)) { }
            var rec = new BackupRecord { OwnerUserId = userId, CreatedUtc = DateTime.UtcNow, FileName = filename, SizeBytes = new FileInfo(filepath).Length, Source = "Test", StoragePath = filename };
            db.Backups.Add(rec);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);

            var ok = await svc.DeleteAsync(userId, rec.Id, CancellationToken.None);
            Assert.True(ok);
            Assert.False(File.Exists(filepath));
            var found = await db.Backups.FindAsync(new object?[] { rec.Id }, TestContext.Current.CancellationToken);
            Assert.Null(found);

            try { Directory.Delete(temp, true); } catch { }
        }

        /// <summary>
        /// Verifies that downloading a backup returns a stream whose bytes match the file on disk exactly -
        /// guarding against accidental corruption (e.g. wrong encoding or partial reads) in the download path.
        /// </summary>
        [Fact]
        public async Task OpenDownloadAsync_ReturnsStream_WhenFileExists()
        {
            var dbName = Guid.NewGuid().ToString();
            var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(dbName).Options;
            await using var db = new AppDbContext(options);

            var temp = Path.Combine(Path.GetTempPath(), "fmtests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(temp);
            var backupsDir = Path.Combine(temp, "backups");
            Directory.CreateDirectory(backupsDir);
            var env = new TestHostEnvironment { ContentRootPath = temp };
            var services = new ServiceCollection().BuildServiceProvider();
            var logger = NullLogger<BackupService>.Instance;

            var svc = new BackupService(db, env, logger, services);
            var userId = Guid.NewGuid();

            var filename = "todownload.zip";
            var filepath = Path.Combine(temp, "backups", filename);
            await using (var fs = File.Create(filepath)) { var b = new byte[] { 1, 2, 3 }; await fs.WriteAsync(b, TestContext.Current.CancellationToken); }
            var rec = new BackupRecord { OwnerUserId = userId, CreatedUtc = DateTime.UtcNow, FileName = filename, SizeBytes = new FileInfo(filepath).Length, Source = "Test", StoragePath = filename };
            db.Backups.Add(rec);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);

            var stream = await svc.OpenDownloadAsync(userId, rec.Id, CancellationToken.None);
            Assert.NotNull(stream);
            using var sr = new MemoryStream();
            await stream!.CopyToAsync(sr, TestContext.Current.CancellationToken);
            Assert.Equal(new byte[] { 1, 2, 3 }, sr.ToArray());

            try { Directory.Delete(temp, true); } catch { }
        }

        /// <summary>
        /// Verifies that a created backup's NDJSON payload includes budget rules keyed by both a category and by a
        /// purpose - budget rules can target either dimension, so an export that only walked one association would
        /// silently drop half of a user's budget configuration on restore.
        /// </summary>
        [Fact]
        public async Task CreateAsync_IncludesBudgetRulesForCategoryAndPurpose()
        {
            var svc = CreateService(out var db, out var temp);

            var userId = Guid.NewGuid();
            var category = new BudgetCategory(userId, "Arbeit");
            var purpose = new BudgetPurpose(userId, "Gehalt", BudgetSourceType.Contact, Guid.NewGuid());
            var categoryRule = new BudgetRule(userId, null, category.Id, 100m, BudgetIntervalType.Monthly, new DateOnly(2026, 1, 1));
            var purposeRule = new BudgetRule(userId, purpose.Id, null, 200m, BudgetIntervalType.Monthly, new DateOnly(2026, 1, 1));

            db.BudgetCategories.Add(category);
            db.BudgetPurposes.Add(purpose);
            db.BudgetRules.Add(categoryRule);
            db.BudgetRules.Add(purposeRule);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);

            var dto = await svc.CreateAsync(userId, CancellationToken.None);
            Assert.NotNull(dto);

            var rec = db.Backups.FirstOrDefault(b => b.Id == dto.Id);
            Assert.NotNull(rec);
            var full = Path.Combine(temp, "backups", rec.StoragePath);
            Assert.True(File.Exists(full));

            using var fs = File.OpenRead(full);
            using var zip = new ZipArchive(fs, ZipArchiveMode.Read, leaveOpen: false);
            var entry = zip.Entries.FirstOrDefault();
            Assert.NotNull(entry);
            using var es = entry.Open();
            using var sr = new StreamReader(es, Encoding.UTF8);
            _ = await sr.ReadLineAsync(TestContext.Current.CancellationToken);
            var data = await sr.ReadToEndAsync(TestContext.Current.CancellationToken);

            using var doc = JsonDocument.Parse(data);
            var rules = doc.RootElement.GetProperty("BudgetRules");
            Assert.Equal(2, rules.GetArrayLength());

            var hasCategoryRule = rules.EnumerateArray().Any(r =>
                r.TryGetProperty("BudgetCategoryId", out var c) &&
                c.GetString() == category.Id.ToString());

            var hasPurposeRule = rules.EnumerateArray().Any(r =>
                r.TryGetProperty("BudgetPurposeId", out var p) &&
                p.GetString() == purpose.Id.ToString());

            Assert.True(hasCategoryRule, "Expected a budget rule for a category.");
            Assert.True(hasPurposeRule, "Expected a budget rule for a purpose.");

            try { Directory.Delete(temp, true); } catch { }
        }

        private static BackupService CreateService(out AppDbContext db, out string temp, BackupSecurityOptions? securityOptions = null)
        {
            var dbName = Guid.NewGuid().ToString();
            var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(dbName).Options;
            db = new AppDbContext(options);

            temp = Path.Combine(Path.GetTempPath(), "fmtests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(temp);
            Directory.CreateDirectory(Path.Combine(temp, "backups"));
            var env = new TestHostEnvironment { ContentRootPath = temp };
            var services = new ServiceCollection().BuildServiceProvider();
            var logger = NullLogger<BackupService>.Instance;
            return new BackupService(db, env, logger, services, Options.Create(securityOptions ?? new BackupSecurityOptions()));
        }

        private static MemoryStream CreateZip(params (string EntryName, string Content)[] entries)
        {
            var stream = new MemoryStream();
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
            {
                foreach (var (entryName, content) in entries)
                {
                    var entry = zip.CreateEntry(entryName, CompressionLevel.NoCompression);
                    using var entryStream = entry.Open();
                    var bytes = Encoding.UTF8.GetBytes(content);
                    entryStream.Write(bytes, 0, bytes.Length);
                }
            }

            stream.Position = 0;
            return stream;
        }

        private static string CreateValidNdjson(int version = 3)
        {
            var data = new Dictionary<string, object[]>
            {
                ["Accounts"] = [],
                ["Contacts"] = [],
                ["ContactCategories"] = [],
                ["AliasNames"] = [],
                ["SavingsPlanCategories"] = [],
                ["SavingsPlans"] = [],
                ["SecurityCategories"] = [],
                ["Securities"] = [],
                ["SecurityPrices"] = [],
                ["StatementImports"] = [],
                ["StatementEntries"] = [],
                ["Postings"] = [],
                ["StatementDrafts"] = [],
                ["StatementDraftEntries"] = [],
                ["ReportFavorites"] = [],
                ["HomeKpis"] = [],
                ["AttachmentCategories"] = [],
                ["Attachments"] = [],
                ["Notifications"] = [],
                ["AccountShares"] = [],
                ["BudgetCategories"] = [],
                ["BudgetPurposes"] = [],
                ["BudgetRules"] = [],
                ["BudgetOverrides"] = []
            };

            return System.Text.Json.JsonSerializer.Serialize(new { Type = "Backup", Version = version }) + "\n" +
                   System.Text.Json.JsonSerializer.Serialize(data);
        }
    }
}
