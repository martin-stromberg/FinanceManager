using FinanceManager.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace FinanceManager.Tests.Infrastructure;

/// <summary>
/// Verifies the SQL backfill inside the <c>AddPostingTypeAndAccountPostingRules</c> migration: existing
/// <c>Postings</c> rows of kind <c>Bank</c> and all <c>StatementDraftEntries</c> rows must be classified from
/// their amount sign (negative = Outflow, otherwise Inflow) when the migration runs on a database that was
/// created before the posting type existed.
/// </summary>
public sealed class PostingTypeMigrationBackfillTests
{
    private const string PredecessorMigration = "20261116163424_AddReportSuppressedExpectations";

    /// <summary>
    /// After migrating a database seeded at the predecessor schema, bank postings and draft entries must
    /// carry the sign-derived posting type while non-bank postings remain <c>Unspecified</c>.
    /// </summary>
    [Fact]
    public async Task Migration_BackfillsPostingType_FromAmountSign()
    {
        var conn = new SqliteConnection("DataSource=:memory:");
        await conn.OpenAsync(TestContext.Current.CancellationToken);
        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options;
            await using var db = new AppDbContext(options);

            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync(PredecessorMigration, TestContext.Current.CancellationToken);

            // Seed rows against the predecessor schema using raw SQL (the runtime model already knows the new
            // columns, so EF inserts would reference columns that do not exist yet). Foreign keys are disabled
            // because the referenced parent rows are irrelevant for this test.
            await ExecAsync(conn, "PRAGMA foreign_keys=OFF");

            var negativeBankPostingId = Guid.NewGuid();
            var positiveBankPostingId = Guid.NewGuid();
            var contactPostingId = Guid.NewGuid();
            var draftId = Guid.NewGuid();
            var negativeEntryId = Guid.NewGuid();
            var positiveEntryId = Guid.NewGuid();

            await ExecAsync(conn, $"""
                INSERT INTO [Postings] ([Id], [Kind], [AccountId], [BookingDate], [ValutaDate], [Amount], [CreatedUtc], [GroupId], [IsPreliminary], [SourceId])
                VALUES ('{negativeBankPostingId}', 0, '{Guid.NewGuid()}', '2024-01-02 00:00:00', '2024-01-02 00:00:00', -25.5, '2024-01-02 00:00:00', '{Guid.NewGuid()}', 0, '{Guid.NewGuid()}'),
                       ('{positiveBankPostingId}', 0, '{Guid.NewGuid()}', '2024-01-03 00:00:00', '2024-01-03 00:00:00', 42.0, '2024-01-03 00:00:00', '{Guid.NewGuid()}', 0, '{Guid.NewGuid()}'),
                       ('{contactPostingId}', 1, '{Guid.NewGuid()}', '2024-01-04 00:00:00', '2024-01-04 00:00:00', -25.5, '2024-01-04 00:00:00', '{Guid.NewGuid()}', 0, '{Guid.NewGuid()}')
                """);

            await ExecAsync(conn, $"""
                INSERT INTO [StatementDrafts] ([Id], [OwnerUserId], [OriginalFileName], [CreatedUtc], [Status], [IsPreliminary])
                VALUES ('{draftId}', '{Guid.NewGuid()}', 'file.csv', '2024-01-01 00:00:00', 0, 0)
                """);

            await ExecAsync(conn, $"""
                INSERT INTO [StatementDraftEntries] ([Id], [DraftId], [EntryNumber], [BookingDate], [Amount], [CurrencyCode], [Subject], [Status], [CreatedUtc], [ArchiveSavingsPlanOnBooking], [IsAnnounced], [IsCostNeutral])
                VALUES ('{negativeEntryId}', '{draftId}', 1, '2024-01-05 00:00:00', -7.5, 'EUR', 'Kartenzahlung', 0, '2024-01-05 00:00:00', 0, 0, 0),
                       ('{positiveEntryId}', '{draftId}', 2, '2024-01-06 00:00:00', 12.0, 'EUR', 'Gutschrift', 0, '2024-01-06 00:00:00', 0, 0, 0)
                """);

            // Apply the remaining migrations (the feature migration).
            await migrator.MigrateAsync(cancellationToken: TestContext.Current.CancellationToken);

            Assert.Equal(2, await ScalarIntAsync(conn, $"SELECT [PostingType] FROM [Postings] WHERE [Id] = '{negativeBankPostingId}'"));
            Assert.Equal(1, await ScalarIntAsync(conn, $"SELECT [PostingType] FROM [Postings] WHERE [Id] = '{positiveBankPostingId}'"));
            Assert.Equal(0, await ScalarIntAsync(conn, $"SELECT [PostingType] FROM [Postings] WHERE [Id] = '{contactPostingId}'"));

            Assert.Equal(2, await ScalarIntAsync(conn, $"SELECT [PostingType] FROM [StatementDraftEntries] WHERE [Id] = '{negativeEntryId}'"));
            Assert.Equal(1, await ScalarIntAsync(conn, $"SELECT [PostingType] FROM [StatementDraftEntries] WHERE [Id] = '{positiveEntryId}'"));
            Assert.Equal(0, await ScalarIntAsync(conn, $"SELECT [IsPostingTypeManual] FROM [StatementDraftEntries] WHERE [Id] = '{negativeEntryId}'"));

            Assert.Equal(1, await ScalarIntAsync(conn, "SELECT COUNT(*) FROM [sqlite_master] WHERE [type] = 'table' AND [name] = 'AccountPostingRules'"));
        }
        finally
        {
            await conn.DisposeAsync();
        }
    }

    private static async Task ExecAsync(SqliteConnection conn, string sql)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<int> ScalarIntAsync(SqliteConnection conn, string sql)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        var result = await cmd.ExecuteScalarAsync(TestContext.Current.CancellationToken);
        return Convert.ToInt32(result);
    }
}
