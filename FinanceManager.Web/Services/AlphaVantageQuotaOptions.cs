namespace FinanceManager.Web.Services;

/// <summary>
/// Configuration options that control quota and pacing behaviour when querying AlphaVantage.
/// </summary>
public sealed class AlphaVantageQuotaOptions
{
    /// <summary>
    /// Maximum number of symbols processed per worker run. Defaults to 10.
    /// </summary>
    public int MaxSymbolsPerRun { get; set; } = 10;

    /// <summary>
    /// Allowed requests per minute used to space calls between symbols. A value of 0 or negative disables pacing.
    /// Defaults to 4 requests per minute.
    /// </summary>
    public int RequestsPerMinute { get; set; } = 4;
}