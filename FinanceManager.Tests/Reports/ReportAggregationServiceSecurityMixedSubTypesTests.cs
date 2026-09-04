using FinanceManager.Domain.Postings;
using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Reports;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace FinanceManager.Tests.Reports;

/// <summary>
/// Covers <see cref="ReportAggregationService.QueryAsync"/>'s handling of Security postings that are stored as
/// separate <see cref="PostingAggregate"/> rows per <see cref="SecurityPostingSubType"/> (Buy, Fee, Dividend,
/// Tax, etc.) for the same security and period - specifically that querying without a subtype filter collapses
/// them all into a single summed row per security/period rather than one row per subtype.
/// </summary>
public sealed class ReportAggregationServiceSecurityMixedSubTypesTests
{
    private static AppDbContext CreateDb()
    {
        var conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options;
        var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    /// <summary>
    /// When a security has four separate per-subtype aggregates (Buy, Fee, Dividend, Tax) for the same month and
    /// no subtype filter is applied, the query must sum all four into a single result row for that security and
    /// period - not report them as four separate rows or silently drop any subtype.
    /// </summary>
    [Fact]
    public async Task QueryAsync_SecurityMonth_NoSubtypeFilter_ShouldSumAllSubTypes()
    {
        using var db = CreateDb();
        var user = new FinanceManager.Domain.Users.User("owner", "pw", false);
        db.Users.Add(user);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var sec = new FinanceManager.Domain.Securities.Security(user.Id, "ACME CORP", "ACME-ISIN", null, null, "EUR", null);
        db.Securities.Add(sec);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Target month (analysis)
        var analysis = new DateTime(2025, 8, 1);

        // Seed four separate aggregates for the same month+security but different sub types
        void Add(SecurityPostingSubType subtype, decimal amount)
        {
            var agg = new PostingAggregate(PostingKind.Security, null, null, null, sec.Id, analysis, AggregatePeriod.Month, subtype);
            agg.Add(amount);
            db.PostingAggregates.Add(agg);
        }

        // Example amounts (Buy negative, Fee negative, Dividend positive, Tax negative)
        Add(SecurityPostingSubType.Buy, -1000.00m);
        Add(SecurityPostingSubType.Fee, -4.50m);
        Add(SecurityPostingSubType.Dividend, 25.75m);
        Add(SecurityPostingSubType.Tax, -3.90m);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var sut = new ReportAggregationService(db, new NullLogger<ReportAggregationService>());
        var query = new ReportAggregationQuery(
            OwnerUserId: user.Id,
            PostingKind: PostingKind.Security,
            Interval: ReportInterval.Month,
            Take: 12,
            IncludeCategory: false,
            ComparePrevious: false,
            CompareYear: false,
            PostingKinds: null,
            AnalysisDate: analysis,
            Filters: null // no subtype filter -> include all
        );

        var result = await sut.QueryAsync(query, CancellationToken.None);
        Assert.Equal(ReportInterval.Month, result.Interval);
        Assert.NotEmpty(result.Points);

        var key = $"Security:{sec.Id}";
        var row = result.Points.Single(p => p.GroupKey == key && p.PeriodStart == analysis);

        var expected = -1000.00m - 4.50m + 25.75m - 3.90m; // sum of all sub types
        Assert.Equal(expected, row.Amount);

        // Ensure only one row for that security+month (aggregated across sub types)
        Assert.Equal(1, result.Points.Count(p => p.GroupKey == key && p.PeriodStart == analysis));
    }
}
