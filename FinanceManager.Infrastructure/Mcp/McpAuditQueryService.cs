using FinanceManager.Application.Mcp;
using FinanceManager.Domain.Mcp;
using FinanceManager.Shared.Dtos.Mcp;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Infrastructure.Mcp;

/// <summary>
/// Read path of the MCP audit trail (F-0601/F-0603; audit-konzept.md §6, architekturkonzept.md
/// §6/§8). Owner-isolated queries over <see cref="McpAuditEntry"/> including field deltas,
/// hash-chain verification delegated to the write path, export projection (serialized data
/// only) and the per-access usage aggregation. The service is strictly read-only — the
/// append-only invariant of the audit trail is preserved.
/// </summary>
public sealed class McpAuditQueryService : IMcpAuditQueryService
{
    /// <summary>Paging clamp consistent with the API convention (skip/take ≤ 200).</summary>
    private const int MaxTake = 200;

    /// <summary>Hard row cap for exports (JSON full / CSV compact).</summary>
    private const int MaxExportRows = 5000;

    /// <summary>Cap of the recent-calls excerpt of the usage overview.</summary>
    private const int MaxRecentCalls = 20;

    /// <summary>Cap of the top-tools aggregation of the usage overview.</summary>
    private const int TopToolsTake = 5;

    private readonly AppDbContext _db;
    private readonly IMcpAuditWriter _auditWriter;
    private readonly ILogger<McpAuditQueryService> _logger;

