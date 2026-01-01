namespace FinanceManager.Application.Securities;

/// <summary>
/// Service to manage securities (list, get, create, update, archive, delete, count).
/// </summary>
public interface ISecurityService
{
    /// <summary>
    /// Lists securities for the specified owner, optionally filtering by active state.
    /// </summary>
    /// <param name="ownerUserId">Identifier of the owner whose securities should be listed.</param>
    /// <param name="onlyActive">If <c>true</c>, returns only active securities; if <c>false</c>, returns only inactive securities; if <c>null</c>, returns all securities.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>A read-only list of <see cref="SecurityDto"/> instances matching the criteria.</returns>
    Task<IReadOnlyList<SecurityDto>> ListAsync(Guid ownerUserId, bool onlyActive, CancellationToken ct);

    /// <summary>
    /// Gets a security by its identifier for the specified owner.
    /// </summary>
    /// <param name="id">Identifier of the security to retrieve.</param>
    /// <param name="ownerUserId">Identifier of the owner requesting the security.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>The matching <see cref="SecurityDto"/>, or <c>null</c> when not found.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="id"/> is <see cref="Guid.Empty"/>.</exception>
    Task<SecurityDto?> GetAsync(Guid id, Guid ownerUserId, CancellationToken ct);

    /// <summary>
    /// Creates a new security for the specified owner.
    /// </summary>
    /// <param name="ownerUserId">Identifier of the owner creating the security.</param>
    /// <param name="name">Display name of the security. Required.</param>
    /// <param name="identifier">An external identifier (e.g. ISIN) or internal symbol. Required.</param>
    /// <param name="description">Optional description text.</param>
    /// <param name="alphaVantageCode">Optional code used for external price lookups (AlphaVantage).</param>
    /// <param name="currencyCode">ISO currency code (e.g. "USD", "EUR"). Required.</param>
    /// <param name="categoryId">Optional category id grouping the security.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>The created <see cref="SecurityDto"/> representing the new security.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/>, <paramref name="identifier"/> or <paramref name="currencyCode"/> is <c>null</c> or empty.</exception>
    Task<SecurityDto> CreateAsync(Guid ownerUserId, string name, string identifier, string? description, string? alphaVantageCode, string currencyCode, Guid? categoryId, CancellationToken ct);

    /// <summary>
    /// Updates an existing security and returns the updated DTO.
    /// </summary>
    /// <param name="id">Identifier of the security to update.</param>
    /// <param name="ownerUserId">Identifier of the owner performing the update.</param>
    /// <param name="name">New display name. Required.</param>
    /// <param name="identifier">New identifier/symbol. Required.</param>
    /// <param name="description">Optional description text.</param>
    /// <param name="alphaVantageCode">Optional external lookup code.</param>
    /// <param name="currencyCode">ISO currency code. Required.</param>
    /// <param name="categoryId">Optional category id.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>The updated <see cref="SecurityDto"/>, or <c>null</c> if the security does not exist.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="id"/> is <see cref="Guid.Empty"/>.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/>, <paramref name="identifier"/> or <paramref name="currencyCode"/> is <c>null</c> or empty.</exception>
    Task<SecurityDto?> UpdateAsync(Guid id, Guid ownerUserId, string name, string identifier, string? description, string? alphaVantageCode, string currencyCode, Guid? categoryId, CancellationToken ct);

    /// <summary>
    /// Archives (marks as inactive) the specified security.
    /// </summary>
    /// <param name="id">Identifier of the security to archive.</param>
    /// <param name="ownerUserId">Identifier of the owner performing the archive action.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns><c>true</c> when the security was archived; otherwise <c>false</c> (for example, when not found).</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="id"/> is <see cref="Guid.Empty"/>.</exception>
    Task<bool> ArchiveAsync(Guid id, Guid ownerUserId, CancellationToken ct);

    /// <summary>
    /// Deletes the specified security permanently.
    /// </summary>
    /// <param name="id">Identifier of the security to delete.</param>
    /// <param name="ownerUserId">Identifier of the owner performing the deletion.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns><c>true</c> when the security was deleted; otherwise <c>false</c>.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="id"/> is <see cref="Guid.Empty"/>.</exception>
    Task<bool> DeleteAsync(Guid id, Guid ownerUserId, CancellationToken ct);

    /// <summary>
    /// Returns the count of securities for the specified owner, optionally filtered by active state.
    /// </summary>
    /// <param name="ownerUserId">Identifier of the owner whose securities are counted.</param>
    /// <param name="onlyActive">If <c>true</c>, counts only active securities; if <c>false</c>, only inactive; if <c>null</c>, counts all.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>Non-negative integer representing the number of matching securities.</returns>
    Task<int> CountAsync(Guid ownerUserId, bool onlyActive, CancellationToken ct);

    /// <summary>
    /// Sets or clears the symbol attachment for a security.
    /// </summary>
    /// <param name="id">Identifier of the security to update.</param>
    /// <param name="ownerUserId">Identifier of the owner performing the update.</param>
    /// <param name="attachmentId">Attachment id to set as symbol, or <c>null</c> to clear the symbol.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>A task that completes when the operation has finished.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="id"/> is <see cref="Guid.Empty"/>.</exception>
    Task SetSymbolAttachmentAsync(Guid id, Guid ownerUserId, Guid? attachmentId, CancellationToken ct);
}
