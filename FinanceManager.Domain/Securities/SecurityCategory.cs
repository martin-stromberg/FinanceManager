namespace FinanceManager.Domain.Securities;

/// <summary>
/// Represents a user-defined category for securities. Categories can have an optional symbol attachment.
/// </summary>
public sealed class SecurityCategory
{
    /// <summary>
    /// Gets the identifier of the category.
    /// </summary>
    /// <value>The category GUID.</value>
    public Guid Id { get; private set; }

    /// <summary>
    /// Gets the identifier of the user who owns this category.
    /// </summary>
    /// <value>The owner user's GUID.</value>
    public Guid OwnerUserId { get; private set; }

    /// <summary>
    /// Gets the display name of the category.
    /// </summary>
    /// <value>The category name.</value>
    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// Optional reference to a symbol attachment associated with this category.
    /// Passing <see cref="Guid.Empty"/> when setting is treated as clearing the attachment (null).
    /// </summary>
    /// <value>The attachment GUID or <c>null</c>.</value>
    public Guid? SymbolAttachmentId { get; private set; }

    /// <summary>
    /// Parameterless constructor for ORM/deserialization.
    /// </summary>
    private SecurityCategory() { }

    /// <summary>
    /// Creates a new <see cref="SecurityCategory"/> for the specified owner with the given name.
    /// </summary>
    /// <param name="ownerUserId">The identifier of the user who owns this category.</param>
    /// <param name="name">The display name of the category. Must not be null or whitespace.</param>
    public SecurityCategory(Guid ownerUserId, string name)
    {
        Id = Guid.NewGuid();
        OwnerUserId = ownerUserId;
        Rename(name);
    }

    /// <summary>
    /// Renames the category.
    /// </summary>
    /// <param name="name">New name for the category. Must not be null or whitespace.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is null, empty or consists only of white-space characters.</exception>
    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name required", nameof(name));
        }
        Name = name.Trim();
    }

    /// <summary>
    /// Sets or clears the symbol attachment reference for this category.
    /// </summary>
    /// <param name="attachmentId">Attachment GUID to set, or <see cref="Guid.Empty"/>/<c>null</c> to clear.</param>
    public void SetSymbolAttachment(Guid? attachmentId)
    {
        SymbolAttachmentId = attachmentId == Guid.Empty ? null : attachmentId;
    }
}