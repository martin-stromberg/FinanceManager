using FinanceManager.Shared.Dtos.Portfolio;

namespace FinanceManager.Application.Portfolio;

/// <summary>
/// Computes aggregated portfolio-level KPIs across all securities, postings and prices owned by a user.
/// </summary>
public interface IPortfolioAnalysisReportService
{
    /// <summary>
    /// Computes the full portfolio analysis report for the given owner.
    /// </summary>
    /// <param name="ownerUserId">Owner user identifier. All aggregated data is scoped to this user.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The computed <see cref="PortfolioAnalysisReportDto"/>.</returns>
    Task<PortfolioAnalysisReportDto> GetPortfolioAnalysisReportAsync(Guid ownerUserId, CancellationToken ct);
}
