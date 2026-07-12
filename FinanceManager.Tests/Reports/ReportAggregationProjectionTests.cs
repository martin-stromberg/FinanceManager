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
        var expectedDividend = Assert.Single(missing.ProjectionExpectedDividends!);
        Assert.Equal(missingCurrent.Id, expectedDividend.SecurityId);
        Assert.Equal("Missing Current", expectedDividend.SecurityName);
        Assert.Equal(new DateTime(2026, 5, 10), expectedDividend.ExpectedDate);
        Assert.Equal(new DateTime(2025, 5, 10), expectedDividend.PriorYearDate);
        Assert.Equal(70m, expectedDividend.Amount);
        Assert.Equal(50m, confirmed.Amount);
        Assert.Equal(50m, confirmed.ProjectionAmount);
        Assert.Null(confirmed.ProjectionExpectedDividends);
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
        Assert.Equal(45m, point.ProjectionAmount);
        Assert.Null(point.ProjectionExpectedDividends);
    }

    [Fact]
    public async Task QueryAsync_SecurityDividendProjection_TreatsEarlierCurrentYearDividendAsConfirmed()
    {
        using var db = CreateDb();
        var user = await AddUserAsync(db, "projection-shifted");
        var security = AddSecurity(db, user.Id, "Shifted Annual Dividend");
        await db.SaveChangesAsync();

        var analysis = new DateTime(2026, 5, 1);
        AddDividendGroup(db, security, new DateTime(2025, 5, 12), 86m, tax: -22.68m);
        AddDividendGroup(db, security, new DateTime(2026, 4, 21), 70m);
        await db.SaveChangesAsync();

        var sut = new ReportAggregationService(db, new NullLogger<ReportAggregationService>());
        var result = await sut.QueryAsync(new ReportAggregationQuery(
            user.Id,
            PostingKind.Security,
            ReportInterval.Month,
            2,
            IncludeCategory: false,
            ComparePrevious: false,
            CompareYear: false,
            CompareProjection: true,
            AnalysisDate: analysis), CancellationToken.None);

        var mayPoint = result.Points.Single(p => p.GroupKey == $"Security:{security.Id}" && p.PeriodStart == analysis);
        Assert.Equal(0m, mayPoint.Amount);
        Assert.Equal(0m, mayPoint.ProjectionAmount);
        Assert.Null(mayPoint.ProjectionExpectedDividends);
    }

    [Fact]
    public async Task QueryAsync_SecurityDividendProjection_MonthlyPatternDoesNotExpectMissedPastMonth()
    {
        using var db = CreateDb();
        var user = await AddUserAsync(db, "projection-monthly");
        var security = AddSecurity(db, user.Id, "Monthly Dividend");
        await db.SaveChangesAsync();

        for (var month = 1; month <= 7; month++)
        {
            AddDividendGroup(db, security, new DateTime(2025, month, 10), 10m);
        }

        foreach (var month in new[] { 1, 2, 3, 5, 6 })
        {
            AddDividendGroup(db, security, new DateTime(2026, month, 10), 10m);
        }

        await db.SaveChangesAsync();

        var sut = new ReportAggregationService(db, new NullLogger<ReportAggregationService>());
        var result = await sut.QueryAsync(new ReportAggregationQuery(
            user.Id,
            PostingKind.Security,
            ReportInterval.Month,
            7,
            IncludeCategory: false,
            ComparePrevious: false,
            CompareYear: false,
            CompareProjection: true,
            AnalysisDate: new DateTime(2026, 7, 1)), CancellationToken.None);

        var aprilPoint = result.Points.SingleOrDefault(p => p.GroupKey == $"Security:{security.Id}" && p.PeriodStart == new DateTime(2026, 4, 1));
        var julyPoint = result.Points.Single(p => p.GroupKey == $"Security:{security.Id}" && p.PeriodStart == new DateTime(2026, 7, 1));
        if (aprilPoint is not null)
        {
            Assert.Equal(0m, aprilPoint.Amount);
            Assert.Equal(0m, aprilPoint.ProjectionAmount);
            Assert.Null(aprilPoint.ProjectionExpectedDividends);
        }
        Assert.Equal(0m, julyPoint.Amount);
        Assert.Equal(10m, julyPoint.ProjectionAmount);
        Assert.Equal(new DateTime(2026, 7, 10), Assert.Single(julyPoint.ProjectionExpectedDividends!).ExpectedDate);
    }

    [Fact]
    public async Task QueryAsync_SecurityDividendProjection_MonthlyPatternIgnoresCorrectionPairsForFutureExpectations()
    {
        using var db = CreateDb();
        var user = await AddUserAsync(db, "projection-monthly-corrections");
        var security = AddSecurity(db, user.Id, "Monthly Corrections Dividend");
        await db.SaveChangesAsync();

        for (var month = 1; month <= 12; month++)
        {
            AddDividendGroup(db, security, new DateTime(2025, month, 10), 10m);
        }

        for (var month = 1; month <= 6; month++)
        {
            AddDividendGroup(db, security, new DateTime(2026, month, 10), 10m);
        }

        AddDividendGroup(db, security, new DateTime(2026, 4, 20), -10m);
        AddDividendGroup(db, security, new DateTime(2026, 4, 20), 10m);
        AddDividendGroup(db, security, new DateTime(2026, 4, 25), -10m);
        AddDividendGroup(db, security, new DateTime(2026, 4, 25), 10m);
        await db.SaveChangesAsync();

        var sut = new ReportAggregationService(db, new NullLogger<ReportAggregationService>());
        var result = await sut.QueryAsync(new ReportAggregationQuery(
            user.Id,
            PostingKind.Security,
            ReportInterval.Year,
            1,
            IncludeCategory: false,
            ComparePrevious: false,
            CompareYear: false,
            CompareProjection: true,
            AnalysisDate: new DateTime(2026, 6, 1)), CancellationToken.None);

        var yearPoint = result.Points.Single(p => p.GroupKey == $"Security:{security.Id}" && p.PeriodStart == new DateTime(2026, 1, 1));
        Assert.Equal(60m, yearPoint.Amount);
        Assert.Equal(120m, yearPoint.ProjectionAmount);
        var expectedDates = yearPoint.ProjectionExpectedDividends!.Select(p => p.ExpectedDate).ToArray();
        Assert.Equal(
            new[]
            {
                new DateTime(2026, 7, 10),
                new DateTime(2026, 8, 10),
                new DateTime(2026, 9, 10),
                new DateTime(2026, 10, 10),
                new DateTime(2026, 11, 10),
                new DateTime(2026, 12, 10)
            },
            expectedDates);
    }

    [Fact]
    public async Task QueryAsync_SecurityDividendProjection_MonthlyPatternIgnoresManyPriorYearCorrectionRows()
    {
        using var db = CreateDb();
        var user = await AddUserAsync(db, "projection-gladstone-corrections");
        var security = AddSecurity(db, user.Id, "Gladstone Commercial Corp");
        await db.SaveChangesAsync();

        AddDividendGroup(db, security, new DateTime(2026, 5, 4), 9.41m);
        AddDividendGroup(db, security, new DateTime(2026, 4, 1), 9.49m);
        AddDividendGroup(db, security, new DateTime(2026, 3, 2), 9.41m);
        AddDividendGroup(db, security, new DateTime(2026, 2, 3), 9.28m);
        AddDividendGroup(db, security, new DateTime(2026, 1, 2), 9.39m, tax: -1.16m);

        AddDividendGroup(db, security, new DateTime(2025, 11, 27), 9.5m, tax: -1.18m);
        AddDividendGroup(db, security, new DateTime(2025, 11, 3), 9.56m, tax: -1.18m);
        AddDividendGroup(db, security, new DateTime(2025, 10, 2), 9.37m, tax: -1.17m);
        AddDividendGroup(db, security, new DateTime(2025, 9, 1), 9.39m, tax: -1.16m);
        AddDividendGroup(db, security, new DateTime(2025, 8, 1), 9.65m, tax: -1.2m);
        AddDividendGroup(db, security, new DateTime(2025, 7, 1), 9.32m, tax: -1.16m);
        AddDividendGroup(db, security, new DateTime(2025, 6, 2), 9.64m, tax: -2.99m);
        AddDividendGroup(db, security, new DateTime(2025, 5, 2), 9.71m, tax: -1.21m);
        AddDividendGroup(db, security, new DateTime(2025, 4, 29), 4.3m);
        AddDividendGroup(db, security, new DateTime(2025, 4, 29), -5.07m);
        AddDividendGroup(db, security, new DateTime(2025, 4, 11), 12.02m);
        AddDividendGroup(db, security, new DateTime(2025, 4, 11), -10.14m);
        AddDividendGroup(db, security, new DateTime(2025, 4, 11), 5.07m);
        AddDividendGroup(db, security, new DateTime(2025, 4, 11), -10.21m);
        AddDividendGroup(db, security, new DateTime(2025, 4, 11), -10.65m);
        AddDividendGroup(db, security, new DateTime(2025, 4, 11), 12.53m);
        AddDividendGroup(db, security, new DateTime(2025, 4, 11), 11.68m);
        AddDividendGroup(db, security, new DateTime(2025, 4, 11), -9.93m);
        AddDividendGroup(db, security, new DateTime(2025, 4, 11), 12.31m);
        AddDividendGroup(db, security, new DateTime(2025, 4, 11), -4.3m);
        AddDividendGroup(db, security, new DateTime(2025, 4, 11), -10.46m);
        AddDividendGroup(db, security, new DateTime(2025, 4, 11), -9.97m);
        AddDividendGroup(db, security, new DateTime(2025, 4, 11), 11.73m);
        AddDividendGroup(db, security, new DateTime(2025, 4, 11), 11.92m);
        AddDividendGroup(db, security, new DateTime(2025, 4, 10), 5.12m);
        AddDividendGroup(db, security, new DateTime(2025, 4, 10), -4.33m);
        AddDividendGroup(db, security, new DateTime(2025, 4, 10), -4.35m);
        AddDividendGroup(db, security, new DateTime(2025, 4, 10), -4.33m);
        AddDividendGroup(db, security, new DateTime(2025, 4, 10), 5.1m);
        AddDividendGroup(db, security, new DateTime(2025, 4, 10), 5.1m);
        AddDividendGroup(db, security, new DateTime(2025, 4, 10), -4.29m);
        AddDividendGroup(db, security, new DateTime(2025, 4, 10), 5.05m);
        AddDividendGroup(db, security, new DateTime(2025, 4, 1), 10.22m);
        AddDividendGroup(db, security, new DateTime(2025, 3, 3), 10.52m);
        AddDividendGroup(db, security, new DateTime(2025, 2, 4), 10.75m);
        AddDividendGroup(db, security, new DateTime(2025, 1, 2), 10.65m, tax: -1.31m);
        await db.SaveChangesAsync();

        var sut = new ReportAggregationService(db, new NullLogger<ReportAggregationService>());
        var result = await sut.QueryAsync(new ReportAggregationQuery(
            user.Id,
            PostingKind.Security,
            ReportInterval.Year,
            1,
            IncludeCategory: false,
            ComparePrevious: false,
            CompareYear: false,
            CompareProjection: true,
            AnalysisDate: new DateTime(2026, 7, 12)), CancellationToken.None);

        var yearPoint = result.Points.Single(p => p.GroupKey == $"Security:{security.Id}" && p.PeriodStart == new DateTime(2026, 1, 1));
        Assert.Equal(45.82m, yearPoint.Amount);
        Assert.Equal(95.56m, yearPoint.ProjectionAmount);
        var expected = yearPoint.ProjectionExpectedDividends!.Select(p => (p.ExpectedDate, p.Amount)).ToArray();
        Assert.Equal(
            new[]
            {
                (new DateTime(2026, 7, 1), 8.16m),
                (new DateTime(2026, 8, 1), 8.45m),
                (new DateTime(2026, 9, 1), 8.23m),
                (new DateTime(2026, 10, 2), 8.2m),
                (new DateTime(2026, 11, 3), 8.38m),
                (new DateTime(2026, 11, 27), 8.32m)
            },
            expected);
    }

    [Fact]
    public async Task QueryAsync_SecurityDividendProjection_QuarterlyPatternMatchesWithinQuarterAndDropsElapsedQuarter()
    {
        using var db = CreateDb();
        var user = await AddUserAsync(db, "projection-quarterly");
        var security = AddSecurity(db, user.Id, "Quarterly Dividend");
        await db.SaveChangesAsync();

        AddDividendGroup(db, security, new DateTime(2025, 3, 10), 30m);
        AddDividendGroup(db, security, new DateTime(2025, 6, 12), 40m);
        AddDividendGroup(db, security, new DateTime(2026, 3, 25), 35m);
        await db.SaveChangesAsync();

        var sut = new ReportAggregationService(db, new NullLogger<ReportAggregationService>());
        var result = await sut.QueryAsync(new ReportAggregationQuery(
            user.Id,
            PostingKind.Security,
            ReportInterval.Month,
            7,
            IncludeCategory: false,
            ComparePrevious: false,
            CompareYear: false,
            CompareProjection: true,
            AnalysisDate: new DateTime(2026, 7, 1)), CancellationToken.None);

        var marchPoint = result.Points.Single(p => p.GroupKey == $"Security:{security.Id}" && p.PeriodStart == new DateTime(2026, 3, 1));
        var junePoint = result.Points.SingleOrDefault(p => p.GroupKey == $"Security:{security.Id}" && p.PeriodStart == new DateTime(2026, 6, 1));
        Assert.Equal(35m, marchPoint.Amount);
        Assert.Equal(35m, marchPoint.ProjectionAmount);
        Assert.Null(marchPoint.ProjectionExpectedDividends);
        if (junePoint is not null)
        {
            Assert.Equal(0m, junePoint.Amount);
            Assert.Equal(0m, junePoint.ProjectionAmount);
            Assert.Null(junePoint.ProjectionExpectedDividends);
        }
    }

    [Fact]
    public async Task QueryAsync_SecurityDividendProjection_QuarterlyPatternExpectsCurrentOpenQuarter()
    {
        using var db = CreateDb();
        var user = await AddUserAsync(db, "projection-quarter-open");
        var security = AddSecurity(db, user.Id, "Open Quarter Dividend");
        await db.SaveChangesAsync();

        AddDividendGroup(db, security, new DateTime(2025, 3, 10), 30m);
        AddDividendGroup(db, security, new DateTime(2025, 6, 12), 40m);
        await db.SaveChangesAsync();

        var sut = new ReportAggregationService(db, new NullLogger<ReportAggregationService>());
        var result = await sut.QueryAsync(new ReportAggregationQuery(
            user.Id,
            PostingKind.Security,
            ReportInterval.Month,
            2,
            IncludeCategory: false,
            ComparePrevious: false,
            CompareYear: false,
            CompareProjection: true,
            AnalysisDate: new DateTime(2026, 6, 1)), CancellationToken.None);

        var junePoint = result.Points.Single(p => p.GroupKey == $"Security:{security.Id}" && p.PeriodStart == new DateTime(2026, 6, 1));
        Assert.Equal(0m, junePoint.Amount);
        Assert.Equal(40m, junePoint.ProjectionAmount);
        Assert.Equal(new DateTime(2026, 6, 12), Assert.Single(junePoint.ProjectionExpectedDividends!).ExpectedDate);
    }

    [Fact]
    public async Task QueryAsync_SecurityDividendProjection_IrregularPatternDoesNotExpectMoreWhenCurrentYearHasPayment()
    {
        using var db = CreateDb();
        var user = await AddUserAsync(db, "projection-irregular-paid");
        var security = AddSecurity(db, user.Id, "Irregular Paid Dividend");
        await db.SaveChangesAsync();

        AddDividendGroup(db, security, new DateTime(2025, 1, 10), 10m);
        AddDividendGroup(db, security, new DateTime(2025, 2, 20), 20m);
        AddDividendGroup(db, security, new DateTime(2025, 6, 15), 30m);
        AddDividendGroup(db, security, new DateTime(2026, 4, 30), 25m);
        await db.SaveChangesAsync();

        var sut = new ReportAggregationService(db, new NullLogger<ReportAggregationService>());
        var result = await sut.QueryAsync(new ReportAggregationQuery(
            user.Id,
            PostingKind.Security,
            ReportInterval.Month,
            6,
            IncludeCategory: false,
            ComparePrevious: false,
            CompareYear: false,
            CompareProjection: true,
            AnalysisDate: new DateTime(2026, 6, 1)), CancellationToken.None);

        var junePoint = result.Points.Single(p => p.GroupKey == $"Security:{security.Id}" && p.PeriodStart == new DateTime(2026, 6, 1));
        Assert.Equal(0m, junePoint.Amount);
        Assert.Equal(0m, junePoint.ProjectionAmount);
        Assert.Null(junePoint.ProjectionExpectedDividends);
    }

    [Fact]
    public async Task QueryAsync_SecurityDividendProjection_IrregularPatternCautiouslyExpectsWhenCurrentYearHasNoPayment()
    {
        using var db = CreateDb();
        var user = await AddUserAsync(db, "projection-irregular-open");
        var security = AddSecurity(db, user.Id, "Irregular Open Dividend");
        await db.SaveChangesAsync();

        AddDividendGroup(db, security, new DateTime(2025, 1, 10), 10m);
        AddDividendGroup(db, security, new DateTime(2025, 2, 20), 20m);
        AddDividendGroup(db, security, new DateTime(2025, 6, 15), 30m);
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
            AnalysisDate: new DateTime(2026, 6, 1)), CancellationToken.None);

        var junePoint = result.Points.Single(p => p.GroupKey == $"Security:{security.Id}" && p.PeriodStart == new DateTime(2026, 6, 1));
        Assert.Equal(0m, junePoint.Amount);
        Assert.Equal(30m, junePoint.ProjectionAmount);
        Assert.Equal(new DateTime(2026, 6, 15), Assert.Single(junePoint.ProjectionExpectedDividends!).ExpectedDate);
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
        Assert.Equal(80m, Assert.Single(categoryPoint.ProjectionExpectedDividends!).Amount);
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
