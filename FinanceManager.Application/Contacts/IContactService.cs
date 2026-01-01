namespace FinanceManager.Application.Contacts;

/// <summary>
/// Service for managing contacts and related aliases.
/// </summary>
public interface IContactService
{
    /// <summary>
    /// Creates a new contact.
    /// </summary>
    Task<ContactDto> CreateAsync(Guid ownerUserId, string name, ContactType type, Guid? categoryId, string? description, bool? isPaymentIntermediary, CancellationToken ct);

    /// <summary>
    /// Updates an existing contact and returns the updated DTO or null when not found.
    /// </summary>
    Task<ContactDto?> UpdateAsync(Guid id, Guid ownerUserId, string name, ContactType type, Guid? categoryId, string? description, bool? isPaymentIntermediary, CancellationToken ct);

    /// <summary>
    /// Deletes a contact.
    /// </summary>
    Task<bool> DeleteAsync(Guid id, Guid ownerUserId, CancellationToken ct);

    /// <summary>
    /// Lists contacts with optional filtering by type and name.
    /// </summary>
    Task<IReadOnlyList<ContactDto>> ListAsync(Guid ownerUserId, int skip, int take, ContactType? type, string? nameFilter, CancellationToken ct);

    /// <summary>
    /// Gets a contact by id or null when not found.
    /// </summary>
    Task<ContactDto?> GetAsync(Guid id, Guid ownerUserId, CancellationToken ct);

    /// <summary>
    /// Adds an alias pattern to a contact.
    /// </summary>
    Task AddAliasAsync(Guid contactId, Guid ownerUserId, string pattern, CancellationToken ct);

    /// <summary>
    /// Deletes an alias by id for the given contact.
    /// </summary>
    Task DeleteAliasAsync(Guid contactId, Guid ownerUserId, Guid aliasId, CancellationToken ct);

    /// <summary>
    /// Lists aliases for the given contact.
    /// </summary>
    Task<IReadOnlyList<AliasNameDto>> ListAliases(Guid id, Guid userId, CancellationToken ct);

    /// <summary>
    /// Merges two contacts into a single target contact according to the preference strategy.
    /// </summary>
    Task<ContactDto> MergeAsync(Guid ownerUserId, Guid sourceContactId, Guid targetContactId, CancellationToken ct, FinanceManager.Shared.Dtos.Contacts.MergePreference preference = FinanceManager.Shared.Dtos.Contacts.MergePreference.DestinationFirst);

    /// <summary>
    /// Returns the total number of contacts for the owner.
    /// </summary>
    Task<int> CountAsync(Guid ownerUserId, CancellationToken ct);

    /// <summary>
    /// Assigns or clears a symbol attachment for a contact.
    /// </summary>
    Task SetSymbolAttachmentAsync(Guid id, Guid ownerUserId, Guid? attachmentId, CancellationToken ct);
}
