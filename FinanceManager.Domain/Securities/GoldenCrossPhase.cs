namespace FinanceManager.Domain.Securities;

/// <summary>
/// Phase of the golden cross analysis for a security (relation of the short-term to the long-term moving average).
/// </summary>
public enum GoldenCrossPhase
{
    /// <summary>Not enough price history to compute both moving averages.</summary>
    InsufficientData = 0,

    /// <summary>The short-term average is clearly below the long-term average; no golden cross in sight.</summary>
    Far = 1,

    /// <summary>The short-term average is below but close to the long-term average; a golden cross is approaching.</summary>
    Approaching = 2,

    /// <summary>The short-term average is at or above the long-term average; the golden cross has been reached.</summary>
    Crossed = 3
}
