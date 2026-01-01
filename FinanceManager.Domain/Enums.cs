namespace FinanceManager.Domain;

/// <summary>
/// Roles for account sharing permissions.
/// </summary>
public enum AccountShareRole
{
    /// <summary>
    /// Read-only access to the shared account.
    /// </summary>
    Read = 0,

    /// <summary>
    /// Read and write access.
    /// </summary>
    Write = 1,

    /// <summary>
    /// Administrative access including management of shares.
    /// </summary>
    Admin = 2
}

/// <summary>
/// Processing status of a statement entry.
/// </summary>
public enum StatementEntryStatus
{
    /// <summary>
    /// Entry is pending processing.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Entry has been booked/posted.
    /// </summary>
    Booked = 1,

    /// <summary>
    /// Entry was recognized as a duplicate and ignored.
    /// </summary>
    IgnoredDuplicate = 2
}




