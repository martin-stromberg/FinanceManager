namespace FinanceManager.Application.Securities.ReturnAnalysis;

// ReturnSummaryDto, DetailedReturnMetricsDto, KpiBreakdownDto, KpiFormulaGroup, KpiBreakdownItem
// have been moved to FinanceManager.Shared.Dtos.Securities.ReturnAnalysisDtos and are available
// globally via the FinanceManager.Application.GlobalUsings (global using FinanceManager.Shared.Dtos.Securities).

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
/// <param name="TotalCostBasis">
/// Total cost basis of all remaining lots, including standalone fees (fees not linked to any lot
/// via GroupId). Represents the total invested capital as seen from a FIFO perspective.
/// </param>
/// <param name="RealizedGains">Total realized gains from sells (FIFO).</param>
/// <param name="RemainingLots">Remaining open lots after all sells.</param>
/// <param name="TotalSharesHeld">Total shares currently held.</param>
/// <param name="HasOversellWarning">True when sells exceeded available lots (incomplete data).</param>
/// <param name="OversellWarningMessage">Warning message when HasOversellWarning is true.</param>
/// <param name="StandaloneFeeTotal">
/// Sum of all fee amounts whose GroupId did not match any Buy lot (standalone fees).
/// These are included in <see cref="TotalCostBasis"/> but are tracked separately to allow
/// detailed cost-basis breakdowns in the UI (e.g. "Kaufpreise + Gebühren = Investiertes Kapital").
/// </param>
public sealed record FifoCostBasisResult(
    decimal TotalCostBasis,
    decimal RealizedGains,
    IReadOnlyList<FifoLot> RemainingLots,
    decimal TotalSharesHeld,
    bool HasOversellWarning,
    string? OversellWarningMessage,
    decimal StandaloneFeeTotal
);

/// <summary>A single FIFO lot representing a purchase batch.</summary>
/// <param name="PurchaseDate">Date of purchase.</param>
/// <param name="Quantity">Number of shares in this lot.</param>
/// <param name="CostPerUnit">Cost per share for this lot.</param>
public sealed record FifoLot(DateTime PurchaseDate, decimal Quantity, decimal CostPerUnit);

// KpiBreakdownDto, KpiFormulaGroup, KpiBreakdownItem have been moved to
// FinanceManager.Shared.Dtos.Securities.ReturnAnalysisDtos and are available globally.
