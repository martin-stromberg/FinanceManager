namespace FinanceManager.Application.Reports;

/// <summary>
/// Service for managing report favorites (saved report configurations) for a user.
/// </summary>
public interface IReportFavoriteService
{
    /// <summary>
    /// Lists report favorites for the specified owner.
    /// </summary>
    /// <param name="ownerUserId">Owner user identifier for which to list favorites.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>A read-only list of <see cref="ReportFavoriteDto"/> instances belonging to the owner.</returns>
    Task<IReadOnlyList<ReportFavoriteDto>> ListAsync(Guid ownerUserId, CancellationToken ct);

    /// <summary>
    /// Gets a specific report favorite by id for the owner.
    /// </summary>
    /// <param name="id">Identifier of the report favorite to retrieve.</param>
    /// <param name="ownerUserId">Owner user identifier requesting the favorite.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>The matching <see cref="ReportFavoriteDto"/>, or <c>null</c> if not found.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="id"/> or <paramref name="ownerUserId"/> is <see cref="Guid.Empty"/>.</exception>
    Task<ReportFavoriteDto?> GetAsync(Guid id, Guid ownerUserId, CancellationToken ct);

    /// <summary>
    /// Creates a new report favorite for the owner.
    /// </summary>
    /// <param name="ownerUserId">Owner user identifier creating the favorite.</param>
    /// <param name="request">Creation request containing favorite details.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>The created <see cref="ReportFavoriteDto"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is <c>null</c> or required fields are missing.</exception>
    Task<ReportFavoriteDto> CreateAsync(Guid ownerUserId, ReportFavoriteCreateRequest request, CancellationToken ct);

    /// <summary>
    /// Updates an existing report favorite.
    /// </summary>
    /// <param name="id">Identifier of the report favorite to update.</param>
    /// <param name="ownerUserId">Owner user identifier performing the update.</param>
    /// <param name="request">Update request payload containing fields to change.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>The updated <see cref="ReportFavoriteDto"/>, or <c>null</c> if the favorite does not exist.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="id"/> or <paramref name="ownerUserId"/> is <see cref="Guid.Empty"/>.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is <c>null</c>.</exception>
    Task<ReportFavoriteDto?> UpdateAsync(Guid id, Guid ownerUserId, ReportFavoriteUpdateRequest request, CancellationToken ct);

    /// <summary>
    /// Deletes a report favorite for the owner.
    /// </summary>
    /// <param name="id">Identifier of the report favorite to delete.</param>
    /// <param name="ownerUserId">Owner user identifier performing the deletion.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns><c>true</c> when the favorite was deleted; otherwise <c>false</c> (for example, when not found).</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="id"/> or <paramref name="ownerUserId"/> is <see cref="Guid.Empty"/>.</exception>
    Task<bool> DeleteAsync(Guid id, Guid ownerUserId, CancellationToken ct);
}


