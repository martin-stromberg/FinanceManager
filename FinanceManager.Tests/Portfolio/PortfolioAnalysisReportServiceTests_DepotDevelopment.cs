using FinanceManager.Application.Securities.ReturnAnalysis;
using FinanceManager.Domain.Postings;
using FinanceManager.Domain.Securities;
using FinanceManager.Domain.Users;
using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Portfolio;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace FinanceManager.Tests.Portfolio;

/// <summary>
/// Tests for the <c>DepotDevelopment</c> section built by <see cref="PortfolioAnalysisReportService"/>:
/// the year-over-year series of portfolio market values, net dividends and cumulative invested capital.
/// </summary>
public sealed class PortfolioAnalysisReportServiceTests_DepotDevelopment : IDisposable
{
    private readonly AppDbContext _db;
    private readonly PortfolioAnalysisReportService _sut;

    /// <summary>
    /// Sets up a fresh in-memory <see cref="AppDbContext"/> and wires the service under test with
    /// real <see cref="FifoCostBasisCalculator"/> and <see cref="ReturnCalculationService"/> implementations.
    /// </summary>
    public PortfolioAnalysisReportServiceTests_DepotDevelopment()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);

        var fifo = new FifoCostBasisCalculator(NullLogger<FifoCostBasisCalculator>.Instance);
        var calc = new ReturnCalculationService(NullLogger<ReturnCalculationService>.Instance);
        _sut = new PortfolioAnalysisReportService(_db, fifo, calc);
    }

    /// <summary>Releases the in-memory <see cref="AppDbContext"/> used by each test.</summary>
    public void Dispose() => _db.Dispose();

    private static int CurrentYear => DateTime.UtcNow.Year;

    private User CreateUser()
    {
        var user = new User($"user-{Guid.NewGuid():N}", "hash");
        _db.Users.Add(user);
        return user;
    }

    private Security CreateSecurity(Guid ownerUserId, string name)
    {
        var security = new Security(ownerUserId, name, name.ToUpperInvariant(), null, null, "EUR", null, null, null);
        _db.Securities.Add(security);
        return security;
    }

    private void AddBuyPosting(Guid securityId, DateTime date, decimal amount, decimal quantity)
        => _db.Postings.Add(new Posting(Guid.NewGuid(), PostingKind.Security, null, null, null, securityId, date, -Math.Abs(amount), null, null, null, SecurityPostingSubType.Buy, quantity));

    private void AddSellPosting(Guid securityId, DateTime date, decimal amount, decimal quantity)
        => _db.Postings.Add(new Posting(Guid.NewGuid(), PostingKind.Security, null, null, null, securityId, date, Math.Abs(amount), null, null, null, SecurityPostingSubType.Sell, quantity));

    private void AddDividendPosting(Guid securityId, DateTime date, decimal amount)
        => _db.Postings.Add(new Posting(Guid.NewGuid(), PostingKind.Security, null, null, null, securityId, date, Math.Abs(amount), null, null, null, SecurityPostingSubType.Dividend, null));

    private void AddTaxPosting(Guid securityId, DateTime date, decimal amount)
        => _db.Postings.Add(new Posting(Guid.NewGuid(), PostingKind.Security, null, null, null, securityId, date, -Math.Abs(amount), null, null, null, SecurityPostingSubType.Tax, null));

    private void AddFeePosting(Guid securityId, DateTime date, decimal amount)
    {
        // A standalone fee has a posting group that does not match any buy lot; an ungrouped fee
        // (GroupId.Empty) would link to an ungrouped buy's lot instead.
        var posting = new Posting(Guid.NewGuid(), PostingKind.Security, null, null, null, securityId, date, -Math.Abs(amount), null, null, null, SecurityPostingSubType.Fee, null);
        posting.SetGroup(Guid.NewGuid());
        _db.Postings.Add(posting);
    }

    private void AddPrice(Guid securityId, DateTime date, decimal close)
        => _db.SecurityPrices.Add(new SecurityPrice(securityId, date, close));

    /// <summary>
    /// With buys and prices spanning multiple calendar years, the depot development section returns
    /// one row per year in ascending order with the correct year-end market values.
    /// </summary>
    [Fact]
    public async Task GetPortfolioReport_DepotDevelopment_MultipleYears_ReturnsYearRows()
    {
        var user = CreateUser();
        var security = CreateSecurity(user.Id, "Apple");
        AddBuyPosting(security.Id, new DateTime(CurrentYear - 2, 3, 10), 1000m, 10m);
        AddPrice(security.Id, new DateTime(CurrentYear - 2, 12, 20), 110m);
        AddPrice(security.Id, new DateTime(CurrentYear - 1, 12, 20), 120m);
        AddPrice(security.Id, DateTime.UtcNow, 125m);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var report = await _sut.GetPortfolioAnalysisReportAsync(user.Id, CancellationToken.None);

        var years = report.DepotDevelopment.Years;
        years.Select(y => y.Year).Should().Equal(CurrentYear - 2, CurrentYear - 1, CurrentYear);
        years[0].YearEndValue.Should().Be(1100m);
        years[1].YearEndValue.Should().Be(1200m);
    }

    /// <summary>
    /// The year-start value of every year after the first equals the previous year's end value.
    /// </summary>
    [Fact]
    public async Task GetPortfolioReport_DepotDevelopment_YearStartValue_EqualsPreviousYearEnd()
    {
        var user = CreateUser();
        var security = CreateSecurity(user.Id, "Apple");
        AddBuyPosting(security.Id, new DateTime(CurrentYear - 2, 3, 10), 1000m, 10m);
        AddPrice(security.Id, new DateTime(CurrentYear - 2, 12, 20), 110m);
        AddPrice(security.Id, new DateTime(CurrentYear - 1, 12, 20), 120m);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var report = await _sut.GetPortfolioAnalysisReportAsync(user.Id, CancellationToken.None);

        var years = report.DepotDevelopment.Years;
        years[1].YearStartValue.Should().Be(years[0].YearEndValue);
    }

    /// <summary>
    /// The first year has no previous year-end value, so its start value is the year's own
    /// cumulative invested capital (acquisition costs).
    /// </summary>
    [Fact]
    public async Task GetPortfolioReport_DepotDevelopment_FirstYear_UsesInvestedCapitalAsStart()
    {
        var user = CreateUser();
        var security = CreateSecurity(user.Id, "Apple");
        AddBuyPosting(security.Id, new DateTime(CurrentYear - 1, 5, 10), 1000m, 10m);
        AddPrice(security.Id, new DateTime(CurrentYear - 1, 12, 20), 110m);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var report = await _sut.GetPortfolioAnalysisReportAsync(user.Id, CancellationToken.None);

        var firstYear = report.DepotDevelopment.Years[0];
        firstYear.YearStartValue.Should().Be(1000m);
        firstYear.YearStartValue.Should().Be(firstYear.CumulativeInvestedCapital);
    }

    /// <summary>
    /// Net dividends of a year are the sum of dividend postings minus the sum of tax postings.
    /// </summary>
    [Fact]
    public async Task GetPortfolioReport_DepotDevelopment_NetDividends_SubtractsTax()
    {
        var user = CreateUser();
        var security = CreateSecurity(user.Id, "Apple");
        AddBuyPosting(security.Id, new DateTime(CurrentYear - 1, 3, 10), 1000m, 10m);
        AddDividendPosting(security.Id, new DateTime(CurrentYear - 1, 6, 15), 100m);
        AddTaxPosting(security.Id, new DateTime(CurrentYear - 1, 6, 15), 25m);
        AddPrice(security.Id, DateTime.UtcNow, 110m);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var report = await _sut.GetPortfolioAnalysisReportAsync(user.Id, CancellationToken.None);

        report.DepotDevelopment.Years.Single(y => y.Year == CurrentYear - 1).NetDividends.Should().Be(75m);
    }

    /// <summary>
    /// After a partial sell the cumulative invested capital of that year drops to the remaining
    /// FIFO cost basis (net of standalone fees).
    /// </summary>
    [Fact]
    public async Task GetPortfolioReport_DepotDevelopment_CumulativeCapital_FollowsFifoCostBasis()
    {
        var user = CreateUser();
        var security = CreateSecurity(user.Id, "Apple");
        AddBuyPosting(security.Id, new DateTime(CurrentYear - 2, 3, 10), 1000m, 10m);
        AddFeePosting(security.Id, new DateTime(CurrentYear - 2, 3, 11), 30m);
        AddSellPosting(security.Id, new DateTime(CurrentYear - 1, 5, 10), 520m, 4m);
        AddPrice(security.Id, DateTime.UtcNow, 110m);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var report = await _sut.GetPortfolioAnalysisReportAsync(user.Id, CancellationToken.None);

        report.DepotDevelopment.Years.Single(y => y.Year == CurrentYear - 2).CumulativeInvestedCapital.Should().Be(1000m);
        report.DepotDevelopment.Years.Single(y => y.Year == CurrentYear - 1).CumulativeInvestedCapital.Should().Be(600m);
    }

    /// <summary>
    /// The net flow of a year is the sum of buy posting amounts minus the sum of sell posting
    /// amounts: a year with only a buy shows a positive inflow, a year with a larger sell than
    /// buy shows a net withdrawal.
    /// </summary>
    [Fact]
    public async Task GetPortfolioReport_DepotDevelopment_NetFlow_BuysMinusSells()
    {
        var user = CreateUser();
        var security = CreateSecurity(user.Id, "Apple");
        AddBuyPosting(security.Id, new DateTime(CurrentYear - 2, 3, 10), 1000m, 10m);
        AddBuyPosting(security.Id, new DateTime(CurrentYear - 1, 2, 10), 400m, 4m);
        AddSellPosting(security.Id, new DateTime(CurrentYear - 1, 5, 10), 650m, 5m);
        AddPrice(security.Id, DateTime.UtcNow, 110m);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var report = await _sut.GetPortfolioAnalysisReportAsync(user.Id, CancellationToken.None);

        report.DepotDevelopment.Years.Single(y => y.Year == CurrentYear - 2).NetFlow.Should().Be(1000m);
        report.DepotDevelopment.Years.Single(y => y.Year == CurrentYear - 1).NetFlow.Should().Be(-250m);
    }

    /// <summary>
    /// While positions are still held, the current year appears as a marked YTD row.
    /// </summary>
    [Fact]
    public async Task GetPortfolioReport_DepotDevelopment_CurrentYear_MarkedIsYtd()
    {
        var user = CreateUser();
        var security = CreateSecurity(user.Id, "Apple");
        AddBuyPosting(security.Id, new DateTime(CurrentYear - 1, 3, 10), 1000m, 10m);
        AddPrice(security.Id, DateTime.UtcNow, 110m);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var report = await _sut.GetPortfolioAnalysisReportAsync(user.Id, CancellationToken.None);

        var last = report.DepotDevelopment.Years[^1];
        last.Year.Should().Be(CurrentYear);
        last.IsYtd.Should().BeTrue();
        report.DepotDevelopment.Years.Take(report.DepotDevelopment.Years.Count - 1).Should().OnlyContain(y => !y.IsYtd);
    }

    /// <summary>
    /// The year-end value uses the latest price on or before the reference date; a position
    /// without any price contributes zero to the portfolio value.
    /// </summary>
    [Fact]
    public async Task GetPortfolioReport_DepotDevelopment_StaleOrMissingPrice_FallsBackToLatestKnownOrZero()
    {
        var user = CreateUser();
        var security = CreateSecurity(user.Id, "Apple");
        var noPriceSecurity = CreateSecurity(user.Id, "SAP");
        AddBuyPosting(security.Id, new DateTime(CurrentYear - 1, 3, 10), 1000m, 10m);
        AddBuyPosting(noPriceSecurity.Id, new DateTime(CurrentYear - 1, 3, 10), 500m, 5m);
        AddPrice(security.Id, new DateTime(CurrentYear - 1, 6, 1), 50m);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var report = await _sut.GetPortfolioAnalysisReportAsync(user.Id, CancellationToken.None);

        report.DepotDevelopment.Years.Single(y => y.Year == CurrentYear - 1).YearEndValue.Should().Be(500m);
    }

    /// <summary>
    /// Once every position has been sold in a previous year the series ends at the last
    /// transaction year: no row for the current year and no YTD marker.
    /// </summary>
    [Fact]
    public async Task GetPortfolioReport_DepotDevelopment_AllPositionsSold_SeriesEndsAtLastTransactionYear()
    {
        var user = CreateUser();
        var security = CreateSecurity(user.Id, "Apple");
        AddBuyPosting(security.Id, new DateTime(CurrentYear - 2, 3, 10), 1000m, 10m);
        AddSellPosting(security.Id, new DateTime(CurrentYear - 1, 5, 10), 1200m, 10m);
        AddPrice(security.Id, DateTime.UtcNow, 110m);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var report = await _sut.GetPortfolioAnalysisReportAsync(user.Id, CancellationToken.None);

        var years = report.DepotDevelopment.Years;
        years.Select(y => y.Year).Should().Equal(CurrentYear - 2, CurrentYear - 1);
        years.Should().OnlyContain(y => !y.IsYtd);
    }

    /// <summary>
    /// Without any security postings the depot development section returns an empty year list.
    /// </summary>
    [Fact]
    public async Task GetPortfolioReport_DepotDevelopment_NoPostings_ReturnsEmptyYears()
    {
        var user = CreateUser();
        CreateSecurity(user.Id, "Apple");
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var report = await _sut.GetPortfolioAnalysisReportAsync(user.Id, CancellationToken.None);

        report.DepotDevelopment.Years.Should().BeEmpty();
    }
}
