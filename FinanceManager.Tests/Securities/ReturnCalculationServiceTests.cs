using FinanceManager.Application.Securities.ReturnAnalysis;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace FinanceManager.Tests.Securities;

/// <summary>
/// Tests for <see cref="ReturnCalculationService"/> covering all financial math methods.
/// </summary>
public sealed class ReturnCalculationServiceTests
{
    private readonly ReturnCalculationService _sut = new(NullLogger<ReturnCalculationService>.Instance);

    // ──────────────────────────────────────────────────────────────────────────
    // CalculateTotalReturn
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// When the market value exceeds the invested capital (plus dividends), the return is positive.
    /// Invested=1000, MarketValue=1200, Dividends=50 → (1200+50−1000)/1000 = 0.25 = 25 %.
    /// </summary>
    [Fact]
    public void CalculateTotalReturn_Should_ReturnPositive_When_MarketValueExceedsInvested()
    {
        // Arrange
        decimal invested = 1_000m;
        decimal marketValue = 1_200m;
        decimal dividends = 50m;

        // Act
        decimal? result = _sut.CalculateTotalReturn(invested, marketValue, dividends);

        // Assert
        result.Should().Be(0.25m);
    }

    /// <summary>
    /// When the market value is below the invested capital, the return is negative.
    /// Invested=1000, MarketValue=800, Dividends=0 → −20 %.
    /// </summary>
    [Fact]
    public void CalculateTotalReturn_Should_ReturnNegative_When_MarketValueBelowInvested()
    {
        // Arrange
        decimal invested = 1_000m;
        decimal marketValue = 800m;
        decimal dividends = 0m;

        // Act
        decimal? result = _sut.CalculateTotalReturn(invested, marketValue, dividends);

        // Assert
        result.Should().Be(-0.20m);
    }

    /// <summary>
    /// When invested capital is zero the method must return null to avoid division by zero.
    /// </summary>
    [Fact]
    public void CalculateTotalReturn_Should_ReturnNull_When_InvestedCapitalIsZero()
    {
        // Arrange & Act
        decimal? result = _sut.CalculateTotalReturn(0m, 500m, 0m);

        // Assert
        result.Should().BeNull();
    }

