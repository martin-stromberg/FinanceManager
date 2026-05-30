using System.ComponentModel.DataAnnotations;

namespace FinanceManager.Web.Controllers;

/// <summary>Request payload for updating return analysis settings.</summary>
public sealed class ReturnAnalysisSettingsRequest
{
    /// <summary>Optional benchmark security id. Null clears the benchmark.</summary>
    public Guid? BenchmarkSecurityId { get; set; }

    /// <summary>Whether to show Sharpe Ratio in the UI.</summary>
    public bool ShowSharpeRatio { get; set; }

    /// <summary>Risk-free rate for Sharpe Ratio calculation (e.g. 0.04 = 4%). Must be >= 0.</summary>
    [Range(0, double.MaxValue)]
    public decimal RiskFreeRate { get; set; }
}
