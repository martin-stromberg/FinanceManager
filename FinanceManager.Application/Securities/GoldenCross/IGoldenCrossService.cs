using FinanceManager.Shared.Dtos.Securities;

namespace FinanceManager.Application.Securities.GoldenCross;

/// <summary>
/// Golden cross analysis for individual securities and creation of the related home page notifications.
/// </summary>
public interface IGoldenCrossService
{
    /// <summary>
    /// Computes the golden cross statistics for a security owned by the given user.
    /// </summary>
    /// <param name="securityId">Security identifier.</param>
    /// <param name="ownerUserId">Owner user id (for access scoping).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The statistics, or <c>null</c> when the security does not exist or is not owned by the user.</returns>
    Task<GoldenCrossDto?> GetAsync(Guid securityId, Guid ownerUserId, CancellationToken ct);

    /// <summary>
    /// Re-evaluates the golden cross phase of a security after new prices have been stored and raises a
    /// home page notification for the owner when the cross is approaching or has been reached.
    /// A notification is raised at most once per phase and cycle; the cycle resets once the cross is far away again.
    /// </summary>
    /// <param name="securityId">Security identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><c>true</c> when a notification has been created; otherwise <c>false</c>.</returns>
    Task<bool> EvaluateAndNotifyAsync(Guid securityId, CancellationToken ct);
}
