namespace FinanceManager.Application.Securities.ReturnAnalysis;

/// <summary>
/// Shared valuation primitives operating on a single security's transaction and price history.
/// Reused by both the single-security return analysis and the portfolio-level analysis report,
/// so the two never drift apart on the same underlying calculation.
/// </summary>
public static class SecurityValuationHelper
{
    /// <summary>
    /// Computes shares held on or before a given date from transaction history.
    /// Transactions do not need to be pre-sorted; every transaction up to and including <paramref name="date"/> is considered.
    /// </summary>
    /// <param name="transactions">Transactions for a single security.</param>
    /// <param name="date">Reference date (inclusive).</param>
    /// <returns>Total shares held on the given date, clamped to a minimum of zero.</returns>
    public static decimal SharesHeldOnDate(IReadOnlyList<SecurityTransaction> transactions, DateTime date)
    {
        decimal shares = 0m;
        foreach (var tx in transactions)
        {
            if (tx.Date.Date > date.Date) { break; }
            if (tx.Type == SecurityPostingSubType.Buy) { shares += tx.Quantity ?? 0m; }
            else if (tx.Type == SecurityPostingSubType.Sell) { shares -= Math.Abs(tx.Quantity ?? 0m); }
        }
        return Math.Max(0m, shares);
    }

    /// <summary>
    /// Returns the latest known close price on or before <paramref name="date"/>. Prices must be sorted ascending by date.
    /// </summary>
    /// <param name="prices">Price history for a single security, sorted ascending by date.</param>
    /// <param name="date">Reference date (inclusive).</param>
    /// <returns>The latest close price on or before <paramref name="date"/>, or <c>0</c> when no price exists yet.</returns>
    public static decimal LatestPriceOnOrBefore(IReadOnlyList<(DateTime Date, decimal Close)> prices, DateTime date)
    {
        decimal price = 0m;
        foreach (var pr in prices)
        {
            if (pr.Date.Date > date.Date) { break; }
            price = pr.Close;
        }
        return price;
    }
}
