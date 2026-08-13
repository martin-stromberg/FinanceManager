using FinanceManager.Shared.Dtos.Portfolio;

namespace FinanceManager.Application.Portfolio;

/// <summary>
/// Provides monthly-cached access to the portfolio analysis report and cache invalidation.
/// Wraps <see cref="IPortfolioAnalysisReportService"/> with a database-backed cache that is valid until the
/// end of the current calendar month (<c>CacheValidUntilUtc</c>).
/// </summary>
public interface IPortfolioAnalysisReportCacheService
{
    /// <summary>
    /// Returns the cached portfolio analysis report when a valid cache entry exists for the current month;
    /// otherwise computes it via <see cref="IPortfolioAnalysisReportService"/> and stores the result.
    /// </summary>
    /// <param name="ownerUserId">Owner user identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The (possibly cached) <see cref="PortfolioAnalysisReportDto"/>.</returns>
    Task<PortfolioAnalysisReportDto> GetPortfolioReportAsync(Guid ownerUserId, CancellationToken ct);

    /// <summary>
    /// Deletes the cached portfolio analysis report entry for the given user, forcing recalculation on the next request.
    /// </summary>
    /// <param name="ownerUserId">Owner user identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    Task InvalidateCacheAsync(Guid ownerUserId, CancellationToken ct);
}
