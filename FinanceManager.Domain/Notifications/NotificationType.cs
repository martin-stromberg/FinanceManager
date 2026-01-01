namespace FinanceManager.Domain.Notifications;

/// <summary>
/// Types of notifications supported by the system.
/// </summary>
public enum NotificationType
{
    /// <summary>
    /// Monthly reminder type.
    /// </summary>
    MonthlyReminder = 0,

    /// <summary>
    /// Event-driven notification.
    /// </summary>
    EventDriven = 1,

    /// <summary>
    /// System alert notification.
    /// </summary>
    SystemAlert = 2
}
