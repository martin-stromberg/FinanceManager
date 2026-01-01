using System.ComponentModel.DataAnnotations;

namespace FinanceManager.Domain.Notifications;

/// <summary>
/// Represents a user or system notification displayed in the UI or scheduled to appear.
/// </summary>
public sealed class Notification
{
    /// <summary>
    /// Unique notification identifier.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Optional owner user identifier; when null the notification is global.
    /// </summary>
    public Guid? OwnerUserId { get; set; }

    /// <summary>
    /// Short title of the notification.
    /// </summary>
    [MaxLength(140)]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Detailed message text.
    /// </summary>
    [MaxLength(1000)]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Notification type used to classify and filter notifications.
    /// </summary>
    public NotificationType Type { get; set; }

    /// <summary>
    /// Target area where the notification should be displayed (homepage, dashboard, modal, toast).
    /// </summary>
    public NotificationTarget Target { get; set; } = NotificationTarget.HomePage;

    /// <summary>
    /// Scheduled date in UTC when the notification becomes active.
    /// </summary>
    public DateTime ScheduledDateUtc { get; set; }

    /// <summary>
    /// Indicates whether the notification is enabled.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Indicates whether the notification has been dismissed by the user.
    /// </summary>
    public bool IsDismissed { get; set; } = false;

    /// <summary>
    /// Optional event key to trigger the notification.
    /// </summary>
    [MaxLength(120)]
    public string? TriggerEventKey { get; set; }

    /// <summary>
    /// Creation timestamp in UTC.
    /// </summary>
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last modification timestamp in UTC.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }
}
