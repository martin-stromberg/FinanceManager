namespace FinanceManager.Application.Securities.ReturnAnalysis;

// ReturnSummaryDto, DetailedReturnMetricsDto, KpiBreakdownDto, KpiFormulaGroup, KpiBreakdownItem,
// PeriodicReturnsDto, AnnualReturnPoint, MonthlyReturnPoint, AnnualDividendPoint,
// CashflowTimelineDto, CashflowEntry, AnnualCashflowSummary,
// PerformanceChartDataDto, BenchmarkComparisonDto, ChartPoint, ChartTimeRange
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
