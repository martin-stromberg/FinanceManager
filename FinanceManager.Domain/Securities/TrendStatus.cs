namespace FinanceManager.Domain.Securities;

/// <summary>
/// Aggregated trend status of a security derived from the detected bearish reversal signals
/// (death cross, long-term line break, market structure, MACD crossover, RSI divergence).
/// </summary>
/// <remarks>
/// The numeric order matters for the notification cycle: a higher status than the persisted
/// <c>Security.TrendReversalNotifiedStatus</c> triggers a notification, a lower or equal one does not.
/// </remarks>
public enum TrendStatus
{
    /// <summary>Not enough price history to evaluate the trend.</summary>
    InsufficientData = 0,

    /// <summary>No bearish reversal signal detected; the uptrend is intact.</summary>
    Intact = 1,

    /// <summary>Weak reversal signals detected; the trend appears weakened.</summary>
    Weakened = 2,

    /// <summary>Strong or multiple reversal signals detected; a trend reversal is likely.</summary>
    ReversalLikely = 3
}
