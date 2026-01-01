namespace FinanceManager.Application.Contacts;

/// <summary>
/// Service to manage contact categories for a user.
/// </summary>
public interface IContactCategoryService
{
    /// <summary>
    /// Lists all contact categories for the owner.
    /// </summary>
    Task<IReadOnlyList<ContactCategoryDto>> ListAsync(Guid ownerUserId, CancellationToken ct);

    /// <summary>
    /// Creates a new contact category for the owner.
    /// </summary>
    Task<ContactCategoryDto> CreateAsync(Guid ownerUserId, string name, CancellationToken ct);

    /// <summary>
    /// Sets or clears the symbol attachment for the category.
    /// </summary>
    Task SetSymbolAttachmentAsync(Guid id, Guid ownerUserId, Guid? attachmentId, CancellationToken ct);

    /// <summary>
    /// Gets a contact category by id or null when not found.
    /// </summary>
    Task<ContactCategoryDto?> GetAsync(Guid id, Guid ownerUserId, CancellationToken ct);

    /// <summary>
    /// Updates the name of a contact category.
    /// </summary>
    Task UpdateAsync(Guid id, Guid ownerUserId, string name, CancellationToken ct);

    /// <summary>
    /// Deletes a contact category.
    /// </summary>
    Task DeleteAsync(Guid id, Guid ownerUserId, CancellationToken ct);
}