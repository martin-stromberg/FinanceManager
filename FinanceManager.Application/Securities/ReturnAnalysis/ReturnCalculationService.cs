using Microsoft.Extensions.Logging;

namespace FinanceManager.Application.Securities.ReturnAnalysis;

/// <summary>
/// Implements pure financial mathematics calculations. Stateless, thread-safe, no DB access.
/// </summary>
public sealed class ReturnCalculationService : IReturnCalculationService
{
    private readonly ILogger<ReturnCalculationService> _logger;

    /// <summary>
    /// Annualisation factor for volatility (trading days per year).
    /// </summary>
    private const double TradingDaysPerYear = 252.0;

    /// <summary>
    /// Tolerance used for IRR convergence check.
    /// </summary>
    private const double IrrTolerance = 1e-7;

    /// <summary>
    /// Initializes a new instance of <see cref="ReturnCalculationService"/>.
    /// </summary>
    /// <param name="logger">Logger instance for diagnostic output.</param>
    public ReturnCalculationService(ILogger<ReturnCalculationService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public decimal? CalculateTotalReturn(decimal investedCapital, decimal currentMarketValue, decimal netDividends)
    {
        if (investedCapital == 0m)
        {
            _logger.LogDebug("CalculateTotalReturn: investedCapital is zero – returning null.");
            return null;
        }

        return (currentMarketValue + netDividends - investedCapital) / investedCapital;
    }

    /// <inheritdoc/>
    public decimal? CalculateTwr(IReadOnlyList<TwrPeriodInput> periods)
    {
        if (periods is null || periods.Count == 0)
            return null;

        double twr = 1.0;
        int validPeriods = 0;

        foreach (var period in periods)
        {
            double startValue = (double)period.StartValue;
            double endValue = (double)period.EndValue;
            double cashflow = (double)period.ExternalCashflow;

            // Modified Dietz denominator: StartValue + 0.5 × ExternalCashflow
            double denominator = startValue + 0.5 * cashflow;

            if (denominator == 0.0)
            {
                // Guard: skip period when denominator is zero (e.g. first purchase, no prior holdings)
                _logger.LogDebug(
                    "CalculateTwr: Skipping period {Start:d}–{End:d} – denominator is zero (first purchase or empty portfolio).",
                    period.Start, period.End);
                continue;
            }

            double periodReturn = (endValue - startValue - cashflow) / denominator;
            twr *= (1.0 + periodReturn);
            validPeriods++;
        }

        if (validPeriods == 0)
        {
            _logger.LogDebug("CalculateTwr: No valid periods found – returning null.");
            return null;
        }

        return (decimal)(twr - 1.0);
    }

    /// <inheritdoc/>
    public decimal? CalculateIrr(IReadOnlyList<CashflowPoint> cashflows, int maxIterations = 100)
    {
        if (cashflows is null || cashflows.Count < 2)
        {
            _logger.LogDebug("CalculateIrr: Fewer than 2 cashflows – returning null.");
            return null;
        }

        // Validate sign change (must have both positive and negative cashflows for convergence)
        bool hasPositive = false;
        bool hasNegative = false;
        foreach (var cf in cashflows)
        {
            if (cf.Amount > 0) hasPositive = true;
            if (cf.Amount < 0) hasNegative = true;
        }

        if (!hasPositive || !hasNegative)
        {
            _logger.LogDebug("CalculateIrr: No sign change in cashflows – IRR not computable.");
            return null;
        }

        DateTime t0 = cashflows[0].Date;

        // Convert cashflows to arrays for performance
        int n = cashflows.Count;
        double[] amounts = new double[n];
        double[] years = new double[n];

        for (int i = 0; i < n; i++)
        {
            amounts[i] = (double)cashflows[i].Amount;
            years[i] = (cashflows[i].Date - t0).TotalDays / 365.0;
        }

        // NPV function: Σ CF_i / (1 + r)^t_i
        double Npv(double r)
        {
            double result = 0.0;
            for (int i = 0; i < n; i++)
            {
                double denominator = Math.Pow(1.0 + r, years[i]);
                if (denominator == 0.0) return double.NaN;
                result += amounts[i] / denominator;
            }
            return result;
        }

        // NPV derivative: -Σ CF_i × t_i / (1 + r)^(t_i + 1)
        double NpvDerivative(double r)
        {
            double result = 0.0;
            for (int i = 0; i < n; i++)
            {
                if (years[i] == 0.0) continue;
                double denominator = Math.Pow(1.0 + r, years[i] + 1.0);
                if (denominator == 0.0) return double.NaN;
                result -= amounts[i] * years[i] / denominator;
            }
            return result;
        }

        // Newton-Raphson starting at 10%
        double rate = 0.1;
        for (int iter = 0; iter < maxIterations; iter++)
        {
            double npv = Npv(rate);
            double derivative = NpvDerivative(rate);

            if (double.IsNaN(npv) || double.IsNaN(derivative) || derivative == 0.0)
            {
                _logger.LogDebug("CalculateIrr: Newton-Raphson stalled at iteration {Iter} (derivative = {Derivative:G4}). Switching to Bisection.", iter, derivative);
                break;
            }

            double newRate = rate - npv / derivative;

            if (Math.Abs(newRate - rate) < IrrTolerance)
            {
                _logger.LogDebug("CalculateIrr: Newton-Raphson converged after {Iter} iterations. IRR = {Rate:P4}.", iter + 1, newRate);
                return (decimal)newRate;
            }

            rate = newRate;

            // Stay within reasonable bounds to avoid divergence
            if (rate < -0.99 || rate > 100.0)
            {
                _logger.LogDebug("CalculateIrr: Newton-Raphson diverged (rate = {Rate:G6}). Switching to Bisection.", rate);
                break;
            }
        }

        // Bisection fallback in [-0.99, 10.0]
        double lo = -0.99;
        double hi = 10.0;
        double npvLo = Npv(lo);
        double npvHi = Npv(hi);

        if (double.IsNaN(npvLo) || double.IsNaN(npvHi) || npvLo * npvHi > 0)
        {
            _logger.LogWarning("CalculateIrr: Bisection interval [{Lo}, {Hi}] has no sign change – IRR not computable.", lo, hi);
            return null;
        }

        for (int iter = 0; iter < maxIterations; iter++)
        {
            double mid = (lo + hi) / 2.0;
            double npvMid = Npv(mid);

            if (double.IsNaN(npvMid)) break;

            if (Math.Abs(npvMid) < IrrTolerance || (hi - lo) / 2.0 < IrrTolerance)
            {
                _logger.LogDebug("CalculateIrr: Bisection converged after {Iter} iterations. IRR = {Mid:P4}.", iter + 1, mid);
                return (decimal)mid;
            }

            if (npvLo * npvMid < 0)
            {
                hi = mid;
                npvHi = npvMid;
            }
            else
            {
                lo = mid;
                npvLo = npvMid;
            }
        }

        _logger.LogWarning("CalculateIrr: Did not converge after {MaxIterations} bisection iterations.", maxIterations);
        return null;
    }

    /// <inheritdoc/>
    public decimal? CalculateCagr(decimal startValue, decimal endValue, double years)
    {
        if (years <= 0 || startValue <= 0m)
        {
            _logger.LogDebug("CalculateCagr: Invalid inputs (years={Years}, startValue={StartValue}) – returning null.", years, startValue);
            return null;
        }

        double result = Math.Pow((double)endValue / (double)startValue, 1.0 / years) - 1.0;

        if (double.IsNaN(result) || double.IsInfinity(result))
        {
            _logger.LogWarning("CalculateCagr: Result is NaN or Infinity – returning null.");
            return null;
        }

        return (decimal)result;
    }

    /// <inheritdoc/>
    public decimal? CalculateVolatility(IReadOnlyList<decimal> dailyPrices)
    {
        if (dailyPrices is null || dailyPrices.Count < 2)
        {
            _logger.LogDebug("CalculateVolatility: Fewer than 2 price points – returning null.");
            return null;
        }

        int n = dailyPrices.Count;
        double[] logReturns = new double[n - 1];

        for (int i = 1; i < n; i++)
        {
            double previous = (double)dailyPrices[i - 1];
            double current = (double)dailyPrices[i];

            if (previous <= 0.0 || current <= 0.0)
            {
                _logger.LogDebug("CalculateVolatility: Non-positive price at index {Index} – skipping log return.", i);
                continue;
            }

            logReturns[i - 1] = Math.Log(current / previous);
        }

        // Mean of log returns
        double mean = 0.0;
        int validCount = 0;
        foreach (double r in logReturns)
        {
            if (!double.IsNaN(r) && !double.IsInfinity(r))
            {
                mean += r;
                validCount++;
            }
        }

        if (validCount < 2)
        {
            _logger.LogDebug("CalculateVolatility: Fewer than 2 valid log returns – returning null.");
            return null;
        }

        mean /= validCount;

        // Sample variance (Bessel's correction: n-1)
        double variance = 0.0;
        foreach (double r in logReturns)
        {
            if (!double.IsNaN(r) && !double.IsInfinity(r))
            {
                double diff = r - mean;
                variance += diff * diff;
            }
        }
        variance /= (validCount - 1);

        double stdDev = Math.Sqrt(variance);
        double annualisedVolatility = stdDev * Math.Sqrt(TradingDaysPerYear);

        return (decimal)annualisedVolatility;
    }

    /// <inheritdoc/>
    public decimal? CalculateMaxDrawdown(IReadOnlyList<decimal> portfolioValues)
    {
        if (portfolioValues is null || portfolioValues.Count < 2)
        {
            _logger.LogDebug("CalculateMaxDrawdown: Fewer than 2 data points – returning null.");
            return null;
        }

        double peak = (double)portfolioValues[0];
        double maxDrawdown = 0.0;

        foreach (decimal value in portfolioValues)
        {
            double v = (double)value;

            if (v > peak)
                peak = v;

            if (peak > 0.0)
            {
                double drawdown = (v - peak) / peak;
                if (drawdown < maxDrawdown)
                    maxDrawdown = drawdown;
            }
        }

        return (decimal)maxDrawdown;
    }

    /// <inheritdoc/>
    public decimal? CalculateSharpeRatio(decimal annualisedReturn, decimal riskFreeRate, decimal volatility)
    {
        if (volatility == 0m)
        {
            _logger.LogDebug("CalculateSharpeRatio: Volatility is zero – returning null.");
            return null;
        }

        return (annualisedReturn - riskFreeRate) / volatility;
    }

    /// <inheritdoc/>
    public decimal? CalculateDividendYield(decimal totalDividends, decimal investedCapital)
    {
        if (investedCapital == 0m)
        {
            _logger.LogDebug("CalculateDividendYield: investedCapital is zero – returning null.");
            return null;
        }

        return totalDividends / investedCapital;
    }

    /// <inheritdoc/>
    public decimal? CalculateTaxRate(decimal totalTaxes, decimal grossReturn)
    {
        if (grossReturn == 0m)
        {
            _logger.LogDebug("CalculateTaxRate: grossReturn is zero – returning null.");
            return null;
        }

        return totalTaxes / Math.Abs(grossReturn);
    }
}
