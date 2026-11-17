using FinanceManager.Application.Mcp;
using FinanceManager.Domain.Mcp;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace FinanceManager.Infrastructure.Mcp;

/// <summary>
/// Retention executor of the MCP audit trail and the approval-request queue (F-1104;
/// audit-konzept.md §5, datenmodell-erweiterungen.md §8). This is the <b>only</b> deletion
/// path of the application on <c>McpAuditEntries</c> — the trail stays append-only for
/// every other writer.
/// </summary>
/// <remarks>
/// <para>
/// <b>Tiers (defaults, configurable via <c>Mcp:Retention:*</c>):</b>
/// <c>ToolCall</c>/<c>AccessEvent</c>/<c>SecurityEvent</c> entries →
/// <c>ToolCallMonths</c> (12); <c>ApprovalEvent</c>/<c>DataChange</c> entries →
/// <c>ApprovalMonths</c> (36); terminal <see cref="AiActionRequest"/> rows →
/// <c>RequestMonths</c> (12).
/// </para>
/// <para>
/// <b>Chain truncation (Kettenkürzung):</b> audit chains mix categories, so the expired
/// set of a chain is generally not a contiguous prefix. Before a chain's expired entries
/// are deleted, the pass appends one <c>RetentionPurged</c> anchor entry documenting the
/// removed sequence ranges in its payload. <see cref="McpAuditWriter.VerifyAsync"/> treats
/// sequence gaps fully covered by such a marker as documented truncation — the entry
/// following a documented gap anchors the remaining chain (its dangling
/// <c>PrevHash</c> references the deleted predecessor by design), so verification
/// continues "ab diesem Eintrag" while unexplained gaps still fail. Anchor entries are
/// never removed themselves.
/// </para>
/// <para>
/// <b>Self-audit:</b> the <c>RetentionPurged</c> entry is itself the audit record of the
/// pass — per chain it carries the removed ranges, the per-tier configuration and the
/// number of deleted approval requests of that owner.
/// </para>
/// </remarks>
public sealed class McpAuditRetentionExecutor : IMcpAuditRetention
{
    /// <summary>Event type of the retention anchor entry (chain-truncation marker).</summary>
    public const string CutEventType = "RetentionPurged";

    /// <summary>Payload flag identifying the documented-removal ranges.</summary>
    public const string CutPayloadFlag = "retentionCut";

    private static readonly McpAuditCategory[] ShortTier =
        [McpAuditCategory.ToolCall, McpAuditCategory.AccessEvent, McpAuditCategory.SecurityEvent];

    private static readonly McpAuditCategory[] LongTier =
        [McpAuditCategory.ApprovalEvent, McpAuditCategory.DataChange];

    private static readonly AiActionRequestStatus[] TerminalStates =
        [AiActionRequestStatus.Executed, AiActionRequestStatus.Failed,
         AiActionRequestStatus.Rejected, AiActionRequestStatus.Expired];

    private readonly AppDbContext _db;
    private readonly IMcpAuditWriter _auditWriter;
    private readonly IOptionsMonitor<McpOptions> _options;
    private readonly ILogger<McpAuditRetentionExecutor> _logger;

