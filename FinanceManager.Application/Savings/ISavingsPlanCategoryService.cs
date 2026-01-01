namespace FinanceManager.Application.Savings;

/// <summary>
/// Service to manage savings plan categories for a user.
/// </summary>
public interface ISavingsPlanCategoryService
{
    /// <summary>
    /// Lists all savings plan categories for the specified owner.
    /// </summary>
    /// <param name="ownerUserId">The owner user identifier for which to list categories.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>A read-only list of <see cref="SavingsPlanCategoryDto"/> instances belonging to the owner.</returns>
    Task<IReadOnlyList<SavingsPlanCategoryDto>> ListAsync(Guid ownerUserId, CancellationToken ct);

    /// <summary>
    /// Gets a single savings plan category by id for the specified owner.
    /// </summary>
    /// <param name="id">The category identifier to retrieve.</param>
    /// <param name="ownerUserId">The owner user identifier.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>The matching <see cref="SavingsPlanCategoryDto"/>, or <c>null</c> if not found.</returns>
    Task<SavingsPlanCategoryDto?> GetAsync(Guid id, Guid ownerUserId, CancellationToken ct);

    /// <summary>
    /// Creates a new savings plan category for the specified owner.
    /// </summary>
    /// <param name="ownerUserId">The owner user identifier.</param>
    /// <param name="name">Display name of the new category.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>The created <see cref="SavingsPlanCategoryDto"/>.</returns>
    Task<SavingsPlanCategoryDto> CreateAsync(Guid ownerUserId, string name, CancellationToken ct);

    /// <summary>
    /// Updates the name of an existing savings plan category.
    /// </summary>
    /// <param name="id">Identifier of the category to update.</param>
    /// <param name="ownerUserId">Owner user identifier performing the update.</param>
    /// <param name="name">New display name for the category.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>The updated <see cref="SavingsPlanCategoryDto"/>, or <c>null</c> if the category does not exist.</returns>
    Task<SavingsPlanCategoryDto?> UpdateAsync(Guid id, Guid ownerUserId, string name, CancellationToken ct);

    /// <summary>
    /// Deletes a savings plan category.
    /// </summary>
    /// <param name="id">Identifier of the category to delete.</param>
    /// <param name="ownerUserId">Owner user identifier performing the deletion.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns><c>true</c> when the category was deleted; otherwise <c>false</c> (for example, when not found or protected).</returns>
    Task<bool> DeleteAsync(Guid id, Guid ownerUserId, CancellationToken ct);

    /// <summary>
    /// Sets or clears the symbol attachment for a savings plan category.
    /// </summary>
    /// <param name="id">Identifier of the category to update.</param>
    /// <param name="ownerUserId">Owner user identifier performing the update.</param>
    /// <param name="attachmentId">Attachment id to set as symbol, or <c>null</c> to clear.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    Task SetSymbolAttachmentAsync(Guid id, Guid ownerUserId, Guid? attachmentId, CancellationToken ct);
}