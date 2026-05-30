namespace FinanceManager.Domain.Users;

public sealed partial class User
{
    /// <summary>
    /// Optional benchmark security identifier for return analysis comparison (FR-7).
    /// Stored as FK reference but constraint enforced at application level.
    /// </summary>
    public Guid? BenchmarkSecurityId { get; private set; }

    /// <summary>
    /// Whether to show Sharpe Ratio in return analysis (opt-in, FR-8).
    /// </summary>
    public bool ShowSharpeRatio { get; private set; }

    /// <summary>
    /// Risk-free rate used for Sharpe Ratio calculation (e.g. 0.04 = 4 %). Must be >= 0.
    /// </summary>
    public decimal RiskFreeRate { get; private set; }

    /// <summary>
    /// Updates return analysis settings for the user.
    /// </summary>
    /// <param name="benchmarkSecurityId">Optional benchmark security id. Null clears the benchmark.</param>
    /// <param name="showSharpeRatio">Whether to show Sharpe Ratio.</param>
    /// <param name="riskFreeRate">Risk-free interest rate. Must be >= 0.</param>
    /// <exception cref="ArgumentOutOfRangeException">When riskFreeRate is negative.</exception>
    public void SetReturnAnalysisSettings(Guid? benchmarkSecurityId, bool showSharpeRatio, decimal riskFreeRate)
    {
        if (riskFreeRate < 0)
            throw new ArgumentOutOfRangeException(nameof(riskFreeRate), "Risk-free rate must be >= 0.");
        BenchmarkSecurityId = benchmarkSecurityId;
        ShowSharpeRatio = showSharpeRatio;
        RiskFreeRate = riskFreeRate;
    }
}
