namespace FinanceManager.Shared.Dtos.Postings;

/// <summary>
/// Specifies the kind of posting (source/target domain) for a transaction line.
/// </summary>
public enum PostingKind
{
    /// <summary>
    /// Posting related to a bank account.
    /// </summary>
    Bank = 0,

    /// <summary>
    /// Posting related to a contact (person or organization).
    /// </summary>
    Contact = 1,

    /// <summary>
    /// Posting related to a savings plan.
    /// </summary>
    SavingsPlan = 2,

    /// <summary>
    /// Posting related to a security (investment instrument).
    /// </summary>
    Security = 3
}
