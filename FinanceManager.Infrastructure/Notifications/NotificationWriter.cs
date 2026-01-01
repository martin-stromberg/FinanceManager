using FinanceManager.Application.Notifications;
using FinanceManager.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Infrastructure.Notifications;

/// <summary>
/// Writes notifications into the persistent store for users, administrators or globally.
/// Handles creation of notification entities and logs failures instead of throwing to the caller.
/// </summary>
public sealed class NotificationWriter : INotificationWriter
{
    private readonly AppDbContext _db;
    private readonly ILogger<NotificationWriter> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationWriter"/> class.
    /// </summary>
    /// <param name="db">Database context used to persist notification entities.</param>
    /// <param name="logger">Logger used to record errors when notification creation fails.</param>
    public NotificationWriter(AppDbContext db, ILogger<NotificationWriter> logger)
    {
        _db = db; _logger = logger;
    }

    /// <summary>
    /// Creates a notification targeted at a specific user.
    /// The method swallows exceptions and logs them; callers should not rely on exceptions for control flow.
    /// </summary>
    /// <param name="ownerUserId">The user id that should receive the notification.</param>
    /// <param name="title">Short title of the notification.</param>
    /// <param name="message">Detailed message text.</param>
    /// <param name="type">The notification type.</param>
    /// <param name="target">UI target where the notification should point to.</param>
    /// <param name="scheduledDateUtc">Date (UTC) when the notification becomes active.</param>
    /// <param name="triggerEventKey">Optional trigger key used to correlate notifications with events.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the write operation has finished. Exceptions are logged and not propagated.</returns>
    public async Task CreateForUserAsync(Guid ownerUserId, string title, string message, NotificationType type, NotificationTarget target, DateTime scheduledDateUtc, string? triggerEventKey, CancellationToken ct)
    {
        try
        {
            _db.Notifications.Add(new Notification
            {
                OwnerUserId = ownerUserId,
                Title = title,
                Message = message,
                Type = type,
                Target = target,
                ScheduledDateUtc = scheduledDateUtc,
                IsEnabled = true,
                IsDismissed = false,
                CreatedUtc = DateTime.UtcNow,
                TriggerEventKey = triggerEventKey
            });
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create notification for user {UserId}", ownerUserId);
        }
    }

    /// <summary>
    /// Creates a notification for all users with administrator privileges.
    /// The method queries current admins and creates per-user notifications. Errors are logged and not thrown.
    /// </summary>
    /// <param name="title">Short title of the notification.</param>
    /// <param name="message">Detailed message text.</param>
    /// <param name="type">The notification type.</param>
    /// <param name="target">UI target where the notification should point to.</param>
    /// <param name="scheduledDateUtc">Date (UTC) when the notification becomes active.</param>
    /// <param name="triggerEventKey">Optional trigger key used to correlate notifications with events.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when admin notifications have been created (or an error has been logged).</returns>
    public async Task CreateForAdminsAsync(string title, string message, NotificationType type, NotificationTarget target, DateTime scheduledDateUtc, string? triggerEventKey, CancellationToken ct)
    {
        try
        {
            var admins = await _db.Users.AsNoTracking().Where(u => u.IsAdmin && u.Active).Select(u => u.Id).ToListAsync(ct);
            if (admins.Count == 0) return;
            foreach (var id in admins)
            {
                _db.Notifications.Add(new Notification
                {
                    OwnerUserId = id,
                    Title = title,
                    Message = message,
                    Type = type,
                    Target = target,
                    ScheduledDateUtc = scheduledDateUtc,
                    IsEnabled = true,
                    IsDismissed = false,
                    CreatedUtc = DateTime.UtcNow,
                    TriggerEventKey = triggerEventKey,
                });
            }
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create admin notifications");
        }
    }

    /// <summary>
    /// Creates a global notification visible to all users. The notification <c>OwnerUserId</c> is set to <c>null</c>.
    /// Errors are logged and not rethrown.
    /// </summary>
    /// <param name="title">Short title of the notification.</param>
    /// <param name="message">Detailed message text.</param>
    /// <param name="type">The notification type.</param>
    /// <param name="target">UI target where the notification should point to.</param>
    /// <param name="scheduledDateUtc">Date (UTC) when the notification becomes active.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the global notification has been persisted or an error has been logged.</returns>
    public async Task CreateGlobalAsync(string title, string message, NotificationType type, NotificationTarget target, DateTime scheduledDateUtc, CancellationToken ct)
    {
        try
        {
            _db.Notifications.Add(new Notification
            {
                OwnerUserId = null,
                Title = title,
                Message = message,
                Type = type,
                Target = target,
                ScheduledDateUtc = scheduledDateUtc,
                IsEnabled = true,
                IsDismissed = false,
                CreatedUtc = DateTime.UtcNow
            });
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create global notification");
        }
    }
}
