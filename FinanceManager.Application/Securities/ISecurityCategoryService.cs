namespace FinanceManager.Application.Securities;

/// <summary>
/// Service to manage security categories for a user.
/// </summary>
public interface ISecurityCategoryService
{
    /// <summary>
    /// Lists security categories for the specified owner.
    /// </summary>
    /// <param name="ownerUserId">Identifier of the owner whose categories should be listed.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>A read-only list of <see cref="SecurityCategoryDto"/> instances belonging to the owner.</returns>
    Task<IReadOnlyList<SecurityCategoryDto>> ListAsync(Guid ownerUserId, CancellationToken ct);

    /// <summary>
    /// Gets a security category by id for the specified owner.
    /// </summary>
    /// <param name="id">Identifier of the category to retrieve.</param>
    /// <param name="ownerUserId">Identifier of the owner requesting the category.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>The matching <see cref="SecurityCategoryDto"/>, or <c>null</c> if not found.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="id"/> or <paramref name="ownerUserId"/> is <see cref="Guid.Empty"/>.</exception>
    Task<SecurityCategoryDto?> GetAsync(Guid id, Guid ownerUserId, CancellationToken ct);

    /// <summary>
    /// Creates a new security category for the specified owner.
    /// </summary>
    /// <param name="ownerUserId">Identifier of the owner creating the category.</param>
    /// <param name="name">Display name of the category. Required.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>The created <see cref="SecurityCategoryDto"/> representing the new category.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is <c>null</c> or empty.</exception>
    Task<SecurityCategoryDto> CreateAsync(Guid ownerUserId, string name, CancellationToken ct);

    /// <summary>
    /// Updates the display name of an existing security category.
    /// </summary>
    /// <param name="id">Identifier of the category to update.</param>
    /// <param name="ownerUserId">Identifier of the owner performing the update.</param>
    /// <param name="name">New display name for the category.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>The updated <see cref="SecurityCategoryDto"/>, or <c>null</c> if the category does not exist.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="id"/> or <paramref name="ownerUserId"/> is <see cref="Guid.Empty"/>.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is <c>null</c> or empty.</exception>
    Task<SecurityCategoryDto?> UpdateAsync(Guid id, Guid ownerUserId, string name, CancellationToken ct);

    /// <summary>
    /// Deletes a security category.
    /// </summary>
    /// <param name="id">Identifier of the category to delete.</param>
    /// <param name="ownerUserId">Identifier of the owner performing the deletion.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns><c>true</c> when the category was deleted; otherwise <c>false</c> (for example when not found or protected).</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="id"/> or <paramref name="ownerUserId"/> is <see cref="Guid.Empty"/>.</exception>
    Task<bool> DeleteAsync(Guid id, Guid ownerUserId, CancellationToken ct);

    /// <summary>
    /// Sets or clears the symbol attachment for a security category.
    /// </summary>
    /// <param name="id">Identifier of the category to update.</param>
    /// <param name="ownerUserId">Identifier of the owner performing the update.</param>
    /// <param name="attachmentId">Attachment id to set as symbol, or <c>null</c> to clear.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>A task that completes when the operation has finished.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="id"/> or <paramref name="ownerUserId"/> is <see cref="Guid.Empty"/>.</exception>
    Task SetSymbolAttachmentAsync(Guid id, Guid ownerUserId, Guid? attachmentId, CancellationToken ct);
}