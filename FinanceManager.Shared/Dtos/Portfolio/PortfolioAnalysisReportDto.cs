namespace FinanceManager.Shared.Dtos.Portfolio;

/// <summary>
/// Top-level DTO structuring the full portfolio analysis report for API responses and caching.
/// Groups all computed KPIs by tile category.
/// </summary>
/// <param name="Structure">Portfolio structure KPIs (market value, allocation, top positions).</param>
/// <param name="Performance">Performance KPIs (TWR, annual/monthly returns).</param>
/// <param name="Cashflow">Cashflow KPIs (deposits, dividends, realized gains, liquidity).</param>
/// <param name="Risk">Risk analysis KPIs (Phase 2 placeholder).</param>
/// <param name="GeneratedUtc">UTC timestamp when the report was computed.</param>
/// <param name="CacheValidUntilUtc">UTC timestamp until which the report is considered valid (end of the current month).</param>
/// <returns>A portfolio analysis report record.</returns>
public sealed record PortfolioAnalysisReportDto(
    PortfolioStructureDto Structure,
    PortfolioPerformanceDto Performance,
    PortfolioCashflowDto Cashflow,
    PortfolioRiskDto Risk,
    DateTime GeneratedUtc,
    DateTime CacheValidUntilUtc);
