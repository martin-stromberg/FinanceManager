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
            .Returns(new FifoCostBasisResult(1_000m, 0m, Array.Empty<FifoLot>(), 10m, false, null, 0m));

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
            .Returns(new FifoCostBasisResult(500m, 0m, Array.Empty<FifoLot>(), 0m, true, "Oversell detected", 0m));

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

    // ─────────────────────────────────────────────────────────────────────────
    // GetKpiBreakdownsAsync
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// When the security does not exist or does not belong to the requesting user,
    /// the method must return null (user-scoping S-1).
    /// </summary>
    [Fact]
    public async Task GetKpiBreakdownsAsync_Should_ReturnNull_When_SecurityNotFound()
    {
        // Arrange – security exists but belongs to a different user
        var (security, _) = SetupSecurityAndUser();
        var otherUserId = Guid.NewGuid();

        // Act
        IReadOnlyList<KpiBreakdownDto>? result = await _sut.GetKpiBreakdownsAsync(security.Id, otherUserId, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    /// <summary>
    /// When the security is owned by the user but has no transactions,
    /// the method must return null (no data to break down).
    /// </summary>
    [Fact]
    public async Task GetKpiBreakdownsAsync_Should_ReturnNull_When_NoTransactions()
    {
        // Arrange
        var (security, user) = SetupSecurityAndUser();

        // Act
        IReadOnlyList<KpiBreakdownDto>? result = await _sut.GetKpiBreakdownsAsync(security.Id, user.Id, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    /// <summary>
    /// When there are transactions, the method must return a non-null list
    /// containing breakdowns for all widget KPIs (TotalReturn, MarketValue, InvestedCapital, Cagr, Irr).
    /// </summary>
    [Fact]
    public async Task GetKpiBreakdownsAsync_Should_ReturnAllKpis_When_TransactionsExist()
    {
        // Arrange – one buy posting
        var (security, user) = SetupSecurityAndUser();
        var posting = CreateBuyPosting(security.Id, DateTime.Today.AddDays(-400), -1_000m, 10m);
        _db.Postings.Add(posting);
        _db.SecurityPrices.Add(new SecurityPrice(security.Id, DateTime.Today, 110m));
        await _db.SaveChangesAsync();

        // Act
        IReadOnlyList<KpiBreakdownDto>? result = await _sut.GetKpiBreakdownsAsync(security.Id, user.Id, CancellationToken.None);

        // Assert – all five KPI breakdowns must be present
        result.Should().NotBeNull();
        result!.Select(b => b.KpiKey)
            .Should().BeEquivalentTo(["TotalReturn", "MarketValue", "InvestedCapital", "Cagr", "Irr"]);
    }

    /// <summary>
    /// Each returned breakdown must have a non-empty display name, formula text, and description.
    /// </summary>
    [Fact]
    public async Task GetKpiBreakdownsAsync_Should_PopulateMetadata_For_EachKpi()
    {
        // Arrange
        var (security, user) = SetupSecurityAndUser();
        var posting = CreateBuyPosting(security.Id, DateTime.Today.AddDays(-30), -1_000m, 10m);
        _db.Postings.Add(posting);
        await _db.SaveChangesAsync();

        // Act
        IReadOnlyList<KpiBreakdownDto>? result = await _sut.GetKpiBreakdownsAsync(security.Id, user.Id, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Should().AllSatisfy(b =>
        {
            b.DisplayName.Should().NotBeNullOrEmpty();
            b.FormulaText.Should().NotBeNullOrEmpty();
            b.Description.Should().NotBeNullOrEmpty();
            b.Groups.Should().NotBeNull();
        });
    }

    /// <summary>
    /// The InvestedCapital breakdown must contain a "Käufe" group with one item per buy posting
    /// and the group total must equal the sum of all buy amounts.
    /// </summary>
    [Fact]
    public async Task GetKpiBreakdownsAsync_Should_IncludeBuyPostings_In_InvestedCapitalBreakdown()
    {
        // Arrange – two buy postings at different dates
        var (security, user) = SetupSecurityAndUser();
        var buy1 = CreateBuyPosting(security.Id, DateTime.Today.AddDays(-200), -500m, 5m);
        var buy2 = CreateBuyPosting(security.Id, DateTime.Today.AddDays(-100), -600m, 6m);
        _db.Postings.AddRange(buy1, buy2);
        await _db.SaveChangesAsync();

        // Configure FIFO mock to return the sum of buys as cost basis
        _fifoMock
            .Setup(f => f.Calculate(It.IsAny<IReadOnlyList<SecurityTransaction>>()))
            .Returns(new FifoCostBasisResult(1_100m, 0m, Array.Empty<FifoLot>(), 11m, false, null, 0m));

        // Act
        IReadOnlyList<KpiBreakdownDto>? result = await _sut.GetKpiBreakdownsAsync(security.Id, user.Id, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        var ic = result!.First(b => b.KpiKey == "InvestedCapital");
        var buysGroup = ic.Groups.FirstOrDefault(g => g.GroupName == "Käufe");

        buysGroup.Should().NotBeNull();
        buysGroup!.Items.Should().HaveCount(2);
        buysGroup.GroupTotal.Should().Be(1_100m); // 500 + 600
    }

    /// <summary>
    /// The TotalReturn breakdown must include a "Dividenden (netto)" group (Group[2])
    /// containing each dividend posting with the correct amount.
    /// </summary>
    [Fact]
    public async Task GetKpiBreakdownsAsync_Should_IncludeDividends_In_TotalReturnBreakdown()
    {
        // Arrange – one buy and one dividend posting
        var (security, user) = SetupSecurityAndUser();
        var buy = CreateBuyPosting(security.Id, DateTime.Today.AddDays(-200), -1_000m, 10m);
        var dividend = new Posting(
            Guid.NewGuid(),
            PostingKind.Security,
            null, null, null,
            security.Id,
            DateTime.Today.AddDays(-30),
            50m,
            null, null, null,
            SecurityPostingSubType.Dividend,
            null);
        _db.Postings.AddRange(buy, dividend);
        await _db.SaveChangesAsync();

        // Act
        IReadOnlyList<KpiBreakdownDto>? result = await _sut.GetKpiBreakdownsAsync(security.Id, user.Id, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        var tr = result!.First(b => b.KpiKey == "TotalReturn");
        var dividendsGroup = tr.Groups.FirstOrDefault(g => g.GroupName == "Dividenden (netto)");

        dividendsGroup.Should().NotBeNull("group 'Dividenden (netto)' must be present in TotalReturn");
        dividendsGroup!.Items.Should().HaveCount(1);
        dividendsGroup.Items[0].Amount.Should().Be(50m);
        dividendsGroup.GroupTotal.Should().Be(50m, "netDividends = 50 Dividende − 0 Steuern");
    }

    /// <summary>
    /// The TotalReturn breakdown must use the new four-group structure:
    /// [0] "Aktueller Marktwert" (positiv), [1] "Investiertes Kapital" (negativ),
    /// [2] "Dividenden (netto)" (positiv), [3] "Gesamtrendite" (Ergebnis).
    /// Group[0] must have one item with the current market value.
    /// Group[1] must have the buy items negated (outflows).
    /// </summary>
    [Fact]
    public async Task GetKpiBreakdownsAsync_Should_BreakDownTotalReturn_Into_FourGroups()
    {
        // Arrange – one buy posting, price available
        var (security, user) = SetupSecurityAndUser();
        var buy = CreateBuyPosting(security.Id, DateTime.Today.AddDays(-200), -1_000m, 10m);
        _db.Postings.Add(buy);
        _db.SecurityPrices.Add(new SecurityPrice(security.Id, DateTime.Today, 120m)); // 10 shares × 120 = 1 200 market value
        await _db.SaveChangesAsync();

        // FIFO mock: cost basis 1 000, 10 shares held
        _fifoMock
            .Setup(f => f.Calculate(It.IsAny<IReadOnlyList<SecurityTransaction>>()))
            .Returns(new FifoCostBasisResult(1_000m, 0m, Array.Empty<FifoLot>(), 10m, false, null, 0m));

        // Act
        IReadOnlyList<KpiBreakdownDto>? result = await _sut.GetKpiBreakdownsAsync(security.Id, user.Id, CancellationToken.None);

        // Assert – group order: [0] Marktwert, [1] Investiertes Kapital, [2] Dividenden (netto), [3] Gesamtrendite
        result.Should().NotBeNull();
        var tr = result!.First(b => b.KpiKey == "TotalReturn");

        tr.Groups.Should().HaveCount(4, "TotalReturn must have exactly four formula groups");
        tr.Groups[0].GroupName.Should().Be("Aktueller Marktwert");
        tr.Groups[1].GroupName.Should().Be("Investiertes Kapital");
        tr.Groups[2].GroupName.Should().Be("Dividenden (netto)");
        tr.Groups[3].GroupName.Should().Be("Gesamtrendite");

        // Group[0]: Aktueller Marktwert – single item = 10 × 120 = 1 200
        var marktwertGroup = tr.Groups[0];
        marktwertGroup.IsPositiveContribution.Should().BeTrue();
        marktwertGroup.Items.Should().HaveCount(1, "exactly one item: current market value");
        marktwertGroup.Items[0].Amount.Should().Be(1_200m, "market value = 10 shares × 120");
        marktwertGroup.Items[0].Note.Should().Contain("Anteile", "note must reference share count and price");
        marktwertGroup.GroupTotal.Should().Be(1_200m);

        // Group[1]: Investiertes Kapital – buy item negated (outflow)
        var icGroup = tr.Groups[1];
        icGroup.IsPositiveContribution.Should().BeFalse();
        icGroup.Items.Should().HaveCount(1, "one item per buy posting");
        icGroup.Items[0].Amount.Should().Be(-1_000m, "invested capital must appear as a negative outflow");
        icGroup.Items[0].Note.Should().Contain("Anteile", "note must reference the number of shares and price per share");
        icGroup.GroupTotal.Should().Be(-1_000m, "GroupTotal = −investedCapital");

        // Group[2]: Dividenden (netto) – no dividends in this test
        tr.Groups[2].Items.Should().BeEmpty("no dividend postings were created");
        tr.Groups[2].GroupTotal.Should().Be(0m);

        // Group[3]: Gesamtrendite – totalReturnAbsolute = 1200 − 1000 + 0 = 200
        var resultGroup = tr.Groups[3];
        resultGroup.Items.Should().HaveCount(1, "one summary item for total return");
        resultGroup.Items[0].Amount.Should().Be(200m, "total return = market value − invested capital + net dividends");
        resultGroup.IsPositiveContribution.Should().BeTrue("positive total return");
        resultGroup.GroupTotal.Should().Be(200m);

        // GroupTotalPercent must be set so the info panel can show both % and € side by side.
        // The mock returns 0.05 m for CalculateTotalReturn (see constructor setup).
        resultGroup.GroupTotalPercent.Should().Be(0.05m,
            "Group[3] must carry the percentage return so the info-panel can render '▲+5.00 % (200,00 EUR)'");
    }

    /// <summary>
    /// Fees in TotalReturn must appear inside Group[1] "Investiertes Kapital" as negated items
    /// (standalone fees with note "Gebühr (ohne Kaufzuordnung)"), not in a separate fees group.
    /// This is a regression test ensuring individual fee rows are always visible in the info panel.
    /// </summary>
    [Fact]
    public async Task GetKpiBreakdownsAsync_Should_PopulateItems_For_FeesGroup()
    {
        // Arrange – one buy and two fee postings (standalone, no GroupId)
        var (security, user) = SetupSecurityAndUser();
        var buy = CreateBuyPosting(security.Id, DateTime.Today.AddDays(-200), -1_000m, 10m);
        var fee1 = new Posting(
            Guid.NewGuid(),
            PostingKind.Security,
            null, null, null,
            security.Id,
            DateTime.Today.AddDays(-100),
            -4m,
            null, null, null,
            SecurityPostingSubType.Fee,
            null);
        var fee2 = new Posting(
            Guid.NewGuid(),
            PostingKind.Security,
            null, null, null,
            security.Id,
            DateTime.Today.AddDays(-50),
            -5.64m,
            null, null, null,
            SecurityPostingSubType.Fee,
            null);
        _db.Postings.AddRange(buy, fee1, fee2);
        await _db.SaveChangesAsync();

        // Act
        IReadOnlyList<KpiBreakdownDto>? result = await _sut.GetKpiBreakdownsAsync(security.Id, user.Id, CancellationToken.None);

        // Assert – TotalReturn Group[1] "Investiertes Kapital" must contain negated items for each fee
        result.Should().NotBeNull();
        var tr = result!.First(b => b.KpiKey == "TotalReturn");

        // Verify group order
        tr.Groups[0].GroupName.Should().Be("Aktueller Marktwert");
        tr.Groups[1].GroupName.Should().Be("Investiertes Kapital");
        tr.Groups[2].GroupName.Should().Be("Dividenden (netto)");
        tr.Groups[3].GroupName.Should().Be("Gesamtrendite");

        // Group[1]: one buy + two standalone fees = 3 items, all negated
        var icGroup = tr.Groups[1];
        icGroup.IsPositiveContribution.Should().BeFalse("invested capital is a negative contribution to total return");
        icGroup.Items.Should().HaveCount(3, "one buy item + two fee items must each appear separately");

        // buy item (index 0)
        icGroup.Items[0].Amount.Should().Be(-1_000m, "buy amount must be negated (outflow)");

        // fee1 (index 1) – standalone, appears after the buy
        icGroup.Items[1].Amount.Should().Be(-4m, "first fee amount must be negated (outflow)");
        icGroup.Items[1].Date.Should().Be(DateTime.Today.AddDays(-100));
        icGroup.Items[1].Note.Should().Be("Gebühr (ohne Kaufzuordnung)");

        // fee2 (index 2)
        icGroup.Items[2].Amount.Should().Be(-5.64m, "second fee amount must be negated (outflow)");
        icGroup.Items[2].Date.Should().Be(DateTime.Today.AddDays(-50));
        icGroup.Items[2].Note.Should().Be("Gebühr (ohne Kaufzuordnung)");

        // Regression: Items must never be empty when fee postings exist
        icGroup.Items.Should().NotBeEmpty("Items must be populated – not just GroupTotal");

        // No separate fees group in TotalReturn any more
        tr.Groups.Should().NotContain(g => g.GroupName == "Gebühren (Abzug)",
            "fees are now embedded in 'Investiertes Kapital', not a standalone group");
    }

    /// <summary>
    /// The InvestedCapital and TotalReturn breakdowns must list each buy posting and each linked fee
    /// (same GroupId) as separate items. Fees with a GroupId that does not match any buy (standalone)
    /// must be appended at the end of the "Käufe" group with the note "Gebühr (ohne Kaufzuordnung)".
    /// </summary>
    [Fact]
    public async Task GetKpiBreakdownsAsync_Should_ListEachBuyAndFeeAsSeperateItem_In_InvestedCapitalBreakdown()
    {
        // Arrange – two buys, one fee linked to the first buy via GroupId, one standalone fee
        var (security, user) = SetupSecurityAndUser();

        var groupId1 = Guid.NewGuid(); // shared by buy1 + fee1 (linked)
        var groupId2 = Guid.NewGuid(); // used only by buy2 (no linked fee)
        var standaloneGroupId = Guid.NewGuid(); // no matching buy → standalone fee

        var buy1 = CreateBuyPosting(security.Id, DateTime.Today.AddDays(-200), -500m, 5m);
        buy1.SetGroup(groupId1);

        var fee1 = new Posting(
            Guid.NewGuid(),
            PostingKind.Security,
            null, null, null,
            security.Id,
            DateTime.Today.AddDays(-200),
            -10m,
            null, null, null,
            SecurityPostingSubType.Fee,
            null);
        fee1.SetGroup(groupId1);

        var buy2 = CreateBuyPosting(security.Id, DateTime.Today.AddDays(-100), -600m, 6m);
        buy2.SetGroup(groupId2);

        var standaloneFee = new Posting(
            Guid.NewGuid(),
            PostingKind.Security,
            null, null, null,
            security.Id,
            DateTime.Today.AddDays(-50),
            -5m,
            null, null, null,
            SecurityPostingSubType.Fee,
            null);
        standaloneFee.SetGroup(standaloneGroupId);

        _db.Postings.AddRange(buy1, fee1, buy2, standaloneFee);
        await _db.SaveChangesAsync();

        // FIFO: cost basis = 500 + 10 + 600 + 5 = 1 115; 11 shares; standalone fee total = 5
        _fifoMock
            .Setup(f => f.Calculate(It.IsAny<IReadOnlyList<SecurityTransaction>>()))
            .Returns(new FifoCostBasisResult(1_115m, 0m, Array.Empty<FifoLot>(), 11m, false, null, 5m));

        // Act
        IReadOnlyList<KpiBreakdownDto>? result = await _sut.GetKpiBreakdownsAsync(security.Id, user.Id, CancellationToken.None);

        // Assert – InvestedCapital "Käufe" group
        result.Should().NotBeNull();
        var ic = result!.First(b => b.KpiKey == "InvestedCapital");
        var buysGroup = ic.Groups.First(g => g.GroupName == "Käufe");

        buysGroup.Items.Should().HaveCount(4,
            "two buys + one linked fee + one standalone fee must each appear as a separate item");

        // buy1
        buysGroup.Items[0].Date.Should().Be(DateTime.Today.AddDays(-200));
        buysGroup.Items[0].Amount.Should().Be(500m, "buy1 amount must be absolute (positive)");
        buysGroup.Items[0].Note.Should().Contain("Anteile", "note must reference share count and price");

        // fee linked to buy1 (immediately after buy1)
        buysGroup.Items[1].Date.Should().Be(DateTime.Today.AddDays(-200));
        buysGroup.Items[1].Amount.Should().Be(10m, "linked fee amount must be absolute (positive)");
        buysGroup.Items[1].Note.Should().Be("Gebühr");

        // buy2
        buysGroup.Items[2].Date.Should().Be(DateTime.Today.AddDays(-100));
        buysGroup.Items[2].Amount.Should().Be(600m, "buy2 amount must be absolute (positive)");
        buysGroup.Items[2].Note.Should().Contain("Anteile");

        // standalone fee at the end
        buysGroup.Items[3].Date.Should().Be(DateTime.Today.AddDays(-50));
        buysGroup.Items[3].Amount.Should().Be(5m, "standalone fee amount must be absolute (positive)");
        buysGroup.Items[3].Note.Should().Be("Gebühr (ohne Kaufzuordnung)");

        buysGroup.GroupTotal.Should().Be(1_115m, "group total must equal sum of all buy and fee amounts");

        // Assert – TotalReturn Group[1] "Investiertes Kapital" (same items but negated; market value is now Group[0])
        var tr = result.First(b => b.KpiKey == "TotalReturn");

        // Verify group order: [0] Marktwert, [1] Investiertes Kapital, [2] Dividenden (netto), [3] Gesamtrendite
        tr.Groups[0].GroupName.Should().Be("Aktueller Marktwert");
        tr.Groups[1].GroupName.Should().Be("Investiertes Kapital");
        tr.Groups[2].GroupName.Should().Be("Dividenden (netto)");
        tr.Groups[3].GroupName.Should().Be("Gesamtrendite");

        var icGroupTr = tr.Groups[1];
        icGroupTr.IsPositiveContribution.Should().BeFalse();
        icGroupTr.Items.Should().HaveCount(4,
            "two buys + one linked fee + one standalone fee must each appear as a negative item");

        icGroupTr.Items[0].Amount.Should().Be(-500m, "buy1 must appear as negative outflow");
        icGroupTr.Items[1].Amount.Should().Be(-10m, "linked fee must appear as negative outflow");
        icGroupTr.Items[2].Amount.Should().Be(-600m, "buy2 must appear as negative outflow");
        icGroupTr.Items[3].Amount.Should().Be(-5m, "standalone fee must appear as negative outflow");

        // Group[0]: current market value as separate positive group
        tr.Groups[0].Items.Should().HaveCount(1, "exactly one item: current market value");
        tr.Groups[0].Items[0].Amount.Should().BeGreaterThanOrEqualTo(0m, "market value must be positive");
        tr.Groups[0].Items[0].Note.Should().Contain("Anteile", "note must reference share count and price");
    }
}
