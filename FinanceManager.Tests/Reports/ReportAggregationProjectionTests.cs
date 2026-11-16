using FinanceManager.Domain.Postings;
using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Aggregates;
using FinanceManager.Infrastructure.Reports;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace FinanceManager.Tests.Reports;

/// <summary>
/// Covers <see cref="ReportAggregationService"/>'s dividend projection feature (<c>CompareProjection = true</c>)
/// for Security postings: recognizing recurring dividend patterns (monthly, quarterly, irregular) from
/// prior-year payment history, projecting an expected amount and date for periods with no booked payment yet,
/// treating an already-booked current-period dividend as "confirmed" rather than projected, filtering out
/// same-date correction/reversal pairs from the pattern, and respecting the current holding (buy/sell/reversal
/// quantities, valuta-vs-booking date) so a fully-sold position stops generating projections.
/// </summary>
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

    private static Posting AddTrade(
        AppDbContext db,
        FinanceManager.Domain.Securities.Security security,
        DateTime bookingDate,
        SecurityPostingSubType subType,
        decimal? quantity,
        DateTime? valutaDate = null)
    {
        var effectiveValutaDate = valutaDate ?? bookingDate;
        var posting = new Posting(
            Guid.NewGuid(),
            PostingKind.Security,
            null,
            null,
            null,
            security.Id,
            bookingDate,
            effectiveValutaDate,
            0m,
            subType.ToString(),
            null,
            null,
            subType,
            quantity);
        db.Postings.Add(posting);
        return posting;
    }

    /// <summary>
    /// For a security with no dividend booked yet in the analysis period but one in the prior year, the
    /// projection must add the prior year's net dividend (after fee/tax) as the expected amount for the current
    /// period, exposing it via <c>ProjectionExpectedItems</c> with the security id/name and both the
    /// expected and prior-year dates - while a security whose current-period dividend is already booked reports
    /// <c>ProjectionAmount</c> equal to its actual (confirmed) amount with no expected-dividend entries.
    /// </summary>
    [Fact]
    public async Task QueryAsync_SecurityDividendProjection_AddsUnconfirmedPriorYearNetDividend()
    {
        using var db = CreateDb();
        var user = await AddUserAsync(db, "projection-owner");
        var missingCurrent = AddSecurity(db, user.Id, "Missing Current");
        var confirmedCurrent = AddSecurity(db, user.Id, "Confirmed Current");
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var analysis = new DateTime(2026, 5, 1);
        AddTrade(db, missingCurrent, new DateTime(2024, 1, 1), SecurityPostingSubType.Buy, 10m);
        AddDividendGroup(db, missingCurrent, new DateTime(2025, 5, 10), 100m, -5m, -25m);
        AddDividendGroup(db, confirmedCurrent, new DateTime(2025, 5, 12), 40m);
        AddDividendGroup(db, confirmedCurrent, new DateTime(2026, 5, 20), 50m);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

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
        var expectedDividend = Assert.Single(missing.ProjectionExpectedItems!);
        Assert.Equal(missingCurrent.Id, expectedDividend.EntityId);
        Assert.Equal("Missing Current", expectedDividend.EntityName);
        Assert.Equal(new DateTime(2026, 5, 10), expectedDividend.ExpectedDate);
        Assert.Equal(new DateTime(2025, 5, 10), expectedDividend.PriorYearDate);
        Assert.Equal(70m, expectedDividend.Amount);
        Assert.Equal(50m, confirmed.Amount);
        Assert.Equal(50m, confirmed.ProjectionAmount);
        Assert.Null(confirmed.ProjectionExpectedItems);
    }

    /// <summary>
    /// When a security paid dividends twice in the prior year on distinct dates, matching against the current
    /// year's already-booked payment must resolve to the correct individual prior-year event rather than summing
    /// or conflating both prior-year payments; here the current dividend is already booked, so the result reports
    /// it as confirmed (actual = projection) with no expected-dividend entries.
    /// </summary>
    [Fact]
    public async Task QueryAsync_SecurityDividendProjection_MatchesPriorYearEventsIndividually()
    {
        using var db = CreateDb();
        var user = await AddUserAsync(db, "projection-events");
        var security = AddSecurity(db, user.Id, "Quarterly Dividend");
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var analysis = new DateTime(2026, 5, 1);
        AddDividendGroup(db, security, new DateTime(2025, 5, 10), 40m);
        AddDividendGroup(db, security, new DateTime(2025, 5, 25), 60m);
        AddDividendGroup(db, security, new DateTime(2026, 5, 10), 45m);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

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
        Assert.Null(point.ProjectionExpectedItems);
    }

    /// <summary>
    /// When the current year's dividend was already paid earlier than the analysis month (rather than exactly in
    /// the analysis period), that already-booked payment must be recognized as satisfying the year's expectation
    /// - the analysis-month point must show zero actual and zero projection rather than still expecting another
    /// payment for a month that was never going to pay one.
    /// </summary>
    [Fact]
    public async Task QueryAsync_SecurityDividendProjection_TreatsEarlierCurrentYearDividendAsConfirmed()
    {
        using var db = CreateDb();
        var user = await AddUserAsync(db, "projection-shifted");
        var security = AddSecurity(db, user.Id, "Shifted Annual Dividend");
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var analysis = new DateTime(2026, 5, 1);
        AddDividendGroup(db, security, new DateTime(2025, 5, 12), 86m, tax: -22.68m);
        AddDividendGroup(db, security, new DateTime(2026, 4, 21), 70m);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

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
        Assert.Null(mayPoint.ProjectionExpectedItems);
    }

    /// <summary>
    /// For a security with an established monthly dividend pattern, a month in the recent past that unexpectedly
    /// received no payment (April) must not retroactively get a projected/expected dividend injected - only the
    /// still-open current month (July) should carry a projection, derived from the pattern's expected
    /// day-of-month.
    /// </summary>
    [Fact]
    public async Task QueryAsync_SecurityDividendProjection_MonthlyPatternDoesNotExpectMissedPastMonth()
    {
        using var db = CreateDb();
        var user = await AddUserAsync(db, "projection-monthly");
        var security = AddSecurity(db, user.Id, "Monthly Dividend");
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        AddTrade(db, security, new DateTime(2024, 1, 1), SecurityPostingSubType.Buy, 10m);
        for (var month = 1; month <= 7; month++)
        {
            AddDividendGroup(db, security, new DateTime(2025, month, 10), 10m);
        }

        foreach (var month in new[] { 1, 2, 3, 5, 6 })
        {
            AddDividendGroup(db, security, new DateTime(2026, month, 10), 10m);
        }

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

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
            Assert.Null(aprilPoint.ProjectionExpectedItems);
        }
        Assert.Equal(0m, julyPoint.Amount);
        Assert.Equal(10m, julyPoint.ProjectionAmount);
        Assert.Equal(new DateTime(2026, 7, 10), Assert.Single(julyPoint.ProjectionExpectedItems!).ExpectedDate);
    }

    /// <summary>
    /// Same-date offsetting correction pairs (a negative dividend immediately followed by an equal positive one,
    /// or vice versa) appearing in the payment history must be excluded when deriving the monthly pattern's
    /// future expected dates - they must not distort the projected schedule for the remaining months of the year.
    /// </summary>
    [Fact]
    public async Task QueryAsync_SecurityDividendProjection_MonthlyPatternIgnoresCorrectionPairsForFutureExpectations()
    {
        using var db = CreateDb();
        var user = await AddUserAsync(db, "projection-monthly-corrections");
        var security = AddSecurity(db, user.Id, "Monthly Corrections Dividend");
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        AddTrade(db, security, new DateTime(2024, 1, 1), SecurityPostingSubType.Buy, 10m);
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
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

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
        var expectedDates = yearPoint.ProjectionExpectedItems!.Select(p => p.ExpectedDate).ToArray();
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

    /// <summary>
    /// Regression test using a real-world-shaped history full of many same-date correction/reversal row pairs (a
    /// "Gladstone Commercial Corp"-style dataset): the monthly pattern detection and the resulting future
    /// expected-dividend amounts and dates must remain correct despite this noise, verifying the
    /// correction-filtering logic scales to messy real data rather than only the small hand-crafted cases.
    /// </summary>
    [Fact]
    public async Task QueryAsync_SecurityDividendProjection_MonthlyPatternIgnoresManyPriorYearCorrectionRows()
    {
        using var db = CreateDb();
        var user = await AddUserAsync(db, "projection-gladstone-corrections");
        var security = AddSecurity(db, user.Id, "Gladstone Commercial Corp");
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        AddTrade(db, security, new DateTime(2024, 1, 1), SecurityPostingSubType.Buy, 10m);
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
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

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
        var expected = yearPoint.ProjectionExpectedItems!.Select(p => (p.ExpectedDate, p.Amount)).ToArray();
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

    /// <summary>
    /// For a security paying quarterly, a payment that already occurred within the current quarter must be
    /// treated as satisfying that quarter's expectation (reported as confirmed with no projected dividends),
    /// while a quarter that has already fully elapsed without a corresponding prior-year event must not carry
    /// forward a stale expectation into the result.
    /// </summary>
    [Fact]
    public async Task QueryAsync_SecurityDividendProjection_QuarterlyPatternMatchesWithinQuarterAndDropsElapsedQuarter()
    {
        using var db = CreateDb();
        var user = await AddUserAsync(db, "projection-quarterly");
        var security = AddSecurity(db, user.Id, "Quarterly Dividend");
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        AddDividendGroup(db, security, new DateTime(2025, 3, 10), 30m);
        AddDividendGroup(db, security, new DateTime(2025, 6, 12), 40m);
        AddDividendGroup(db, security, new DateTime(2026, 3, 25), 35m);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

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
        Assert.Null(marchPoint.ProjectionExpectedItems);
        if (junePoint is not null)
        {
            Assert.Equal(0m, junePoint.Amount);
            Assert.Equal(0m, junePoint.ProjectionAmount);
            Assert.Null(junePoint.ProjectionExpectedItems);
        }
    }

    /// <summary>
    /// For a still-open (not yet elapsed) quarter that has no payment booked yet, the quarterly pattern must
    /// project the expected amount and date based on the matching prior-year quarterly payment.
    /// </summary>
    [Fact]
    public async Task QueryAsync_SecurityDividendProjection_QuarterlyPatternExpectsCurrentOpenQuarter()
    {
        using var db = CreateDb();
        var user = await AddUserAsync(db, "projection-quarter-open");
        var security = AddSecurity(db, user.Id, "Open Quarter Dividend");
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        AddTrade(db, security, new DateTime(2024, 1, 1), SecurityPostingSubType.Buy, 10m);
        AddDividendGroup(db, security, new DateTime(2025, 3, 10), 30m);
        AddDividendGroup(db, security, new DateTime(2025, 6, 12), 40m);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

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
        Assert.Equal(new DateTime(2026, 6, 12), Assert.Single(junePoint.ProjectionExpectedItems!).ExpectedDate);
    }

    /// <summary>
    /// For a security with an irregular (non-monthly, non-quarterly) payment history, once the current year has
    /// already received at least one dividend payment, the projection must not add a further speculative
    /// expectation for the analysis period - irregular payers are only cautiously projected when nothing has been
    /// paid yet this year.
    /// </summary>
    [Fact]
    public async Task QueryAsync_SecurityDividendProjection_IrregularPatternDoesNotExpectMoreWhenCurrentYearHasPayment()
    {
        using var db = CreateDb();
        var user = await AddUserAsync(db, "projection-irregular-paid");
        var security = AddSecurity(db, user.Id, "Irregular Paid Dividend");
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        AddDividendGroup(db, security, new DateTime(2025, 1, 10), 10m);
        AddDividendGroup(db, security, new DateTime(2025, 2, 20), 20m);
        AddDividendGroup(db, security, new DateTime(2025, 6, 15), 30m);
        AddDividendGroup(db, security, new DateTime(2026, 4, 30), 25m);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

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
        Assert.Null(junePoint.ProjectionExpectedItems);
    }

    /// <summary>
    /// Conversely, for an irregular payer that has received nothing at all in the current year, the projection
    /// cautiously expects a dividend based on the most recent prior-year irregular payment's amount and date.
    /// </summary>
    [Fact]
    public async Task QueryAsync_SecurityDividendProjection_IrregularPatternCautiouslyExpectsWhenCurrentYearHasNoPayment()
    {
        using var db = CreateDb();
        var user = await AddUserAsync(db, "projection-irregular-open");
        var security = AddSecurity(db, user.Id, "Irregular Open Dividend");
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        AddTrade(db, security, new DateTime(2024, 1, 1), SecurityPostingSubType.Buy, 10m);
        AddDividendGroup(db, security, new DateTime(2025, 1, 10), 10m);
        AddDividendGroup(db, security, new DateTime(2025, 2, 20), 20m);
        AddDividendGroup(db, security, new DateTime(2025, 6, 15), 30m);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

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
        Assert.Equal(new DateTime(2026, 6, 15), Assert.Single(junePoint.ProjectionExpectedItems!).ExpectedDate);
    }

    /// <summary>
    /// With <c>UseValutaDate = true</c>, the projection must key its prior-year-vs-current-period matching off
    /// each dividend's valuta date, falling back to the booking date only for postings that never had a distinct
    /// valuta date recorded - confirmed here with two securities, one exercising the valuta-date path and one the
    /// booking-date fallback.
    /// </summary>
    [Fact]
    public async Task QueryAsync_SecurityDividendProjection_UsesValutaDateAndBookingFallback()
    {
        using var db = CreateDb();
        var user = await AddUserAsync(db, "projection-valuta");
        var valutaSecurity = AddSecurity(db, user.Id, "Valuta Security");
        var bookingSecurity = AddSecurity(db, user.Id, "Booking Fallback Security");
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var analysis = new DateTime(2026, 5, 1);
        AddTrade(db, valutaSecurity, new DateTime(2024, 1, 1), SecurityPostingSubType.Buy, 10m);
        AddTrade(db, bookingSecurity, new DateTime(2024, 1, 1), SecurityPostingSubType.Buy, 10m);
        AddDividendGroup(db, valutaSecurity, new DateTime(2025, 4, 30), 30m, valutaDate: new DateTime(2025, 5, 2));
        AddDividendGroup(db, bookingSecurity, new DateTime(2025, 5, 4), 70m);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

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

    /// <summary>
    /// A bank report with <c>CompareProjection = true</c> projects a recurring annual posting that is
    /// still missing in the current period: <c>ComparedProjection = true</c>, the account row carries
    /// the expected amount plus an expected item with <c>Kind = PostingKind.Bank</c> and the account
    /// id/name.
    /// </summary>
    [Fact]
    public async Task QueryAsync_BankProjection_AddsUnconfirmedPriorYearPosting()
    {
        using var db = CreateDb();
        var user = await AddUserAsync(db, "projection-bank");
        var account = new FinanceManager.Domain.Accounts.Account(user.Id, AccountType.Giro, "Projection Account", null, Guid.NewGuid());
        db.Accounts.Add(account);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var analysis = new DateTime(2026, 5, 1);
        db.Postings.Add(new Posting(Guid.NewGuid(), PostingKind.Bank, account.Id, null, null, null, new DateTime(2025, 5, 10), 120m, "Annual Contribution", null, null, null));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        await new PostingAggregateService(db).RebuildForUserAsync(user.Id, (_, _) => { }, TestContext.Current.CancellationToken);

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
            AnalysisDate: analysis), CancellationToken.None);

        Assert.True(result.ComparedProjection);
        var point = result.Points.Single(p => p.GroupKey == $"Account:{account.Id}" && p.PeriodStart == analysis);
        Assert.Equal(0m, point.Amount);
        Assert.Equal(120m, point.ProjectionAmount);
        var expected = Assert.Single(point.ProjectionExpectedItems!);
        Assert.Equal(PostingKind.Bank, expected.Kind);
        Assert.Equal(account.Id, expected.EntityId);
        Assert.Equal("Projection Account", expected.EntityName);
        Assert.Equal(new DateTime(2026, 5, 10), expected.ExpectedDate);
        Assert.Equal(new DateTime(2025, 5, 10), expected.PriorYearDate);
        Assert.Equal(120m, expected.Amount);
    }

    /// <summary>
    /// A multi-kind query keeps the projection for the non-security kinds even when the security
    /// sub-type filter excludes dividends entirely: <c>ComparedProjection = true</c>, the account row
    /// carries its expectation while the security (buy-only) rows carry no projection.
    /// </summary>
    [Fact]
    public async Task QueryAsync_Projection_ContradictingSecuritySubTypes_SuppressesOnlySecurity()
    {
        using var db = CreateDb();
        var user = await AddUserAsync(db, "projection-invalid");
        var account = new FinanceManager.Domain.Accounts.Account(user.Id, AccountType.Giro, "Projection Account", null, Guid.NewGuid());
        db.Accounts.Add(account);
        var security = AddSecurity(db, user.Id, "Invalid Selection Security");
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var analysis = new DateTime(2026, 5, 1);
        db.Postings.Add(new Posting(Guid.NewGuid(), PostingKind.Bank, account.Id, null, null, null, new DateTime(2025, 5, 10), 120m, "Annual Contribution", null, null, null));
        AddDividendGroup(db, security, new DateTime(2025, 5, 10), 20m);
        db.Postings.Add(new Posting(Guid.NewGuid(), PostingKind.Security, null, null, null, security.Id, new DateTime(2026, 5, 15), new DateTime(2026, 5, 15), -50m, "Buy", null, null, SecurityPostingSubType.Buy, 5m));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        await new PostingAggregateService(db).RebuildForUserAsync(user.Id, (_, _) => { }, TestContext.Current.CancellationToken);

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
            PostingKinds: new[] { PostingKind.Security, PostingKind.Bank },
            AnalysisDate: analysis,
            Filters: new ReportAggregationFilters(SecuritySubTypes: new[] { (int)SecurityPostingSubType.Buy })), CancellationToken.None);

        Assert.True(result.ComparedProjection);
        var bankPoint = result.Points.Single(p => p.GroupKey == $"Account:{account.Id}" && p.PeriodStart == analysis);
        Assert.Equal(120m, bankPoint.ProjectionAmount);
        var expected = Assert.Single(bankPoint.ProjectionExpectedItems!);
        Assert.Equal(PostingKind.Bank, expected.Kind);
        Assert.Equal(account.Id, expected.EntityId);
        Assert.All(result.Points.Where(p => p.GroupKey.StartsWith("Security:", StringComparison.Ordinal)), p => Assert.Null(p.ProjectionAmount));
    }

    /// <summary>
    /// A pure security selection with a contradicting security sub-type filter cannot project at all
    /// because the only selected kind is suppressed: the query reports
    /// <c>ComparedProjection = false</c>.
    /// </summary>
    [Fact]
    public async Task QueryAsync_Projection_ContradictingSecuritySubTypes_NoProjection_ForSecurityOnly()
    {
        using var db = CreateDb();
        var user = await AddUserAsync(db, "projection-invalid-security-only");
        var security = AddSecurity(db, user.Id, "Invalid Selection Security");
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var analysis = new DateTime(2026, 5, 1);
        AddDividendGroup(db, security, new DateTime(2025, 5, 10), 20m);
        db.Postings.Add(new Posting(Guid.NewGuid(), PostingKind.Security, null, null, null, security.Id, new DateTime(2026, 5, 15), new DateTime(2026, 5, 15), -50m, "Buy", null, null, SecurityPostingSubType.Buy, 5m));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        await new PostingAggregateService(db).RebuildForUserAsync(user.Id, (_, _) => { }, TestContext.Current.CancellationToken);

        var sut = new ReportAggregationService(db, new NullLogger<ReportAggregationService>());
        var securityOnlyResult = await sut.QueryAsync(new ReportAggregationQuery(
            user.Id,
            PostingKind.Security,
            ReportInterval.Month,
            1,
            IncludeCategory: false,
            ComparePrevious: false,
            CompareYear: false,
            CompareProjection: true,
            AnalysisDate: analysis,
            Filters: new ReportAggregationFilters(SecuritySubTypes: new[] { (int)SecurityPostingSubType.Buy })), CancellationToken.None);

        Assert.False(securityOnlyResult.ComparedProjection);
    }

    /// <summary>
    /// When <c>IncludeCategory</c> is enabled, the projected expected-dividend amount for an individually held
    /// security must roll up correctly into its category-level aggregate row (<c>GroupKey = "Category:Security:{categoryId}"</c>),
    /// so category summaries reflect projected income the same way per-security rows do.
    /// </summary>
    [Fact]
    public async Task QueryAsync_SecurityDividendProjection_AggregatesCategoryAndTypeRows_WhenIncludeCategory()
    {
        using var db = CreateDb();
        var user = await AddUserAsync(db, "projection-category");
        var category = new FinanceManager.Domain.Securities.SecurityCategory(user.Id, "Income");
        db.SecurityCategories.Add(category);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var security = AddSecurity(db, user.Id, "Categorized Security", category.Id);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var analysis = new DateTime(2026, 5, 1);
        AddTrade(db, security, new DateTime(2024, 1, 1), SecurityPostingSubType.Buy, 10m);
        AddDividendGroup(db, security, new DateTime(2025, 5, 10), 80m);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

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
        Assert.Equal(80m, Assert.Single(categoryPoint.ProjectionExpectedItems!).Amount);
    }

    /// <summary>
    /// If the entire holding of a security was sold before the analysis date, the projection must not expect a
    /// future dividend for it even though its prior-year payment history would otherwise suggest one - a position
    /// no longer owned cannot receive a future dividend.
    /// </summary>
    [Fact]
    public async Task QueryAsync_SecurityDividendProjection_DoesNotExpectDividend_WhenHoldingIsFullySold()
    {
        using var db = CreateDb();
        var user = await AddUserAsync(db, "projection-sold");
        var security = AddSecurity(db, user.Id, "Sold Security");
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var analysis = new DateTime(2026, 5, 1);
        AddTrade(db, security, new DateTime(2024, 1, 1), SecurityPostingSubType.Buy, 10m);
        AddTrade(db, security, new DateTime(2026, 5, 10), SecurityPostingSubType.Sell, -10m);
        AddDividendGroup(db, security, new DateTime(2025, 5, 10), 100m);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

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
        Assert.Equal(0m, point.Amount);
        Assert.Equal(0m, point.ProjectionAmount);
        Assert.Null(point.ProjectionExpectedItems);
    }

    /// <summary>
    /// For a Year-interval report, the holding check must correctly load and consider trades dated later within
    /// the year (a sale that happens after the year starts but before the expected dividend date) rather than
    /// only trades known at the start of the year - here a mid-year full sale correctly suppresses the
    /// projection for the whole year.
    /// </summary>
    [Fact]
    public async Task QueryAsync_SecurityDividendProjection_YearIntervalLoadsTradesUntilExpectedDividendDate()
    {
        using var db = CreateDb();
        var user = await AddUserAsync(db, "projection-year-sold-after-start");
        var security = AddSecurity(db, user.Id, "Year Sold Security");
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        AddTrade(db, security, new DateTime(2024, 1, 1), SecurityPostingSubType.Buy, 10m);
        AddTrade(db, security, new DateTime(2026, 5, 10), SecurityPostingSubType.Sell, -10m);
        AddDividendGroup(db, security, new DateTime(2025, 5, 10), 100m);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

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
            AnalysisDate: new DateTime(2026, 5, 1)), CancellationToken.None);

        var point = result.Points.Single(p => p.GroupKey == $"Security:{security.Id}" && p.PeriodStart == new DateTime(2026, 1, 1));
        Assert.Equal(0m, point.Amount);
        Assert.Equal(0m, point.ProjectionAmount);
        Assert.Null(point.ProjectionExpectedItems);
    }

    /// <summary>
    /// A Sell posting with a positive quantity value (rather than the usual negative) must still be treated as
    /// reducing the holding - a disposition - as long as it is not flagged as a reversal of an earlier sell, so a
    /// fully-sold position correctly stops projecting dividends even when the sell's quantity sign looks unusual.
    /// </summary>
    [Fact]
    public async Task QueryAsync_SecurityDividendProjection_TreatsPositiveSellQuantityAsDisposition_WhenNotReversal()
    {
        using var db = CreateDb();
        var user = await AddUserAsync(db, "projection-positive-sell");
        var security = AddSecurity(db, user.Id, "Positive Sell Security");
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var analysis = new DateTime(2026, 5, 1);
        AddTrade(db, security, new DateTime(2024, 1, 1), SecurityPostingSubType.Buy, 10m);
        AddTrade(db, security, new DateTime(2026, 5, 1), SecurityPostingSubType.Sell, 10m);
        AddDividendGroup(db, security, new DateTime(2025, 5, 10), 100m);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

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
        Assert.Equal(0m, point.ProjectionAmount);
        Assert.Null(point.ProjectionExpectedItems);
    }

    /// <summary>
    /// When a Sell posting is later reversed (a second Sell posting explicitly marked via
    /// <c>SetReversalFor</c> as reversing the first), the net effect must restore the original holding, so the
    /// projection resumes expecting a dividend as if the sell/reversal pair had never happened.
    /// </summary>
    [Fact]
    public async Task QueryAsync_SecurityDividendProjection_ExpectsDividend_WhenSellWasReversed()
    {
        using var db = CreateDb();
        var user = await AddUserAsync(db, "projection-sell-reversal");
        var security = AddSecurity(db, user.Id, "Reversed Sell Security");
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var analysis = new DateTime(2026, 5, 1);
        AddTrade(db, security, new DateTime(2024, 1, 1), SecurityPostingSubType.Buy, 10m);
        var sell = AddTrade(db, security, new DateTime(2026, 5, 1), SecurityPostingSubType.Sell, -10m);
        var reversal = AddTrade(db, security, new DateTime(2026, 5, 2), SecurityPostingSubType.Sell, 10m);
        reversal.SetReversalFor(sell);
        AddDividendGroup(db, security, new DateTime(2025, 5, 10), 100m);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

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
        Assert.Equal(100m, point.ProjectionAmount);
        Assert.Equal(new DateTime(2026, 5, 10), Assert.Single(point.ProjectionExpectedItems!).ExpectedDate);
    }

    /// <summary>
    /// A partial sell that reduces but does not eliminate the holding (selling 4 of 10 units) must still leave
    /// the projection active, since some quantity of the security remains owned and could still receive a
    /// dividend.
    /// </summary>
    [Fact]
    public async Task QueryAsync_SecurityDividendProjection_ExpectsDividend_WhenHoldingRemainsAfterPartialSell()
    {
        using var db = CreateDb();
        var user = await AddUserAsync(db, "projection-partial-sell");
        var security = AddSecurity(db, user.Id, "Partially Sold Security");
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var analysis = new DateTime(2026, 5, 1);
        AddTrade(db, security, new DateTime(2024, 1, 1), SecurityPostingSubType.Buy, 10m);
        AddTrade(db, security, new DateTime(2026, 5, 1), SecurityPostingSubType.Sell, -4m);
        AddDividendGroup(db, security, new DateTime(2025, 5, 10), 100m);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

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
        Assert.Equal(100m, point.ProjectionAmount);
        Assert.Equal(new DateTime(2026, 5, 10), Assert.Single(point.ProjectionExpectedItems!).ExpectedDate);
    }

    /// <summary>
    /// If a security was fully sold partway through the year after several months of an established monthly
    /// dividend pattern, the year-to-date actual (already-booked) dividend total must be preserved in the
    /// result, while no further projected amount is added for the months after the sale - already-earned income
    /// is never erased just because the position was later closed.
    /// </summary>
    [Fact]
    public async Task QueryAsync_SecurityDividendProjection_KeepsBookedDividend_WhenFutureExpectationHasNoHolding()
    {
        using var db = CreateDb();
        var user = await AddUserAsync(db, "projection-booked-closed");
        var security = AddSecurity(db, user.Id, "Closed Monthly Security");
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        AddTrade(db, security, new DateTime(2024, 1, 1), SecurityPostingSubType.Buy, 10m);
        AddTrade(db, security, new DateTime(2026, 7, 10), SecurityPostingSubType.Sell, -10m);
        for (var month = 1; month <= 7; month++)
        {
            AddDividendGroup(db, security, new DateTime(2025, month, 10), 10m);
        }

        for (var month = 1; month <= 6; month++)
        {
            AddDividendGroup(db, security, new DateTime(2026, month, 10), 10m);
        }

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

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

        var point = result.Points.Single(p => p.GroupKey == $"Security:{security.Id}" && p.PeriodStart == new DateTime(2026, 1, 1));
        Assert.Equal(60m, point.Amount);
        Assert.Equal(60m, point.ProjectionAmount);
        Assert.Null(point.ProjectionExpectedItems);
    }

    /// <summary>
    /// Within a category aggregate combining a still-held security and a fully-sold one (both with qualifying
    /// prior-year dividend history), the category-level <c>ProjectionExpectedItems</c> list must include only
    /// the still-held security's expectation and exclude the sold one's, even though both belong to the same
    /// category and both individually paid dividends last year.
    /// </summary>
    [Fact]
    public async Task QueryAsync_SecurityDividendProjection_CategoryExcludesSoldSecurityExpectations()
    {
        using var db = CreateDb();
        var user = await AddUserAsync(db, "projection-category-holding");
        var category = new FinanceManager.Domain.Securities.SecurityCategory(user.Id, "Income");
        db.SecurityCategories.Add(category);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var heldSecurity = AddSecurity(db, user.Id, "Held Security", category.Id);
        var soldSecurity = AddSecurity(db, user.Id, "Sold Security", category.Id);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var analysis = new DateTime(2026, 5, 1);
        AddTrade(db, heldSecurity, new DateTime(2024, 1, 1), SecurityPostingSubType.Buy, 10m);
        AddTrade(db, soldSecurity, new DateTime(2024, 1, 1), SecurityPostingSubType.Buy, 10m);
        AddTrade(db, soldSecurity, new DateTime(2026, 5, 10), SecurityPostingSubType.Sell, -10m);
        AddDividendGroup(db, heldSecurity, new DateTime(2025, 5, 10), 80m);
        AddDividendGroup(db, soldSecurity, new DateTime(2025, 5, 10), 40m);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

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
        var expectedDividend = Assert.Single(categoryPoint.ProjectionExpectedItems!);
        Assert.Equal(80m, categoryPoint.ProjectionAmount);
        Assert.Equal(heldSecurity.Id, expectedDividend.EntityId);
        Assert.Equal(80m, expectedDividend.Amount);
    }

    /// <summary>
    /// Projections must be computed strictly per owner: another user's security trade must never be used to
    /// determine whether the querying user's own security counts as "held" (or otherwise influence its
    /// projection) - a straightforward multi-tenant isolation guard for the holding check.
    /// </summary>
    [Fact]
    public async Task QueryAsync_SecurityDividendProjection_IgnoresOtherUsersHoldings()
    {
        using var db = CreateDb();
        var owner = await AddUserAsync(db, "projection-owner-isolation");
        var otherUser = await AddUserAsync(db, "projection-other-isolation");
        var ownSecurity = AddSecurity(db, owner.Id, "Own Security");
        var otherSecurity = AddSecurity(db, otherUser.Id, "Other Security");
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var analysis = new DateTime(2026, 5, 1);
        AddTrade(db, otherSecurity, new DateTime(2024, 1, 1), SecurityPostingSubType.Buy, 10m);
        AddDividendGroup(db, ownSecurity, new DateTime(2025, 5, 10), 100m);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var sut = new ReportAggregationService(db, new NullLogger<ReportAggregationService>());
        var result = await sut.QueryAsync(new ReportAggregationQuery(
            owner.Id,
            PostingKind.Security,
            ReportInterval.Month,
            1,
            IncludeCategory: false,
            ComparePrevious: false,
            CompareYear: false,
            CompareProjection: true,
            AnalysisDate: analysis), CancellationToken.None);

        var point = result.Points.Single(p => p.GroupKey == $"Security:{ownSecurity.Id}" && p.PeriodStart == analysis);
        Assert.Equal(0m, point.ProjectionAmount);
        Assert.Null(point.ProjectionExpectedItems);
    }

    /// <summary>
    /// With <c>UseValutaDate = true</c>, the holding check that decides whether a projection should fire must
    /// also key off valuta dates - a sell whose booking date is before the expected dividend date but whose
    /// valuta date falls after it must still count as "held" as of the expected dividend date.
    /// </summary>
    [Fact]
    public async Task QueryAsync_SecurityDividendProjection_UsesValutaDateForHoldingCheck()
    {
        using var db = CreateDb();
        var user = await AddUserAsync(db, "projection-holding-valuta");
        var security = AddSecurity(db, user.Id, "Valuta Holding Security");
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var analysis = new DateTime(2026, 5, 1);
        AddTrade(db, security, new DateTime(2024, 1, 1), SecurityPostingSubType.Buy, 10m);
        AddTrade(db, security, new DateTime(2026, 5, 1), SecurityPostingSubType.Sell, -10m, new DateTime(2026, 5, 20));
        AddDividendGroup(db, security, new DateTime(2025, 5, 10), 100m);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

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

        var point = result.Points.Single(p => p.GroupKey == $"Security:{security.Id}" && p.PeriodStart == analysis);
        Assert.Equal(100m, point.ProjectionAmount);
        Assert.Equal(new DateTime(2026, 5, 10), Assert.Single(point.ProjectionExpectedItems!).ExpectedDate);
    }

    /// <summary>
    /// A Buy trade recorded without a quantity value must not be treated as establishing a holding for
    /// projection purposes, since the actual position size is unknown - the projection conservatively skips this
    /// security rather than guessing that it is held.
    /// </summary>
    [Fact]
    public async Task QueryAsync_SecurityDividendProjection_DoesNotUseTradeWithoutQuantityAsHolding()
    {
        using var db = CreateDb();
        var user = await AddUserAsync(db, "projection-no-quantity");
        var security = AddSecurity(db, user.Id, "No Quantity Security");
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var analysis = new DateTime(2026, 5, 1);
        AddTrade(db, security, new DateTime(2024, 1, 1), SecurityPostingSubType.Buy, null);
        AddDividendGroup(db, security, new DateTime(2025, 5, 10), 100m);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

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
        Assert.Equal(0m, point.ProjectionAmount);
        Assert.Null(point.ProjectionExpectedItems);
    }

    /// <summary>
    /// The projection logic must correctly restrict its prior-year comparison window for both a Ytd
    /// (year-to-date, cut off at the analysis date's month) and a Quarter interval, matching only the prior-year
    /// dividend that falls within the equivalent cutoff/period rather than the security's full prior-year total.
    /// </summary>
    [Fact]
    public async Task QueryAsync_SecurityDividendProjection_HandlesYtdCutoffAndQuarterInterval()
    {
        using var db = CreateDb();
        var user = await AddUserAsync(db, "projection-intervals");
        var security = AddSecurity(db, user.Id, "Interval Security");
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        AddTrade(db, security, new DateTime(2024, 1, 1), SecurityPostingSubType.Buy, 10m);
        AddDividendGroup(db, security, new DateTime(2025, 1, 10), 10m);
        AddDividendGroup(db, security, new DateTime(2025, 5, 10), 20m);
        AddDividendGroup(db, security, new DateTime(2025, 6, 10), 40m);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

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
