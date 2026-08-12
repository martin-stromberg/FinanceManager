using FinanceManager.Application.Portfolio;
using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Portfolio;
using FinanceManager.Shared.Dtos.Portfolio;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace FinanceManager.Tests.Portfolio;

/// <summary>
/// Tests for <see cref="PortfolioAnalysisReportCacheService"/> covering monthly cache validity
/// and explicit invalidation.
/// </summary>
public sealed class PortfolioAnalysisReportCacheServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly Mock<IPortfolioAnalysisReportService> _serviceMock = new();
    private readonly PortfolioAnalysisReportCacheService _sut;

    public PortfolioAnalysisReportCacheServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);
        _sut = new PortfolioAnalysisReportCacheService(_db, _serviceMock.Object);
    }

    public void Dispose() => _db.Dispose();

    private static PortfolioAnalysisReportDto CreateReport(decimal marketValue, DateTime generatedUtc, DateTime validUntilUtc)
        => new(
            new PortfolioStructureDto(marketValue, 0m, marketValue, [], [], [], [], [], []),
            new PortfolioPerformanceDto(null, null, [], []),
            new PortfolioCashflowDto(0m, 0m, 0m),
            new PortfolioRiskDto(null, null, null, null, null),
            generatedUtc,
            validUntilUtc);

    [Fact]
    public async Task CacheHit_WithinMonth_ReturnsCachedData()
    {
        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var validUntil = PortfolioAnalysisReportService.EndOfMonthUtc(now);
        var firstReport = CreateReport(1000m, now, validUntil);
        _serviceMock.Setup(s => s.GetPortfolioAnalysisReportAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(firstReport);

        var first = await _sut.GetPortfolioReportAsync(userId, CancellationToken.None);
        var second = await _sut.GetPortfolioReportAsync(userId, CancellationToken.None);

        second.Structure.TotalMarketValue.Should().Be(first.Structure.TotalMarketValue);
        _serviceMock.Verify(s => s.GetPortfolioAnalysisReportAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CacheMiss_EndOfMonth_RecalculatesReport()
    {
        var userId = Guid.NewGuid();
        var lastMonth = DateTime.UtcNow.AddMonths(-2);
        var expiredValidUntil = PortfolioAnalysisReportService.EndOfMonthUtc(lastMonth);

        var entry = new Domain.Reports.ReportCacheEntry(
            userId,
            $"portfolio-analysis-report-{userId:N}",
            System.Text.Json.JsonSerializer.Serialize(CreateReport(500m, lastMonth, expiredValidUntil)),
            parameter: null,
            needsRefresh: false,
            cacheValidUntilUtc: expiredValidUntil);
        _db.ReportCacheEntries.Add(entry);
        await _db.SaveChangesAsync();

        var freshReport = CreateReport(999m, DateTime.UtcNow, PortfolioAnalysisReportService.EndOfMonthUtc(DateTime.UtcNow));
        _serviceMock.Setup(s => s.GetPortfolioAnalysisReportAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(freshReport);

        var result = await _sut.GetPortfolioReportAsync(userId, CancellationToken.None);

        result.Structure.TotalMarketValue.Should().Be(999m);
        _serviceMock.Verify(s => s.GetPortfolioAnalysisReportAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CacheHit_EntryFromOlderDtoSchema_TreatedAsMissAndRecalculated()
    {
        // Simulates a cache entry written before PortfolioStructureDto gained AllPositions/InvestedCapitalBreakdown:
        // its JSON lacks those properties, so deserializing it directly would yield null for non-nullable
        // record members. The cache service must recompute instead of returning that partially-null DTO.
        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var validUntil = PortfolioAnalysisReportService.EndOfMonthUtc(now);

        var entry = new Domain.Reports.ReportCacheEntry(
            userId,
            $"portfolio-analysis-report-{userId:N}",
            System.Text.Json.JsonSerializer.Serialize(CreateReport(500m, now, validUntil)),
            parameter: null,
            needsRefresh: false,
            cacheValidUntilUtc: validUntil);
        _db.ReportCacheEntries.Add(entry);
        await _db.SaveChangesAsync();

        var freshReport = CreateReport(999m, now, validUntil);
        _serviceMock.Setup(s => s.GetPortfolioAnalysisReportAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(freshReport);

        var result = await _sut.GetPortfolioReportAsync(userId, CancellationToken.None);

        result.Structure.TotalMarketValue.Should().Be(999m);
        _serviceMock.Verify(s => s.GetPortfolioAnalysisReportAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InvalidateCache_AfterPostingUpdate_DeletesCacheEntry()
    {
        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var validUntil = PortfolioAnalysisReportService.EndOfMonthUtc(now);
        _serviceMock.Setup(s => s.GetPortfolioAnalysisReportAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateReport(1000m, now, validUntil));

        await _sut.GetPortfolioReportAsync(userId, CancellationToken.None);
        (await _db.ReportCacheEntries.CountAsync(e => e.OwnerUserId == userId)).Should().Be(1);

        await _sut.InvalidateCacheAsync(userId, CancellationToken.None);

        (await _db.ReportCacheEntries.CountAsync(e => e.OwnerUserId == userId)).Should().Be(0);

        await _sut.GetPortfolioReportAsync(userId, CancellationToken.None);
        _serviceMock.Verify(s => s.GetPortfolioAnalysisReportAsync(userId, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }
}
