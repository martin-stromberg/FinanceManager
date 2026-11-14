using FinanceManager.Domain.Postings;
using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Reports;
using FinanceManager.Shared.Dtos.Securities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace FinanceManager.Tests.Reports;

/// <summary>
/// Covers <see cref="ReportAggregationService.QueryAsync"/>'s handling of Security postings that are stored as
/// separate <see cref="PostingAggregate"/> rows per <see cref="SecurityPostingSubType"/> (Buy, Fee, Dividend,
/// Tax, etc.) for the same security and period - the query must emit ONE point per (security, period, sub type),
/// each tagged with its own <see cref="ReportAggregatePointDto.SecuritySubType"/>, so consumers can distinguish
/// e.g. dividends from buys. Roll-up rows (Category:/Type:) stay netted across sub types.
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

    private static async Task<(AppDbContext, FinanceManager.Domain.Users.User)> NewUserDb()
    {
        var db = CreateDb();
        var user = new FinanceManager.Domain.Users.User("owner", "pw", false);
        db.Users.Add(user);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return (db, user);
    }

    private static void AddAgg(AppDbContext db, Guid secId, DateTime period, SecurityPostingSubType subtype, decimal amount)
    {
        var agg = new PostingAggregate(PostingKind.Security, null, null, null, secId, period, AggregatePeriod.Month, subtype);
        agg.Add(amount);
        db.PostingAggregates.Add(agg);
    }

    /// <summary>
    /// When a security has four separate per-subtype aggregates (Buy, Fee, Dividend, Tax) for the same month and
    /// no subtype filter is applied, the query must emit one point per sub type — each tagged with its own
    /// <see cref="ReportAggregatePointDto.SecuritySubType"/> and amount — instead of a single flattened row.
    /// The sum of the split amounts must equal the previously flattened total.
    /// </summary>
    [Fact]
    public async Task QueryAsync_SecurityMonth_NoSubtypeFilter_ShouldSplitAllSubTypes()
    {
        var (db, user) = await NewUserDb();
        using (db)
        {
            var sec = new FinanceManager.Domain.Securities.Security(user.Id, "ACME CORP", "ACME-ISIN", null, null, "EUR", null);
            db.Securities.Add(sec);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);

            // Target month (analysis)
            var analysis = new DateTime(2025, 8, 1);

            // Seed four separate aggregates for the same month+security but different sub types
            // Example amounts (Buy negative, Fee negative, Dividend positive, Tax negative)
            AddAgg(db, sec.Id, analysis, SecurityPostingSubType.Buy, -1000.00m);
            AddAgg(db, sec.Id, analysis, SecurityPostingSubType.Fee, -4.50m);
            AddAgg(db, sec.Id, analysis, SecurityPostingSubType.Dividend, 25.75m);
            AddAgg(db, sec.Id, analysis, SecurityPostingSubType.Tax, -3.90m);
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
            var rows = result.Points.Where(p => p.GroupKey.StartsWith(key + ":") && p.PeriodStart == analysis).ToList();

            // One row per sub type, each tagged with its own SecuritySubType
            Assert.Equal(4, rows.Count);

            var expectedAmounts = new Dictionary<SecurityPostingSubType, decimal>
            {
                [SecurityPostingSubType.Buy] = -1000.00m,
                [SecurityPostingSubType.Fee] = -4.50m,
                [SecurityPostingSubType.Dividend] = 25.75m,
                [SecurityPostingSubType.Tax] = -3.90m
            };
            foreach (var (subtype, expectedAmount) in expectedAmounts)
            {
                var row = rows.Single(p => p.SecuritySubType == subtype);
                Assert.Equal(expectedAmount, row.Amount);
                Assert.Equal($"{key}:{subtype}", row.GroupKey);
            }

            // Sum of split amounts reproduces the previously flattened total
            var expectedTotal = -1000.00m - 4.50m + 25.75m - 3.90m;
            Assert.Equal(expectedTotal, rows.Sum(r => r.Amount));
        }
    }

    /// <summary>
    /// With <c>securitySubTypes:[Dividend]</c> only Dividend rows may be emitted for the security.
    /// </summary>
    [Fact]
    public async Task QueryAsync_SecuritySubtypeFilter_ShouldEmitOnlyMatchingSubType()
    {
        var (db, user) = await NewUserDb();
        using (db)
        {
            var sec = new FinanceManager.Domain.Securities.Security(user.Id, "ACME CORP", "ACME-ISIN", null, null, "EUR", null);
            db.Securities.Add(sec);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);

            var analysis = new DateTime(2025, 8, 1);
            AddAgg(db, sec.Id, analysis, SecurityPostingSubType.Buy, -1000.00m);
            AddAgg(db, sec.Id, analysis, SecurityPostingSubType.Dividend, 25.75m);
            AddAgg(db, sec.Id, analysis, SecurityPostingSubType.Sell, 500.00m);
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
                Filters: new ReportAggregationFilters(
                    null, null, null, null, null, null, null,
                    SecuritySubTypes: new[] { (int)SecurityPostingSubType.Dividend })
            );

            var result = await sut.QueryAsync(query, CancellationToken.None);
            var key = $"Security:{sec.Id}";
            var rows = result.Points.Where(p => p.GroupKey.StartsWith(key)).ToList();

            Assert.Single(rows);
            Assert.Equal(SecurityPostingSubType.Dividend, rows[0].SecuritySubType);
            Assert.Equal(25.75m, rows[0].Amount);
            Assert.Equal($"{key}:Dividend", rows[0].GroupKey);
        }
    }

    /// <summary>
    /// Non-security points must carry <c>SecuritySubType = null</c>.
    /// </summary>
    [Fact]
    public async Task QueryAsync_BankPoints_HaveNullSecuritySubType()
    {
        var (db, user) = await NewUserDb();
        using (db)
        {
            var acc = new FinanceManager.Domain.Accounts.Account(user.Id, AccountType.Giro, "Giro", null, Guid.NewGuid());
            db.Accounts.Add(acc);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);

            var analysis = new DateTime(2025, 8, 1);
            var agg = new PostingAggregate(PostingKind.Bank, acc.Id, null, null, null, analysis, AggregatePeriod.Month, null);
            agg.Add(-42.5m);
            db.PostingAggregates.Add(agg);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);

            var sut = new ReportAggregationService(db, new NullLogger<ReportAggregationService>());
            var query = new ReportAggregationQuery(
                OwnerUserId: user.Id,
                PostingKind: PostingKind.Bank,
                Interval: ReportInterval.Month,
                Take: 12,
                IncludeCategory: false,
                ComparePrevious: false,
                CompareYear: false,
                PostingKinds: null,
                AnalysisDate: analysis,
                Filters: null
            );

            var result = await sut.QueryAsync(query, CancellationToken.None);
            var row = result.Points.Single(p => p.GroupKey == $"Account:{acc.Id}" && p.PeriodStart == analysis);
            Assert.Null(row.SecuritySubType);
            Assert.Equal(-42.5m, row.Amount);
        }
    }

    /// <summary>
    /// Comparison values must be tracked per (security, sub type): a Dividend point's PreviousAmount must come
    /// from the previous period's Dividend row, not from other sub types of the same security.
    /// </summary>
    [Fact]
    public async Task QueryAsync_SecurityComparePrevious_ShouldMatchPerSubType()
    {
        var (db, user) = await NewUserDb();
        using (db)
        {
            var sec = new FinanceManager.Domain.Securities.Security(user.Id, "ACME CORP", "ACME-ISIN", null, null, "EUR", null);
            db.Securities.Add(sec);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);

            var analysis = new DateTime(2025, 8, 1);
            var prev = analysis.AddMonths(-1);

            AddAgg(db, sec.Id, prev, SecurityPostingSubType.Buy, -800m);
            AddAgg(db, sec.Id, prev, SecurityPostingSubType.Dividend, 10m);
            AddAgg(db, sec.Id, analysis, SecurityPostingSubType.Buy, -1200m);
            AddAgg(db, sec.Id, analysis, SecurityPostingSubType.Dividend, 30m);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);

            var sut = new ReportAggregationService(db, new NullLogger<ReportAggregationService>());
            var query = new ReportAggregationQuery(
                OwnerUserId: user.Id,
                PostingKind: PostingKind.Security,
                Interval: ReportInterval.Month,
                Take: 12,
                IncludeCategory: false,
                ComparePrevious: true,
                CompareYear: true,
                PostingKinds: null,
                AnalysisDate: analysis,
                Filters: null
            );

            var result = await sut.QueryAsync(query, CancellationToken.None);
            var key = $"Security:{sec.Id}";

            var buyRow = result.Points.Single(p => p.GroupKey == $"{key}:Buy" && p.PeriodStart == analysis);
            Assert.Equal(-1200m, buyRow.Amount);
            Assert.Equal(-800m, buyRow.PreviousAmount); // previous month's BUY amount, not dividend

            var divRow = result.Points.Single(p => p.GroupKey == $"{key}:Dividend" && p.PeriodStart == analysis);
            Assert.Equal(30m, divRow.Amount);
            Assert.Equal(10m, divRow.PreviousAmount); // previous month's DIVIDEND amount
        }
    }

    /// <summary>
    /// The parent linkage of split security children stays consistent: all sub type rows of one security point at
    /// the same category row, which remains netted across sub types.
    /// </summary>
    [Fact]
    public async Task QueryAsync_SecurityCategory_StaysNetOverSubTypes()
    {
        var (db, user) = await NewUserDb();
        using (db)
        {
            var cat = new FinanceManager.Domain.Securities.SecurityCategory(user.Id, "Aktien");
            db.SecurityCategories.Add(cat);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            var sec = new FinanceManager.Domain.Securities.Security(user.Id, "ACME CORP", "ACME-ISIN", null, null, "EUR", cat.Id);
            db.Securities.Add(sec);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);

            var analysis = new DateTime(2025, 8, 1);
            AddAgg(db, sec.Id, analysis, SecurityPostingSubType.Buy, -100m);
            AddAgg(db, sec.Id, analysis, SecurityPostingSubType.Dividend, 40m);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);

            var sut = new ReportAggregationService(db, new NullLogger<ReportAggregationService>());
            var query = new ReportAggregationQuery(
                OwnerUserId: user.Id,
                PostingKind: PostingKind.Security,
                Interval: ReportInterval.Month,
                Take: 12,
                IncludeCategory: true,
                ComparePrevious: false,
                CompareYear: false,
                PostingKinds: null,
                AnalysisDate: analysis,
                Filters: null
            );

            var result = await sut.QueryAsync(query, CancellationToken.None);
            var catKey = $"Category:Security:{cat.Id}";
            var secKey = $"Security:{sec.Id}";

            // Category row: one netted row over all sub types
            var catRow = result.Points.Single(p => p.GroupKey == catKey && p.PeriodStart == analysis);
            Assert.Equal(-60m, catRow.Amount);
            Assert.Null(catRow.SecuritySubType);

            // Both subtype children point at the same category row
            var children = result.Points.Where(p => p.GroupKey.StartsWith(secKey + ":") && p.PeriodStart == analysis).ToList();
            Assert.Equal(2, children.Count);
            Assert.All(children, c => Assert.Equal(catKey, c.ParentGroupKey));
        }
    }
}
