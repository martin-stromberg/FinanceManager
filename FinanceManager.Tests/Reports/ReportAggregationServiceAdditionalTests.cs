using FinanceManager.Domain.Contacts;
using FinanceManager.Domain.Postings;
using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Reports;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace FinanceManager.Tests.Reports;

/// <summary>
/// Covers additional edge cases of <see cref="ReportAggregationService.QueryAsync"/> beyond the core suite:
/// entity-only output when <c>IncludeCategory</c> is off, auto-injecting a zero-amount row for a group missing
/// data in the latest period when comparisons are enabled, pruning groups with no meaningful (non-zero,
/// no-comparison) data, the <c>Take</c> period-window limit, aggregation across Quarter/HalfYear/Year intervals
/// with previous- and year-over-year comparisons, and grouping contacts without a category under an "_none"
/// pseudo-category.
/// </summary>
public sealed class ReportAggregationServiceAdditionalTests
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

    private static Contact CreateContact(AppDbContext db, Guid userId, string name, ContactCategory? cat = null)
    {
        var c = new Contact(userId, name, ContactType.Person, cat?.Id, null);
        db.Contacts.Add(c);
        return c;
    }

    /// <summary>
    /// With <c>IncludeCategory = false</c>, the result must contain only entity-level rows (<c>GroupKey</c>
    /// starting "Contact:") and no synthetic category rows; and a group whose only data point is the latest
    /// period (no earlier period to compare against) must report a null <c>PreviousAmount</c> rather than zero.
    /// </summary>
    [Fact]
    public async Task QueryAsync_ShouldReturnOnlyEntityRows_WhenIncludeCategoryFalse()
    {
        using var db = CreateDb();
        var user = new FinanceManager.Domain.Users.User("u", "pw", false);
        db.Users.Add(user);
        var cat = new ContactCategory(user.Id, "Food");
        db.ContactCategories.Add(cat);
        var c1 = CreateContact(db, user.Id, "A", cat);
        var c2 = CreateContact(db, user.Id, "B", cat);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var jan = new PostingAggregate(PostingKind.Contact, null, c1.Id, null, null, new DateTime(2025, 1, 1), AggregatePeriod.Month); jan.Add(10);
        var feb = new PostingAggregate(PostingKind.Contact, null, c2.Id, null, null, new DateTime(2025, 2, 1), AggregatePeriod.Month); feb.Add(20);
        db.PostingAggregates.AddRange(jan, feb);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var sut = new ReportAggregationService(db, new NullLogger<ReportAggregationService>());
        var result = await sut.QueryAsync(new ReportAggregationQuery(user.Id, PostingKind.Contact, ReportInterval.Month, 12, IncludeCategory: false, ComparePrevious: true, CompareYear: false, AnalysisDate: new DateTime(2025, 2, 1)), CancellationToken.None);

        Assert.All(result.Points, p => Assert.StartsWith("Contact:", p.GroupKey));
        Assert.DoesNotContain(result.Points, p => p.GroupKey.StartsWith("Category:"));
        // Previous for Feb contact (c2) should be null because it only has single period itself
        var febPoint = result.Points.Single(p => p.GroupKey == $"Contact:{c2.Id}" && p.PeriodStart == new DateTime(2025, 2, 1));
        Assert.Null(febPoint.PreviousAmount);
    }

    /// <summary>
    /// When comparisons are enabled and one contact has no aggregate for the latest period while another does,
    /// the query must synthesize a zero-amount row for the latest period for the missing contact, so its
    /// <c>PreviousAmount</c> (carried from its last available period) can still be surfaced - rather than simply
    /// omitting that contact from the latest period entirely.
    /// </summary>
    [Fact]
    public async Task QueryAsync_ShouldCreateZeroRowForMissingLatestPeriod_WhenComparisonsEnabled()
    {
        using var db = CreateDb();
        var user = new FinanceManager.Domain.Users.User("u", "pw", false);
        db.Users.Add(user);
        var c1 = CreateContact(db, user.Id, "A");
        var c2 = CreateContact(db, user.Id, "B");
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        // c1 has Jan & Feb, c2 only Jan. Latest period Feb. IncludeCategory true to also test parent/child creation.
        var c1Jan = new PostingAggregate(PostingKind.Contact, null, c1.Id, null, null, new DateTime(2025, 1, 1), AggregatePeriod.Month); c1Jan.Add(10);
        var c1Feb = new PostingAggregate(PostingKind.Contact, null, c1.Id, null, null, new DateTime(2025, 2, 1), AggregatePeriod.Month); c1Feb.Add(20);
        var c2Jan = new PostingAggregate(PostingKind.Contact, null, c2.Id, null, null, new DateTime(2025, 1, 1), AggregatePeriod.Month); c2Jan.Add(30);
        db.PostingAggregates.AddRange(c1Jan, c1Feb, c2Jan);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var sut = new ReportAggregationService(db, new NullLogger<ReportAggregationService>());
        var latest = new DateTime(2025, 2, 1);
        var result = await sut.QueryAsync(new ReportAggregationQuery(user.Id, PostingKind.Contact, ReportInterval.Month, 12, IncludeCategory: true, ComparePrevious: true, CompareYear: false, AnalysisDate: latest), CancellationToken.None);

        // c2 should have auto-added zero row in Feb with previous = Jan amount
        var c2Feb = result.Points.SingleOrDefault(p => p.GroupKey == $"Contact:{c2.Id}" && p.PeriodStart == latest);
        Assert.NotNull(c2Feb);
        Assert.Equal(0m, c2Feb!.Amount);
        Assert.Equal(30m, c2Feb.PreviousAmount);
    }

    /// <summary>
    /// A group whose only aggregate is a zero-amount historic period, with no current-period activity and
    /// nothing meaningful to compare, must be pruned from the result entirely - keeping the report free of noise
    /// rows for contacts with no real financial activity in the requested window.
    /// </summary>
    [Fact]
    public async Task QueryAsync_ShouldRemoveEmptyGroupWithoutComparisonData()
    {
        using var db = CreateDb();
        var user = new FinanceManager.Domain.Users.User("u", "pw", false);
        db.Users.Add(user);
        var c1 = CreateContact(db, user.Id, "A"); // will have zero historic amount only
        var c2 = CreateContact(db, user.Id, "B");
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var c1Jan = new PostingAggregate(PostingKind.Contact, null, c1.Id, null, null, new DateTime(2025, 1, 1), AggregatePeriod.Month); // amount 0
        var c2Feb = new PostingAggregate(PostingKind.Contact, null, c2.Id, null, null, new DateTime(2025, 2, 1), AggregatePeriod.Month); c2Feb.Add(50);
        db.PostingAggregates.AddRange(c1Jan, c2Feb);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var sut = new ReportAggregationService(db, new NullLogger<ReportAggregationService>());
        var result = await sut.QueryAsync(new ReportAggregationQuery(user.Id, PostingKind.Contact, ReportInterval.Month, 12, IncludeCategory: false, ComparePrevious: true, CompareYear: true, AnalysisDate: new DateTime(2025, 2, 1)), CancellationToken.None);

        // Group for c1 should be removed (only zero data + zero row + no previous/year non-zero)
        Assert.DoesNotContain(result.Points, p => p.GroupKey == $"Contact:{c1.Id}");
        Assert.Contains(result.Points, p => p.GroupKey == $"Contact:{c2.Id}");
    }

    /// <summary>
    /// With 15 months of history available but <c>Take = 5</c>, the query must return exactly the most recent 5
    /// periods relative to the analysis date (November 2024 through March 2025), not the full history.
    /// </summary>
    [Fact]
    public async Task QueryAsync_ShouldRespectTake_PeriodLimitation()
    {
        using var db = CreateDb();
        var user = new FinanceManager.Domain.Users.User("u", "pw", false);
        db.Users.Add(user);
        var c1 = CreateContact(db, user.Id, "A");
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Create 15 consecutive months starting Jan 2024
        for (int i = 0; i < 15; i++)
        {
            var dt = new DateTime(2024, 1, 1).AddMonths(i);
            var agg = new PostingAggregate(PostingKind.Contact, null, c1.Id, null, null, new DateTime(dt.Year, dt.Month, 1), AggregatePeriod.Month);
            agg.Add(i + 1);
            db.PostingAggregates.Add(agg);
        }
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var sut = new ReportAggregationService(db, new NullLogger<ReportAggregationService>());
        var take = 5;
        var result = await sut.QueryAsync(new ReportAggregationQuery(user.Id, PostingKind.Contact, ReportInterval.Month, take, IncludeCategory: false, ComparePrevious: false, CompareYear: false, AnalysisDate: new DateTime(2025, 3, 1)), CancellationToken.None);

        var periods = result.Points.Select(p => p.PeriodStart).Distinct().OrderBy(d => d).ToList();
        Assert.Equal(take, periods.Count);
        Assert.Equal(new DateTime(2024, 11, 1), periods.First()); // last 5 of 15 months (2024-11 .. 2025-03)
    }

    /// <summary>
    /// Confirms the aggregation service correctly reads precomputed Quarter/HalfYear/Year
    /// <see cref="PostingAggregate"/> rows (not just Month) and wires up previous-period and year-over-year
    /// comparisons correctly at each of these coarser granularities.
    /// </summary>
    [Fact]
    public async Task QueryAsync_ShouldAggregateQuarterHalfYearYear()
    {
        using var db = CreateDb();
        var user = new FinanceManager.Domain.Users.User("u", "pw", false);
        db.Users.Add(user);
        var c = CreateContact(db, user.Id, "A");
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Q1/Q2 2024, H1/H2 2024, Years 2024/2025
        var q1 = new PostingAggregate(PostingKind.Contact, null, c.Id, null, null, new DateTime(2024, 1, 1), AggregatePeriod.Quarter); q1.Add(100);
        var q2 = new PostingAggregate(PostingKind.Contact, null, c.Id, null, null, new DateTime(2024, 4, 1), AggregatePeriod.Quarter); q2.Add(150);
        var h1 = new PostingAggregate(PostingKind.Contact, null, c.Id, null, null, new DateTime(2024, 1, 1), AggregatePeriod.HalfYear); h1.Add(250);
        var h2 = new PostingAggregate(PostingKind.Contact, null, c.Id, null, null, new DateTime(2024, 7, 1), AggregatePeriod.HalfYear); h2.Add(300);
        var y2024 = new PostingAggregate(PostingKind.Contact, null, c.Id, null, null, new DateTime(2024, 1, 1), AggregatePeriod.Year); y2024.Add(550);
        var y2025 = new PostingAggregate(PostingKind.Contact, null, c.Id, null, null, new DateTime(2025, 1, 1), AggregatePeriod.Year); y2025.Add(50);
        db.PostingAggregates.AddRange(q1, q2, h1, h2, y2024, y2025);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var sut = new ReportAggregationService(db, new NullLogger<ReportAggregationService>());

        var quarters = await sut.QueryAsync(new ReportAggregationQuery(user.Id, PostingKind.Contact, ReportInterval.Quarter, 10, IncludeCategory: false, ComparePrevious: true, CompareYear: true, AnalysisDate: new DateTime(2024, 4, 1)), CancellationToken.None);
        Assert.Equal(2, quarters.Points.Count(p => p.GroupKey.StartsWith("Contact:")));
        var q2Point = quarters.Points.Single(p => p.PeriodStart == new DateTime(2024, 4, 1) && p.GroupKey.StartsWith("Contact:"));
        Assert.Equal(100m, q2Point.PreviousAmount);

        var halfYears = await sut.QueryAsync(new ReportAggregationQuery(user.Id, PostingKind.Contact, ReportInterval.HalfYear, 10, IncludeCategory: false, ComparePrevious: true, CompareYear: false, AnalysisDate: new DateTime(2024, 7, 1)), CancellationToken.None);
        Assert.Equal(2, halfYears.Points.Count);
        var h2Point = halfYears.Points.Single(p => p.PeriodStart == new DateTime(2024, 7, 1));
        Assert.Equal(250m, h2Point.PreviousAmount);

        var years = await sut.QueryAsync(new ReportAggregationQuery(user.Id, PostingKind.Contact, ReportInterval.Year, 10, IncludeCategory: false, ComparePrevious: true, CompareYear: true, AnalysisDate: new DateTime(2025, 1, 1)), CancellationToken.None);
        Assert.Equal(2, years.Points.Count);
        var y2025Point = years.Points.Single(p => p.PeriodStart == new DateTime(2025, 1, 1));
        Assert.Equal(550m, y2025Point.PreviousAmount);
        Assert.Equal(550m, y2025Point.YearAgoAmount);
    }

    /// <summary>
    /// With <c>IncludeCategory = true</c>, a contact that has no assigned category must be grouped under a
    /// synthetic "_none" pseudo-category row (<c>GroupKey = "Category:{kind}:_none"</c>) rather than being
    /// dropped or grouped incorrectly, and the contact's own row must reference that pseudo-category via
    /// <c>ParentGroupKey</c>.
    /// </summary>
    [Fact]
    public async Task QueryAsync_ShouldGroupUncategorizedContacts()
    {
        using var db = CreateDb();
        var user = new FinanceManager.Domain.Users.User("u", "pw", false);
        db.Users.Add(user);
        var c1 = CreateContact(db, user.Id, "NoCat");
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var agg = new PostingAggregate(PostingKind.Contact, null, c1.Id, null, null, new DateTime(2025, 3, 1), AggregatePeriod.Month); agg.Add(42);
        db.PostingAggregates.Add(agg);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var sut = new ReportAggregationService(db, new NullLogger<ReportAggregationService>());
        var result = await sut.QueryAsync(new ReportAggregationQuery(user.Id, PostingKind.Contact, ReportInterval.Month, 5, IncludeCategory: true, ComparePrevious: false, CompareYear: false, AnalysisDate: new DateTime(2025, 3, 1)), CancellationToken.None);
        Assert.Contains(result.Points, p => p.GroupKey == $"Category:{PostingKind.Contact}:_none" && p.Amount == 42m);
        var child = result.Points.Single(p => p.GroupKey == $"Contact:{c1.Id}");
        Assert.Equal($"Category:{PostingKind.Contact}:_none", child.ParentGroupKey);
    }
}
