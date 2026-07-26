using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Backups;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace FinanceManager.Tests.Infrastructure
{
    public class BackupServiceTests
    {
        private sealed class TestHostEnvironment : IHostEnvironment
        {
            public string EnvironmentName { get; set; } = "Development";
            public string ApplicationName { get; set; } = "FinanceManager.Tests";
            public string ContentRootPath { get; set; }
            public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; }
        }

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
            await db.SaveChangesAsync();

            var list = await svc.ListAsync(userId, CancellationToken.None);
            Assert.Contains(list, x => x.Id == rec.Id);

            try { Directory.Delete(temp, true); } catch { }
        }

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
            await db.SaveChangesAsync();

            var ok = await svc.DeleteAsync(userId, rec.Id, CancellationToken.None);
            Assert.True(ok);
            Assert.False(File.Exists(filepath));
            var found = await db.Backups.FindAsync(rec.Id);
            Assert.Null(found);

            try { Directory.Delete(temp, true); } catch { }
        }

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
            await using (var fs = File.Create(filepath)) { var b = new byte[] {1,2,3}; await fs.WriteAsync(b); }
            var rec = new BackupRecord { OwnerUserId = userId, CreatedUtc = DateTime.UtcNow, FileName = filename, SizeBytes = new FileInfo(filepath).Length, Source = "Test", StoragePath = filename };
            db.Backups.Add(rec);
            await db.SaveChangesAsync();

            var stream = await svc.OpenDownloadAsync(userId, rec.Id, CancellationToken.None);
            Assert.NotNull(stream);
            using var sr = new MemoryStream();
            await stream!.CopyToAsync(sr);
            Assert.Equal(new byte[] {1,2,3}, sr.ToArray());

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
