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

    // ──────────────────────────────────────────────────────────────────────────
    // Edge cases: null / zero quantity on Buy and Sell
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A buy transaction with a null quantity must not create a lot.
    /// The result should have zero shares held and empty lots.
    /// </summary>
    [Fact]
    public void Calculate_Should_NotCreateLot_When_BuyQuantityIsNull()
    {
        // Arrange
        var tx = new SecurityTransaction(Guid.NewGuid(), DateTime.Today, SecurityPostingSubType.Buy, -1_000m, null, Guid.NewGuid());

        // Act
        FifoCostBasisResult result = _sut.Calculate([tx]);

        // Assert
        result.RemainingLots.Should().BeEmpty();
        result.TotalSharesHeld.Should().Be(0m);
        result.TotalCostBasis.Should().Be(0m);
    }

    /// <summary>
    /// A buy transaction with quantity zero must not create a lot.
    /// </summary>
    [Fact]
    public void Calculate_Should_NotCreateLot_When_BuyQuantityIsZero()
    {
        // Arrange
        var tx = new SecurityTransaction(Guid.NewGuid(), DateTime.Today, SecurityPostingSubType.Buy, -1_000m, 0m, Guid.NewGuid());

        // Act
        FifoCostBasisResult result = _sut.Calculate([tx]);

        // Assert
        result.RemainingLots.Should().BeEmpty();
        result.TotalSharesHeld.Should().Be(0m);
    }

    /// <summary>
    /// A sell transaction with a null quantity must be silently skipped.
    /// </summary>
    [Fact]
    public void Calculate_Should_SkipSell_When_SellQuantityIsNull()
    {
        // Arrange: buy 10 shares, then attempt to sell with null quantity
        var buy  = Buy(DateTime.Today.AddDays(-1), 1_000m, 10m);
        var sell = new SecurityTransaction(Guid.NewGuid(), DateTime.Today, SecurityPostingSubType.Sell, 1_000m, null, Guid.NewGuid());

        // Act
        FifoCostBasisResult result = _sut.Calculate([buy, sell]);

        // Assert – sell is skipped; 10 shares still held
        result.TotalSharesHeld.Should().Be(10m);
        result.HasOversellWarning.Should().BeFalse();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Edge cases: fee without matching lot
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A fee whose GroupId does not match any buy lot must be silently skipped
    /// without affecting cost basis.
    /// </summary>
    [Fact]
    public void Calculate_Should_NotCrash_When_FeeHasNoMatchingBuyLot()
    {
        // Arrange – fee with a random GroupId; no matching buy
        var fee = Fee(DateTime.Today, 10m, Guid.NewGuid());

        // Act
        FifoCostBasisResult result = _sut.Calculate([fee]);

        // Assert
        result.TotalCostBasis.Should().Be(0m);
        result.RemainingLots.Should().BeEmpty();
    }

    /// <summary>
    /// A fee linked to a fully-sold lot must not crash.
    /// The lot is gone after the sell, but the fee arrives later.
    /// </summary>
    [Fact]
    public void Calculate_Should_NotCrash_When_FeeLinkedToFullySoldLot()
    {
        // Arrange: Buy 10 → Sell 10 → Fee referencing same GroupId as Buy
        var groupId = Guid.NewGuid();
        var buy  = Buy(DateTime.Today.AddDays(-2), 1_000m, 10m, groupId);
        var sell = Sell(DateTime.Today.AddDays(-1), 1_000m, 10m);
        var fee  = Fee(DateTime.Today, 5m, groupId);

        // Act
        FifoCostBasisResult result = _sut.Calculate([buy, sell, fee]);

        // Assert – no crash; all lots consumed; no oversell warning
        result.RemainingLots.Should().BeEmpty();
        result.TotalSharesHeld.Should().Be(0m);
        result.HasOversellWarning.Should().BeFalse();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Multiple fees on the same GroupId
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Multiple fees referencing the same buy lot via GroupId must accumulate on the lot's cost basis.
    /// Buy 10 @ 100 = 1000 + fee1 (5) + fee2 (3) → TotalCostBasis = 1008.
    /// </summary>
    [Fact]
    public void Calculate_Should_AccumulateFees_When_MultipleFeesShareSameGroupId()
    {
        // Arrange – buy on an earlier date so it is always sorted before the fees
        var groupId = Guid.NewGuid();
        var buy  = Buy(DateTime.Today.AddDays(-1), 1_000m, 10m, groupId);
        var fee1 = Fee(DateTime.Today, 5m, groupId);
        var fee2 = Fee(DateTime.Today, 3m, groupId);

        // Act
        FifoCostBasisResult result = _sut.Calculate([buy, fee1, fee2]);

        // Assert – 1000 + 5 + 3 = 1008
        result.TotalCostBasis.Should().Be(1_008m);
        result.TotalSharesHeld.Should().Be(10m);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Oversell across multiple lots
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A sell that exceeds all available lots must set the oversell warning flag.
    /// After an oversell, TotalSharesHeld must not go below zero.
    /// </summary>
    [Fact]
    public void Calculate_Should_SetOversellWarning_When_SellExceedsAllLots()
    {
        // Arrange: buy 10 + buy 5 = 15 total; sell 20 = oversell
        var buy1 = Buy(DateTime.Today.AddDays(-2), 1_000m, 10m);
        var buy2 = Buy(DateTime.Today.AddDays(-1),   500m,  5m);
        var sell = Sell(DateTime.Today, 2_000m, 20m);

        // Act
        FifoCostBasisResult result = _sut.Calculate([buy1, buy2, sell]);

        // Assert
        result.HasOversellWarning.Should().BeTrue();
        result.OversellWarningMessage.Should().NotBeNullOrEmpty();
        result.TotalSharesHeld.Should().Be(0m);
        result.RemainingLots.Should().BeEmpty();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Buy after full sell
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// After a complete sell-out (0 shares remaining), a new buy creates a fresh lot
    /// as if starting from scratch.
    /// </summary>
    [Fact]
    public void Calculate_Should_CreateNewLot_When_BuyAfterFullSell()
    {
        // Arrange: buy 10 → sell 10 → buy 5
        var buy1 = Buy(DateTime.Today.AddDays(-2), 1_000m, 10m);
        var sell = Sell(DateTime.Today.AddDays(-1), 1_000m, 10m);
        var buy2 = Buy(DateTime.Today, 600m, 5m);

        // Act
        FifoCostBasisResult result = _sut.Calculate([buy1, sell, buy2]);

        // Assert
        result.TotalSharesHeld.Should().Be(5m);
        result.RemainingLots.Should().HaveCount(1);
        result.TotalCostBasis.Should().Be(600m);
        result.HasOversellWarning.Should().BeFalse();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Same-date sort: Buy-before-Sell when Buy Id is smaller
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// When a buy and a sell share the same date, the buy (lower Id) is processed
    /// first due to the ThenBy(Id) sort — so the sell does not trigger an oversell warning.
    /// </summary>
    [Fact]
    public void Calculate_Should_ProcessBuyBeforeSell_When_SameDateAndBuyIdIsSmaller()
    {
        // Arrange – deterministic GUIDs: buy Id < sell Id → buy sorts first
        var buyId  = new Guid("00000000-0000-0000-0000-000000000001");
        var sellId = new Guid("00000000-0000-0000-0000-000000000002");
        var date   = new DateTime(2024, 6, 1);

        var buy  = new SecurityTransaction(buyId,  date, SecurityPostingSubType.Buy,  -1_000m, 10m, Guid.NewGuid());
        var sell = new SecurityTransaction(sellId, date, SecurityPostingSubType.Sell,    900m,  8m, Guid.NewGuid());

        // Act – deliberately pass in reversed order; the sorter corrects it
        FifoCostBasisResult result = _sut.Calculate([sell, buy]);

        // Assert
        result.TotalSharesHeld.Should().Be(2m);
        result.HasOversellWarning.Should().BeFalse();
    }
}
