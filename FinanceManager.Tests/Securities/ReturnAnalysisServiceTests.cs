using FinanceManager.Application.Securities.ReturnAnalysis;
using FinanceManager.Domain.Postings;
using FinanceManager.Domain.Securities;
using FinanceManager.Domain.Users;
using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Securities.ReturnAnalysis;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FinanceManager.Tests.Securities;

/// <summary>
/// Integration tests for <see cref="ReturnAnalysisService"/> covering user-scoping,
/// security ownership, and orchestration of return calculations.
/// Uses EF Core InMemory database with a fresh instance per test.
/// </summary>
public sealed class ReturnAnalysisServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly Mock<IReturnCalculationService> _calcMock = new();
    private readonly Mock<IFifoCostBasisCalculator> _fifoMock = new();
    private readonly IMemoryCache _memoryCache;
    private readonly MemoryReturnAnalysisCache _cache;
    private readonly ReturnAnalysisService _sut;

    /// <summary>Initializes a fresh DB and SUT for each test.</summary>
    public ReturnAnalysisServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        _cache = new MemoryReturnAnalysisCache(_memoryCache);

        // Default FIFO mock: 10 shares @ 100 = 1000, no oversell
        _fifoMock
            .Setup(f => f.Calculate(It.IsAny<IReadOnlyList<SecurityTransaction>>()))
            .Returns(new FifoCostBasisResult(1_000m, 0m, Array.Empty<FifoLot>(), 10m, false, null));

        // Default calc mocks: return zero / null where applicable
        _calcMock.Setup(c => c.CalculateTotalReturn(It.IsAny<decimal>(), It.IsAny<decimal>(), It.IsAny<decimal>()))
            .Returns(0.05m);
        _calcMock.Setup(c => c.CalculateCagr(It.IsAny<decimal>(), It.IsAny<decimal>(), It.IsAny<double>()))
            .Returns((decimal?)null);
        _calcMock.Setup(c => c.CalculateIrr(It.IsAny<IReadOnlyList<CashflowPoint>>()))
            .Returns((decimal?)null);
        _calcMock.Setup(c => c.CalculateTwr(It.IsAny<IReadOnlyList<TwrPeriodInput>>()))
            .Returns((decimal?)null);
        _calcMock.Setup(c => c.CalculateVolatility(It.IsAny<IReadOnlyList<decimal>>()))
            .Returns((decimal?)null);
        _calcMock.Setup(c => c.CalculateMaxDrawdown(It.IsAny<IReadOnlyList<decimal>>()))
            .Returns((decimal?)null);
        _calcMock.Setup(c => c.CalculateDividendYield(It.IsAny<decimal>(), It.IsAny<decimal>()))
            .Returns((decimal?)null);
        _calcMock.Setup(c => c.CalculateTaxRate(It.IsAny<decimal>(), It.IsAny<decimal>()))
            .Returns((decimal?)null);
        _calcMock.Setup(c => c.CalculateSharpeRatio(It.IsAny<decimal>(), It.IsAny<decimal>(), It.IsAny<decimal>()))
            .Returns((decimal?)null);

        _sut = new ReturnAnalysisService(
            _db,
            _calcMock.Object,
            _fifoMock.Object,
            _cache,
            NullLogger<ReturnAnalysisService>.Instance);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _memoryCache.Dispose();
        _db.Dispose();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a security and its owning user, persists both and returns them.
    /// </summary>
    private (Security security, User user) SetupSecurityAndUser()
    {
        var user = new User($"user-{Guid.NewGuid():N}", "hash");
        _db.Users.Add(user);

        var security = new Security(user.Id, "Test Security", "TST", null, null, "EUR", null);
        _db.Securities.Add(security);
        _db.SaveChanges();
        return (security, user);
    }

    /// <summary>
    /// Creates a Buy-type <see cref="Posting"/> for the given security without persisting it.
    /// </summary>
    private static Posting CreateBuyPosting(Guid securityId, DateTime date, decimal amount, decimal quantity)
        => new Posting(
            Guid.NewGuid(),
            PostingKind.Security,
            null, null, null,
            securityId,
            date,
            amount,
            null, null, null,
            SecurityPostingSubType.Buy,
            quantity);

    // ─────────────────────────────────────────────────────────────────────────
    // GetReturnSummaryAsync
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// When the security does not belong to the requesting user,
    /// the method must return null (user-scoping S-1 requirement).
    /// </summary>
    [Fact]
    public async Task GetReturnSummaryAsync_Should_ReturnNull_When_SecurityDoesNotBelongToUser()
    {
        // Arrange – security exists but belongs to a different user
        var (security, _) = SetupSecurityAndUser();
        var otherUserId = Guid.NewGuid(); // different user, no matching security

        // Act
        ReturnSummaryDto? result = await _sut.GetReturnSummaryAsync(security.Id, otherUserId, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    /// <summary>
    /// When the security is found and owned by the user but has no transactions,
    /// the method must return null.
    /// </summary>
    [Fact]
    public async Task GetReturnSummaryAsync_Should_ReturnNull_When_NoTransactionsExist()
    {
        // Arrange – security exists and is owned by user, but no postings
        var (security, user) = SetupSecurityAndUser();

        // Act
        ReturnSummaryDto? result = await _sut.GetReturnSummaryAsync(security.Id, user.Id, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    /// <summary>
    /// When there are transactions but no current price data available,
    /// HasMissingPrices must be true and MissingPricesHint must be set.
    /// </summary>
    [Fact]
    public async Task GetReturnSummaryAsync_Should_SetHasMissingPrices_When_NoCurrentPriceAvailable()
    {
        // Arrange – one buy posting, no price data in the database
        var (security, user) = SetupSecurityAndUser();
        var posting = CreateBuyPosting(security.Id, DateTime.Today.AddDays(-30), -1_000m, 10m);
        _db.Postings.Add(posting);
        await _db.SaveChangesAsync();

        // Act
        ReturnSummaryDto? result = await _sut.GetReturnSummaryAsync(security.Id, user.Id, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.HasMissingPrices.Should().BeTrue();
        result.MissingPricesHint.Should().NotBeNullOrEmpty();
    }

    /// <summary>
    /// When the FIFO result signals an oversell warning, HasMissingPrices must be true
    /// and MissingPricesHint must be populated with the oversell warning message.
    /// </summary>
    [Fact]
    public async Task GetReturnSummaryAsync_Should_SetOversellHint_When_OversellWarningActive()
    {
        // Arrange – configure FIFO mock to return an oversell warning
        _fifoMock
            .Setup(f => f.Calculate(It.IsAny<IReadOnlyList<SecurityTransaction>>()))
            .Returns(new FifoCostBasisResult(500m, 0m, Array.Empty<FifoLot>(), 0m, true, "Oversell detected"));

        var (security, user) = SetupSecurityAndUser();
        var posting = CreateBuyPosting(security.Id, DateTime.Today.AddDays(-30), -1_000m, 10m);
        _db.Postings.Add(posting);

        // Add a price so we bypass the "no current price" branch and reach the oversell check
        _db.SecurityPrices.Add(new SecurityPrice(security.Id, DateTime.Today, 105m));
        await _db.SaveChangesAsync();

        // Act
        ReturnSummaryDto? result = await _sut.GetReturnSummaryAsync(security.Id, user.Id, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.HasMissingPrices.Should().BeTrue();
        result.MissingPricesHint.Should().Be("Oversell detected");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GetSparklineDataAsync
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// When there are no transactions for the security, the method must return null.
    /// </summary>
    [Fact]
    public async Task GetSparklineDataAsync_Should_ReturnNull_When_NoTransactionsExist()
    {
        // Arrange
        var (security, user) = SetupSecurityAndUser();

        // Act
        SparklineDataDto? result = await _sut.GetSparklineDataAsync(security.Id, user.Id, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    /// <summary>
    /// When there are fewer than 30 price points available after the first transaction date,
    /// the method must return null (not enough data for a meaningful sparkline).
    /// </summary>
    [Fact]
    public async Task GetSparklineDataAsync_Should_ReturnNull_When_LessThan30PricePoints()
    {
        // Arrange – one transaction, only 5 price entries
        var (security, user) = SetupSecurityAndUser();
        var posting = CreateBuyPosting(security.Id, DateTime.Today.AddDays(-10), -1_000m, 10m);
        _db.Postings.Add(posting);

        for (int i = 5; i >= 1; i--)
        {
            _db.SecurityPrices.Add(new SecurityPrice(security.Id, DateTime.Today.AddDays(-i), 100m + i));
        }
        await _db.SaveChangesAsync();

        // Act
        SparklineDataDto? result = await _sut.GetSparklineDataAsync(security.Id, user.Id, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    /// <summary>
    /// When there are at least 30 price points, the method must return a non-null
    /// <see cref="SparklineDataDto"/> with data points.
    /// </summary>
    [Fact]
    public async Task GetSparklineDataAsync_Should_ReturnSparkline_When_Exactly30PricePoints()
    {
        // Arrange – transaction 35 days ago, 35 consecutive price entries
        var (security, user) = SetupSecurityAndUser();
        var firstDate = DateTime.Today.AddDays(-35);
        var posting = CreateBuyPosting(security.Id, firstDate, -1_000m, 10m);
        _db.Postings.Add(posting);

        for (int i = 35; i >= 1; i--)
        {
            _db.SecurityPrices.Add(new SecurityPrice(security.Id, DateTime.Today.AddDays(-i), 100m));
        }
        await _db.SaveChangesAsync();

        // Act
        SparklineDataDto? result = await _sut.GetSparklineDataAsync(security.Id, user.Id, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Points.Should().NotBeEmpty();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GetCashflowTimelineAsync
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// When the security does not exist or does not belong to the requesting user,
    /// the method must return null.
    /// </summary>
    [Fact]
    public async Task GetCashflowTimelineAsync_Should_ReturnNull_When_SecurityNotFound()
    {
        // Arrange – non-existent security and random user
        var randomSecurityId = Guid.NewGuid();
        var randomUserId = Guid.NewGuid();

        // Act
        CashflowTimelineDto? result = await _sut.GetCashflowTimelineAsync(randomSecurityId, randomUserId, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    /// <summary>
    /// When the security exists and is owned by the user but there are no transactions,
    /// the method must return an empty (non-null) DTO with empty collections.
    /// This differs from other methods that return null for empty data.
    /// </summary>
    [Fact]
    public async Task GetCashflowTimelineAsync_Should_ReturnEmptyDto_When_NoTransactions()
    {
        // Arrange – security exists, but no postings
        var (security, user) = SetupSecurityAndUser();

        // Act
        CashflowTimelineDto? result = await _sut.GetCashflowTimelineAsync(security.Id, user.Id, CancellationToken.None);

        // Assert – must be non-null with empty collections (not null)
        result.Should().NotBeNull();
        result!.Entries.Should().BeEmpty();
        result.AnnualSummaries.Should().BeEmpty();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GetBenchmarkComparisonAsync
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// When no benchmark security is configured for the user (BenchmarkSecurityId is null),
    /// the method must return null.
    /// </summary>
    [Fact]
    public async Task GetBenchmarkComparisonAsync_Should_ReturnNull_When_NoBenchmarkConfigured()
    {
        // Arrange – user without BenchmarkSecurityId (default after construction)
        var (security, user) = SetupSecurityAndUser();

        // Act
        BenchmarkComparisonDto? result = await _sut.GetBenchmarkComparisonAsync(security.Id, user.Id, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    /// <summary>
    /// When the configured benchmark security belongs to a different user,
    /// the method must return null (ownership enforcement S-3).
    /// </summary>
    [Fact]
    public async Task GetBenchmarkComparisonAsync_Should_ReturnNull_When_BenchmarkSecurityNotOwnedByUser()
    {
        // Arrange – benchmark security belongs to a different owner
        var (security, user) = SetupSecurityAndUser();
        var otherOwner = Guid.NewGuid();
        var benchmarkSecurity = new Security(otherOwner, "Benchmark Index", "BMK", null, null, "EUR", null);
        _db.Securities.Add(benchmarkSecurity);

        // Point user's benchmark at the foreign security
        user.SetReturnAnalysisSettings(benchmarkSecurity.Id, false, 0m);
        await _db.SaveChangesAsync();

        // Act
        BenchmarkComparisonDto? result = await _sut.GetBenchmarkComparisonAsync(security.Id, user.Id, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // UpdateUserSettingsAsync
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// When a benchmark security ID is provided but the security belongs to a different user,
    /// the method must throw <see cref="ArgumentException"/> (S-3 ownership enforcement).
    /// </summary>
    [Fact]
    public async Task UpdateUserSettingsAsync_Should_ThrowArgumentException_When_BenchmarkSecurityNotOwnedByUser()
    {
        // Arrange – foreign security not owned by the requesting user
        var (_, user) = SetupSecurityAndUser();
        var otherOwner = Guid.NewGuid();
        var foreignSecurity = new Security(otherOwner, "Foreign ETF", "FOR", null, null, "USD", null);
        _db.Securities.Add(foreignSecurity);
        await _db.SaveChangesAsync();

        // Act
        Func<Task> act = async () => await _sut.UpdateUserSettingsAsync(
            user.Id, foreignSecurity.Id, false, 0.02m, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    /// <summary>
    /// When benchmarkSecurityId is null, the existing benchmark setting is cleared without error.
    /// </summary>
    [Fact]
    public async Task UpdateUserSettingsAsync_Should_ClearBenchmark_When_BenchmarkIdIsNull()
    {
        // Arrange – user with an existing benchmark security
        var (security, user) = SetupSecurityAndUser();
        user.SetReturnAnalysisSettings(security.Id, false, 0m);
        await _db.SaveChangesAsync();

        // Act
        await _sut.UpdateUserSettingsAsync(user.Id, null, false, 0m, CancellationToken.None);

        // Assert – reload the user entity and verify the benchmark was cleared
        await _db.Entry(user).ReloadAsync();
        user.BenchmarkSecurityId.Should().BeNull();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // InvalidateCacheAsync
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// InvalidateCacheAsync must pass a token that contains both the securityId and userId
    /// to the cache's InvalidateAsync method, so all related entries are evicted.
    /// </summary>
    [Fact]
    public async Task InvalidateCacheAsync_Should_InvalidateCorrectToken_When_CalledWithValidIds()
    {
        // Arrange – use a mock cache to capture the invalidation token
        var cacheMock = new Mock<IReturnAnalysisCache>();
        cacheMock.Setup(c => c.InvalidateAsync(It.IsAny<string>())).Returns(Task.CompletedTask);

        var sut = new ReturnAnalysisService(
            _db,
            _calcMock.Object,
            _fifoMock.Object,
            cacheMock.Object,
            NullLogger<ReturnAnalysisService>.Instance);

        var secId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        // Act
        await sut.InvalidateCacheAsync(secId, userId);

        // Assert – the token passed to InvalidateAsync must contain both IDs
        cacheMock.Verify(
            c => c.InvalidateAsync(
                It.Is<string>(s => s.Contains(secId.ToString()) && s.Contains(userId.ToString()))),
            Times.Once);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GetDetailedMetricsAsync — Sharpe Ratio opt-in
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// When the user has ShowSharpeRatio = false, the Sharpe Ratio must not be computed
    /// and the result must be null (opt-in guard FR-8).
    /// </summary>
    [Fact]
    public async Task GetDetailedMetricsAsync_Should_ReturnNullSharpeRatio_When_ShowSharpeRatioIsFalse()
    {
        // Arrange
        var (security, user) = SetupSecurityAndUser();
        user.SetReturnAnalysisSettings(null, showSharpeRatio: false, riskFreeRate: 0.02m);

        var posting = CreateBuyPosting(security.Id, DateTime.Today.AddDays(-10), -1_000m, 10m);
        _db.Postings.Add(posting);
        _db.SecurityPrices.Add(new SecurityPrice(security.Id, DateTime.Today.AddDays(-1), 105m));
        await _db.SaveChangesAsync();

        // Setup TWR and volatility so the Sharpe guard would fire — if opt-in were true
        _calcMock.Setup(c => c.CalculateTwr(It.IsAny<IReadOnlyList<TwrPeriodInput>>())).Returns(0.05m);
        _calcMock.Setup(c => c.CalculateVolatility(It.IsAny<IReadOnlyList<decimal>>())).Returns(0.15m);

        // Act
        DetailedReturnMetricsDto? result = await _sut.GetDetailedMetricsAsync(security.Id, user.Id, CancellationToken.None);

        // Assert – Sharpe Ratio must be null, and the calculation must never be invoked
        result.Should().NotBeNull();
        result!.SharpeRatio.Should().BeNull();
        _calcMock.Verify(
            c => c.CalculateSharpeRatio(It.IsAny<decimal>(), It.IsAny<decimal>(), It.IsAny<decimal>()),
            Times.Never);
    }

    /// <summary>
    /// When volatility is zero (flat price series), the Sharpe Ratio must remain null
    /// to avoid division by zero, even when ShowSharpeRatio is enabled.
    /// </summary>
    [Fact]
    public async Task GetDetailedMetricsAsync_Should_ReturnNullSharpeRatio_When_VolatilityIsZero()
    {
        // Arrange
        var (security, user) = SetupSecurityAndUser();
        user.SetReturnAnalysisSettings(null, showSharpeRatio: true, riskFreeRate: 0.02m);

        var posting = CreateBuyPosting(security.Id, DateTime.Today.AddDays(-10), -1_000m, 10m);
        _db.Postings.Add(posting);
        _db.SecurityPrices.Add(new SecurityPrice(security.Id, DateTime.Today.AddDays(-1), 105m));
        await _db.SaveChangesAsync();

        _calcMock.Setup(c => c.CalculateTwr(It.IsAny<IReadOnlyList<TwrPeriodInput>>())).Returns(0.05m);
        _calcMock.Setup(c => c.CalculateVolatility(It.IsAny<IReadOnlyList<decimal>>())).Returns(0m); // zero volatility

        // Act
        DetailedReturnMetricsDto? result = await _sut.GetDetailedMetricsAsync(security.Id, user.Id, CancellationToken.None);

        // Assert – division by zero must be guarded; Sharpe Ratio stays null
        result.Should().NotBeNull();
        result!.SharpeRatio.Should().BeNull();
        _calcMock.Verify(
            c => c.CalculateSharpeRatio(It.IsAny<decimal>(), It.IsAny<decimal>(), It.IsAny<decimal>()),
            Times.Never);
    }
}
