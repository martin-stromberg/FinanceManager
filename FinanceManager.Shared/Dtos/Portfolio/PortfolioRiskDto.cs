namespace FinanceManager.Shared.Dtos.Portfolio;

/// <summary>
/// Bundles portfolio risk analysis KPIs (Phase 2). All values are <c>null</c> in the current implementation;
/// the DTO exists so the UI tile and report structure are already in place for a future Phase 2 extension.
/// </summary>
/// <param name="Volatility">Annualized portfolio volatility, or <c>null</c> when not yet computed (Phase 2).</param>
/// <param name="MaxDrawdown">Maximum drawdown from peak, or <c>null</c> when not yet computed (Phase 2).</param>
/// <param name="SharpeRatio">Sharpe ratio, or <c>null</c> when not yet computed (Phase 2).</param>
/// <param name="Beta">Beta against the configured benchmark, or <c>null</c> when not yet computed (Phase 2).</param>
/// <param name="ValueAtRisk">Value at Risk, or <c>null</c> when not yet computed (Phase 2).</param>
/// <returns>A portfolio risk record.</returns>
public sealed record PortfolioRiskDto(
    decimal? Volatility,
    decimal? MaxDrawdown,
    decimal? SharpeRatio,
    decimal? Beta,
    decimal? ValueAtRisk);
