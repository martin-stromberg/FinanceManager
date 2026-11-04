using FinanceManager.Application.Mcp;
using System.Collections.Concurrent;
using System.Threading.Channels;

namespace FinanceManager.Web.Mcp;

/// <summary>
/// Streamable-HTTP session hub of the <c>/mcp</c> endpoint (transport state only,
/// architekturkonzept.md §5.2 — no domain state is bound to sessions). Issues server-side
/// session ids returned in the <c>Mcp-Session-Id</c> header, tracks last activity and lets
/// stale sessions expire. Since F-1101 it additionally holds the SSE subscriptions of the
/// streamable-HTTP upgrade: a <c>GET /mcp</c> stream subscribes to the request status
/// events of its own access (events never cross access boundaries).
/// </summary>
public sealed class McpSessionHub
{
    /// <summary>Session header name of the MCP transport.</summary>
    public const string SessionHeader = "Mcp-Session-Id";

    /// <summary>Bounded capacity of one subscription queue; a slow client drops the oldest
    /// queued events first — the freshest status always survives (the request row plus
    /// <c>fm_task_status</c> polling stay the source of truth).</summary>
    private const int SubscriberQueueCapacity = 128;

    private static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(30);
    private readonly ConcurrentDictionary<string, DateTime> _sessions = new();
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, Subscriber>> _subscribers = new();

    /// <summary>Creates a new session and returns its id.</summary>
    public string Create()
    {
        var id = Guid.NewGuid().ToString("N");
        _sessions[id] = DateTime.UtcNow;
        Cleanup();
        return id;
    }

    /// <summary>
    /// Checks whether the session id is known and alive; refreshes its activity timestamp.
    /// </summary>
    public bool Touch(string? sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return false;
        }
        if (_sessions.TryGetValue(sessionId, out var last) && DateTime.UtcNow - last <= IdleTimeout)
        {
            _sessions[sessionId] = DateTime.UtcNow;
            return true;
        }
        _sessions.TryRemove(sessionId, out _);
        return false;
    }

    /// <summary>Terminates a session. Returns false when the session was unknown.</summary>
    public bool Remove(string? sessionId)
        => !string.IsNullOrWhiteSpace(sessionId) && _sessions.TryRemove(sessionId, out _);

    /// <summary>
    /// Subscribes to the request status events of one access (F-1101). The subscription is
    /// bound to the access, not to the transport session — an SSE stream ends via
    /// <see cref="McpSseSubscription.Dispose"/> when the client disconnects. Events are
    /// queued bounded; overflow drops the newest event (polling fallback stays correct).
    /// </summary>
    /// <param name="accessId">The authenticated access.</param>
    /// <param name="maxPerAccess">Subscription cap of the access (0 = unlimited).</param>
    /// <returns>The subscription, or <see langword="null"/> when the cap is reached.</returns>
    public McpSseSubscription? Subscribe(Guid accessId, int maxPerAccess)
    {
        if (accessId == Guid.Empty)
        {
            return null;
        }
        var channel = Channel.CreateBounded<McpRequestStatusEvent>(new BoundedChannelOptions(SubscriberQueueCapacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });
        var subscriber = new Subscriber(channel);
        var bucket = _subscribers.GetOrAdd(accessId, _ => new ConcurrentDictionary<Guid, Subscriber>());
        if (maxPerAccess > 0 && bucket.Count >= maxPerAccess)
        {
            return null;
        }
        if (!bucket.TryAdd(subscriber.Id, subscriber))
        {
            return null;
        }
        return new McpSseSubscription(subscriber.Id, accessId, channel.Reader, () =>
        {
            if (_subscribers.TryGetValue(accessId, out var b))
            {
                b.TryRemove(subscriber.Id, out _);
                if (b.IsEmpty)
                {
                    _subscribers.TryRemove(accessId, out _);
                }
            }
            channel.Writer.TryComplete();
        });
    }

    /// <summary>
    /// Fans one request status event out to the SSE subscriptions of the owning access.
    /// Non-blocking: a full subscriber queue drops the event (bounded queue, documented
    /// best-effort channel).
    /// </summary>
    /// <param name="statusEvent">The event to deliver.</param>
    /// <returns>Number of subscribers the event was queued to.</returns>
    public int Publish(McpRequestStatusEvent statusEvent)
    {
        ArgumentNullException.ThrowIfNull(statusEvent);
        if (!_subscribers.TryGetValue(statusEvent.McpAccessId, out var bucket))
        {
            return 0;
        }
        var delivered = 0;
        foreach (var subscriber in bucket.Values)
        {
            if (subscriber.Channel.Writer.TryWrite(statusEvent))
            {
                delivered++;
            }
        }
        return delivered;
    }

    /// <summary>Current subscriber count of one access (diagnostics/tests).</summary>
    public int SubscriberCount(Guid accessId)
        => _subscribers.TryGetValue(accessId, out var bucket) ? bucket.Count : 0;

    private void Cleanup()
    {
        var threshold = DateTime.UtcNow - IdleTimeout;
        foreach (var pair in _sessions)
        {
            if (pair.Value < threshold)
            {
                _sessions.TryRemove(pair.Key, out _);
            }
        }
    }

    private sealed class Subscriber(Channel<McpRequestStatusEvent> channel)
    {
        public Guid Id { get; } = Guid.NewGuid();
        public Channel<McpRequestStatusEvent> Channel { get; } = channel;
    }
}

/// <summary>
/// Handle of one SSE subscription of an access (F-1101). Disposing detaches the
/// subscription and completes the channel — called when the <c>GET /mcp</c> stream ends.
/// </summary>
public sealed class McpSseSubscription : IDisposable
{
    private readonly Action _unsubscribe;
    private int _disposed;

    internal McpSseSubscription(Guid id, Guid accessId, ChannelReader<McpRequestStatusEvent> reader, Action unsubscribe)
    {
        Id = id;
        AccessId = accessId;
        Reader = reader;
        _unsubscribe = unsubscribe;
    }

    /// <summary>Subscription identifier.</summary>
    public Guid Id { get; }

    /// <summary>Access the subscription is bound to.</summary>
    public Guid AccessId { get; }

    /// <summary>Event queue of the subscription (bounded, drop-write on overflow).</summary>
    public ChannelReader<McpRequestStatusEvent> Reader { get; }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _unsubscribe();
        }
    }
}
