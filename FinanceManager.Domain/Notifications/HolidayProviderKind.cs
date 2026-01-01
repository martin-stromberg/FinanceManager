namespace FinanceManager.Domain.Notifications;

/// <summary>
/// Available holiday provider kinds used by the notifications subsystem.
/// </summary>
public enum HolidayProviderKind
{
    /// <summary>
    /// In-memory provider (for testing or static lists).
    /// </summary>
    Memory = 0,

    /// <summary>
    /// Nager.Date public holiday provider.
    /// </summary>
    NagerDate = 1
}
