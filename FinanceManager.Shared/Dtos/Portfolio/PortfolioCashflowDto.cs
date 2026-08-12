namespace FinanceManager.Shared.Dtos.Portfolio;

/// <summary>
/// Bundles portfolio cashflow KPIs for the current calendar year: net deposits, dividends,
/// realized gains/losses and the liquidity ratio.
/// </summary>
/// <param name="NetDepositsCurrentYear">Net capital committed to the portfolio this year (buys minus sells, absolute amounts).</param>
/// <param name="DividendsCurrentYear">Gross dividends received this year across all positions.</param>
/// <param name="RealizedGainsCurrentYear">Realized gains/losses (FIFO) from sells executed this year.</param>
/// <param name="LiquidityRatio">
/// Ratio of uninvested cash to total portfolio value. Always <c>0</c> in Phase 1 because cash account
/// balances are not linked to the portfolio holdings model; see the follow-up note in the service implementation.
/// </param>
/// <returns>A portfolio cashflow record.</returns>
public sealed record PortfolioCashflowDto(
    decimal NetDepositsCurrentYear,
    decimal DividendsCurrentYear,
    decimal RealizedGainsCurrentYear,
    decimal LiquidityRatio);
