using FinanceManager.Application.Securities.ReturnAnalysis;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace FinanceManager.Tests.Securities;

/// <summary>
/// Tests for <see cref="FifoCostBasisCalculator"/> covering FIFO cost basis calculations.
/// </summary>
public sealed class FifoCostBasisCalculatorTests
{
    private readonly FifoCostBasisCalculator _sut = new(NullLogger<FifoCostBasisCalculator>.Instance);

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    private static SecurityTransaction Buy(DateTime date, decimal amount, decimal quantity, Guid? groupId = null)
        => new(Guid.NewGuid(), date, SecurityPostingSubType.Buy, -Math.Abs(amount), quantity, groupId ?? Guid.NewGuid());

    private static SecurityTransaction Sell(DateTime date, decimal amount, decimal quantity)
        => new(Guid.NewGuid(), date, SecurityPostingSubType.Sell, Math.Abs(amount), quantity, Guid.NewGuid());

    private static SecurityTransaction Dividend(DateTime date, decimal amount)
        => new(Guid.NewGuid(), date, SecurityPostingSubType.Dividend, Math.Abs(amount), null, Guid.NewGuid());

    private static SecurityTransaction Tax(DateTime date, decimal amount)
        => new(Guid.NewGuid(), date, SecurityPostingSubType.Tax, -Math.Abs(amount), null, Guid.NewGuid());

    private static SecurityTransaction Fee(DateTime date, decimal amount, Guid? groupId = null)
        => new(Guid.NewGuid(), date, SecurityPostingSubType.Fee, -Math.Abs(amount), null, groupId ?? Guid.NewGuid());

