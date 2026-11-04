using FinanceManager.Application.Mcp;
using FinanceManager.Application.Notifications;
using FinanceManager.Domain.Notifications;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace FinanceManager.Web.Mcp;

/// <summary>
/// Lightweight anomaly detection of the MCP surface (sicherheitskonzept.md §6.3): counts
/// scope denials per access in a sliding window; on threshold crossing the owner is notified
/// (EventDriven) and admins receive a SystemAlert — once per cooldown period.
/// </summary>
public sealed class McpAnomalyTracker
{
    private readonly IOptionsMonitor<McpOptions> _options;
    private readonly IServiceProvider _services;
    private readonly ILogger<McpAnomalyTracker> _logger;
    private readonly ConcurrentDictionary<Guid, Queue<DateTime>> _denials = new();
    private readonly ConcurrentDictionary<Guid, DateTime> _lastNotification = new();

    /// <summary>Creates the tracker.</summary>
    public McpAnomalyTracker(IOptionsMonitor<McpOptions> options, IServiceProvider services, ILogger<McpAnomalyTracker> logger)
    {
        _options = options;
        _services = services;
        _logger = logger;
    }

    /// <summary>
    /// Records a scope denial of an access and triggers notifications on threshold crossing.
    /// </summary>
    /// <param name="accessId">The denied access.</param>
    /// <param name="ownerUserId">The access owner.</param>
    /// <param name="accessName">Access name snapshot for the notification.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task RecordScopeDenialAsync(Guid accessId, Guid ownerUserId, string accessName, CancellationToken ct)
    {
        var anomaly = _options.CurrentValue.Anomaly;
        var now = DateTime.UtcNow;
        var window = TimeSpan.FromMinutes(Math.Max(1, anomaly.ScopeDenialWindowMinutes));

        var queue = _denials.GetOrAdd(accessId, _ => new Queue<DateTime>());
        int count;
        lock (queue)
        {
            while (queue.Count > 0 && now - queue.Peek() > window)
            {
                queue.Dequeue();
            }
            queue.Enqueue(now);
            count = queue.Count;
        }
        if (count < anomaly.ScopeDenialThreshold)
        {
            return;
        }
        if (_lastNotification.TryGetValue(accessId, out var last)
            && now - last < TimeSpan.FromMinutes(Math.Max(1, anomaly.NotificationCooldownMinutes)))
        {
            return;
        }
        _lastNotification[accessId] = now;

        try
        {
            await using var scope = _services.CreateAsyncScope();
            var writer = scope.ServiceProvider.GetRequiredService<INotificationWriter>();
            var title = "MCP security alert: repeated scope denials";
            var msg = $"MCP access \"{accessName}\" exceeded {anomaly.ScopeDenialThreshold} scope denials within {anomaly.ScopeDenialWindowMinutes} minutes.";
            await writer.CreateForUserAsync(ownerUserId, title, msg, NotificationType.EventDriven, NotificationTarget.HomePage, now.Date, $"mcp-access:{accessId}", ct);
            await writer.CreateForAdminsAsync(title, msg, NotificationType.SystemAlert, NotificationTarget.HomePage, now.Date, $"mcp-access:{accessId}", ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to raise MCP anomaly notification for access {AccessId}", accessId);
        }
    }
}
