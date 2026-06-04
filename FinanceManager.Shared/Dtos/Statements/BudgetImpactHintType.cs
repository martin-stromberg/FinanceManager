namespace FinanceManager.Shared.Dtos.Statements;

/// <summary>
/// Categorized budget impact hint type for statement booking flows.
/// </summary>
public enum BudgetImpactHintType
{
    /// <summary>
    /// No critical budget effect detected or no mapping available.
    /// </summary>
    Neutral = 0,

    /// <summary>
    /// Fulfillment changed significantly based on configured delta thresholds.
    /// </summary>
    StronglyChanged = 1,

    /// <summary>
    /// Budget is close to the configured exhaustion threshold.
    /// </summary>
    AlmostExhausted = 2,

    /// <summary>
    /// Budget target is exceeded.
    /// </summary>
    Exceeded = 3
}
