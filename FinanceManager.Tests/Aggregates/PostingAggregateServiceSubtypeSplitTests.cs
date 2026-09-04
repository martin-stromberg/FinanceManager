using FinanceManager.Domain.Postings;
using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Aggregates;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Tests.Aggregates;

/// <summary>
/// Covers <see cref="PostingAggregateService.UpsertForPostingAsync"/> for security postings that carry a
/// <see cref="SecurityPostingSubType"/> (e.g. dividend vs. tax): verifies that the sub-type participates
/// in the aggregate key, so postings with different sub-types on the same security and date are kept as
/// separate aggregate rows instead of being netted into one.
/// </summary>
public sealed class PostingAggregateServiceSubtypeSplitTests
{
    private static AppDbContext CreateDb()
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
    /// Posts a Dividend and a Tax posting for the same security and date, then checks that each of the
    /// four aggregate periods (Month/Quarter/HalfYear/Year) ends up with 4 aggregate rows (2 sub-types x
    /// 2 date kinds) and that both the dividend and the tax amount are present in each period - i.e. the
    /// sub-type split does not merge or drop either side of the pair.
    /// </summary>
    [Fact]
    public async Task UpsertForPostingAsync_Security_SubTypes_Dividend_And_Tax_ShouldCreateTwoAggregatesPerPeriod()
    {
        using var db = CreateDb();
        var svc = new PostingAggregateService(db);
        var ct = CancellationToken.None;

        var securityId = Guid.NewGuid();
        var date = new DateTime(2025, 8, 19);

        // Create Dividend (+1.64) and Tax (-0.24) postings for same security/date
        var pDiv = new FinanceManager.Domain.Postings.Posting(
            Guid.NewGuid(), PostingKind.Security,
            accountId: null, contactId: null, savingsPlanId: null, securityId: securityId,
            bookingDate: date, amount: 1.64m,
            subject: null, recipientName: null, description: null,
            securitySubType: SecurityPostingSubType.Dividend, quantity: null);

        var pTax = new FinanceManager.Domain.Postings.Posting(
            Guid.NewGuid(), PostingKind.Security,
            accountId: null, contactId: null, savingsPlanId: null, securityId: securityId,
            bookingDate: date, amount: -0.24m,
            subject: null, recipientName: null, description: null,
            securitySubType: SecurityPostingSubType.Tax, quantity: null);

        await svc.UpsertForPostingAsync(pDiv, ct);
        await svc.UpsertForPostingAsync(pTax, ct);
        await db.SaveChangesAsync(ct);

        var monthStart = new DateTime(2025, 8, 1);
        var quarterStart = new DateTime(2025, 7, 1);
        var halfStart = new DateTime(2025, 7, 1);
        var yearStart = new DateTime(2025, 1, 1);

        // Expected: two aggregates per period for the security (one for Dividend, one for Tax) since subtype now part of key
        // Note: aggregates are created per DateKind (Booking + Valuta) resulting in doubled rows -> expect 4
        int CountPer(DateTime start, AggregatePeriod period)
            => db.PostingAggregates.Count(a => a.Kind == PostingKind.Security && a.SecurityId == securityId && a.Period == period && a.PeriodStart == start);

        Assert.Equal(4, CountPer(monthStart, AggregatePeriod.Month));
        Assert.Equal(4, CountPer(quarterStart, AggregatePeriod.Quarter));
        Assert.Equal(4, CountPer(halfStart, AggregatePeriod.HalfYear));
        Assert.Equal(4, CountPer(yearStart, AggregatePeriod.Year));

        // And amounts should include both +1.64 and -0.24 for each period
        void AssertAmounts(DateTime start, AggregatePeriod period)
        {
            var amts = db.PostingAggregates
                .Where(a => a.Kind == PostingKind.Security && a.SecurityId == securityId && a.Period == period && a.PeriodStart == start)
                .Select(a => a.Amount)
                .AsEnumerable() // force client-side ordering to avoid SQLite decimal ORDER BY limitation
                .OrderBy(x => x)
                .ToList();
            Assert.Contains(-0.24m, amts);
            Assert.Contains(1.64m, amts);
        }

        AssertAmounts(monthStart, AggregatePeriod.Month);
        AssertAmounts(quarterStart, AggregatePeriod.Quarter);
        AssertAmounts(halfStart, AggregatePeriod.HalfYear);
        AssertAmounts(yearStart, AggregatePeriod.Year);
    }
}
