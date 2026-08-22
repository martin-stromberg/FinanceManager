namespace FinanceManager.Shared.Dtos.Portfolio;

/// <summary>
/// Bundles portfolio cashflow KPIs for the current calendar year: net deposits, dividends,
/// realized gains/losses, the current liquidity ratio and its input values.
/// </summary>
/// <param name="NetDepositsCurrentYear">Net capital committed to the portfolio this year (buys minus sells, absolute amounts).</param>
/// <param name="DividendsCurrentYear">Gross dividends received this year across all positions.</param>
/// <param name="RealizedGainsCurrentYear">Realized gains/losses (FIFO) from sells executed this year.</param>
/// <param name="LiquidityRatio">Current depot cash balance divided by market value plus depot cash balance, or null when unavailable.</param>
/// <param name="LiquidityCashBalance">Current cash balance of the depot-related settlement accounts.</param>
/// <param name="LiquidityTotalMarketValue">Current market value of the securities portfolio used for the liquidity ratio.</param>
/// <returns>A portfolio cashflow record.</returns>
public sealed record PortfolioCashflowDto(
    decimal NetDepositsCurrentYear,
    decimal DividendsCurrentYear,
    decimal RealizedGainsCurrentYear,
    decimal? LiquidityRatio,
    decimal LiquidityCashBalance,
    decimal LiquidityTotalMarketValue);
