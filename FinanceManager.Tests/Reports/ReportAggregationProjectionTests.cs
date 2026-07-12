using FinanceManager.Domain.Postings;
using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Reports;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace FinanceManager.Tests.Reports;

public sealed class ReportAggregationProjectionTests
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

    private static async Task<FinanceManager.Domain.Users.User> AddUserAsync(AppDbContext db, string name)
    {
        var user = new FinanceManager.Domain.Users.User(name, "pw", false);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private static FinanceManager.Domain.Securities.Security AddSecurity(
        AppDbContext db,
        Guid ownerUserId,
        string name,
        Guid? categoryId = null)
    {
        var security = new FinanceManager.Domain.Securities.Security(ownerUserId, name, $"ISIN-{Guid.NewGuid():N}", null, null, "EUR", categoryId);
        db.Securities.Add(security);
        return security;
    }

    private static void AddDividendGroup(
        AppDbContext db,
        FinanceManager.Domain.Securities.Security security,
        DateTime bookingDate,
        decimal dividend,
        decimal fee = 0m,
        decimal tax = 0m,
        DateTime? valutaDate = null)
    {
        var groupId = Guid.NewGuid();
        var effectiveValutaDate = valutaDate ?? bookingDate;
        db.Postings.Add(new Posting(Guid.NewGuid(), PostingKind.Security, null, null, null, security.Id, bookingDate, effectiveValutaDate, dividend, "Dividend", null, null, SecurityPostingSubType.Dividend).SetGroup(groupId));
        if (fee != 0m)
        {
            db.Postings.Add(new Posting(Guid.NewGuid(), PostingKind.Security, null, null, null, security.Id, bookingDate, effectiveValutaDate, fee, "Fee", null, null, SecurityPostingSubType.Fee).SetGroup(groupId));
        }
        if (tax != 0m)
        {
            db.Postings.Add(new Posting(Guid.NewGuid(), PostingKind.Security, null, null, null, security.Id, bookingDate, effectiveValutaDate, tax, "Tax", null, null, SecurityPostingSubType.Tax).SetGroup(groupId));
        }
    }

    [Fact]
    public async Task QueryAsync_SecurityDividendProjection_AddsUnconfirmedPriorYearNetDividend()
    {
        using var db = CreateDb();
        var user = await AddUserAsync(db, "projection-owner");
        var missingCurrent = AddSecurity(db, user.Id, "Missing Current");
        var confirmedCurrent = AddSecurity(db, user.Id, "Confirmed Current");
        await db.SaveChangesAsync();

        var analysis = new DateTime(2026, 5, 1);
        AddDividendGroup(db, missingCurrent, new DateTime(2025, 5, 10), 100m, -5m, -25m);
        AddDividendGroup(db, confirmedCurrent, new DateTime(2025, 5, 12), 40m);
        AddDividendGroup(db, confirmedCurrent, new DateTime(2026, 5, 20), 50m);
        await db.SaveChangesAsync();

        var sut = new ReportAggregationService(db, new NullLogger<ReportAggregationService>());
        var query = new ReportAggregationQuery(
            user.Id,
            PostingKind.Security,
            ReportInterval.Month,
            2,
            IncludeCategory: false,
            ComparePrevious: false,
            CompareYear: false,
            CompareProjection: true,
            AnalysisDate: analysis);

        var result = await sut.QueryAsync(query, CancellationToken.None);

        Assert.True(result.ComparedProjection);
        var missing = result.Points.Single(p => p.GroupKey == $"Security:{missingCurrent.Id}" && p.PeriodStart == analysis);
        var confirmed = result.Points.Single(p => p.GroupKey == $"Security:{confirmedCurrent.Id}" && p.PeriodStart == analysis);
        Assert.Equal(0m, missing.Amount);
        Assert.Equal(70m, missing.ProjectionAmount);
        Assert.Equal(50m, confirmed.Amount);
        Assert.Equal(50m, confirmed.ProjectionAmount);
    }

    [Fact]
    public async Task QueryAsync_SecurityDividendProjection_MatchesPriorYearEventsIndividually()
    {
        using var db = CreateDb();
        var user = await AddUserAsync(db, "projection-events");
        var security = AddSecurity(db, user.Id, "Quarterly Dividend");
        await db.SaveChangesAsync();

        var analysis = new DateTime(2026, 5, 1);
        AddDividendGroup(db, security, new DateTime(2025, 5, 10), 40m);
        AddDividendGroup(db, security, new DateTime(2025, 5, 25), 60m);
        AddDividendGroup(db, security, new DateTime(2026, 5, 10), 45m);
        await db.SaveChangesAsync();

        var sut = new ReportAggregationService(db, new NullLogger<ReportAggregationService>());
        var result = await sut.QueryAsync(new ReportAggregationQuery(
            user.Id,
            PostingKind.Security,
            ReportInterval.Month,
            1,
            IncludeCategory: false,
            ComparePrevious: false,
            CompareYear: false,
            CompareProjection: true,
            AnalysisDate: analysis), CancellationToken.None);

        var point = result.Points.Single(p => p.GroupKey == $"Security:{security.Id}" && p.PeriodStart == analysis);
        Assert.Equal(45m, point.Amount);
        Assert.Equal(105m, point.ProjectionAmount);
    }

    [Fact]
    public async Task QueryAsync_SecurityDividendProjection_UsesValutaDateAndBookingFallback()
    {
        using var db = CreateDb();
        var user = await AddUserAsync(db, "projection-valuta");
        var valutaSecurity = AddSecurity(db, user.Id, "Valuta Security");
        var bookingSecurity = AddSecurity(db, user.Id, "Booking Fallback Security");
        await db.SaveChangesAsync();

        var analysis = new DateTime(2026, 5, 1);
        AddDividendGroup(db, valutaSecurity, new DateTime(2025, 4, 30), 30m, valutaDate: new DateTime(2025, 5, 2));
        AddDividendGroup(db, bookingSecurity, new DateTime(2025, 5, 4), 70m);
        await db.SaveChangesAsync();

        var sut = new ReportAggregationService(db, new NullLogger<ReportAggregationService>());
        var result = await sut.QueryAsync(new ReportAggregationQuery(
            user.Id,
            PostingKind.Security,
            ReportInterval.Month,
            1,
            IncludeCategory: false,
            ComparePrevious: false,
            CompareYear: false,
            CompareProjection: true,
            AnalysisDate: analysis,
            UseValutaDate: true), CancellationToken.None);

        Assert.Equal(30m, result.Points.Single(p => p.GroupKey == $"Security:{valutaSecurity.Id}" && p.PeriodStart == analysis).ProjectionAmount);
        Assert.Equal(70m, result.Points.Single(p => p.GroupKey == $"Security:{bookingSecurity.Id}" && p.PeriodStart == analysis).ProjectionAmount);
    }

    [Fact]
    public async Task QueryAsync_ProjectionIsIgnored_ForNonSecurityKind()
    {
        using var db = CreateDb();
        var user = new FinanceManager.Domain.Users.User("projection-bank", "pw", false);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var sut = new ReportAggregationService(db, new NullLogger<ReportAggregationService>());
        var result = await sut.QueryAsync(new ReportAggregationQuery(
            user.Id,
            PostingKind.Bank,
            ReportInterval.Month,
            1,
            IncludeCategory: false,
            ComparePrevious: false,
            CompareYear: false,
            CompareProjection: true,
            AnalysisDate: new DateTime(2026, 5, 1)), CancellationToken.None);

        Assert.False(result.ComparedProjection);
        Assert.All(result.Points, p => Assert.Null(p.ProjectionAmount));
    }

    [Fact]
    public async Task QueryAsync_ProjectionIsIgnored_ForMultiKindAndInvalidSecuritySubtype()
    {
        using var db = CreateDb();
        var user = await AddUserAsync(db, "projection-invalid");
        var security = AddSecurity(db, user.Id, "Invalid Selection Security");
        await db.SaveChangesAsync();
        AddDividendGroup(db, security, new DateTime(2026, 5, 10), 20m);
        await db.SaveChangesAsync();

        var sut = new ReportAggregationService(db, new NullLogger<ReportAggregationService>());
        var multiKind = await sut.QueryAsync(new ReportAggregationQuery(
            user.Id,
            PostingKind.Security,
            ReportInterval.Month,
            1,
            IncludeCategory: false,
            ComparePrevious: false,
            CompareYear: false,
            CompareProjection: true,
            PostingKinds: new[] { PostingKind.Security, PostingKind.Bank },
            AnalysisDate: new DateTime(2026, 5, 1)), CancellationToken.None);

        var invalidSubtype = await sut.QueryAsync(new ReportAggregationQuery(
            user.Id,
            PostingKind.Security,
            ReportInterval.Month,
            1,
            IncludeCategory: false,
            ComparePrevious: false,
            CompareYear: false,
            CompareProjection: true,
            AnalysisDate: new DateTime(2026, 5, 1),
            Filters: new ReportAggregationFilters(SecuritySubTypes: new[] { (int)SecurityPostingSubType.Buy })), CancellationToken.None);

        Assert.False(multiKind.ComparedProjection);
        Assert.All(multiKind.Points, p => Assert.Null(p.ProjectionAmount));
        Assert.False(invalidSubtype.ComparedProjection);
        Assert.All(invalidSubtype.Points, p => Assert.Null(p.ProjectionAmount));
    }

    [Fact]
    public async Task QueryAsync_SecurityDividendProjection_AggregatesCategoryAndTypeRows_WhenIncludeCategory()
    {
        using var db = CreateDb();
        var user = await AddUserAsync(db, "projection-category");
        var category = new FinanceManager.Domain.Securities.SecurityCategory(user.Id, "Income");
        db.SecurityCategories.Add(category);
        await db.SaveChangesAsync();
        var security = AddSecurity(db, user.Id, "Categorized Security", category.Id);
        await db.SaveChangesAsync();
        var analysis = new DateTime(2026, 5, 1);
        AddDividendGroup(db, security, new DateTime(2025, 5, 10), 80m);
        await db.SaveChangesAsync();

        var sut = new ReportAggregationService(db, new NullLogger<ReportAggregationService>());
        var result = await sut.QueryAsync(new ReportAggregationQuery(
            user.Id,
            PostingKind.Security,
            ReportInterval.Month,
            1,
            IncludeCategory: true,
            ComparePrevious: false,
            CompareYear: false,
            CompareProjection: true,
            AnalysisDate: analysis), CancellationToken.None);

        var categoryPoint = result.Points.Single(p => p.GroupKey == $"Category:Security:{category.Id}" && p.PeriodStart == analysis);
        Assert.Equal(80m, categoryPoint.ProjectionAmount);
    }

    [Fact]
    public async Task QueryAsync_SecurityDividendProjection_HandlesYtdCutoffAndQuarterInterval()
    {
        using var db = CreateDb();
        var user = await AddUserAsync(db, "projection-intervals");
        var security = AddSecurity(db, user.Id, "Interval Security");
        await db.SaveChangesAsync();

        AddDividendGroup(db, security, new DateTime(2025, 1, 10), 10m);
        AddDividendGroup(db, security, new DateTime(2025, 5, 10), 20m);
        AddDividendGroup(db, security, new DateTime(2025, 6, 10), 40m);
        await db.SaveChangesAsync();

        var sut = new ReportAggregationService(db, new NullLogger<ReportAggregationService>());
        var ytd = await sut.QueryAsync(new ReportAggregationQuery(
            user.Id,
            PostingKind.Security,
            ReportInterval.Ytd,
            1,
            IncludeCategory: false,
            ComparePrevious: false,
            CompareYear: false,
            CompareProjection: true,
            AnalysisDate: new DateTime(2026, 5, 1)), CancellationToken.None);

        var quarter = await sut.QueryAsync(new ReportAggregationQuery(
            user.Id,
            PostingKind.Security,
            ReportInterval.Quarter,
            1,
            IncludeCategory: false,
            ComparePrevious: false,
            CompareYear: false,
            CompareProjection: true,
            AnalysisDate: new DateTime(2026, 5, 1)), CancellationToken.None);

        Assert.Equal(30m, ytd.Points.Single(p => p.GroupKey == $"Security:{security.Id}" && p.PeriodStart == new DateTime(2026, 1, 1)).ProjectionAmount);
        Assert.Equal(20m, quarter.Points.Single(p => p.GroupKey == $"Security:{security.Id}" && p.PeriodStart == new DateTime(2026, 4, 1)).ProjectionAmount);
    }
}
