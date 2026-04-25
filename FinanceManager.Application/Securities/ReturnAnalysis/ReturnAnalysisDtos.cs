namespace FinanceManager.Application.Securities.ReturnAnalysis;

/// <summary>
/// Compact return summary for the widget on the security detail page (FR-1).
/// SparklineData is loaded separately (see IReturnAnalysisService.GetSparklineDataAsync).
/// </summary>
/// <param name="InvestedCapital">Total invested capital (sum of all buy amounts).</param>
/// <param name="CurrentMarketValue">Current market value (shares held × current price).</param>
/// <param name="TotalReturnAbsolute">Absolute total return (market value + net dividends - invested capital).</param>
/// <param name="TotalReturnPercent">Percentage total return.</param>
/// <param name="Cagr">Compound Annual Growth Rate, or null if holding period &lt; 1 year.</param>
/// <param name="Irr">Internal Rate of Return (personal return), or null when not computable.</param>
/// <param name="CostBasisPerShare">Average cost per share (FIFO basis).</param>
/// <param name="CurrentPricePerShare">Latest available price per share.</param>
/// <param name="NetDividends">Net dividends received (after taxes).</param>
/// <param name="CurrencyCode">ISO currency code.</param>
/// <param name="HasMissingPrices">Whether price data gaps exist.</param>
/// <param name="MissingPricesHint">Hint about missing price data, or null.</param>
public sealed record ReturnSummaryDto(
    decimal InvestedCapital,
    decimal CurrentMarketValue,
    decimal TotalReturnAbsolute,
    decimal TotalReturnPercent,
    decimal? Cagr,
    decimal? Irr,
    decimal CostBasisPerShare,
    decimal CurrentPricePerShare,
    decimal NetDividends,
    string CurrencyCode,
    bool HasMissingPrices,
    string? MissingPricesHint
);

/// <summary>
/// Sparkline data for the mini-chart (FR-1.1). Loaded separately to keep ReturnSummaryDto lean.
/// </summary>
/// <param name="Points">Time series of (date, value) pairs showing invested capital vs. market value.</param>
public sealed record SparklineDataDto(IReadOnlyList<SparklinePoint> Points);

/// <summary>A single point in the sparkline chart.</summary>
/// <param name="Date">Date of the data point.</param>
/// <param name="MarketValue">Portfolio market value on this date.</param>
/// <param name="InvestedCapital">Cumulative invested capital on this date.</param>
public sealed record SparklinePoint(DateTime Date, decimal MarketValue, decimal InvestedCapital);

/// <summary>
/// Detailed return metrics for the Kennzahlen tab (FR-2.1).
/// </summary>
/// <param name="GrossReturn">Gross return before taxes and fees.</param>
/// <param name="NetReturn">Net return after taxes.</param>
/// <param name="TotalTaxes">Total taxes paid.</param>
/// <param name="TotalFees">Total fees paid.</param>
/// <param name="TaxRate">Tax rate as fraction of gross return (0..1).</param>
/// <param name="Twr">Time-Weighted Return (Modified Dietz, GIPS-konform).</param>
/// <param name="Volatility">Annualised volatility (std. dev. of log returns × √252).</param>
/// <param name="MaxDrawdown">Maximum drawdown from peak (negative fraction).</param>
/// <param name="SharpeRatio">Sharpe Ratio, or null when opt-in not enabled or not enough data.</param>
/// <param name="RealizedGains">Realized capital gains (FIFO).</param>
/// <param name="UnrealizedGains">Unrealized capital gains on current holdings.</param>
/// <param name="Irr">Internal Rate of Return, or null when not computable.</param>
/// <param name="DividendYieldCurrentYear">Dividend yield for current calendar year.</param>
public sealed record DetailedReturnMetricsDto(
    decimal GrossReturn,
    decimal NetReturn,
    decimal TotalTaxes,
    decimal TotalFees,
    decimal TaxRate,
    decimal? Twr,
    decimal? Volatility,
    decimal? MaxDrawdown,
    decimal? SharpeRatio,
    decimal RealizedGains,
    decimal UnrealizedGains,
    decimal? Irr,
    decimal DividendYieldCurrentYear
);

/// <summary>
/// Periodic returns for the Zeitliche Entwicklung tab (FR-2.2 + FR-2.5).
/// </summary>
/// <param name="AnnualReturns">Annual return data points for the bar chart.</param>
/// <param name="MonthlyReturns">Monthly return data points for the heatmap.</param>
/// <param name="AnnualDividends">Annual dividend data for the dividend chart.</param>
public sealed record PeriodicReturnsDto(
    IReadOnlyList<AnnualReturnPoint> AnnualReturns,
    IReadOnlyList<MonthlyReturnPoint> MonthlyReturns,
    IReadOnlyList<AnnualDividendPoint> AnnualDividends
);

/// <summary>Annual return data point.</summary>
/// <param name="Year">Calendar year.</param>
/// <param name="ReturnPercent">Annual return as percentage.</param>
/// <param name="IsYtd">True if this is the current year-to-date figure.</param>
public sealed record AnnualReturnPoint(int Year, decimal ReturnPercent, bool IsYtd);

