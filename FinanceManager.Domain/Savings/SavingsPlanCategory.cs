namespace FinanceManager.Domain.Savings;

/// <summary>
/// Represents a category for savings plans owned by a user. Categories can have an optional symbol attachment.
/// </summary>
public sealed class SavingsPlanCategory
{
    /// <summary>
    /// Gets the identifier of the savings plan category.
    /// </summary>
    /// <value>The category GUID.</value>
    public Guid Id { get; private set; }

    /// <summary>
    /// Gets the owner user identifier for this category.
    /// </summary>
    /// <value>The owner's user GUID.</value>
    public Guid OwnerUserId { get; private set; }

    /// <summary>
    /// Gets the category name.
    /// </summary>
    /// <value>The category name string.</value>
    public string Name { get; private set; }

    /// <summary>
    /// Optional reference to a symbol attachment associated with this category.
    /// Passing <see cref="Guid.Empty"/> is treated as <c>null</c> when setting.
    /// </summary>
    /// <value>Attachment GUID or <c>null</c>.</value>
    public Guid? SymbolAttachmentId { get; private set; }

    /// <summary>
    /// Creates a new <see cref="SavingsPlanCategory"/> instance.
    /// </summary>
    /// <param name="ownerUserId">The identifier of the user who owns the category.</param>
    /// <param name="name">The display name of the category.</param>
    public SavingsPlanCategory(Guid ownerUserId, string name)
    {
        Id = Guid.NewGuid();
        OwnerUserId = ownerUserId;
        Name = name;
    }

    /// <summary>
    /// Renames the category.
    /// </summary>
    /// <param name="name">New name for the category.</param>
    public void Rename(string name) => Name = name;

    /// <summary>
    /// Sets or clears the symbol attachment reference. Passing <see cref="Guid.Empty"/> clears the attachment.
    /// </summary>
    /// <param name="attachmentId">Attachment GUID to set, or <see cref="Guid.Empty"/>/<c>null</c> to clear.</param>
    public void SetSymbolAttachment(Guid? attachmentId)
    {
        SymbolAttachmentId = attachmentId == Guid.Empty ? null : attachmentId;
    }
}