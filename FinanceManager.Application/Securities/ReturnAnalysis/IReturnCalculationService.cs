namespace FinanceManager.Application.Securities.ReturnAnalysis;

/// <summary>
/// Pure financial mathematics service. All methods are stateless and thread-safe.
/// No database access. All formulas are documented in XML comments.
/// </summary>
public interface IReturnCalculationService
{
    /// <summary>
    /// Calculates total return percentage.
    /// Formula: (MarketValue + NetDividends - InvestedCapital) / InvestedCapital
    /// Returns null when InvestedCapital is zero.
    /// </summary>
    /// <param name="investedCapital">Total invested capital.</param>
    /// <param name="currentMarketValue">Current market value of holdings.</param>
    /// <param name="netDividends">Net dividends received (after taxes).</param>
    /// <returns>Total return as a fraction (e.g. 0.25 = 25%), or null when investedCapital is zero.</returns>
    decimal? CalculateTotalReturn(decimal investedCapital, decimal currentMarketValue, decimal netDividends);

    /// <summary>
    /// Calculates Time-Weighted Return (Modified Dietz, GIPS-konform) over linked periods.
    /// Each period return = (EndValue - StartValue - ExternalCashflow) / (StartValue + 0.5 × ExternalCashflow).
    /// Guard: When denominator = 0 (first purchase with zero prior holdings), the period is skipped (returns 0).
    /// TWR = product of (1 + period_return) - 1.
    /// Returns null when no valid periods exist.
    /// </summary>
    /// <param name="periods">Ordered list of TWR period inputs.</param>
    /// <returns>Time-Weighted Return as a fraction, or null when no valid periods exist.</returns>
    decimal? CalculateTwr(IReadOnlyList<TwrPeriodInput> periods);

    /// <summary>
    /// Calculates IRR (Internal Rate of Return / XIRR) via Newton-Raphson with Bisection fallback.
    /// Cashflow convention: negative = investment (outflow), positive = return (inflow).
    /// Day count: Actual/365 (XIRR standard).
    /// Bisection fallback interval: [-0.99, 10.0] when Newton-Raphson derivative is zero or NaN.
    /// Returns null when not converged after maxIterations or when cashflows have no sign change.
    /// </summary>
    /// <param name="cashflows">List of dated cashflows; must contain both positive and negative amounts.</param>
    /// <param name="maxIterations">Maximum Newton-Raphson or Bisection iterations (default 100).</param>
    /// <returns>Annualised IRR as a fraction, or null when not computable.</returns>
    decimal? CalculateIrr(IReadOnlyList<CashflowPoint> cashflows, int maxIterations = 100);

    /// <summary>
    /// Calculates CAGR (Compound Annual Growth Rate).
    /// Formula: (EndValue/StartValue)^(1/years) - 1
    /// Returns null when years &lt;= 0 or StartValue &lt;= 0.
    /// </summary>
    /// <param name="startValue">Value at the beginning of the period. Must be > 0.</param>
    /// <param name="endValue">Value at the end of the period.</param>
    /// <param name="years">Number of years in the holding period. Must be > 0.</param>
    /// <returns>CAGR as a fraction, or null when preconditions are not met.</returns>
    decimal? CalculateCagr(decimal startValue, decimal endValue, double years);

    /// <summary>
    /// Calculates annualised volatility as std. dev. of log daily returns × √252.
    /// Returns null when fewer than 2 data points.
    /// </summary>
    /// <param name="dailyPrices">Chronologically ordered daily price series.</param>
    /// <returns>Annualised volatility as a fraction, or null when insufficient data.</returns>
    decimal? CalculateVolatility(IReadOnlyList<decimal> dailyPrices);

    /// <summary>
    /// Calculates maximum drawdown from peak.
    /// Formula: min over time of (Value - Peak) / Peak
    /// Returns null when fewer than 2 data points.
    /// Result is a negative fraction (e.g. -0.25 = -25% drawdown).
    /// </summary>
    /// <param name="portfolioValues">Chronologically ordered portfolio value series.</param>
    /// <returns>Maximum drawdown as a negative fraction, or null when insufficient data.</returns>
    decimal? CalculateMaxDrawdown(IReadOnlyList<decimal> portfolioValues);

    /// <summary>
    /// Calculates Sharpe Ratio.
    /// Formula: (AnnualisedReturn - RiskFreeRate) / Volatility
    /// Returns null when volatility is zero or null.
    /// </summary>
    /// <param name="annualisedReturn">Annualised portfolio return (e.g. 0.12 = 12%).</param>
    /// <param name="riskFreeRate">Risk-free rate (e.g. 0.04 = 4%).</param>
    /// <param name="volatility">Annualised volatility. Must be > 0 for a meaningful result.</param>
    /// <returns>Sharpe Ratio, or null when volatility is zero or null.</returns>
    decimal? CalculateSharpeRatio(decimal annualisedReturn, decimal riskFreeRate, decimal volatility);

    /// <summary>
    /// Calculates dividend yield for a given period.
    /// Formula: TotalDividends / InvestedCapital
    /// Returns null when InvestedCapital is zero.
    /// </summary>
    /// <param name="totalDividends">Total dividends received in the period.</param>
    /// <param name="investedCapital">Total invested capital. Must be != 0 for a valid result.</param>
    /// <returns>Dividend yield as a fraction, or null when investedCapital is zero.</returns>
    decimal? CalculateDividendYield(decimal totalDividends, decimal investedCapital);

    /// <summary>
    /// Calculates tax rate as fraction of gross return.
    /// Formula: TotalTaxes / GrossReturn (absolute)
    /// Returns null when gross return is zero.
    /// </summary>
    /// <param name="totalTaxes">Total taxes paid (positive amount).</param>
    /// <param name="grossReturn">Gross return before taxes. Must be != 0 for a valid result.</param>
    /// <returns>Tax rate as a fraction (0..1), or null when gross return is zero.</returns>
    decimal? CalculateTaxRate(decimal totalTaxes, decimal grossReturn);
}