    /// <summary>
    /// Creates a new instance of <see cref="McpAuditQueryService"/>.
    /// </summary>
    /// <param name="db">Application database context.</param>
    /// <param name="auditWriter">Hash-chained audit writer; used for chain verification
    /// (single implementation of the canonicalization/hash rule).</param>
    /// <param name="logger">Logger.</param>
    public McpAuditQueryService(
        AppDbContext db,
        IMcpAuditWriter auditWriter,
        ILogger<McpAuditQueryService> logger)
    {
        _db = db;
        _auditWriter = auditWriter;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<McpAuditEntryListItemDto>> ListAsync(
        Guid ownerUserId, McpAuditQueryFilter filter, int skip, int take, CancellationToken ct)
    {
        var query = ApplyFilter(_db.McpAuditEntries.AsNoTracking(), ownerUserId, filter);
        var rows = await query
            .OrderByDescending(e => e.Utc)
            .ThenByDescending(e => e.SequenceNo)
            .Skip(Math.Max(0, skip))
            .Take(Math.Clamp(take, 1, MaxTake))
            .Select(e => new
            {
                e.Id, e.Utc, e.SequenceNo, e.Category, e.EventType, e.ResultStatus,
                e.McpAccessId, e.McpAccessName, e.ToolName, e.CorrelationId, e.Context,
                e.DurationMs, e.AffectedCount,
                ChangeCount = e.Changes.Count
            })
            .ToListAsync(ct);
        return rows.Select(e => new McpAuditEntryListItemDto(
            e.Id, e.Utc, e.SequenceNo, (short)e.Category, e.EventType, e.ResultStatus,
            e.McpAccessId, e.McpAccessName, e.ToolName, e.CorrelationId, e.Context,
            e.DurationMs, e.AffectedCount, e.ChangeCount)).ToList();
    }

    /// <inheritdoc />
    public async Task<McpAuditEntryDetailDto?> GetAsync(Guid ownerUserId, Guid id, CancellationToken ct)
    {
        var entry = await _db.McpAuditEntries.AsNoTracking()
            .Include(e => e.Changes)
            .FirstOrDefaultAsync(e => e.Id == id && e.OwnerUserId == ownerUserId, ct);
        return entry is null ? null : ToDetail(entry);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Verification is delegated to <see cref="IMcpAuditWriter.VerifyAsync"/> so the
    /// canonicalization/hash rule exists exactly once (F-0302, audit-konzept.md §7.2/§7.3).
    /// </remarks>
    public async Task<McpAuditVerifyResultDto> VerifyAsync(Guid ownerUserId, CancellationToken ct)
    {
        var result = await _auditWriter.VerifyAsync(ownerUserId, ct);
        return new McpAuditVerifyResultDto(
            result.EntriesChecked,
            result.IsIntact,
            result.FirstBrokenSequenceNo,
            result.FailureReason,
            result.SequenceGaps
                .Select(g => new McpAuditChainGapDto(g.FirstMissingSequenceNo, g.LastMissingSequenceNo))
                .ToList());
    }

    /// <inheritdoc />
    /// <remarks>
    /// The export writes an <c>AuditExported</c> security event into the owner's own chain —
    /// the export of the protocol is itself an audit event (F-0601 AK, audit-konzept.md §6.3).
    /// A failed audit write is logged but must not block the read path (F-0301 read rule).
    /// </remarks>
    public async Task<IReadOnlyList<McpAuditEntryDetailDto>> ExportAsync(
        Guid ownerUserId, McpAuditQueryFilter filter, int maxRows, CancellationToken ct)
    {
        var query = ApplyFilter(_db.McpAuditEntries.AsNoTracking().Include(e => e.Changes), ownerUserId, filter);
        var rows = await query
            .OrderBy(e => e.Utc)
            .ThenBy(e => e.SequenceNo)
            .Take(Math.Clamp(maxRows, 1, MaxExportRows))
            .ToListAsync(ct);

        try
        {
            await _auditWriter.WriteAsync(new McpAuditWriteRequest(
                Category: McpAuditCategory.SecurityEvent,
                EventType: "AuditExported",
                ResultStatus: "Success",
                OwnerUserId: ownerUserId,
                Context: "Ui",
                AffectedCount: rows.Count,
                PayloadJson: BuildExportAuditPayload(filter, rows.Count)), ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write the MCP audit-export record for owner {OwnerUserId}", ownerUserId);
        }

        return rows.Select(ToDetail).ToList();
    }

    /// <inheritdoc />
    /// <remarks>
    /// The usage overview aggregates <see cref="McpAuditCategory.ToolCall"/> entries of the
    /// access (wer/wann/was, audit-konzept.md §6.1). The ownership check runs against the
    /// access row itself — a foreign id behaves like a non-existing record.
    /// </remarks>
    public async Task<McpAccessUsageDto?> GetAccessUsageAsync(
        Guid ownerUserId, Guid mcpAccessId, int recentTake, CancellationToken ct)
    {
        var owns = await _db.McpAccesses.AsNoTracking()
            .AnyAsync(a => a.Id == mcpAccessId && a.OwnerUserId == ownerUserId, ct);
        if (!owns)
        {
            return null;
        }

        var calls = _db.McpAuditEntries.AsNoTracking()
            .Where(e => e.OwnerUserId == ownerUserId
                && e.McpAccessId == mcpAccessId
                && e.Category == McpAuditCategory.ToolCall);

        var total = await calls.CountAsync(ct);
        DateTime? first = null;
        DateTime? last = null;
        var success = 0;
        if (total > 0)
        {
            success = await calls.CountAsync(e => e.ResultStatus == "Success", ct);
            first = await calls.MinAsync(e => e.Utc, ct);
            last = await calls.MaxAsync(e => e.Utc, ct);
        }

        // GroupBy over the nullable column; shaping into the DTO happens in memory so the
        // aggregation also works with providers that cannot translate record factories.
        var toolGroups = await calls
            .Where(e => e.ToolName != null)
            .GroupBy(e => e.ToolName)
            .Select(g => new { ToolName = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var topTools = toolGroups
            .Where(g => g.ToolName is not null)
            .Select(g => new McpAuditToolUsageDto(g.ToolName!, g.Count))
            .OrderByDescending(t => t.CallCount)
            .ThenBy(t => t.ToolName, StringComparer.Ordinal)
            .Take(TopToolsTake)
            .ToList();

        var recent = await calls
            .OrderByDescending(e => e.Utc)
            .ThenByDescending(e => e.SequenceNo)
            .Take(Math.Clamp(recentTake, 1, MaxRecentCalls))
            .Select(e => new
            {
                e.Id, e.Utc, e.SequenceNo, e.Category, e.EventType, e.ResultStatus,
                e.McpAccessId, e.McpAccessName, e.ToolName, e.CorrelationId, e.Context,
                e.DurationMs, e.AffectedCount,
                ChangeCount = e.Changes.Count
            })
            .ToListAsync(ct);

        var failed = total - success;
        return new McpAccessUsageDto(
            mcpAccessId,
            total,
            success,
            failed,
            total == 0 ? 0d : (double)failed / total,
            first,
            last,
            topTools,
            recent.Select(e => new McpAuditEntryListItemDto(
                e.Id, e.Utc, e.SequenceNo, (short)e.Category, e.EventType, e.ResultStatus,
                e.McpAccessId, e.McpAccessName, e.ToolName, e.CorrelationId, e.Context,
                e.DurationMs, e.AffectedCount, e.ChangeCount)).ToList());
    }

    /// <summary>
    /// Applies the owner scope and the AND-combined optional filters of
    /// <see cref="McpAuditQueryFilter"/>.
    /// </summary>
    private static IQueryable<McpAuditEntry> ApplyFilter(
        IQueryable<McpAuditEntry> source, Guid ownerUserId, McpAuditQueryFilter filter)
    {
        var query = source.Where(e => e.OwnerUserId == ownerUserId);
        if (filter is null)
        {
            return query;
        }
        if (filter.FromUtc.HasValue)
        {
            query = query.Where(e => e.Utc >= filter.FromUtc.Value);
        }
        if (filter.ToUtc.HasValue)
        {
            query = query.Where(e => e.Utc <= filter.ToUtc.Value);
        }
        if (filter.Category.HasValue)
        {
            var cat = (McpAuditCategory)filter.Category.Value;
            query = query.Where(e => e.Category == cat);
        }
        if (filter.McpAccessId.HasValue)
        {
            query = query.Where(e => e.McpAccessId == filter.McpAccessId.Value);
        }
        if (filter.CorrelationId.HasValue)
        {
            query = query.Where(e => e.CorrelationId == filter.CorrelationId.Value);
        }
        if (!string.IsNullOrWhiteSpace(filter.ToolName))
        {
            var tool = filter.ToolName.Trim();
            query = query.Where(e => e.ToolName == tool);
        }
        if (!string.IsNullOrWhiteSpace(filter.ResultStatus))
        {
            var result = filter.ResultStatus.Trim();
            query = query.Where(e => e.ResultStatus == result);
        }
        return query;
    }

    private static McpAuditEntryDetailDto ToDetail(McpAuditEntry e)
        => new(
            e.Id, e.Utc, e.SequenceNo, (short)e.Category, e.EventType, e.ResultStatus,
            e.McpAccessId, e.McpAccessName, e.ToolName, e.CatalogVersion, e.Context,
            e.CorrelationId, e.SourceIp, e.DurationMs, e.AffectedCount, e.PayloadJson,
            e.RelatedRequestId, e.PrevHash, e.EntryHash,
            e.Changes
                .OrderBy(c => c.EntityType, StringComparer.Ordinal)
                .ThenBy(c => c.Field, StringComparer.Ordinal)
                .Select(c => new McpAuditChangeDto(c.EntityType, c.EntityId, c.Field, c.OldValue, c.NewValue))
                .ToList(),
            e.CreatedUtc);

    /// <summary>
    /// Small graduated payload of the <c>AuditExported</c> record: the applied filters and
    /// the row count — enough to attribute the export later (no result data).
    /// </summary>
    private static string BuildExportAuditPayload(McpAuditQueryFilter filter, int rows)
        => "{"
            + "\"rows\":" + rows.ToString(System.Globalization.CultureInfo.InvariantCulture)
            + (filter?.FromUtc is DateTime from ? ",\"from\":\"" + from.ToString("O") + "\"" : string.Empty)
            + (filter?.ToUtc is DateTime to ? ",\"to\":\"" + to.ToString("O") + "\"" : string.Empty)
            + (filter?.Category is short cat ? ",\"category\":" + cat.ToString(System.Globalization.CultureInfo.InvariantCulture) : string.Empty)
            + (filter?.McpAccessId is Guid acc ? ",\"mcpAccessId\":\"" + acc + "\"" : string.Empty)
            + (filter?.CorrelationId is Guid corr ? ",\"correlationId\":\"" + corr + "\"" : string.Empty)
            + (string.IsNullOrWhiteSpace(filter?.ToolName) ? string.Empty : ",\"tool\":\"" + filter!.ToolName!.Replace("\"", "") + "\"")
            + (string.IsNullOrWhiteSpace(filter?.ResultStatus) ? string.Empty : ",\"result\":\"" + filter!.ResultStatus!.Replace("\"", "") + "\"")
            + "}";
}
