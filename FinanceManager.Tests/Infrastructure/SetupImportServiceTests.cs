using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FinanceManager.Application.Aggregates;
using FinanceManager.Application.Setup;
using FinanceManager.Application.Statements;
using FinanceManager.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace FinanceManager.Tests.Infrastructure
{
    /// <summary>
    /// Covers <c>SetupImportService.ImportAsync</c>, the initial-setup counterpart to <c>BackupService</c>'s restore
    /// path used to seed a fresh installation from a previously exported backup's NDJSON payload.
    /// </summary>
    public sealed class SetupImportServiceTests
    {
        /// <summary>
        /// Verifies that importing a backup payload restores both category-direct and purpose-direct budget rules
        /// (<c>BudgetCategoryId</c> vs. <c>BudgetPurposeId</c> set) - a setup import that only handled one of the two
        /// rule shapes would silently lose part of the user's budget configuration when moving to a new instance.
        /// </summary>
        [Fact]
        public async Task ImportAsync_RestoresBudgetRulesForPurposeAndCategory()
        {
            // Arrange
            var conn = new SqliteConnection("DataSource=:memory:");
            conn.Open();
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options;
            await using var db = new AppDbContext(options);
            db.Database.EnsureCreated();

            var userId = Guid.NewGuid();
            var categoryId = Guid.NewGuid();
            var purposeId = Guid.NewGuid();

            var data = new
            {
                BudgetCategories = new[]
                {
                    new { Id = categoryId, Name = "Arbeit" }
                },
                BudgetPurposes = new[]
                {
                    new { Id = purposeId, Name = "Gehalt", SourceType = 0, SourceId = Guid.NewGuid() }
                },
                BudgetRules = new[]
                {
                    new
                    {
                        Id = Guid.NewGuid(),
                        BudgetCategoryId = (Guid?)categoryId,
                        BudgetPurposeId = (Guid?)null,
                        Amount = 5000m,
                        Interval = 0,
                        CustomIntervalMonths = (int?)null,
                        StartDate = new DateOnly(2026, 1, 1),
                        EndDate = (DateOnly?)null
                    },
                    new
                    {
                        Id = Guid.NewGuid(),
                        BudgetCategoryId = (Guid?)null,
                        BudgetPurposeId = (Guid?)purposeId,
                        Amount = 5000m,
                        Interval = 0,
                        CustomIntervalMonths = (int?)null,
                        StartDate = new DateOnly(2026, 1, 1),
                        EndDate = (DateOnly?)null
                    }
                }
            };

            var dataJson = JsonSerializer.Serialize(data, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            var meta = "{\"Type\":\"Backup\",\"Version\":3}";
            var fullJson = meta + "\n" + dataJson;

            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(fullJson));

            var statementDraftMock = new Mock<IStatementDraftService>();
            var aggregateMock = new Mock<IPostingAggregateService>();
            aggregateMock
                .Setup(x => x.RebuildForUserAsync(It.IsAny<Guid>(), It.IsAny<Action<int, int>>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var svc = new global::SetupImportService(
                db,
                statementDraftMock.Object,
                aggregateMock.Object,
                NullLogger<global::SetupImportService>.Instance);

            // Act
            await svc.ImportAsync(userId, stream, replaceExisting: false, CancellationToken.None);

            // Assert
            var rules = db.BudgetRules.ToList();
            rules.Should().HaveCount(2);
            rules.Should().ContainSingle(r => r.BudgetCategoryId.HasValue);
            rules.Should().ContainSingle(r => r.BudgetPurposeId.HasValue);
        }

        /// <summary>
        /// Verifies that importing a budget rule payload carrying <c>PurposePattern</c> and
        /// <c>PurposePatternIsRegex</c> restores both fields - previously the DTO fields were
        /// silently dropped on restore.
        /// </summary>
        [Fact]
        public async Task ImportAsync_RestoresBudgetRulePattern()
        {
            // Arrange
            var conn = new SqliteConnection("DataSource=:memory:");
            conn.Open();
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options;
            await using var db = new AppDbContext(options);
            db.Database.EnsureCreated();

            var userId = Guid.NewGuid();
            var categoryId = Guid.NewGuid();

            var data = new
            {
                BudgetCategories = new[]
                {
                    new { Id = categoryId, Name = "Arbeit" }
                },
                BudgetRules = new[]
                {
                    new
                    {
                        Id = Guid.NewGuid(),
                        BudgetCategoryId = (Guid?)categoryId,
                        BudgetPurposeId = (Guid?)null,
                        Amount = 5000m,
                        Interval = 0,
                        CustomIntervalMonths = (int?)null,
                        StartDate = new DateOnly(2026, 1, 1),
                        EndDate = (DateOnly?)null,
                        PurposePattern = (string?)"^Gehalt.*",
                        PurposePatternIsRegex = true
                    }
                }
            };

            var dataJson = JsonSerializer.Serialize(data, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            var meta = "{\"Type\":\"Backup\",\"Version\":3}";
            var fullJson = meta + "\n" + dataJson;

            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(fullJson));

            var statementDraftMock = new Mock<IStatementDraftService>();
            var aggregateMock = new Mock<IPostingAggregateService>();
            aggregateMock
                .Setup(x => x.RebuildForUserAsync(It.IsAny<Guid>(), It.IsAny<Action<int, int>>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var svc = new global::SetupImportService(
                db,
                statementDraftMock.Object,
                aggregateMock.Object,
                NullLogger<global::SetupImportService>.Instance);

            // Act
            await svc.ImportAsync(userId, stream, replaceExisting: false, CancellationToken.None);

            // Assert
            var rule = db.BudgetRules.Single();
            rule.PurposePattern.Should().Be("^Gehalt.*");
            rule.PurposePatternIsRegex.Should().BeTrue();
        }

        /// <summary>
        /// Verifies that a backup posting whose four target references (<c>AccountId</c>, <c>ContactId</c>,
        /// <c>SavingsPlanId</c>, <c>SecurityId</c>) cannot be remapped - because the referenced entities are
        /// not part of the backup - is skipped instead of being imported as an orphaned row: the import
        /// completes successfully and <c>db.Postings</c> stays empty.
        /// </summary>
        [Fact]
        public async Task ImportAsync_SkipsPosting_WhenAllRemappedReferencesAreNull()
        {
            // Arrange
            var conn = new SqliteConnection("DataSource=:memory:");
            conn.Open();
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options;
            await using var db = new AppDbContext(options);
            db.Database.EnsureCreated();

            var userId = Guid.NewGuid();

            var data = new
            {
                Postings = new[]
                {
                    new
                    {
                        Id = Guid.NewGuid(),
                        SourceId = Guid.NewGuid(),
                        Kind = 0,
                        AccountId = (Guid?)Guid.NewGuid(),
                        ContactId = (Guid?)null,
                        SavingsPlanId = (Guid?)null,
                        SecurityId = (Guid?)null,
                        BookingDate = new DateTime(2025, 1, 15),
                        ValutaDate = new DateTime(2025, 1, 15),
                        Amount = 100m,
                        OriginalAmount = (decimal?)null,
                        Subject = (string?)null,
                        RecipientName = (string?)null,
                        Description = (string?)null,
                        SecuritySubType = (int?)null,
                        Quantity = (decimal?)null,
                        GroupId = (Guid?)null,
                        ParentId = (Guid?)null,
                        LinkedPostingId = (Guid?)null,
                        ReversedByPostingId = (Guid?)null,
                        ReversalForPostingId = (Guid?)null,
                        ReversedByUserId = (Guid?)null,
                        ReversedAtUtc = (DateTime?)null,
                        IsPreliminary = false,
                        SavingsPlanSubType = (int?)null
                    }
                }
            };

            var dataJson = JsonSerializer.Serialize(data, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            var meta = "{\"Type\":\"Backup\",\"Version\":3}";
            var fullJson = meta + "\n" + dataJson;

            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(fullJson));

            var statementDraftMock = new Mock<IStatementDraftService>();
            var aggregateMock = new Mock<IPostingAggregateService>();
            aggregateMock
                .Setup(x => x.RebuildForUserAsync(It.IsAny<Guid>(), It.IsAny<Action<int, int>>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var svc = new global::SetupImportService(
                db,
                statementDraftMock.Object,
                aggregateMock.Object,
                NullLogger<global::SetupImportService>.Instance);

            // Act
            await svc.ImportAsync(userId, stream, replaceExisting: false, CancellationToken.None);

            // Assert
            db.Postings.Should().BeEmpty();
        }

        /// <summary>
        /// Verifies that entity references stored in a report favorite's filter lists
        /// (<c>ContactIdsCsv</c>, <c>AccountIdsCsv</c>, ...) are remapped to the ids of the newly
        /// imported entities: imported entities always receive fresh ids, so keeping the backup
        /// ids would leave the favorite filtering on non-existent rows (empty selection, "No data").
        /// Filter ids without a counterpart in the backup are dropped.
        /// </summary>
        [Fact]
        public async Task ImportAsync_RemapsReportFavoriteFilterEntityIds()
        {
            // Arrange
            var conn = new SqliteConnection("DataSource=:memory:");
            conn.Open();
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options;
            await using var db = new AppDbContext(options);
            db.Database.EnsureCreated();

            var userId = Guid.NewGuid();
            var contactId = Guid.NewGuid();
            var staleId = Guid.NewGuid();

            var data = new
            {
                Contacts = new[]
                {
                    new
                    {
                        Id = contactId,
                        OwnerUserId = Guid.NewGuid(),
                        Name = "Alice",
                        Type = 2,
                        CategoryId = (Guid?)null,
                        Description = (string?)null,
                        IsPaymentIntermediary = false,
                        SymbolAttachmentId = (Guid?)null
                    }
                },
                ReportFavorites = new[]
                {
                    new
                    {
                        Id = Guid.NewGuid(),
                        OwnerUserId = Guid.NewGuid(),
                        Name = "Kontaktbericht",
                        PostingKind = 1,
                        IncludeCategory = false,
                        Interval = 0,
                        Take = 24,
                        ComparePrevious = false,
                        CompareYear = false,
                        CompareProjection = false,
                        ShowChart = true,
                        Expandable = true,
                        PostingKindsCsv = (string?)null,
                        AccountIdsCsv = (string?)null,
                        ContactIdsCsv = $"{contactId},{staleId}",
                        SavingsPlanIdsCsv = (string?)null,
                        SecurityIdsCsv = (string?)null,
                        ContactCategoryIdsCsv = (string?)null,
                        SavingsPlanCategoryIdsCsv = (string?)null,
                        SecurityCategoryIdsCsv = (string?)null,
                        SecuritySubTypesCsv = (string?)null,
                        IncludeDividendRelated = (bool?)null,
                        UseValutaDate = false
                    }
                }
            };

            var dataJson = JsonSerializer.Serialize(data, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            var meta = "{\"Type\":\"Backup\",\"Version\":3}";
            var fullJson = meta + "\n" + dataJson;

            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(fullJson));

            var statementDraftMock = new Mock<IStatementDraftService>();
            var aggregateMock = new Mock<IPostingAggregateService>();
            aggregateMock
                .Setup(x => x.RebuildForUserAsync(It.IsAny<Guid>(), It.IsAny<Action<int, int>>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var svc = new global::SetupImportService(
                db,
                statementDraftMock.Object,
                aggregateMock.Object,
                NullLogger<global::SetupImportService>.Instance);

            // Act
            await svc.ImportAsync(userId, stream, replaceExisting: false, CancellationToken.None);

            // Assert
            var contact = db.Contacts.Single();
            contact.Id.Should().NotBe(contactId);
            var fav = db.ReportFavorites.Single();
            fav.GetFilters().ContactIds.Should().BeEquivalentTo(new[] { contact.Id });
        }
    }
}
