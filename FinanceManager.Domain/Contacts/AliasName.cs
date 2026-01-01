namespace FinanceManager.Domain.Contacts;

/// <summary>
/// Represents an alias pattern for a contact used to match imported postings.
/// </summary>
public sealed class AliasName : Entity
{
    /// <summary>
    /// Creates a new alias pattern for the specified contact.
    /// </summary>
    /// <param name="contactId">Contact identifier the alias belongs to.</param>
    /// <param name="pattern">Alias pattern string.</param>
    public AliasName(Guid contactId, string pattern)
    {
        ContactId = Guards.NotEmpty(contactId, nameof(contactId));
        Pattern = Guards.NotNullOrWhiteSpace(pattern, nameof(pattern));
    }

    /// <summary>
    /// Identifier of the contact this alias is associated with.
    /// </summary>
    public Guid ContactId { get; private set; }

    /// <summary>
    /// Alias pattern used for matching (e.g. contains or regex pattern).
    /// </summary>
    public string Pattern { get; private set; } = null!;

    /// <summary>
    /// Reassigns this alias to a different contact.
    /// </summary>
    /// <param name="newContactId">Target contact identifier.</param>
    public void ReassignTo(Guid newContactId)
    {
        ContactId = newContactId;
    }
}