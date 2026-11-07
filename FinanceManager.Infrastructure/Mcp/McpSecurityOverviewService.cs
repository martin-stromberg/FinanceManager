using FinanceManager.Application.Mcp;
using FinanceManager.Domain.Mcp;
using FinanceManager.Shared.Dtos.Mcp;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Mcp;

/// <summary>
/// Infrastructure implementation of <see cref="IMcpSecurityOverviewService"/> (F-1005):
/// aggregates access metadata of every owner, pending-request counters, the global
/// <c>Mcp:*</c> switch state and the newest <see cref="McpAuditCategory.SecurityEvent"/>
/// entries into one metadata-only overview — no payloads, no scope tuples, no business
/// content of foreign users (audit-konzept.md §6.2).
/// </summary>
public sealed class McpSecurityOverviewService : IMcpSecurityOverviewService
{
    /// <summary>Maximum number of recent security events returned.</summary>
    private const int MaxRecentEvents = 50;

    private readonly AppDbContext _db;
    private readonly IMcpSettingsService _settings;

    /// <summary>Creates the service.</summary>
    /// <param name="db">Shared scoped database context.</param>
    /// <param name="settings">Database-stored MCP switches (global switch state).</param>
    public McpSecurityOverviewService(AppDbContext db, IMcpSettingsService settings)
    {
        _db = db;
        _settings = settings;
    }

    /// <inheritdoc />
    public async Task<McpSecurityOverviewDto> GetOverviewAsync(int recentTake, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var since = now.AddHours(-24);
        var take = Math.Clamp(recentTake, 1, MaxRecentEvents);

        var accesses = await _db.McpAccesses.AsNoTracking()
            .OrderBy(a => a.OwnerUserId).ThenBy(a => a.Name)
            .Select(a => new
            {
                a.Id,
                a.OwnerUserId,
                a.Name,
                Level = (short)a.Level,
                Status = (short)a.Status,
                a.ExpiresUtc,
                a.LastUsedUtc
            })
            .ToListAsync(ct);

        var pendingByAccess = await _db.AiActionRequests.AsNoTracking()
            .Where(r => r.Status == AiActionRequestStatus.Pending)
            .GroupBy(r => r.McpAccessId)
            .Select(g => new { AccessId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.AccessId, g => g.Count, ct);

        var approvedQueued = await _db.AiActionRequests.AsNoTracking()
            .CountAsync(r => r.Status == AiActionRequestStatus.Approved, ct);

        // Rate-limit/denial/failure aggregates of the last 24 h derived from the tool-call
        // audit rows — metadata only (audit-konzept.md §6.2).
        var callStats = await _db.McpAuditEntries.AsNoTracking()
            .Where(e => e.Category == McpAuditCategory.ToolCall && e.Utc >= since)
            .GroupBy(e => e.ResultStatus)
            .Select(g => new { Result = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var securityEvents = await _db.McpAuditEntries.AsNoTracking()
            .Where(e => e.Category == McpAuditCategory.SecurityEvent)
            .OrderByDescending(e => e.Utc)
            .Take(take)
            .Select(e => new McpSecurityEventDto(
                e.Utc, e.EventType, e.ResultStatus, e.OwnerUserId, e.McpAccessName, e.SourceIp))
            .ToListAsync(ct);

        var switches = await _settings.GetAsync(ct);
        var aggregates = new McpSecurityAggregatesDto(
            TotalAccesses: accesses.Count,
            ActiveAccesses: accesses.Count(a =>
                a.Status == (short)McpAccessStatus.Active && (a.ExpiresUtc is null || a.ExpiresUtc > now)),
            PendingRequests: pendingByAccess.Values.Sum(),
            ApprovedQueued: approvedQueued,
            DeniedCallsLast24h: callStats.FirstOrDefault(s => s.Result == "Denied")?.Count ?? 0,
            RateLimitedCallsLast24h: callStats.FirstOrDefault(s => s.Result == "RateLimited")?.Count ?? 0,
            FailedCallsLast24h: callStats.FirstOrDefault(s => s.Result == "Failed")?.Count ?? 0);

        var rows = accesses
            .Select(a => new McpSecurityAccessDto(
                a.Id, a.OwnerUserId, a.Name, a.Level, a.Status, a.ExpiresUtc, a.LastUsedUtc,
                pendingByAccess.TryGetValue(a.Id, out var p) ? p : 0))
            .ToList();

        return new McpSecurityOverviewDto(
            switches.Enabled, switches.WriteAccessEnabled, aggregates, rows, securityEvents);
    }
}
