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
/// Integration tests for <see cref="PortfolioAnalysisReportService"/> covering portfolio structure
/// aggregation, grouping and user-scoping. Uses EF Core InMemory database with a fresh instance per test
/// and the real <see cref="FifoCostBasisCalculator"/> / <see cref="ReturnCalculationService"/> implementations.
/// </summary>
public sealed class PortfolioAnalysisReportServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly PortfolioAnalysisReportService _sut;

    public PortfolioAnalysisReportServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);

        var fifo = new FifoCostBasisCalculator(NullLogger<FifoCostBasisCalculator>.Instance);
        var calc = new ReturnCalculationService(NullLogger<ReturnCalculationService>.Instance);
        _sut = new PortfolioAnalysisReportService(_db, fifo, calc);
    }

    public void Dispose() => _db.Dispose();

    private User CreateUser()
    {
        var user = new User($"user-{Guid.NewGuid():N}", "hash");
        _db.Users.Add(user);
        return user;
    }

    private Security CreateSecurity(Guid ownerUserId, string name, Guid? categoryId = null, string? region = null, string? sector = null)
    {
        var security = new Security(ownerUserId, name, name.ToUpperInvariant(), null, null, "EUR", categoryId, region, sector);
        _db.Securities.Add(security);
        return security;
    }

    private void AddBuyPosting(Guid securityId, DateTime date, decimal amount, decimal quantity)
        => _db.Postings.Add(new Posting(Guid.NewGuid(), PostingKind.Security, null, null, null, securityId, date, -Math.Abs(amount), null, null, null, SecurityPostingSubType.Buy, quantity));

    private void AddSellPosting(Guid securityId, DateTime date, decimal amount, decimal quantity)
        => _db.Postings.Add(new Posting(Guid.NewGuid(), PostingKind.Security, null, null, null, securityId, date, Math.Abs(amount), null, null, null, SecurityPostingSubType.Sell, quantity));

    private void AddDividendPosting(Guid securityId, DateTime date, decimal amount)
        => _db.Postings.Add(new Posting(Guid.NewGuid(), PostingKind.Security, null, null, null, securityId, date, Math.Abs(amount), null, null, null, SecurityPostingSubType.Dividend, null));

    private void AddPrice(Guid securityId, DateTime date, decimal close)
        => _db.SecurityPrices.Add(new SecurityPrice(securityId, date, close));

    [Fact]
    public async Task GetPortfolioReport_SingleSecurity_ReturnsCorrectStructure()
    {
        var user = CreateUser();
        var security = CreateSecurity(user.Id, "Apple");
        AddBuyPosting(security.Id, DateTime.Today.AddYears(-1), 1000m, 10m);
        AddPrice(security.Id, DateTime.Today, 120m);
        await _db.SaveChangesAsync();

        var report = await _sut.GetPortfolioAnalysisReportAsync(user.Id, CancellationToken.None);

        report.Structure.TotalMarketValue.Should().Be(1200m);
        report.Structure.InvestedCapital.Should().Be(1000m);
        report.Structure.UnrealizedGainLoss.Should().Be(200m);
        report.Structure.TopPositions.Should().ContainSingle(p => p.SecurityId == security.Id && p.MarketValue == 1200m);
    }

    [Fact]
    public async Task GetPortfolioReport_MultipleCategoriesRegionsSectors_GroupsCorrectly()
    {
        var user = CreateUser();
        var category = new SecurityCategory(user.Id, "Aktien");
        _db.SecurityCategories.Add(category);

        var s1 = CreateSecurity(user.Id, "Apple", category.Id, "Nordamerika", "Technologie");
        var s2 = CreateSecurity(user.Id, "SAP", null, "Europa", "Technologie");

        AddBuyPosting(s1.Id, DateTime.Today.AddYears(-1), 1000m, 10m);
        AddPrice(s1.Id, DateTime.Today, 100m);
        AddBuyPosting(s2.Id, DateTime.Today.AddYears(-1), 500m, 5m);
        AddPrice(s2.Id, DateTime.Today, 100m);
        await _db.SaveChangesAsync();

        var report = await _sut.GetPortfolioAnalysisReportAsync(user.Id, CancellationToken.None);

        report.Structure.AssetAllocation.Should().Contain(a => a.Label == "Aktien" && a.Value == 1000m);
        report.Structure.AssetAllocation.Should().Contain(a => a.Label == "Ohne Kategorie" && a.Value == 500m);
        report.Structure.RegionalDistribution.Should().Contain(r => r.Label == "Nordamerika" && r.Value == 1000m);
        report.Structure.RegionalDistribution.Should().Contain(r => r.Label == "Europa" && r.Value == 500m);
        report.Structure.SectorDistribution.Should().ContainSingle(sc => sc.Label == "Technologie" && sc.Value == 1500m);
    }

    [Fact]
    public async Task GetPortfolioReport_WithDividends_CashflowCalculatedCorrectly()
    {
        var user = CreateUser();
        var security = CreateSecurity(user.Id, "Apple");
        AddBuyPosting(security.Id, DateTime.Today.AddYears(-1), 1000m, 10m);
        AddDividendPosting(security.Id, DateTime.Today, 42m);
        AddPrice(security.Id, DateTime.Today, 100m);
        await _db.SaveChangesAsync();

        var report = await _sut.GetPortfolioAnalysisReportAsync(user.Id, CancellationToken.None);

        report.Cashflow.DividendsCurrentYear.Should().Be(42m);
    }

    [Fact]
    public async Task GetPortfolioReport_NoPostings_ReturnsEmptyStructure()
    {
        var user = CreateUser();
        CreateSecurity(user.Id, "Apple");
        await _db.SaveChangesAsync();

        var report = await _sut.GetPortfolioAnalysisReportAsync(user.Id, CancellationToken.None);

        report.Structure.TotalMarketValue.Should().Be(0m);
        report.Structure.InvestedCapital.Should().Be(0m);
        report.Structure.TopPositions.Should().BeEmpty();
        report.Structure.AssetAllocation.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPortfolioReport_MultipleUsers_OnlyReturnsOwnData()
    {
        var userA = CreateUser();
        var userB = CreateUser();

        var securityA = CreateSecurity(userA.Id, "Apple");
        AddBuyPosting(securityA.Id, DateTime.Today.AddYears(-1), 1000m, 10m);
        AddPrice(securityA.Id, DateTime.Today, 100m);

        var securityB = CreateSecurity(userB.Id, "SAP");
        AddBuyPosting(securityB.Id, DateTime.Today.AddYears(-1), 5000m, 50m);
        AddPrice(securityB.Id, DateTime.Today, 200m);

        await _db.SaveChangesAsync();

        var reportA = await _sut.GetPortfolioAnalysisReportAsync(userA.Id, CancellationToken.None);

        reportA.Structure.TotalMarketValue.Should().Be(1000m);
        reportA.Structure.TopPositions.Should().ContainSingle(p => p.SecurityId == securityA.Id);
        reportA.Structure.TopPositions.Should().NotContain(p => p.SecurityId == securityB.Id);
    }
}
