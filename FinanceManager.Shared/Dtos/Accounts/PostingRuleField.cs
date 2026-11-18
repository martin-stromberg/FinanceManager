namespace FinanceManager.Shared.Dtos.Accounts;

/// <summary>
/// Specifies the text field of a posting or draft entry that a posting rule matches against.
/// </summary>
public enum PostingRuleField
{
    /// <summary>
    /// The subject (purpose) text field.
    /// </summary>
    Subject = 0,

    /// <summary>
    /// The booking description text field.
    /// </summary>
    BookingDescription = 1
}
