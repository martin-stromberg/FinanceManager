namespace FinanceManager.Domain.Notifications;

/// <summary>
/// Target location where a notification should be presented to the user.
/// </summary>
public enum NotificationTarget
{
    /// <summary>
    /// Show on the home page.
    /// </summary>
    HomePage = 0,

    /// <summary>
    /// Show on the dashboard.
    /// </summary>
    Dashboard = 1,

    /// <summary>
    /// Show in a modal dialog.
    /// </summary>
    Modal = 2,

    /// <summary>
    /// Show as a transient toast message.
    /// </summary>
    Toast = 3
}
