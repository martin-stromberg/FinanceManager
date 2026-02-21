using FinanceManager.Shared.Dtos.Admin;
using FinanceManager.Shared.Dtos.Budget;

namespace FinanceManager.Application.Budget;

/// <summary>
/// Provides access to cached report data.
/// </summary>
public interface IReportCacheService
{
    /// <summary>
    /// Reads a cached budget report raw data entry.
    /// Returns <c>null</c> when no cache entry exists or it requires refresh.
    /// </summary>
    /// <param name="ownerUserId">Owner user identifier.</param>
    /// <param name="from">Inclusive range start.</param>
    /// <param name="to">Inclusive range end.</param>
    /// <param name="dateBasis">Date basis used to build the cache key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The cached raw data or <c>null</c> when not available.</returns>
    Task<BudgetReportRawDataDto?> GetBudgetReportRawDataAsync(
        Guid ownerUserId,
        DateOnly from,
        DateOnly to,
        BudgetReportDateBasis dateBasis,
        CancellationToken ct);

    /// <summary>
    /// Updates or creates a cached budget report raw data entry.
    /// </summary>
    /// <param name="ownerUserId">Owner user identifier.</param>
    /// <param name="from">Inclusive range start.</param>
    /// <param name="to">Inclusive range end.</param>
    /// <param name="dateBasis">Date basis used to build the cache key.</param>
    /// <param name="data">Raw data to cache.</param>
    /// <param name="parameter">Additional parameter stored with the cache entry.</param>
    /// <param name="needsRefresh">Whether the cache entry should be marked for refresh.</param>
    /// <param name="ct">Cancellation token.</param>
    Task SetBudgetReportRawDataAsync(
        Guid ownerUserId,
        DateOnly from,
        DateOnly to,
        BudgetReportDateBasis dateBasis,
        BudgetReportRawDataDto data,
        bool needsRefresh,
        CancellationToken ct);

    /// <summary>
    /// Returns the parameter of the first cache entry that requires an update.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The parameter for the next cache update or <c>null</c> when none exist.</returns>
    Task<BudgetReportCacheParameter?> GetNextBudgetReportCacheToUpdateAsync(CancellationToken ct);

    /// <summary>
    /// Marks cache entries for update when their period falls within the specified range.
    /// </summary>
    /// <param name="periodFrom">Inclusive start of the period.</param>
    /// <param name="periodTo">Inclusive end of the period.</param>
    /// <param name="ct">Cancellation token.</param>
    Task MarkBudgetReportCacheEntriesForUpdateAsync(DateOnly periodFrom, DateOnly periodTo, CancellationToken ct);

    /// <summary>
    /// Marks all cache entries for the specified user for refresh.
    /// </summary>
    /// <param name="ownerUserId">Owner user identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    Task MarkAllReportCacheEntriesForUpdateAsync(Guid ownerUserId, CancellationToken ct);

    /// <summary>
    /// Deletes all cached report entries for the specified user.
    /// </summary>
    /// <param name="ownerUserId">Owner user identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    Task ClearReportCacheAsync(Guid ownerUserId, CancellationToken ct);

    /// <summary>
    /// Enqueues a background task to refresh marked budget report cache entries.
    /// </summary>
    /// <param name="ownerUserId">Owner user identifier.</param>
    /// <returns>Information about the enqueued task.</returns>
    BackgroundTaskInfo EnqueueBudgetReportCacheRefresh(Guid ownerUserId);
}
