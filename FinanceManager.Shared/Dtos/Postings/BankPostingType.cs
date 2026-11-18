namespace FinanceManager.Shared.Dtos.Postings;

/// <summary>
/// Specifies the posting type (posting kind classification) of a bank posting or statement draft entry.
/// </summary>
public enum BankPostingType
{
    /// <summary>
    /// No posting type determined (default; used for non-bank postings and not yet classified entries).
    /// </summary>
    Unspecified = 0,

    /// <summary>
    /// Incoming posting (positive amount).
    /// </summary>
    Inflow = 1,

    /// <summary>
    /// Outgoing posting (negative amount).
    /// </summary>
    Outflow = 2,

    /// <summary>
    /// Interest posting.
    /// </summary>
    Interest = 3,

    /// <summary>
    /// Tax posting.
    /// </summary>
    Tax = 4
}
