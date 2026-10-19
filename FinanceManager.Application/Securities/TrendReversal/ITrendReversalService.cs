using FinanceManager.Shared.Dtos.Securities;

namespace FinanceManager.Application.Securities.TrendReversal;

/// <summary>
/// Trend reversal (bearish signal) analysis for individual securities and creation of the related
/// home page notifications.
/// </summary>
public interface ITrendReversalService
{
    /// <summary>
    /// Computes the trend reversal statistics for a security owned by the given user.
    /// </summary>
    /// <param name="securityId">Security identifier.</param>
    /// <param name="ownerUserId">Owner user id (for access scoping).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The statistics, or <c>null</c> when the security does not exist or is not owned by the user.</returns>
    Task<TrendReversalDto?> GetAsync(Guid securityId, Guid ownerUserId, CancellationToken ct);

    /// <summary>
    /// Re-evaluates the trend status of a security after new prices have been stored and raises a
    /// home page notification for the owner when the trend is weakened or a reversal is likely.
    /// A notification is raised at most once per status and cycle; the cycle resets once the trend is intact again.
    /// </summary>
    /// <param name="securityId">Security identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><c>true</c> when a notification has been created; otherwise <c>false</c>.</returns>
    Task<bool> EvaluateAndNotifyAsync(Guid securityId, CancellationToken ct);
}
