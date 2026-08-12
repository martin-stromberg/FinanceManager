namespace FinanceManager.Shared.Dtos.Portfolio;

/// <summary>
/// Bundles portfolio cashflow KPIs for the current calendar year: net deposits, dividends
/// and realized gains/losses.
/// </summary>
/// <param name="NetDepositsCurrentYear">Net capital committed to the portfolio this year (buys minus sells, absolute amounts).</param>
/// <param name="DividendsCurrentYear">Gross dividends received this year across all positions.</param>
/// <param name="RealizedGainsCurrentYear">Realized gains/losses (FIFO) from sells executed this year.</param>
/// <returns>A portfolio cashflow record.</returns>
public sealed record PortfolioCashflowDto(
    decimal NetDepositsCurrentYear,
    decimal DividendsCurrentYear,
    decimal RealizedGainsCurrentYear);
