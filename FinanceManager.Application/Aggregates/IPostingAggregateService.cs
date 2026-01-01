using FinanceManager.Domain.Postings;

namespace FinanceManager.Application.Aggregates;

/// <summary>
/// Service responsible for maintaining posting aggregates for reporting and analysis.
/// </summary>
public interface IPostingAggregateService
{
    /// <summary>
    /// Upserts aggregate entries related to the given posting. Implementations should update or create the necessary PostingAggregate records.
    /// </summary>
    /// <param name="posting">Posting instance to upsert aggregates for.</param>
    /// <param name="ct">Cancellation token.</param>
    Task UpsertForPostingAsync(Posting posting, CancellationToken ct);

    /// <summary>
    /// Rebuilds all posting aggregates for the given user and invokes a progress callback while processing.
    /// </summary>
    /// <param name="userId">User identifier whose aggregates should be rebuilt.</param>
    /// <param name="progressCallback">Callback to report progress. Receives processed count and total count.</param>
    /// <param name="ct">Cancellation token.</param>
    Task RebuildForUserAsync(Guid userId, Action<int, int> progressCallback, CancellationToken ct);
}
