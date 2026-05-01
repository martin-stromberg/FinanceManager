namespace FinanceManager.Shared.Dtos.Securities;

/// <summary>
/// Compact return summary for the widget on the security detail page (FR-1).
/// SparklineData is loaded separately.
/// </summary>
/// <param name="InvestedCapital">Total invested capital (sum of all buy amounts).</param>
/// <param name="CurrentMarketValue">Current market value (shares held × current price).</param>
/// <param name="TotalReturnAbsolute">Absolute total return (market value + net dividends - invested capital).</param>
/// <param name="TotalReturnPercent">Percentage total return.</param>
/// <param name="Cagr">Compound Annual Growth Rate, or null if holding period &lt; 1 year.</param>
/// <param name="Irr">Internal Rate of Return (personal return), or null when not computable.</param>
/// <param name="CostBasisPerShare">Average cost per share (FIFO basis).</param>
/// <param name="CurrentPricePerShare">Latest available price per share.</param>
/// <param name="TotalSharesHeld">Total number of shares currently held (FIFO net position).</param>
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
    decimal TotalSharesHeld,
    decimal NetDividends,
    string CurrencyCode,
    bool HasMissingPrices,
    string? MissingPricesHint
);

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
/// Formula and cashflow breakdown for a single KPI, used in the info side panel (FR-1).
/// </summary>
/// <param name="KpiKey">Stable internal key matching the widget KPI (e.g. "TotalReturn", "InvestedCapital").</param>
/// <param name="DisplayName">Localized display name of the KPI (e.g. "Gesamtrendite").</param>
/// <param name="FormulaText">Human-readable formula as a complete equation.</param>
/// <param name="Description">Short explanation of what this metric measures.</param>
/// <param name="Groups">Posting groups, each representing one element of the formula.</param>
public sealed record KpiBreakdownDto(
    string KpiKey,
    string DisplayName,
    string FormulaText,
    string Description,
    IReadOnlyList<KpiFormulaGroup> Groups
);

/// <summary>
/// A group of cashflow items representing one formula element (e.g. "Dividenden").
/// </summary>
/// <param name="GroupName">Display name of this formula element.</param>
/// <param name="IsPositiveContribution">True when this group increases the KPI value.</param>
/// <param name="Items">Individual cashflow items in this group.</param>
/// <param name="GroupTotal">Sum of all item amounts in this group (absolute monetary value).</param>
/// <param name="GroupTotalPercent">
/// Optional percentage value for this group total (e.g. the percentage return for the
/// "Gesamtrendite" result group). When set, the UI renders both the percentage and the
/// absolute amount side by side in the info-panel result block.
/// </param>
/// <param name="GroupNote">
/// Optional informational text to display instead of a monetary amount in the group header
/// (e.g. the holding period for the "Anlagedauer" group). When set, the UI renders this
/// text in place of the formatted EUR value and omits amount columns from item rows.
/// </param>
public sealed record KpiFormulaGroup(
    string GroupName,
    bool IsPositiveContribution,
    IReadOnlyList<KpiBreakdownItem> Items,
    decimal GroupTotal,
    decimal? GroupTotalPercent = null,
    string? GroupNote = null
);

/// <summary>
/// A single cashflow item within a KPI formula group.
/// </summary>
/// <param name="Date">Date of the cashflow.</param>
/// <param name="Amount">Amount displayed for this item. For IRR breakdown groups this is the present value
/// (discounted cashflow); for all other groups it is the raw cashflow amount.</param>
/// <param name="Note">Optional descriptive note (e.g. number of shares).</param>
public sealed record KpiBreakdownItem(DateTime Date, decimal Amount, string? Note)
{
    /// <summary>Years since t₀ for discounting (used in IRR breakdown). Null when not applicable.</summary>
    public decimal? YearsSinceT0 { get; init; }

    /// <summary>Discount factor 1/(1+r)^t (used in IRR breakdown). Null when not applicable.</summary>
    public decimal? DiscountFactor { get; init; }

    /// <summary>Original (undiscounted) cashflow amount (used in IRR breakdown). Null when not applicable.</summary>
    public decimal? OriginalCashflow { get; init; }
}
