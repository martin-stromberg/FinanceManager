using FinanceManager.Application.Mcp;
using FinanceManager.Domain.Mcp;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace FinanceManager.Web.Mcp;

/// <summary>
/// Per-access, per-security-class fixed-window rate limiter evaluated inside the dispatcher
/// (sicherheitskonzept.md §7.2). Limits come from <c>Mcp:RateLimiting:*</c> and are bound
/// reloadable. The endpoint-level limit runs earlier via <c>UseRateLimiter</c>.
/// </summary>
public sealed class McpToolRateLimiter
{
    private sealed class WindowCounter
    {
        public DateTime WindowStartUtc;
        public int Count;
    }

    private readonly IOptionsMonitor<McpOptions> _options;
    private readonly ConcurrentDictionary<(Guid, string), WindowCounter> _counters = new();

    /// <summary>Creates the limiter.</summary>
    public McpToolRateLimiter(IOptionsMonitor<McpOptions> options)
    {
        _options = options;
    }

    /// <summary>
    /// Tries to acquire a permit for one tool call of the given access and security class.
    /// </summary>
    /// <param name="accessId">The MCP access id (partition).</param>
    /// <param name="descriptor">The tool descriptor (class + meta flag determine the tier).</param>
    /// <returns><see langword="true"/> when the call is within the limit.</returns>
    public bool TryAcquire(Guid accessId, IMcpToolDescriptor descriptor)
    {
        var tier = _options.CurrentValue.RateLimiting.ForClass(descriptor.SecurityClass, descriptor.MetaScope is not null);
        var window = TimeSpan.FromSeconds(Math.Max(1, tier.WindowSeconds));
        var key = (accessId, descriptor.MetaScope is not null ? "meta" : descriptor.SecurityClass.ToString());
        var now = DateTime.UtcNow;

        var counter = _counters.GetOrAdd(key, _ => new WindowCounter { WindowStartUtc = now });
        lock (counter)
        {
            if (now - counter.WindowStartUtc >= window)
            {
                counter.WindowStartUtc = now;
                counter.Count = 0;
            }
            if (counter.Count >= tier.PermitLimit)
            {
                return false;
            }
            counter.Count++;
            return true;
        }
    }
}
