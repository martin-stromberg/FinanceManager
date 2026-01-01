namespace FinanceManager.Domain.Contacts;

/// <summary>
/// Category used to group contacts for the user.
/// </summary>
public sealed class ContactCategory : Entity, IAggregateRoot
{
    private ContactCategory() { }

    /// <summary>
    /// Creates a new contact category for the specified owner.
    /// </summary>
    /// <param name="ownerUserId">Owner user id.</param>
    /// <param name="name">Category display name.</param>
    public ContactCategory(Guid ownerUserId, string name)
    {
        OwnerUserId = Guards.NotEmpty(ownerUserId, nameof(ownerUserId));
        Name = Guards.NotNullOrWhiteSpace(name, nameof(name));
    }

    /// <summary>
    /// Owner user identifier.
    /// </summary>
    public Guid OwnerUserId { get; private set; }

    /// <summary>
    /// Category display name.
    /// </summary>
    public string Name { get; private set; } = null!;

    /// <summary>
    /// Optional symbol attachment id for the category.
    /// </summary>
    public Guid? SymbolAttachmentId { get; private set; }

    /// <summary>
    /// Renames the contact category.
    /// </summary>
    /// <param name="name">New display name.</param>
    public void Rename(string name)
    {
        Name = Guards.NotNullOrWhiteSpace(name, nameof(name));
        Touch();
    }

    /// <summary>
    /// Sets or clears the symbol attachment for the category.
    /// </summary>
    /// <param name="attachmentId">Attachment id or null to clear.</param>
    public void SetSymbolAttachment(Guid? attachmentId)
    {
        SymbolAttachmentId = attachmentId == Guid.Empty ? null : attachmentId;
        Touch();
    }
}