using System.Text;
using System.Text.Json;
using FinanceManager.Application.Aggregates;
using FinanceManager.Application.Statements;
using FinanceManager.Domain;
using FinanceManager.Domain.Accounts;
using FinanceManager.Domain.Budget;
using FinanceManager.Domain.Contacts;
using FinanceManager.Domain.Notifications;
using FinanceManager.Domain.Portfolio;
using FinanceManager.Domain.Postings;
using FinanceManager.Domain.Savings;
using FinanceManager.Domain.Securities;
using FinanceManager.Domain.Statements;
using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Backups;
using FinanceManager.Shared.Dtos.Accounts;
using FinanceManager.Shared.Dtos.Budget;
using FinanceManager.Shared.Dtos.Contacts;
using FinanceManager.Shared.Dtos.SavingsPlans;
using FinanceManager.Shared.Dtos.Statements;
using FluentAssertions;
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
    /// Covers backup and restore of the sections that completed the user backup cycle:
    /// <c>StatementImports</c>/<c>StatementEntries</c>, <c>Notifications</c>, <c>AccountShares</c>,
    /// <c>AccountLinkedIbans</c> and <c>PortfolioKpiConfigurations</c>, plus the extended
    /// <c>BudgetRule</c>/<c>Security</c> backup DTOs and the <c>ClearUserDataAsync</c> budget
    /// rule cleanup.
    /// </summary>
    public sealed class BackupRestoreCoverageTests
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

        private static global::SetupImportService CreateImportService(AppDbContext db)
        {
            var aggregateMock = new Mock<IPostingAggregateService>();
            aggregateMock
                .Setup(x => x.RebuildForUserAsync(It.IsAny<Guid>(), It.IsAny<Action<int, int>>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            var statementDraftMock = new Mock<IStatementDraftService>();
            return new global::SetupImportService(db, statementDraftMock.Object, aggregateMock.Object, NullLogger<global::SetupImportService>.Instance);
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

        private static async Task ImportPayloadAsync(AppDbContext db, Guid userId, object data, bool replaceExisting = false)
        {
            var payload = "{\"Type\":\"Backup\",\"Version\":3}\n" + JsonSerializer.Serialize(data);
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(payload));
            var svc = CreateImportService(db);
            await svc.ImportAsync(userId, stream, replaceExisting, CancellationToken.None);
        }

        /// <summary>
        /// Importing a payload with statement imports and entries restores them with remapped
        /// <c>AccountId</c>/<c>StatementImportId</c>/<c>ContactId</c>/<c>SavingsPlanId</c>/
        /// <c>SecurityTransactionId</c> references while keeping all other fields including
        /// the processing status.
        /// </summary>
        [Fact]
        public async Task ImportAsync_RestoresStatementImportsAndEntries_WithRemappedIds()
        {
            await using var db = CreateDb(out var conn);
            try
            {
                var userId = Guid.NewGuid();
                var oldAccountId = Guid.NewGuid();
                var oldContactId = Guid.NewGuid();
                var oldPlanId = Guid.NewGuid();
                var oldPostingId = Guid.NewGuid();
                var oldImportId = Guid.NewGuid();
                var oldEntryId = Guid.NewGuid();
                var bookingDate = new DateTime(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc);
                var importedAt = new DateTime(2025, 3, 2, 0, 0, 0, DateTimeKind.Utc);

                var data = new
                {
                    Contacts = new[]
                    {
                        new Contact.ContactBackupDto(oldContactId, userId, "Kontakt A", ContactType.Person, null, null, false, null)
                    },
                    Accounts = new[]
                    {
                        new Account.AccountBackupDto(oldAccountId, userId, AccountType.Giro, "Giro", "DE001", 0m, Guid.NewGuid(), null, SavingsPlanExpectation.None, importedAt, null)
                    },
                    SavingsPlans = new[]
                    {
                        new SavingsPlan.SavingsPlanBackupDto(oldPlanId, userId, "Plan", SavingsPlanType.Open, null, null, null, true, importedAt, null, null, null, null, null)
                    },
                    Postings = new[]
                    {
                        new Posting.PostingBackupDto(oldPostingId, Guid.NewGuid(), PostingKind.Bank, oldAccountId, null, null, null, bookingDate, bookingDate, 10m, null, "Subj", null, null, null, null, null, null, null, null, null, null, null, false, null)
                    },
                    StatementImports = new[]
                    {
                        new StatementImport.StatementImportBackupDto(oldImportId, oldAccountId, ImportFormat.Csv, importedAt, "a.csv", 1, importedAt, null)
                    },
                    StatementEntries = new[]
                    {
                        new StatementEntry.StatementEntryBackupDto(oldEntryId, oldImportId, bookingDate, bookingDate, -12.5m, "Subj", "hash1", "Empf", "EUR", "desc", false, false, oldContactId, oldPlanId, oldPostingId, FinanceManager.Domain.StatementEntryStatus.Booked)
                    }
                };

                await ImportPayloadAsync(db, userId, data);

                var account = await db.Accounts.SingleAsync(a => a.Name == "Giro", CancellationToken.None);
                var contact = await db.Contacts.SingleAsync(c => c.Name == "Kontakt A", CancellationToken.None);
                var plan = await db.SavingsPlans.SingleAsync(CancellationToken.None);
                var posting = await db.Postings.SingleAsync(CancellationToken.None);

                var import = await db.StatementImports.SingleAsync(CancellationToken.None);
                import.AccountId.Should().Be(account.Id);
                import.Id.Should().NotBe(oldImportId);
                import.Format.Should().Be(ImportFormat.Csv);
                import.OriginalFileName.Should().Be("a.csv");
                import.ImportedAtUtc.Should().Be(importedAt);
                import.TotalEntries.Should().Be(1);

                var entry = await db.StatementEntries.SingleAsync(CancellationToken.None);
                entry.StatementImportId.Should().Be(import.Id);
                entry.Id.Should().NotBe(oldEntryId);
                entry.BookingDate.Should().Be(bookingDate);
                entry.Amount.Should().Be(-12.5m);
                entry.Subject.Should().Be("Subj");
                entry.RawHash.Should().Be("hash1");
                entry.ContactId.Should().Be(contact.Id);
                entry.SavingsPlanId.Should().Be(plan.Id);
                entry.SecurityTransactionId.Should().Be(posting.Id);
                entry.Status.Should().Be(FinanceManager.Domain.StatementEntryStatus.Booked);
            }
            finally
            {
                conn.Dispose();
            }
        }

        /// <summary>
        /// Importing a payload with notifications restores them bound to the importing user -
        /// not globally - while preserving the backup id and all fields.
        /// </summary>
        [Fact]
        public async Task ImportAsync_RestoresNotifications_WithOwnerUserId()
        {
            await using var db = CreateDb(out var conn);
            try
            {
                var userId = Guid.NewGuid();
                var notificationId = Guid.NewGuid();
                var scheduled = new DateTime(2025, 4, 1, 12, 0, 0, DateTimeKind.Utc);
                var created = new DateTime(2025, 3, 1, 12, 0, 0, DateTimeKind.Utc);

                var data = new
                {
                    Notifications = new[]
                    {
                        new Notification.NotificationBackupDto(notificationId, userId, "Titel", "Nachricht", NotificationType.SystemAlert, NotificationTarget.Dashboard, scheduled, true, false, "evt.key", created, null)
                    }
                };

                await ImportPayloadAsync(db, userId, data);

                var notification = await db.Notifications.SingleAsync(CancellationToken.None);
                notification.Id.Should().Be(notificationId);
                notification.OwnerUserId.Should().Be(userId);
                notification.Title.Should().Be("Titel");
                notification.Message.Should().Be("Nachricht");
                notification.Type.Should().Be(NotificationType.SystemAlert);
                notification.Target.Should().Be(NotificationTarget.Dashboard);
                notification.ScheduledDateUtc.Should().Be(scheduled);
                notification.IsEnabled.Should().BeTrue();
                notification.IsDismissed.Should().BeFalse();
                notification.TriggerEventKey.Should().Be("evt.key");
                notification.CreatedUtc.Should().Be(created);
            }
            finally
            {
                conn.Dispose();
            }
        }

        /// <summary>
        /// Importing a payload with an account share remaps the <c>AccountId</c> of the imported
        /// account while keeping the share recipient, role and grant/revoke timestamps.
        /// </summary>
        [Fact]
        public async Task ImportAsync_RestoresAccountShares_RemapsOwnAccount()
        {
            await using var db = CreateDb(out var conn);
            try
            {
                var userId = Guid.NewGuid();
                var shareTargetUserId = Guid.NewGuid();
                var oldAccountId = Guid.NewGuid();
                var granted = new DateTime(2025, 2, 1, 0, 0, 0, DateTimeKind.Utc);
                var revoked = new DateTime(2025, 2, 10, 0, 0, 0, DateTimeKind.Utc);

                var data = new
                {
                    Accounts = new[]
                    {
                        new Account.AccountBackupDto(oldAccountId, userId, AccountType.Giro, "Giro", null, 0m, Guid.NewGuid(), null, SavingsPlanExpectation.None, granted, null)
                    },
                    AccountShares = new[]
                    {
                        new AccountShare.AccountShareBackupDto(Guid.NewGuid(), oldAccountId, shareTargetUserId, AccountShareRole.Write, granted, revoked)
                    }
                };

                await ImportPayloadAsync(db, userId, data);

                var account = await db.Accounts.SingleAsync(a => a.Name == "Giro", CancellationToken.None);
                var share = await db.AccountShares.SingleAsync(CancellationToken.None);
                share.AccountId.Should().Be(account.Id);
                share.UserId.Should().Be(shareTargetUserId);
                share.Role.Should().Be(AccountShareRole.Write);
                share.GrantedUtc.Should().Be(granted);
                share.RevokedUtc.Should().Be(revoked);
            }
            finally
            {
                conn.Dispose();
            }
        }

        /// <summary>
        /// Importing a payload with linked IBANs restores the entry of the imported collection
        /// account and skips entries whose account id cannot be remapped.
        /// </summary>
        [Fact]
        public async Task ImportAsync_RestoresAccountLinkedIbans_SkipsUnmapped()
        {
            await using var db = CreateDb(out var conn);
            try
            {
                var userId = Guid.NewGuid();
                var oldAccountId = Guid.NewGuid();

                var data = new
                {
                    Accounts = new[]
                    {
                        new Account.AccountBackupDto(oldAccountId, userId, AccountType.Giro, "Sammel", null, 0m, Guid.NewGuid(), null, SavingsPlanExpectation.None, DateTime.UtcNow, null, true, true)
                    },
                    AccountLinkedIbans = new[]
                    {
                        new AccountLinkedIban.AccountLinkedIbanBackupDto(Guid.NewGuid(), oldAccountId, "DE02120300000000202051"),
                        new AccountLinkedIban.AccountLinkedIbanBackupDto(Guid.NewGuid(), Guid.NewGuid(), "DE02120300000000202052")
                    }
                };

                await ImportPayloadAsync(db, userId, data);

                var account = await db.Accounts.SingleAsync(a => a.Name == "Sammel", CancellationToken.None);
                var iban = await db.AccountLinkedIbans.SingleAsync(CancellationToken.None);
                iban.AccountId.Should().Be(account.Id);
                iban.Iban.Should().Be("DE02120300000000202051");
            }
            finally
            {
                conn.Dispose();
            }
        }

        /// <summary>
        /// Importing a payload with a portfolio KPI configuration replaces the existing row in
        /// place (upsert), while an import without the section leaves the configuration untouched.
        /// </summary>
        [Fact]
        public async Task ImportAsync_RestoresPortfolioKpiConfiguration_AsUpsert()
        {
            await using var db = CreateDb(out var conn);
            try
            {
                var userId = Guid.NewGuid();
                var updated = new DateTime(2025, 6, 1, 10, 0, 0, DateTimeKind.Utc);
                var created = new DateTime(2025, 1, 1, 10, 0, 0, DateTimeKind.Utc);

                db.PortfolioKpiConfigurations.Add(new PortfolioKpiConfiguration(userId, "[\"old.a\"]", "[\"old.a\"]"));
                await db.SaveChangesAsync(CancellationToken.None);
                db.ChangeTracker.Clear();

                var withSection = new
                {
                    PortfolioKpiConfigurations = new[]
                    {
                        new PortfolioKpiConfiguration.PortfolioKpiConfigurationBackupDto(Guid.NewGuid(), userId, "[\"kpi.a\",\"kpi.b\"]", "[\"kpi.b\",\"kpi.a\"]", updated, created, null)
                    }
                };

                await ImportPayloadAsync(db, userId, withSection);

                var config = await db.PortfolioKpiConfigurations.SingleAsync(CancellationToken.None);
                config.OwnerUserId.Should().Be(userId);
                config.ActiveTileIds.Should().Be("[\"kpi.a\",\"kpi.b\"]");
                config.TileOrder.Should().Be("[\"kpi.b\",\"kpi.a\"]");
                config.UpdatedUtc.Should().Be(updated);
                config.CreatedUtc.Should().Be(created);

                // Second run without the section must keep the configuration.
                await ImportPayloadAsync(db, userId, new { });

                config = await db.PortfolioKpiConfigurations.SingleAsync(CancellationToken.None);
                config.ActiveTileIds.Should().Be("[\"kpi.a\",\"kpi.b\"]");
                config.TileOrder.Should().Be("[\"kpi.b\",\"kpi.a\"]");
                config.UpdatedUtc.Should().Be(updated);
            }
            finally
            {
                conn.Dispose();
            }
        }

        /// <summary>
        /// A full export/clear/import round trip preserves all six completed sections:
        /// statement imports and entries, notifications, account shares, linked IBANs and the
        /// portfolio KPI configuration, with ids remapped where required.
        /// </summary>
        [Fact]
        public async Task RoundTrip_PreservesNewSections()
        {
            await using var db = CreateDb(out var conn);
            var userId = Guid.NewGuid();
            var otherUserId = Guid.NewGuid();
            var bookingDate = new DateTime(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc);

            var contact = new Contact(userId, "Kontakt A", ContactType.Person, null);
            var account = new Account(userId, AccountType.Giro, "Sammel", "DE001", contact.Id);
            account.SetIsCollectionAccount(true);
            var plan = new SavingsPlan(userId, "Plan", SavingsPlanType.Open, null, null, null);
            var linkedIban = new AccountLinkedIban(account.Id, "DE02120300000000202051");
            var posting = new Posting(Guid.NewGuid(), PostingKind.Bank, account.Id, null, null, null, bookingDate, 10m, "Subj", null, null, null);
            var import = new StatementImport(account.Id, ImportFormat.Csv, "a.csv");
            import.AssignBackupDto(import.ToBackupDto() with { ImportedAtUtc = bookingDate, TotalEntries = 1 });
            var entry = new StatementEntry(import.Id, bookingDate, -12.5m, "Subj", "hash1", "Empf", bookingDate, "EUR", "desc", false, false);
            entry.AssignBackupDto(entry.ToBackupDto() with { ContactId = contact.Id, SavingsPlanId = plan.Id, SecurityTransactionId = posting.Id, Status = FinanceManager.Domain.StatementEntryStatus.Booked });
            var notification = new Notification
            {
                OwnerUserId = userId,
                Title = "Titel",
                Message = "Nachricht",
                Type = NotificationType.SystemAlert,
                Target = NotificationTarget.Dashboard,
                ScheduledDateUtc = bookingDate
            };
            var share = new AccountShare(account.Id, otherUserId, AccountShareRole.Write);
            share.AssignBackupDto(share.ToBackupDto() with { GrantedUtc = bookingDate, RevokedUtc = bookingDate.AddDays(1) });
            var kpiConfig = new PortfolioKpiConfiguration(userId, "[\"kpi.a\",\"kpi.b\"]", "[\"kpi.b\",\"kpi.a\"]");

            db.Contacts.Add(contact);
            db.Accounts.Add(account);
            db.SavingsPlans.Add(plan);
            db.AccountLinkedIbans.Add(linkedIban);
            db.Postings.Add(posting);
            db.StatementImports.Add(import);
            db.StatementEntries.Add(entry);
            db.Notifications.Add(notification);
            db.AccountShares.Add(share);
            db.PortfolioKpiConfigurations.Add(kpiConfig);
            await db.SaveChangesAsync(CancellationToken.None);

            var expectedImport = import.ToBackupDto();
            var expectedEntry = entry.ToBackupDto();
            var expectedNotification = notification.ToBackupDto();
            var expectedShare = share.ToBackupDto();
            var expectedConfig = kpiConfig.ToBackupDto();
            db.ChangeTracker.Clear();

            var temp = Path.Combine(Path.GetTempPath(), "fmtests", Guid.NewGuid().ToString());
            try
            {
                var svc = CreateBackupService(db, temp);
                var ndjson = await ReadBackupNdjsonAsync(db, svc, userId, temp);
                db.ChangeTracker.Clear();

                using var stream = new MemoryStream(Encoding.UTF8.GetBytes(ndjson));
                var importer = CreateImportService(db);
                await importer.ImportAsync(userId, stream, replaceExisting: true, CancellationToken.None);
                db.ChangeTracker.Clear();

                var newAccount = await db.Accounts.SingleAsync(a => a.Name == "Sammel", CancellationToken.None);
                var newContact = await db.Contacts.SingleAsync(c => c.Name == "Kontakt A", CancellationToken.None);
                var newPlan = await db.SavingsPlans.SingleAsync(CancellationToken.None);
                var newPosting = await db.Postings.SingleAsync(p => p.Subject == "Subj", CancellationToken.None);

                var newLinkedIban = await db.AccountLinkedIbans.SingleAsync(CancellationToken.None);
                newLinkedIban.AccountId.Should().Be(newAccount.Id);
                newLinkedIban.Iban.Should().Be(linkedIban.Iban);

                var newImport = await db.StatementImports.SingleAsync(CancellationToken.None);
                newImport.Id.Should().NotBe(expectedImport.Id);
                newImport.AccountId.Should().Be(newAccount.Id);
                newImport.Format.Should().Be(expectedImport.Format);
                newImport.OriginalFileName.Should().Be(expectedImport.OriginalFileName);
                newImport.ImportedAtUtc.Should().Be(expectedImport.ImportedAtUtc);
                newImport.TotalEntries.Should().Be(expectedImport.TotalEntries);

                var newEntry = await db.StatementEntries.SingleAsync(CancellationToken.None);
                newEntry.Id.Should().NotBe(expectedEntry.Id);
                newEntry.StatementImportId.Should().Be(newImport.Id);
                newEntry.BookingDate.Should().Be(expectedEntry.BookingDate);
                newEntry.Amount.Should().Be(expectedEntry.Amount);
                newEntry.Subject.Should().Be(expectedEntry.Subject);
                newEntry.RawHash.Should().Be(expectedEntry.RawHash);
                newEntry.RecipientName.Should().Be(expectedEntry.RecipientName);
                newEntry.CurrencyCode.Should().Be(expectedEntry.CurrencyCode);
                newEntry.ContactId.Should().Be(newContact.Id);
                newEntry.SavingsPlanId.Should().Be(newPlan.Id);
                newEntry.SecurityTransactionId.Should().Be(newPosting.Id);
                newEntry.Status.Should().Be(expectedEntry.Status);

                var newNotification = await db.Notifications.SingleAsync(CancellationToken.None);
                newNotification.Id.Should().Be(expectedNotification.Id);
                newNotification.OwnerUserId.Should().Be(userId);
                newNotification.Title.Should().Be(expectedNotification.Title);
                newNotification.Message.Should().Be(expectedNotification.Message);
                newNotification.Type.Should().Be(expectedNotification.Type);
                newNotification.Target.Should().Be(expectedNotification.Target);
                newNotification.ScheduledDateUtc.Should().Be(expectedNotification.ScheduledDateUtc);
                newNotification.IsEnabled.Should().Be(expectedNotification.IsEnabled);
                newNotification.IsDismissed.Should().Be(expectedNotification.IsDismissed);
                newNotification.TriggerEventKey.Should().Be(expectedNotification.TriggerEventKey);

                var newShare = await db.AccountShares.SingleAsync(CancellationToken.None);
                newShare.AccountId.Should().Be(newAccount.Id);
                newShare.UserId.Should().Be(expectedShare.UserId);
                newShare.Role.Should().Be(expectedShare.Role);
                newShare.GrantedUtc.Should().Be(expectedShare.GrantedUtc);
                newShare.RevokedUtc.Should().Be(expectedShare.RevokedUtc);

                var newConfig = await db.PortfolioKpiConfigurations.SingleAsync(CancellationToken.None);
                newConfig.OwnerUserId.Should().Be(userId);
                newConfig.ActiveTileIds.Should().Be(expectedConfig.ActiveTileIds);
                newConfig.TileOrder.Should().Be(expectedConfig.TileOrder);
                newConfig.UpdatedUtc.Should().Be(expectedConfig.UpdatedUtc);
            }
            finally
            {
                try { Directory.Delete(temp, true); } catch { /* best effort cleanup */ }
                conn.Dispose();
            }
        }

        /// <summary>
        /// A budget rule's purpose pattern and its regex flag survive the export/clear/import
        /// round trip.
        /// </summary>
        [Fact]
        public async Task BudgetRule_PurposePattern_SurvivesBackupRestore()
        {
            await using var db = CreateDb(out var conn);
            var userId = Guid.NewGuid();

            var category = new BudgetCategory(userId, "Arbeit");
            var rule = new BudgetRule(userId, null, category.Id, 500m, BudgetIntervalType.Monthly, new DateOnly(2025, 1, 1), null, null, "^Gehalt.*", true);
            db.BudgetCategories.Add(category);
            db.BudgetRules.Add(rule);
            await db.SaveChangesAsync(CancellationToken.None);
            db.ChangeTracker.Clear();

            var temp = Path.Combine(Path.GetTempPath(), "fmtests", Guid.NewGuid().ToString());
            try
            {
                var svc = CreateBackupService(db, temp);
                var ndjson = await ReadBackupNdjsonAsync(db, svc, userId, temp);
                db.ChangeTracker.Clear();

                using var stream = new MemoryStream(Encoding.UTF8.GetBytes(ndjson));
                var importer = CreateImportService(db);
                await importer.ImportAsync(userId, stream, replaceExisting: true, CancellationToken.None);

                var restored = await db.BudgetRules.SingleAsync(CancellationToken.None);
                restored.PurposePattern.Should().Be("^Gehalt.*");
                restored.PurposePatternIsRegex.Should().BeTrue();
                restored.BudgetCategoryId.Should().Be(await db.BudgetCategories.Select(c => c.Id).SingleAsync(CancellationToken.None));
            }
            finally
            {
                try { Directory.Delete(temp, true); } catch { /* best effort cleanup */ }
                conn.Dispose();
            }
        }

        /// <summary>
        /// A security's price error fields (including the original error timestamp) and the
        /// notified golden cross / trend reversal flags survive the export/clear/import round trip.
        /// </summary>
        [Fact]
        public async Task Security_ErrorAndNotifiedFields_SurviveBackupRestore()
        {
            await using var db = CreateDb(out var conn);
            var userId = Guid.NewGuid();

            var security = new Security(userId, "ACME", "ACMEID", null, null, "EUR", null);
            security.SetPriceError("provider failed");
            security.SetGoldenCrossNotifiedPhase(GoldenCrossPhase.Approaching);
            security.SetTrendReversalNotifiedStatus(TrendStatus.ReversalLikely);
            db.Securities.Add(security);
            await db.SaveChangesAsync(CancellationToken.None);
            var expectedSinceUtc = security.PriceErrorSinceUtc;
            db.ChangeTracker.Clear();

            var temp = Path.Combine(Path.GetTempPath(), "fmtests", Guid.NewGuid().ToString());
            try
            {
                var svc = CreateBackupService(db, temp);
                var ndjson = await ReadBackupNdjsonAsync(db, svc, userId, temp);
                db.ChangeTracker.Clear();

                using var stream = new MemoryStream(Encoding.UTF8.GetBytes(ndjson));
                var importer = CreateImportService(db);
                await importer.ImportAsync(userId, stream, replaceExisting: true, CancellationToken.None);

                var restored = await db.Securities.SingleAsync(CancellationToken.None);
                restored.HasPriceError.Should().BeTrue();
                restored.PriceErrorMessage.Should().Be("provider failed");
                restored.PriceErrorSinceUtc.Should().Be(expectedSinceUtc);
                restored.GoldenCrossNotifiedPhase.Should().Be(GoldenCrossPhase.Approaching);
                restored.TrendReversalNotifiedStatus.Should().Be(TrendStatus.ReversalLikely);
            }
            finally
            {
                try { Directory.Delete(temp, true); } catch { /* best effort cleanup */ }
                conn.Dispose();
            }
        }

        /// <summary>
        /// <c>ClearUserDataAsync</c> removes budget rules that are bound only to a budget
        /// category - previously they survived the clear and were duplicated on re-import.
        /// </summary>
        [Fact]
        public async Task ClearUserDataAsync_RemovesCategoryBoundBudgetRules()
        {
            await using var db = CreateDb(out var conn);
            try
            {
                var userId = Guid.NewGuid();

                var category = new BudgetCategory(userId, "Arbeit");
                var rule = new BudgetRule(userId, null, category.Id, 500m, BudgetIntervalType.Monthly, new DateOnly(2025, 1, 1));
                db.BudgetCategories.Add(category);
                db.BudgetRules.Add(rule);
                await db.SaveChangesAsync(CancellationToken.None);
                db.ChangeTracker.Clear();

                await db.ClearUserDataAsync(userId, (step, count) => { }, CancellationToken.None);

                (await db.BudgetRules.CountAsync(CancellationToken.None)).Should().Be(0);
            }
            finally
            {
                conn.Dispose();
            }
        }

        /// <summary>
        /// A version-3 payload created before the new sections existed (none of the six blocks
        /// present) still imports cleanly.
        /// </summary>
        [Fact]
        public async Task ImportAsync_OldBackupWithoutNewSections_StillWorks()
        {
            await using var db = CreateDb(out var conn);
            try
            {
                var userId = Guid.NewGuid();
                var data = new
                {
                    Accounts = new[]
                    {
                        new Account.AccountBackupDto(Guid.NewGuid(), userId, AccountType.Giro, "Giro", null, 0m, Guid.NewGuid(), null, SavingsPlanExpectation.None, DateTime.UtcNow, null)
                    }
                };

                await ImportPayloadAsync(db, userId, data);

                (await db.Accounts.CountAsync(CancellationToken.None)).Should().Be(1);
                (await db.StatementImports.CountAsync(CancellationToken.None)).Should().Be(0);
                (await db.Notifications.CountAsync(CancellationToken.None)).Should().Be(0);
                (await db.AccountShares.CountAsync(CancellationToken.None)).Should().Be(0);
                (await db.AccountLinkedIbans.CountAsync(CancellationToken.None)).Should().Be(0);
                (await db.PortfolioKpiConfigurations.CountAsync(CancellationToken.None)).Should().Be(0);
            }
            finally
            {
                conn.Dispose();
            }
        }
    }
}