    /// <summary>Creates the executor.</summary>
    /// <param name="db">Shared scoped database context.</param>
    /// <param name="auditWriter">Hash-chained audit writer for the anchor entries.</param>
    /// <param name="options">MCP options (retention tiers, batch size).</param>
    /// <param name="logger">Logger.</param>
    public McpAuditRetentionExecutor(
        AppDbContext db,
        IMcpAuditWriter auditWriter,
        IOptionsMonitor<McpOptions> options,
        ILogger<McpAuditRetentionExecutor> logger)
    {
        _db = db;
        _auditWriter = auditWriter;
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<McpAuditRetentionResult> RunAsync(DateTime utcNow, CancellationToken ct)
    {
        var retention = _options.CurrentValue.Retention;
        var shortCutoff = utcNow.AddMonths(-Math.Max(1, retention.ToolCallMonths));
        var longCutoff = utcNow.AddMonths(-Math.Max(1, retention.ApprovalMonths));
        var requestCutoff = utcNow.AddMonths(-Math.Max(1, retention.RequestMonths));
        var batchSize = Math.Clamp(retention.BatchSize, 1, 100_000);

        // Distinct chain owners touched by this pass (expired audit entries or expired
        // terminal requests). The global chain (OwnerUserId == null) has no requests.
        var entryOwners = await _db.McpAuditEntries.AsNoTracking()
            .Where(e => e.EventType != CutEventType
                && ((ShortTier.Contains(e.Category) && e.Utc < shortCutoff)
                    || (LongTier.Contains(e.Category) && e.Utc < longCutoff)))
            .Select(e => e.OwnerUserId)
            .Distinct()
            .ToListAsync(ct);
        var requestOwners = await _db.AiActionRequests.AsNoTracking()
            .Where(r => TerminalStates.Contains(r.Status) && r.ModifiedUtc < requestCutoff)
            .Select(r => (Guid?)r.OwnerUserId)
            .Distinct()
            .ToListAsync(ct);

        var owners = entryOwners.Union(requestOwners).ToList();
        var entriesRemoved = 0;
        var requestsRemoved = 0;
        var markers = 0;

        foreach (var owner in owners)
        {
            ct.ThrowIfCancellationRequested();

            var removedForOwner = 0;
            if (owner is Guid ownerId)
            {
                // Terminal queue rows are audit substrate but not part of the hash chain —
                // a plain delete is correct here (approval-konzept.md §6.3 keeps the
                // McpAccessId soft reference readable on the remaining rows).
                removedForOwner = await _db.AiActionRequests
                    .Where(r => r.OwnerUserId == ownerId
                        && TerminalStates.Contains(r.Status)
                        && r.ModifiedUtc < requestCutoff)
                    .ExecuteDeleteAsync(ct);
                requestsRemoved += removedForOwner;
            }

            var expired = await _db.McpAuditEntries.AsNoTracking()
                .Where(e => e.OwnerUserId == owner
                    && e.EventType != CutEventType
                    && ((ShortTier.Contains(e.Category) && e.Utc < shortCutoff)
                        || (LongTier.Contains(e.Category) && e.Utc < longCutoff)))
                .OrderBy(e => e.SequenceNo)
                .Select(e => new { e.Id, e.SequenceNo })
                .Take(batchSize)
                .ToListAsync(ct);

            if (expired.Count > 0)
            {
                var ranges = CompressRanges(expired.Select(e => e.SequenceNo));
                // Anchor first, then delete: the marker documents the intended truncation;
                // a pass that dies between the two steps leaves a claim without effect
                // (verification only excuses real gaps — the conservative direction).
                await _auditWriter.WriteAsync(new McpAuditWriteRequest(
                    McpAuditCategory.SecurityEvent, CutEventType, "Success",
                    OwnerUserId: owner, Context: "System/Job",
                    AffectedCount: expired.Count,
                    PayloadJson: JsonSerializer.Serialize(new
                    {
                        retentionCut = true,
                        removedRanges = ranges.Select(r => new { from = r.From, to = r.To }),
                        removedCount = expired.Count,
                        toolCallMonths = retention.ToolCallMonths,
                        approvalMonths = retention.ApprovalMonths,
                        requestsRemoved = removedForOwner
                    })), ct);
                markers++;

                var ids = expired.Select(e => e.Id).ToList();
                entriesRemoved += await _db.McpAuditEntries
                    .Where(e => ids.Contains(e.Id))
                    .ExecuteDeleteAsync(ct);
            }
        }

        var result = new McpAuditRetentionResult(entriesRemoved, requestsRemoved, owners.Count, markers);
        if (entriesRemoved > 0 || requestsRemoved > 0)
        {
            _logger.LogInformation(
                "MCP retention pass removed {Entries} audit entries and {Requests} requests across {Chains} chains ({Markers} cut markers)",
                entriesRemoved, requestsRemoved, owners.Count, markers);
        }
        return result;
    }

    /// <summary>
    /// Compresses an ascending sequence-number list into contiguous [from, to] ranges.
    /// </summary>
    internal static List<(long From, long To)> CompressRanges(IEnumerable<long> sequenceNos)
    {
        var ranges = new List<(long From, long To)>();
        long start = 0, prev = 0;
        var first = true;
        foreach (var seq in sequenceNos)
        {
            if (first)
            {
                start = prev = seq;
                first = false;
                continue;
            }
            if (seq == prev + 1)
            {
                prev = seq;
                continue;
            }
            ranges.Add((start, prev));
            start = prev = seq;
        }
        if (!first)
        {
            ranges.Add((start, prev));
        }
        return ranges;
    }
}