    // ──────────────────────────────────────────────────────────────────────────
    // Empty / null inputs
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// An empty transaction list must return a zeroed result with no lots and no warning.
    /// </summary>
    [Fact]
    public void Calculate_Should_ReturnEmpty_For_NoTransactions()
    {
        // Arrange
        var transactions = Array.Empty<SecurityTransaction>();

        // Act
        FifoCostBasisResult result = _sut.Calculate(transactions);

        // Assert
        result.TotalCostBasis.Should().Be(0m);
        result.RealizedGains.Should().Be(0m);
        result.RemainingLots.Should().BeEmpty();
        result.TotalSharesHeld.Should().Be(0m);
        result.HasOversellWarning.Should().BeFalse();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Single Buy
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A single buy of 10 shares at €100 each sets TotalCostBasis=1000 and creates one lot.
    /// </summary>
    [Fact]
    public void Calculate_Should_SetTotalCostBasis_For_SingleBuy()
    {
        // Arrange
        var date = new DateTime(2024, 1, 15);
        var transactions = new[] { Buy(date, 1_000m, 10m) };

        // Act
        FifoCostBasisResult result = _sut.Calculate(transactions);

        // Assert
        result.TotalCostBasis.Should().Be(1_000m);
        result.TotalSharesHeld.Should().Be(10m);
        result.RemainingLots.Should().HaveCount(1);
        result.RemainingLots[0].Quantity.Should().Be(10m);
        result.RemainingLots[0].CostPerUnit.Should().Be(100m);
        result.RealizedGains.Should().Be(0m);
        result.HasOversellWarning.Should().BeFalse();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // FIFO ordering after partial sell
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Buy 10@100, Buy 5@120, Sell 8 → FIFO removes first 8 shares from lot1.
    /// Remaining: 2@100 + 5@120.
    /// </summary>
    [Fact]
    public void Calculate_Should_ApplyFifoCostBasis_After_PartialSell()
    {
        // Arrange
        var d1 = new DateTime(2024, 1, 1);
        var d2 = new DateTime(2024, 2, 1);
        var d3 = new DateTime(2024, 3, 1);

        var transactions = new[]
        {
            Buy(d1,  1_000m, 10m),   // lot1: 10 @ 100
            Buy(d2,    600m,  5m),   // lot2:  5 @ 120
            Sell(d3, 1_000m,  8m)    // sell 8 (FIFO: all from lot1)
        };

        // Act
        FifoCostBasisResult result = _sut.Calculate(transactions);

        // Assert
        result.TotalSharesHeld.Should().Be(7m);   // 2 + 5
        result.RemainingLots.Should().HaveCount(2);

        // First remaining lot: 2 shares from the original 10@100 lot
        FifoLot firstLot = result.RemainingLots[0];
        firstLot.Quantity.Should().Be(2m);
        firstLot.CostPerUnit.Should().BeApproximately(100m, 0.01m);

        // Second remaining lot: 5 shares from the 5@120 lot
        FifoLot secondLot = result.RemainingLots[1];
        secondLot.Quantity.Should().Be(5m);
        secondLot.CostPerUnit.Should().BeApproximately(120m, 0.01m);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Realized gain calculation
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Buy 10@100, Sell 10@120 → RealizedGains = (120−100)×10 = 200.
    /// </summary>
    [Fact]
    public void Calculate_Should_CalculateRealizedGain_On_Sell()
    {
        // Arrange
        var d1 = new DateTime(2024, 1, 1);
        var d2 = new DateTime(2024, 6, 1);

        var transactions = new[]
        {
            Buy(d1,  1_000m, 10m),   // cost = 1000
            Sell(d2, 1_200m, 10m)    // proceeds = 1200
        };

        // Act
        FifoCostBasisResult result = _sut.Calculate(transactions);

        // Assert
        result.RealizedGains.Should().Be(200m);
        result.TotalSharesHeld.Should().Be(0m);
        result.RemainingLots.Should().BeEmpty();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Fee linked to buy via GroupId
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A Fee with the same GroupId as a Buy is added to that lot's cost basis.
    /// Buy 10@100 (cost=1000) on day 1 + Fee 20 with same groupId on day 2 → lot cost = 1020, CostPerUnit = 102.
    /// The fee is placed on a later date to guarantee it is processed after the buy (FIFO sort order).
    /// </summary>
    [Fact]
    public void Calculate_Should_AddFeeToLotCostBasis_When_SameGroupId()
    {
        // Arrange
        var groupId = Guid.NewGuid();
        var buyDate = new DateTime(2024, 1, 15);
        var feeDate = new DateTime(2024, 1, 16); // later date → processed after Buy

        var transactions = new[]
        {
            Buy(buyDate, 1_000m, 10m, groupId),
            Fee(feeDate,    20m,      groupId)
        };

        // Act
        FifoCostBasisResult result = _sut.Calculate(transactions);

        // Assert
        result.TotalCostBasis.Should().Be(1_020m);
        result.RemainingLots.Should().HaveCount(1);
        result.RemainingLots[0].CostPerUnit.Should().Be(102m);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Oversell warning
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Selling more shares than are held sets HasOversellWarning=true and clamps shares to 0.
    /// Buy 5, Sell 10 → HasOversellWarning = true, TotalSharesHeld = 0.
    /// </summary>
    [Fact]
    public void Calculate_Should_HandleOversell_WithWarning()
    {
        // Arrange
        var d1 = new DateTime(2024, 1, 1);
        var d2 = new DateTime(2024, 6, 1);

        var transactions = new[]
        {
            Buy(d1, 500m, 5m),
            Sell(d2, 1_200m, 10m)
        };

        // Act
        FifoCostBasisResult result = _sut.Calculate(transactions);

        // Assert
        result.HasOversellWarning.Should().BeTrue();
        result.OversellWarningMessage.Should().NotBeNullOrWhiteSpace();
        result.TotalSharesHeld.Should().Be(0m);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Dividends and Tax are ignored in cost basis
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Dividends and taxes must not change the cost basis or the lot count.
    /// Buy 10@100, Dividend 50, Tax −10 → cost basis unchanged at 1000.
    /// </summary>
    [Fact]
    public void Calculate_Should_IgnoreDividendsAndTax_InCostBasis()
    {
        // Arrange
        var d1 = new DateTime(2024, 1, 1);
        var d2 = new DateTime(2024, 6, 1);
        var d3 = new DateTime(2024, 6, 2);

        var transactions = new[]
        {
            Buy(d1, 1_000m, 10m),
            Dividend(d2, 50m),
            Tax(d3, 10m)
        };

        // Act
        FifoCostBasisResult result = _sut.Calculate(transactions);

        // Assert
        result.TotalCostBasis.Should().Be(1_000m);
        result.TotalSharesHeld.Should().Be(10m);
        result.RemainingLots.Should().HaveCount(1);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Sorting: same-date transactions use Id as tiebreaker
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// When two Buy transactions share the same date, the FIFO queue is filled
    /// in ascending Id order (deterministic), and a subsequent Sell removes shares
    /// from the correct first-in lot.
    /// </summary>
    [Fact]
    public void Calculate_Should_SortByDate_ThenById()
    {
        // Arrange: two buys on the same date but with explicitly ordered GUIDs
        var sameDate = new DateTime(2024, 3, 1);

        // Create Ids such that idFirst < idSecond lexicographically
        var idFirst  = new Guid("00000000-0000-0000-0000-000000000001");
        var idSecond = new Guid("00000000-0000-0000-0000-000000000002");

        var buy1 = new SecurityTransaction(idFirst,  sameDate, SecurityPostingSubType.Buy, -500m,  5m, Guid.NewGuid());
        var buy2 = new SecurityTransaction(idSecond, sameDate, SecurityPostingSubType.Buy, -600m,  5m, Guid.NewGuid());
        var sell = Sell(sameDate.AddDays(1), 600m, 5m);

        var transactions = new[] { buy2, buy1, sell }; // deliberately misordered

        // Act
        FifoCostBasisResult result = _sut.Calculate(transactions);

        // Assert: FIFO consumed the first lot (idFirst, cost=500); second lot (cost=600) remains
        result.TotalSharesHeld.Should().Be(5m);
        result.RemainingLots.Should().HaveCount(1);
        result.RemainingLots[0].CostPerUnit.Should().BeApproximately(120m, 0.01m); // 600/5
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Multiple partial sells
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Buy 10@100, Sell 5@110, Sell 5@120 → realized gains = 5×10 + 5×20 = 150.
    /// </summary>
    [Fact]
    public void Calculate_Should_Handle_MultipleFullSells()
    {
        // Arrange
        var d1 = new DateTime(2024, 1, 1);
        var d2 = new DateTime(2024, 4, 1);
        var d3 = new DateTime(2024, 8, 1);

        var transactions = new[]
        {
            Buy(d1,  1_000m, 10m),   // cost 1000 (10@100)
            Sell(d2,   550m,  5m),   // proceeds 550, cost-of-sold 500 → gain 50
            Sell(d3,   600m,  5m)    // proceeds 600, cost-of-sold 500 → gain 100
        };

        // Act
        FifoCostBasisResult result = _sut.Calculate(transactions);

        // Assert
        result.RealizedGains.Should().Be(150m);
        result.TotalSharesHeld.Should().Be(0m);
        result.RemainingLots.Should().BeEmpty();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Full sell leaves zero shares
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// After a full sell, no shares are held and no lots remain.
    /// </summary>
    [Fact]
    public void Calculate_Should_ReturnZeroSharesHeld_After_FullSell()
    {
        // Arrange
        var d1 = new DateTime(2024, 1, 1);
        var d2 = new DateTime(2024, 12, 31);

        var transactions = new[]
        {
            Buy(d1,  1_000m, 10m),
            Sell(d2, 1_000m, 10m)
        };

        // Act
        FifoCostBasisResult result = _sut.Calculate(transactions);

        // Assert
        result.TotalSharesHeld.Should().Be(0m);
        result.RemainingLots.Should().BeEmpty();
        result.HasOversellWarning.Should().BeFalse();
    }
}
