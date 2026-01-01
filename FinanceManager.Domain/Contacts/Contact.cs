namespace FinanceManager.Domain.Contacts;

/// <summary>
/// Domain entity representing a contact belonging to a user.
/// Stores metadata like name, type, category and optional symbol attachment.
/// </summary>
public sealed class Contact : Entity, IAggregateRoot
{
    private Contact() { }
    /// <summary>
    /// Creates a new contact for the given owner.
    /// </summary>
    /// <param name="ownerUserId">Owner user identifier.</param>
    /// <param name="name">Display name of the contact.</param>
    /// <param name="type">Contact type.</param>
    /// <param name="categoryId">Optional category id.</param>
    /// <param name="description">Optional description text.</param>
    /// <param name="isPaymentIntermediary">Optional flag indicating payment intermediary.</param>
    public Contact(Guid ownerUserId, string name, ContactType type, Guid? categoryId, string? description = null, bool? isPaymentIntermediary = false)
    {
        OwnerUserId = Guards.NotEmpty(ownerUserId, nameof(ownerUserId));
        Name = Guards.NotNullOrWhiteSpace(name, nameof(name));
        Type = type;
        CategoryId = categoryId;
        Description = description;
        IsPaymentIntermediary = isPaymentIntermediary ?? false;
    }

    /// <summary>
    /// Identifier of the user who owns the contact.
    /// </summary>
    public Guid OwnerUserId { get; private set; }

    /// <summary>
    /// Display name of the contact.
    /// </summary>
    public string Name { get; private set; } = null!;

    /// <summary>
    /// Type/category of the contact.
    /// </summary>
    public ContactType Type { get; private set; }

    /// <summary>
    /// Optional category id the contact belongs to.
    /// </summary>
    public Guid? CategoryId { get; private set; }

    /// <summary>
    /// Optional description text for the contact.
    /// </summary>
    public string? Description { get; private set; }

    /// <summary>
    /// Flag indicating whether this contact is used as a payment intermediary.
    /// </summary>
    public bool IsPaymentIntermediary { get; private set; }

    /// <summary>
    /// Optional symbol attachment id associated with the contact.
    /// </summary>
    public Guid? SymbolAttachmentId { get; private set; }

    /// <summary>
    /// Renames the contact.
    /// </summary>
    /// <param name="name">New display name.</param>
    public void Rename(string name)
    {
        Name = Guards.NotNullOrWhiteSpace(name, nameof(name));
        Touch();
    }

    /// <summary>
    /// Changes the contact type.
    /// </summary>
    /// <param name="type">New contact type.</param>
    public void ChangeType(ContactType type)
    {
        Type = type;
        Touch();
    }

    /// <summary>
    /// Sets or clears the contact category.
    /// </summary>
    /// <param name="categoryId">Category id or null to clear.</param>
    public void SetCategory(Guid? categoryId)
    {
        CategoryId = categoryId;
        Touch();
    }

    /// <summary>
    /// Sets the description text for the contact.
    /// </summary>
    /// <param name="description">Description text or null to clear.</param>
    public void SetDescription(string? description)
    {
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        Touch();
    }

    /// <summary>
    /// Sets whether the contact is a payment intermediary.
    /// </summary>
    /// <param name="value">Boolean flag value.</param>
    public void SetPaymentIntermediary(bool value)
    {
        IsPaymentIntermediary = value;
        Touch();
    }

    /// <summary>
    /// Sets or clears the symbol attachment for the contact.
    /// </summary>
    /// <param name="attachmentId">Attachment id or null to clear.</param>
    public void SetSymbolAttachment(Guid? attachmentId)
    {
        SymbolAttachmentId = attachmentId == Guid.Empty ? null : attachmentId;
        Touch();
    }
}