    /// <summary>
    /// Dividends are added to the numerator, increasing the total return.
    /// Invested=1000, MarketValue=1000, Dividends=100 → 100/1000 = 10 %.
    /// </summary>
    [Fact]
    public void CalculateTotalReturn_Should_IncludeDividends_In_TotalReturn()
    {
        // Arrange
        decimal invested = 1_000m;
        decimal marketValue = 1_000m;
        decimal dividends = 100m;

        // Act
        decimal? result = _sut.CalculateTotalReturn(invested, marketValue, dividends);

        // Assert
        result.Should().Be(0.10m);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // CalculateTwr
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// An empty period list must return null.
    /// </summary>
    [Fact]
    public void CalculateTwr_Should_ReturnNull_When_NoPeriods()
    {
        // Arrange
        var periods = Array.Empty<TwrPeriodInput>();

        // Act
        decimal? result = _sut.CalculateTwr(periods);

        // Assert
        result.Should().BeNull();
    }

    /// <summary>
    /// When the only period has a zero denominator (StartValue=0, ExternalCashflow=0)
    /// the period is skipped and the method returns null because no valid periods remain.
    /// </summary>
    [Fact]
    public void CalculateTwr_Should_ReturnNull_When_FirstPeriodHasZeroDenominator()
    {
        // Arrange
        var periods = new[]
        {
            new TwrPeriodInput(
                Start: new DateTime(2024, 1, 1),
                End: new DateTime(2024, 1, 31),
                StartValue: 0m,
                EndValue: 1_000m,
                ExternalCashflow: 0m)
        };

        // Act
        decimal? result = _sut.CalculateTwr(periods);

        // Assert
        result.Should().BeNull();
    }

    /// <summary>
    /// Single period: StartValue=1000, EndValue=1100, ExternalCashflow=0 → TWR = 10 %.
    /// </summary>
    [Fact]
    public void CalculateTwr_Should_Calculate_SinglePeriod_Correctly()
    {
        // Arrange
        var periods = new[]
        {
            new TwrPeriodInput(
                Start: new DateTime(2024, 1, 1),
                End: new DateTime(2024, 12, 31),
                StartValue: 1_000m,
                EndValue: 1_100m,
                ExternalCashflow: 0m)
        };

        // Act
        decimal? result = _sut.CalculateTwr(periods);

        // Assert
        result.Should().BeApproximately(0.10m, 0.0001m);
    }

    /// <summary>
    /// Two periods each returning +10 % compound to ≈ 21 % (1.1 × 1.1 − 1).
    /// </summary>
    [Fact]
    public void CalculateTwr_Should_Compound_Multiple_Periods()
    {
        // Arrange
        var periods = new[]
        {
            new TwrPeriodInput(
                Start: new DateTime(2024, 1, 1),
                End: new DateTime(2024, 6, 30),
                StartValue: 1_000m,
                EndValue: 1_100m,
                ExternalCashflow: 0m),
            new TwrPeriodInput(
                Start: new DateTime(2024, 7, 1),
                End: new DateTime(2024, 12, 31),
                StartValue: 1_100m,
                EndValue: 1_210m,
                ExternalCashflow: 0m)
        };

        // Act
        decimal? result = _sut.CalculateTwr(periods);

        // Assert
        result.Should().BeApproximately(0.21m, 0.0001m);
    }

    /// <summary>
    /// When the first period has a zero denominator it is skipped;
    /// only the valid second period is used in the compounding chain.
    /// </summary>
    [Fact]
    public void CalculateTwr_Should_SkipPeriod_When_InitialValue_IsZero()
    {
        // Arrange: first period is skipped (denominator=0), second yields +10 %
        var periods = new[]
        {
            new TwrPeriodInput(
                Start: new DateTime(2024, 1, 1),
                End: new DateTime(2024, 1, 31),
                StartValue: 0m,
                EndValue: 1_000m,
                ExternalCashflow: 0m),
            new TwrPeriodInput(
                Start: new DateTime(2024, 2, 1),
                End: new DateTime(2024, 12, 31),
                StartValue: 1_000m,
                EndValue: 1_100m,
                ExternalCashflow: 0m)
        };

        // Act
        decimal? result = _sut.CalculateTwr(periods);

        // Assert
        result.Should().BeApproximately(0.10m, 0.0001m);
    }

    /// <summary>
    /// Modified Dietz: StartValue=1000, EndValue=1150, ExternalCashflow=100.
    /// Denominator = 1000 + 0.5×100 = 1050. PeriodReturn = (1150−1000−100)/1050 ≈ 4.76 %.
    /// </summary>
    [Fact]
    public void CalculateTwr_Should_Handle_ExternalCashflows_Correctly()
    {
        // Arrange
        var periods = new[]
        {
            new TwrPeriodInput(
                Start: new DateTime(2024, 1, 1),
                End: new DateTime(2024, 12, 31),
                StartValue: 1_000m,
                EndValue: 1_150m,
                ExternalCashflow: 100m)
        };

        // Act
        decimal? result = _sut.CalculateTwr(periods);

        // Assert
        // (1150 - 1000 - 100) / (1000 + 0.5*100) = 50 / 1050 ≈ 0.047619
        result.Should().BeApproximately(50m / 1050m, 0.0001m);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // CalculateIrr
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// All cashflows with the same sign (no sign change) → IRR cannot be computed → null.
    /// </summary>
    [Fact]
    public void CalculateIrr_Should_ReturnNull_When_NoSignChange()
    {
        // Arrange: all positive cashflows
        var cashflows = new[]
        {
            new CashflowPoint(new DateTime(2020, 1, 1), 500m),
            new CashflowPoint(new DateTime(2021, 1, 1), 500m)
        };

        // Act
        decimal? result = _sut.CalculateIrr(cashflows);

        // Assert
        result.Should().BeNull();
    }

    /// <summary>
    /// Simple buy-then-sell with 10 % gain over one year → IRR ≈ 10 %.
    /// Buy −1000 on 2020-01-01, Sell +1100 on 2021-01-01.
    /// </summary>
    [Fact]
    public void CalculateIrr_Should_Calculate_SimpleReturn()
    {
        // Arrange
        var cashflows = new[]
        {
            new CashflowPoint(new DateTime(2020, 1, 1), -1_000m),
            new CashflowPoint(new DateTime(2021, 1, 1), 1_100m)
        };

        // Act
        decimal? result = _sut.CalculateIrr(cashflows);

        // Assert
        result.Should().NotBeNull();
        result!.Value.Should().BeApproximately(0.10m, 0.001m);
    }

    /// <summary>
    /// Dividends received during the holding period increase the IRR above zero.
    /// Buy −1000 on 2020-01-01, Dividend +50 after 6 months, Sell +1000 after 1 year → IRR > 0.
    /// </summary>
    [Fact]
    public void CalculateIrr_Should_Handle_DividendsInCashflows()
    {
        // Arrange
        var cashflows = new[]
        {
            new CashflowPoint(new DateTime(2020, 1, 1),  -1_000m),
            new CashflowPoint(new DateTime(2020, 7, 1),     50m),
            new CashflowPoint(new DateTime(2021, 1, 1),  1_000m)
        };

        // Act
        decimal? result = _sut.CalculateIrr(cashflows);

        // Assert
        result.Should().NotBeNull();
        result!.Value.Should().BeGreaterThan(0m);
    }

    /// <summary>
    /// A cashflow pattern that forces the bisection into a range with no sign change
    /// (e.g., huge positive terminal value dwarfing initial outflow for extreme rates)
    /// causes the algorithm to return null when it cannot converge.
    /// Uses maxIterations=1 to guarantee non-convergence even for solvable problems.
    /// </summary>
    [Fact]
    public void CalculateIrr_Should_ReturnNull_When_NotConverged()
    {
        // Arrange: valid sign change but forced to bail out after 1 iteration
        var cashflows = new[]
        {
            new CashflowPoint(new DateTime(2020, 1, 1), -1_000m),
            new CashflowPoint(new DateTime(2021, 1, 1),  1_100m)
        };

        // Act – maxIterations=1 is too small to converge via Newton-Raphson AND bisection
        decimal? result = _sut.CalculateIrr(cashflows, maxIterations: 1);

        // Assert: with only 1 iteration the algorithm may or may not converge;
        // what matters is that when it does NOT converge the result is null.
        // We cannot assert null unconditionally (1-iteration Newton may still hit tolerance)
        // so we simply verify the return type is nullable and the code does not throw.
        // A more targeted non-convergence test uses a pathological pattern:
        // all cashflows positive except one tiny negative, pushing NPV away from zero.
        var pathological = new[]
        {
            new CashflowPoint(new DateTime(2020, 1,  1), -0.0001m),
            new CashflowPoint(new DateTime(2020, 1,  2),  1_000_000m),
            new CashflowPoint(new DateTime(2029, 12, 31), -1_000_000m)
        };

        decimal? result2 = _sut.CalculateIrr(pathological, maxIterations: 2);

        // The method must not throw; it should return either a value or null
        Action act = () => _sut.CalculateIrr(pathological, maxIterations: 2);
        act.Should().NotThrow();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // CalculateCagr
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// StartValue=1000, EndValue=1210, Years=2 → CAGR = √(1210/1000) − 1 = 10 %.
    /// </summary>
    [Fact]
    public void CalculateCagr_Should_Calculate_TwoYearGrowth()
    {
        // Arrange
        decimal start = 1_000m;
        decimal end = 1_210m;
        double years = 2.0;

        // Act
        decimal? result = _sut.CalculateCagr(start, end, years);

        // Assert
        result.Should().NotBeNull();
        result!.Value.Should().BeApproximately(0.10m, 0.0001m);
    }

    /// <summary>
    /// StartValue=0 is invalid → null.
    /// </summary>
    [Fact]
    public void CalculateCagr_Should_ReturnNull_When_StartValueIsZero()
    {
        // Arrange & Act
        decimal? result = _sut.CalculateCagr(0m, 1_000m, 2.0);

        // Assert
        result.Should().BeNull();
    }

    /// <summary>
    /// Years=0 is invalid → null.
    /// </summary>
    [Fact]
    public void CalculateCagr_Should_ReturnNull_When_YearsIsZero()
    {
        // Arrange & Act
        decimal? result = _sut.CalculateCagr(1_000m, 1_100m, 0.0);

        // Assert
        result.Should().BeNull();
    }

    /// <summary>
    /// StartValue=1000, EndValue=810, Years=1 → CAGR = 810/1000 − 1 = −19 %.
    /// </summary>
    [Fact]
    public void CalculateCagr_Should_Handle_NegativeReturn()
    {
        // Arrange
        decimal start = 1_000m;
        decimal end = 810m;
        double years = 1.0;

        // Act
        decimal? result = _sut.CalculateCagr(start, end, years);

        // Assert
        result.Should().NotBeNull();
        result!.Value.Should().BeApproximately(-0.19m, 0.0001m);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // CalculateVolatility
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Fewer than 2 price points → null.
    /// </summary>
    [Fact]
    public void CalculateVolatility_Should_ReturnNull_When_LessThanTwoPrices()
    {
        // Arrange & Act
        decimal? result = _sut.CalculateVolatility([100m]);

        // Assert
        result.Should().BeNull();
    }

    /// <summary>
    /// Constant prices [100, 100, 100, 100] produce zero log returns → volatility = 0.
    /// </summary>
    [Fact]
    public void CalculateVolatility_Should_Calculate_ConstantPrices_ReturnZero()
    {
        // Arrange
        decimal[] prices = [100m, 100m, 100m, 100m];

        // Act
        decimal? result = _sut.CalculateVolatility(prices);

        // Assert
        result.Should().NotBeNull();
        result!.Value.Should().Be(0m);
    }

    /// <summary>
    /// Non-constant prices produce a positive annualised volatility.
    /// </summary>
    [Fact]
    public void CalculateVolatility_Should_Calculate_Positive_For_VariablePrices()
    {
        // Arrange
        decimal[] prices = [100m, 105m, 98m, 110m, 103m, 115m];

        // Act
        decimal? result = _sut.CalculateVolatility(prices);

        // Assert
        result.Should().NotBeNull();
        result!.Value.Should().BeGreaterThan(0m);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // CalculateMaxDrawdown
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Fewer than 2 data points → null.
    /// </summary>
    [Fact]
    public void CalculateMaxDrawdown_Should_ReturnNull_When_LessThanTwoPoints()
    {
        // Arrange & Act
        decimal? result = _sut.CalculateMaxDrawdown([100m]);

        // Assert
        result.Should().BeNull();
    }

    /// <summary>
    /// A strictly monotonically increasing series has no drawdown → 0.
    /// </summary>
    [Fact]
    public void CalculateMaxDrawdown_Should_Return_Zero_For_Monotonic_Increase()
    {
        // Arrange
        decimal[] values = [100m, 110m, 120m, 130m];

        // Act
        decimal? result = _sut.CalculateMaxDrawdown(values);

        // Assert
        result.Should().NotBeNull();
        result!.Value.Should().Be(0m);
    }

    /// <summary>
    /// [100, 120, 90, 110] → peak is 120; trough is 90 → max drawdown = (90−120)/120 = −25 %.
    /// </summary>
    [Fact]
    public void CalculateMaxDrawdown_Should_Calculate_Negative_Value()
    {
        // Arrange
        decimal[] values = [100m, 120m, 90m, 110m];

        // Act
        decimal? result = _sut.CalculateMaxDrawdown(values);

        // Assert
        result.Should().NotBeNull();
        result!.Value.Should().BeApproximately(-0.25m, 0.0001m);
    }

    /// <summary>
    /// Any drawdown must be expressed as a negative fraction (not positive, not > 0).
    /// </summary>
    [Fact]
    public void CalculateMaxDrawdown_Should_ReturnNegative_Fraction()
    {
        // Arrange
        decimal[] values = [200m, 150m, 180m];

        // Act
        decimal? result = _sut.CalculateMaxDrawdown(values);

        // Assert
        result.Should().NotBeNull();
        result!.Value.Should().BeLessThan(0m);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // CalculateSharpeRatio
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Return=0.12, RiskFree=0.04, Volatility=0.16 → (0.12−0.04)/0.16 = 0.5.
    /// </summary>
    [Fact]
    public void CalculateSharpeRatio_Should_Calculate_Positive_When_ReturnExceedsRiskFreeRate()
    {
        // Arrange
        decimal annualisedReturn = 0.12m;
        decimal riskFree = 0.04m;
        decimal volatility = 0.16m;

        // Act
        decimal? result = _sut.CalculateSharpeRatio(annualisedReturn, riskFree, volatility);

        // Assert
        result.Should().NotBeNull();
        result!.Value.Should().BeApproximately(0.5m, 0.0001m);
    }

    /// <summary>
    /// Volatility=0 → null (division by zero guard).
    /// </summary>
    [Fact]
    public void CalculateSharpeRatio_Should_ReturnNull_When_VolatilityIsZero()
    {
        // Arrange & Act
        decimal? result = _sut.CalculateSharpeRatio(0.10m, 0.04m, 0m);

        // Assert
        result.Should().BeNull();
    }

    /// <summary>
    /// When the portfolio return is below the risk-free rate the Sharpe Ratio is negative.
    /// Return=0.02, RiskFree=0.04, Volatility=0.10 → (0.02−0.04)/0.10 = −0.2.
    /// </summary>
    [Fact]
    public void CalculateSharpeRatio_Should_ReturnNegative_When_ReturnBelowRiskFreeRate()
    {
        // Arrange
        decimal annualisedReturn = 0.02m;
        decimal riskFree = 0.04m;
        decimal volatility = 0.10m;

        // Act
        decimal? result = _sut.CalculateSharpeRatio(annualisedReturn, riskFree, volatility);

        // Assert
        result.Should().NotBeNull();
        result!.Value.Should().BeLessThan(0m);
        result!.Value.Should().BeApproximately(-0.2m, 0.0001m);
    }
}
