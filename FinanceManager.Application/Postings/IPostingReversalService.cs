using FinanceManager.Domain.Postings;
using FinanceManager.Shared.Dtos.Postings;

namespace FinanceManager.Application.Postings;

/// <summary>
/// Service responsible for posting reversal operations, including validation, creation of reversal postings,
/// and updating related aggregates.
/// </summary>
public interface IPostingReversalService
{
    /// <summary>
    /// Reverses a posting by creating a reversal posting with negated amount and updating all related postings in the group.
    /// All operations are performed within a database transaction to ensure atomicity.
    /// </summary>
    /// <param name="postingId">The ID of the posting to reverse.</param>
    /// <param name="userId">The ID of the user initiating the reversal.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>A result containing the reversed and created posting IDs, and the statement import ID.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the posting cannot be reversed (already reversed, is a reversal itself, partially reversed group, etc.).
    /// </exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when the user is not the owner of the posting.</exception>
    Task<ReversalResultDto> ReversePostingAsync(Guid postingId, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Validates whether a posting can be reversed by the specified user.
    /// </summary>
    /// <param name="postingId">The ID of the posting to validate.</param>
    /// <param name="userId">The ID of the user requesting the reversal.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>A validation result indicating whether the reversal is valid and any validation errors.</returns>
    Task<ReversalValidationDto> CanReverseAsync(Guid postingId, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Gets all related postings that would be affected by reversing the specified posting.
    /// Related postings are identified by the same GroupId.
    /// </summary>
    /// <param name="postingId">The ID of the posting to get related postings for.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>A read-only list of related postings (excluding the original posting itself).</returns>
    Task<IReadOnlyList<Posting>> GetRelatedPostingsAsync(Guid postingId, CancellationToken ct = default);
}
