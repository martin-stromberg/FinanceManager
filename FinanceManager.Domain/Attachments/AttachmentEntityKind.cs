namespace FinanceManager.Domain.Attachments;

/// <summary>
/// Enum describing the entity kinds that attachments can be associated with.
/// </summary>
public enum AttachmentEntityKind : short
{
    /// <summary>
    /// No entity specified.
    /// </summary>
    None = -1,

    /// <summary>
    /// Attachment belongs to a statement draft entry.
    /// </summary>
    StatementDraftEntry = 0,

    /// <summary>
    /// Attachment belongs to a statement entry (posted).
    /// </summary>
    StatementEntry = 1,

    /// <summary>
    /// Attachment belongs to a contact.
    /// </summary>
    Contact = 2,

    /// <summary>
    /// Attachment belongs to a savings plan.
    /// </summary>
    SavingsPlan = 3,

    /// <summary>
    /// Attachment belongs to a security.
    /// </summary>
    Security = 4,

    /// <summary>
    /// Attachment belongs to an account.
    /// </summary>
    Account = 5,

    /// <summary>
    /// Attachment belongs to a statement import.
    /// </summary>
    StatementImport = 6,

    /// <summary>
    /// Attachment belongs to a posting.
    /// </summary>
    Posting = 7,

    /// <summary>
    /// Attachment belongs to a statement draft.
    /// </summary>
    StatementDraft = 8,

    /// <summary>
    /// Attachment belongs to a contact category.
    /// </summary>
    ContactCategory = 9,

    /// <summary>
    /// Attachment belongs to a savings plan category.
    /// </summary>
    SavingsPlanCategory = 10,

    /// <summary>
    /// Attachment belongs to a security category.
    /// </summary>
    SecurityCategory = 11
}
