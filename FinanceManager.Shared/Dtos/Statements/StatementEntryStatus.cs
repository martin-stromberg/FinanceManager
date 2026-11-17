namespace FinanceManager.Shared.Dtos.Statements;

/// <summary>
/// Committed status of a <c>StatementEntry</c> as exposed to clients (DTO-level mirror of
/// the domain status; values are identical and normative).
/// </summary>
public enum StatementEntryStatus
{
    /// <summary>Entry is pending processing.</summary>
    Pending = 0,

    /// <summary>Entry has been booked/posted.</summary>
    Booked = 1,

    /// <summary>Entry was recognized as a duplicate and ignored.</summary>
    IgnoredDuplicate = 2
}
