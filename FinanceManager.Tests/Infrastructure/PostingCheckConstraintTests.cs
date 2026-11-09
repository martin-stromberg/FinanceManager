using FinanceManager.Domain.Postings;
using FinanceManager.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Tests.Infrastructure;

/// <summary>
/// Covers the database-level check constraint <c>CK_Postings_HasReference</c> on the
/// <c>Postings</c> table: at least one of the four target reference columns
/// (<c>AccountId</c>, <c>ContactId</c>, <c>SavingsPlanId</c>, <c>SecurityId</c>) must be
/// non-NULL. The constraint is the second line of defense behind the constructor guard of
/// <see cref="Posting"/> and also covers paths that bypass it (e.g. <see cref="Posting.AssignBackupDto"/>).
/// </summary>
public sealed class PostingCheckConstraintTests
{
    private static AppDbContext CreateSqliteContext()
    {
        var conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(conn)
            .Options;
        var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    /// <summary>
    /// Clearing all four target references on a tracked posting via <see cref="Posting.AssignBackupDto"/>
    /// (the path that bypasses the constructor guard) must fail at <c>SaveChanges</c> with a
    /// <see cref="DbUpdateException"/> caused by the check constraint.
    /// </summary>
    [Fact]
    public async Task SaveChanges_Throws_WhenAllReferencesSetToNull()
    {
        // Arrange
        await using var db = CreateSqliteContext();
        var posting = new Posting(
            sourceId: Guid.NewGuid(),
            kind: PostingKind.Bank,
            accountId: Guid.NewGuid(),
            contactId: null,
            savingsPlanId: null,
            securityId: null,
            bookingDate: new DateTime(2025, 1, 15),
            amount: 100m);
        db.Postings.Add(posting);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var dto = posting.ToBackupDto() with
        {
            AccountId = null,
            ContactId = null,
            SavingsPlanId = null,
            SecurityId = null
        };
        posting.AssignBackupDto(dto);
        var act = async () => await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    /// <summary>
    /// Persisting a posting with exactly one target reference succeeds; the check constraint
    /// does not block legitimate rows.
    /// </summary>
    [Fact]
    public async Task Insert_Succeeds_WhenSingleReferenceSet()
    {
        // Arrange
        await using var db = CreateSqliteContext();
        var accountId = Guid.NewGuid();
        var posting = new Posting(
            sourceId: Guid.NewGuid(),
            kind: PostingKind.Bank,
            accountId: accountId,
            contactId: null,
            savingsPlanId: null,
            securityId: null,
            bookingDate: new DateTime(2025, 1, 15),
            amount: 100m);
        db.Postings.Add(posting);

        // Act
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        var stored = await db.Postings.SingleAsync(TestContext.Current.CancellationToken);
        stored.AccountId.Should().Be(accountId);
    }
}
