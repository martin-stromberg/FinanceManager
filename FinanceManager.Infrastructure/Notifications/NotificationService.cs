using FinanceManager.Application.Notifications;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Notifications;

/// <summary>
/// Service for reading and updating application notifications.
/// Provides methods to list active notifications and to dismiss a notification.
/// </summary>
public sealed class NotificationService : INotificationService
{
    private readonly AppDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationService"/> class.
    /// </summary>
    /// <param name="db">The application's <see cref="AppDbContext"/> used to read and persist notifications.</param>
    public NotificationService(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Returns active notifications for the specified owner that are scheduled at or before the given date.
    /// Active notifications are enabled and not dismissed. Notifications with a null <c>OwnerUserId</c> are considered global
    /// (visible to all users).
    /// </summary>
    /// <param name="ownerUserId">The owner user id for which to list notifications.</param>
    /// <param name="asOfUtc">The UTC date used to filter scheduled notifications; only notifications with <c>ScheduledDateUtc</c> &lt;= this date are returned.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of <see cref="NotificationDto"/> matching the criteria.</returns>
    public async Task<IReadOnlyList<NotificationDto>> ListActiveAsync(Guid ownerUserId, DateTime asOfUtc, CancellationToken ct)
    {
        var nowDateUtc = asOfUtc.Date;
        var items = await _db.Notifications.AsNoTracking()
            .Where(n => (n.OwnerUserId == ownerUserId || n.OwnerUserId == null)
                        && n.IsEnabled
                        && !n.IsDismissed
                        && n.ScheduledDateUtc <= nowDateUtc)
            .OrderByDescending(n => n.ScheduledDateUtc)
            .ThenByDescending(n => n.CreatedUtc)
            .Select(n => new NotificationDto(n.Id, n.Title, n.Message, (int)n.Type, (int)n.Target, n.ScheduledDateUtc, n.IsDismissed, n.CreatedUtc, n.TriggerEventKey))
            .ToListAsync(ct);
        return items;
    }

    /// <summary>
    /// Dismisses a notification for the given owner (or a global notification). When dismissed, the notification's <c>IsDismissed</c>
    /// flag is set and the modification timestamp is updated. If the notification encodes a security error in its <c>TriggerEventKey</c>
    /// (prefix "security:error:"), this method will attempt to clear the corresponding security entity error state.
    /// </summary>
    /// <param name="id">Identifier of the notification to dismiss.</param>
    /// <param name="ownerUserId">Owner user identifier performing the dismissal. Global notifications (OwnerUserId == null) can also be dismissed by this call.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <c>true</c> when the notification existed and was dismissed; otherwise <c>false</c> when the notification was not found or not accessible.
    /// </returns>
    public async Task<bool> DismissAsync(Guid id, Guid ownerUserId, CancellationToken ct)
    {
        var entity = await _db.Notifications.FirstOrDefaultAsync(n => n.Id == id && (n.OwnerUserId == ownerUserId || n.OwnerUserId == null), ct);
        if (entity is null)
        {
            return false;
        }
        entity.IsDismissed = true;
        entity.ModifiedUtc = DateTime.UtcNow;

        // NEW: Wenn die Notification einen Security-Error bestätigt, den Block aufheben
        if (!string.IsNullOrWhiteSpace(entity.TriggerEventKey) && entity.TriggerEventKey.StartsWith("security:error:", StringComparison.OrdinalIgnoreCase))
        {
            var idStr = entity.TriggerEventKey["security:error:".Length..];
            if (Guid.TryParse(idStr, out var securityId))
            {
                var sec = await _db.Securities.FirstOrDefaultAsync(s => s.Id == securityId && s.OwnerUserId == ownerUserId, ct);
                if (sec != null)
                {
                    sec.ClearPriceError();
                }
            }
        }

        await _db.SaveChangesAsync(ct);
        return true;
    }
}
