namespace FinanceManager.Application.Reports;

/// <summary>
/// Service for managing per-favorite suppressed projection expectations for a user.
/// A suppression hides one expected posting (identified by posting kind, entity,
/// prior-year date and, when available, source posting id — never the amount) whenever the
/// report runs in the context of that favorite.
/// </summary>
public interface IReportSuppressedExpectationService
{
    /// <summary>
    /// Lists suppressed expectations for one report favorite owned by the user.
    /// </summary>
    /// <param name="ownerUserId">Owner user identifier requesting the list.</param>
    /// <param name="reportFavoriteId">Identifier of the report favorite.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>A read-only list of <see cref="ReportSuppressedExpectationDto"/> instances for the favorite.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="reportFavoriteId"/> or <paramref name="ownerUserId"/> is <see cref="Guid.Empty"/>.</exception>
    Task<IReadOnlyList<ReportSuppressedExpectationDto>> ListAsync(Guid ownerUserId, Guid reportFavoriteId, CancellationToken ct);

    /// <summary>
    /// Suppresses one expected posting for the report favorite. Idempotent: if an identical
    /// suppression already exists, the existing marker is returned.
    /// </summary>
    /// <param name="ownerUserId">Owner user identifier performing the suppression.</param>
    /// <param name="reportFavoriteId">Identifier of the report favorite to scope the suppression to.</param>
    /// <param name="request">Suppression request identifying the expected item.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>The created (or existing) <see cref="ReportSuppressedExpectationDto"/>, or <c>null</c> when the favorite does not belong to the owner.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="reportFavoriteId"/> or <paramref name="ownerUserId"/> is <see cref="Guid.Empty"/>.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is <c>null</c>.</exception>
    Task<ReportSuppressedExpectationDto?> SuppressAsync(Guid ownerUserId, Guid reportFavoriteId, ReportSuppressedExpectationRequest request, CancellationToken ct);

    /// <summary>
    /// Removes a suppression, restoring the original projection behavior for the expected item.
    /// </summary>
    /// <param name="ownerUserId">Owner user identifier performing the removal.</param>
    /// <param name="reportFavoriteId">Identifier of the report favorite the suppression belongs to.</param>
    /// <param name="suppressionId">Identifier of the suppression to remove.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns><c>true</c> when the suppression was found and removed; otherwise <c>false</c>.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="reportFavoriteId"/>, <paramref name="suppressionId"/> or <paramref name="ownerUserId"/> is <see cref="Guid.Empty"/>.</exception>
    Task<bool> UnsuppressAsync(Guid ownerUserId, Guid reportFavoriteId, Guid suppressionId, CancellationToken ct);
}
