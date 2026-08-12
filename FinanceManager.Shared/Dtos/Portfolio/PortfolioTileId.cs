namespace FinanceManager.Shared.Dtos.Portfolio;

/// <summary>
/// Identifies a tile category shown on the portfolio analysis report page.
/// </summary>
public enum PortfolioTileId
{
    /// <summary>Portfolio structure tile (market value, invested capital, allocation, top positions).</summary>
    Structure = 0,
    /// <summary>Performance tile (time-weighted return, annual/monthly returns).</summary>
    Performance = 1,
    /// <summary>Cashflow tile (net deposits, dividends, realized gains, liquidity ratio).</summary>
    Cashflow = 2,
    /// <summary>Risk analysis tile (Phase 2: volatility, drawdown, beta, VaR, Sharpe ratio).</summary>
    Risk = 3
}
