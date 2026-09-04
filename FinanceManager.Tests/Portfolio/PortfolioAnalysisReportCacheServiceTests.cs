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

    /// <summary>
    /// Sets up an in-memory database and a mocked <see cref="IPortfolioAnalysisReportService"/> so
    /// each test can control exactly when the underlying report is (re)computed and observe how
    /// often that computation actually happens.
    /// </summary>
    public PortfolioAnalysisReportCacheServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);
        _sut = new PortfolioAnalysisReportCacheService(_db, _serviceMock.Object);
    }

    /// <summary>Releases the in-memory <see cref="AppDbContext"/> used by each test.</summary>
    public void Dispose() => _db.Dispose();

    private static PortfolioAnalysisReportDto CreateReport(decimal marketValue, DateTime generatedUtc, DateTime validUntilUtc)
        => new(
            new PortfolioStructureDto(marketValue, 0m, marketValue, [], [], [], [], [], []),
            new PortfolioPerformanceDto(null, null, [], []),
            new PortfolioCashflowDto(0m, 0m, 0m, 0m, 0m, 0m),
            new PortfolioRiskDto(null, null, null, null, null),
            generatedUtc,
            validUntilUtc);

    /// <summary>
    /// Verifies that a second request within the same cache validity window returns the previously
    /// computed report and does not call the underlying report service again, confirming the cache
    /// actually avoids recomputation rather than just storing data unused.
    /// </summary>
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

    /// <summary>
    /// Verifies that a cache entry whose validity ended with the previous month is treated as
    /// expired: the service recomputes the report and returns the fresh values rather than the
    /// stale cached ones - the cache's month-boundary expiry policy actually kicks in.
    /// </summary>
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
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var freshReport = CreateReport(999m, DateTime.UtcNow, PortfolioAnalysisReportService.EndOfMonthUtc(DateTime.UtcNow));
        _serviceMock.Setup(s => s.GetPortfolioAnalysisReportAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(freshReport);

        var result = await _sut.GetPortfolioReportAsync(userId, CancellationToken.None);

        result.Structure.TotalMarketValue.Should().Be(999m);
        _serviceMock.Verify(s => s.GetPortfolioAnalysisReportAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Regression guard for a cache entry that was serialized before <c>PortfolioStructureDto</c>
    /// gained the <c>AllPositions</c>/<c>InvestedCapitalBreakdown</c> properties: deserializing that
    /// older JSON directly would produce null for those now-non-nullable record members. Verifies
    /// the cache service instead recomputes the report rather than returning a partially-null DTO
    /// that would blow up downstream.
    /// </summary>
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
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var freshReport = CreateReport(999m, now, validUntil);
        _serviceMock.Setup(s => s.GetPortfolioAnalysisReportAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(freshReport);

        var result = await _sut.GetPortfolioReportAsync(userId, CancellationToken.None);

        result.Structure.TotalMarketValue.Should().Be(999m);
        _serviceMock.Verify(s => s.GetPortfolioAnalysisReportAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Verifies that explicitly invalidating the cache deletes the stored entry and forces the next
    /// request to recompute the report - the mechanism relied on after data changes (e.g. a posting
    /// update) that would otherwise make the cached figures stale until month-end.
    /// </summary>
    [Fact]
    public async Task InvalidateCache_AfterPostingUpdate_DeletesCacheEntry()
    {
        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var validUntil = PortfolioAnalysisReportService.EndOfMonthUtc(now);
        _serviceMock.Setup(s => s.GetPortfolioAnalysisReportAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateReport(1000m, now, validUntil));

        await _sut.GetPortfolioReportAsync(userId, CancellationToken.None);
        (await _db.ReportCacheEntries.CountAsync(e => e.OwnerUserId == userId, cancellationToken: TestContext.Current.CancellationToken)).Should().Be(1);

        await _sut.InvalidateCacheAsync(userId, CancellationToken.None);

        (await _db.ReportCacheEntries.CountAsync(e => e.OwnerUserId == userId, cancellationToken: TestContext.Current.CancellationToken)).Should().Be(0);

        await _sut.GetPortfolioReportAsync(userId, CancellationToken.None);
        _serviceMock.Verify(s => s.GetPortfolioAnalysisReportAsync(userId, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }
}
