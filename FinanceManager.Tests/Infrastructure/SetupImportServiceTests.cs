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
    }
}