/// <summary>Monthly return data point for the heatmap.</summary>
/// <param name="Year">Calendar year.</param>
/// <param name="Month">Calendar month (1-12).</param>
/// <param name="ReturnPercent">Monthly return as percentage, or null when no data.</param>
public sealed record MonthlyReturnPoint(int Year, int Month, decimal? ReturnPercent);

/// <summary>Annual dividend summary.</summary>
/// <param name="Year">Calendar year.</param>
/// <param name="GrossDividend">Gross dividend amount.</param>
/// <param name="NetDividend">Net dividend after taxes.</param>
/// <param name="CumulativeNet">Cumulative net dividends up to and including this year.</param>
public sealed record AnnualDividendPoint(int Year, decimal GrossDividend, decimal NetDividend, decimal CumulativeNet);

/// <summary>
/// Cashflow timeline for the Cashflows tab (FR-2.3 + FR-2.6).
/// </summary>
/// <param name="Entries">Chronological cashflow entries.</param>
/// <param name="AnnualSummaries">Annual aggregated cashflows for the bar chart.</param>
public sealed record CashflowTimelineDto(
    IReadOnlyList<CashflowEntry> Entries,
    IReadOnlyList<AnnualCashflowSummary> AnnualSummaries
);

/// <summary>A single cashflow entry in the timeline.</summary>
/// <param name="Date">Date of the cashflow.</param>
/// <param name="Type">Cashflow type ("Buy", "Sell", "Dividend", "Tax", "Fee").</param>
/// <param name="Amount">Cashflow amount.</param>
/// <param name="Description">Optional description.</param>
/// <param name="PostingId">Reference to the source posting.</param>
public sealed record CashflowEntry(DateTime Date, string Type, decimal Amount, string? Description, Guid PostingId);

/// <summary>Annual cashflow summary for the cost/tax chart.</summary>
/// <param name="Year">Calendar year.</param>
/// <param name="TotalBuys">Total buy cashflows (negative).</param>
/// <param name="TotalSells">Total sell cashflows (positive).</param>
/// <param name="TotalDividends">Total gross dividends.</param>
/// <param name="TotalTaxes">Total taxes (negative).</param>
/// <param name="TotalFees">Total fees (negative).</param>
public sealed record AnnualCashflowSummary(int Year, decimal TotalBuys, decimal TotalSells, decimal TotalDividends, decimal TotalTaxes, decimal TotalFees);

/// <summary>
/// Performance chart data for the Übersicht tab (FR-2.4).
/// </summary>
/// <param name="TimeRange">Selected time range.</param>
/// <param name="PortfolioValues">Portfolio market value time series.</param>
/// <param name="InvestedCapitalValues">Invested capital time series.</param>
public sealed record PerformanceChartDataDto(
    ChartTimeRange TimeRange,
    IReadOnlyList<ChartPoint> PortfolioValues,
    IReadOnlyList<ChartPoint> InvestedCapitalValues
);

/// <summary>
/// Benchmark comparison data for the Benchmark tab (FR-7).
/// </summary>
/// <param name="BenchmarkSecurityId">Id of the benchmark security.</param>
/// <param name="BenchmarkName">Display name of the benchmark security.</param>
/// <param name="SecurityNormalizedValues">Normalized values for the target security (base 100).</param>
/// <param name="BenchmarkNormalizedValues">Normalized values for the benchmark (base 100).</param>
public sealed record BenchmarkComparisonDto(
    Guid BenchmarkSecurityId,
    string BenchmarkName,
    IReadOnlyList<ChartPoint> SecurityNormalizedValues,
    IReadOnlyList<ChartPoint> BenchmarkNormalizedValues
);

/// <summary>A single chart data point.</summary>
/// <param name="Date">Date of the data point.</param>
/// <param name="Value">Value on this date.</param>
public sealed record ChartPoint(DateTime Date, decimal Value);

/// <summary>FIFO cost basis calculation result.</summary>
/// <param name="TotalCostBasis">Total cost basis of all remaining lots.</param>
/// <param name="RealizedGains">Total realized gains from sells (FIFO).</param>
/// <param name="RemainingLots">Remaining open lots after all sells.</param>
/// <param name="TotalSharesHeld">Total shares currently held.</param>
/// <param name="HasOversellWarning">True when sells exceeded available lots (incomplete data).</param>
/// <param name="OversellWarningMessage">Warning message when HasOversellWarning is true.</param>
public sealed record FifoCostBasisResult(
    decimal TotalCostBasis,
    decimal RealizedGains,
    IReadOnlyList<FifoLot> RemainingLots,
    decimal TotalSharesHeld,
    bool HasOversellWarning,
    string? OversellWarningMessage
);

/// <summary>A single FIFO lot representing a purchase batch.</summary>
/// <param name="PurchaseDate">Date of purchase.</param>
/// <param name="Quantity">Number of shares in this lot.</param>
/// <param name="CostPerUnit">Cost per share for this lot.</param>
public sealed record FifoLot(DateTime PurchaseDate, decimal Quantity, decimal CostPerUnit);
