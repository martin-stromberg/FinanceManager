namespace FinanceManager.Shared.Dtos.Portfolio;

/// <summary>
/// A single annual return data point.
/// </summary>
/// <param name="Year">Calendar year.</param>
/// <param name="ReturnPercent">Return for the year as a fraction (e.g. 0.05 = 5%).</param>
/// <param name="IsYtd">Whether this point represents the current, not-yet-completed year (year-to-date).</param>
/// <returns>An annual return point record.</returns>
public sealed record PortfolioAnnualReturnPoint(int Year, decimal ReturnPercent, bool IsYtd);

/// <summary>
/// A single monthly return data point.
/// </summary>
/// <param name="Year">Calendar year.</param>
/// <param name="Month">Calendar month (1-12).</param>
/// <param name="ReturnPercent">Return for the month as a fraction, or <c>null</c> when no capital was deployed in the period.</param>
/// <returns>A monthly return point record.</returns>
public sealed record PortfolioMonthlyReturnPoint(int Year, int Month, decimal? ReturnPercent);

/// <summary>
/// Bundles portfolio performance KPIs: time-weighted return since inception, year-to-date return,
/// and annual/monthly return series.
/// </summary>
/// <param name="TimeWeightedReturn">Time-weighted return (Modified Dietz, chain-linked across years) since inception, or <c>null</c> when not computable.</param>
/// <param name="YtdReturn">Year-to-date return for the current calendar year, or <c>null</c> when not computable.</param>
/// <param name="AnnualReturns">Return per calendar year since the first transaction.</param>
/// <param name="MonthlyReturns">Return per calendar month since the first transaction.</param>
/// <returns>A portfolio performance record.</returns>
public sealed record PortfolioPerformanceDto(
    decimal? TimeWeightedReturn,
    decimal? YtdReturn,
    IReadOnlyList<PortfolioAnnualReturnPoint> AnnualReturns,
    IReadOnlyList<PortfolioMonthlyReturnPoint> MonthlyReturns);
