using System.Collections.Concurrent;

namespace FinanceManager.Web.Mcp;

/// <summary>
/// Streamable-HTTP session hub of the <c>/mcp</c> endpoint (transport state only,
/// architekturkonzept.md §5.2 — no domain state is bound to sessions). Issues server-side
/// session ids returned in the <c>Mcp-Session-Id</c> header, tracks last activity and lets
/// stale sessions expire.
/// </summary>
public sealed class McpSessionHub
{
    /// <summary>Session header name of the MCP transport.</summary>
    public const string SessionHeader = "Mcp-Session-Id";

    private static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(30);
    private readonly ConcurrentDictionary<string, DateTime> _sessions = new();

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
}
