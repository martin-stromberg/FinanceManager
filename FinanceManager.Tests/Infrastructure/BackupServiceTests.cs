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
        public async Task UploadAsync_NonZip_WrapsIntoZipAndPersists()
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

            var ndjson = "{\"Type\":\"Backup\",\"Version\":3}\n{}";
            await using var msIn = new MemoryStream(Encoding.UTF8.GetBytes(ndjson));

            var dto = await svc.UploadAsync(userId, msIn, "upload.ndjson", CancellationToken.None);

            Assert.NotNull(dto);
            var rec = db.Backups.FirstOrDefault(b => b.Id == dto.Id);
            Assert.NotNull(rec);
            var full = Path.Combine(backupsDir, rec.StoragePath);
            Assert.True(File.Exists(full));

            // ensure zip contains an ndjson entry
            using var fs = File.OpenRead(full);
            using var zip = new ZipArchive(fs, ZipArchiveMode.Read, leaveOpen: false);
            var entry = zip.Entries.FirstOrDefault();
            Assert.NotNull(entry);

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
    }
}
