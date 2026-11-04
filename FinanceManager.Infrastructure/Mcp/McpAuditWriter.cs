using FinanceManager.Application.Mcp;
using FinanceManager.Domain.Mcp;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace FinanceManager.Infrastructure.Mcp;

/// <summary>
/// Persistent, hash-chained audit writer of the MCP integration (F-0301/F-0302;
/// audit-konzept.md §4/§7, architekturkonzept.md §7.1, datenmodell-erweiterungen.md §6).
/// </summary>
/// <remarks>
/// <para>
/// The writer shares the scoped <see cref="AppDbContext"/> of the caller. Two write modes
/// implement the transactional coupling rule ("no effect without its audit record"):
/// <see cref="WriteAsync"/> commits the entry bundle immediately (tool-call records,
/// security events), while <see cref="StageAsync"/> tracks the bundle without saving so a
/// write effect and its audit record commit atomically in one <c>SaveChangesAsync</c>.
/// </para>
/// <para>
/// <b>Chain anchoring:</b> every entry gets the next <c>SequenceNo</c> of its chain
/// (chain = <c>OwnerUserId</c>; <see langword="null"/> forms the global chain) and a
/// <c>PrevHash</c>/<c>EntryHash</c> pair computed over a deterministic canonical
/// serialization (fixed field list and order, UTC ticks, lowercase GUIDs, canonically
/// sorted field deltas). A process-wide semaphore serializes chain-head lookup plus
/// insert; for owner chains the unique index <c>(OwnerUserId, SequenceNo)</c> and for the
/// global chain the filtered unique index on <c>SequenceNo</c> enforce the invariant at
/// database level — a concurrent colliding save fails the whole batch, which is the
/// required behaviour for coupled writes.
/// </para>
/// <para>
/// <b>Graduated detail depth:</b> callers pass already graduated payloads (read calls:
/// parameters without result dumps; write calls: full parameter snapshots; attachments:
/// metadata incl. <c>sha256</c> only — never binary content). As a mechanical safety net
/// the writer additionally redacts token/secret-shaped substrings, truncates
/// <c>PayloadJson</c> to <see cref="MaxPayloadChars"/> and field-delta values to
/// <see cref="MaxChangeValueChars"/> (audit-konzept.md §4.2). Redaction and truncation are
/// applied <i>before</i> hashing so stored content and <c>EntryHash</c> always match.
/// </para>
/// <para>
/// <b>Append-only:</b> the writer exposes no update or delete path; the retention executor
/// (EP-11) remains the only deletion path of the application.
/// </para>
/// </remarks>
public sealed class McpAuditWriter : IMcpAuditWriter
{
    /// <summary>Maximum length of <c>PayloadJson</c> before truncation wrapping applies.</summary>
    public const int MaxPayloadChars = 4000;

    /// <summary>Maximum length of one field-delta value (audit-konzept.md §4.2: ~500 chars).</summary>
    public const int MaxChangeValueChars = 500;

    /// <summary>Suffix marking a truncated field-delta value.</summary>
    private const string TruncatedSuffix = "…[truncated]";

    /// <summary>Token/secret-shaped substrings that must never reach the audit trail.</summary>
    private static readonly Regex SecretPatterns = new(
        @"fm_mcp_[A-Za-z0-9\-_]+|pbkdf2\|[^""\\\s]+|(?i:bearer\s+[A-Za-z0-9\-._~+/=]+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Process-wide serialization of chain-head lookup plus bundle insert. SQLite
    /// serializes writers anyway, so contention is minimal; the lock additionally
    /// protects the global (NULL-owner) chain, where the composite unique index cannot
    /// enforce sequence uniqueness.
    /// </summary>
    private static readonly SemaphoreSlim ChainLock = new(1, 1);

    private readonly AppDbContext _db;
    private readonly ILogger<McpAuditWriter> _logger;

    /// <summary>
    /// Creates a new instance of <see cref="McpAuditWriter"/>.
    /// </summary>
    /// <param name="db">Application database context (shared scoped instance).</param>
    /// <param name="logger">Logger.</param>
    public McpAuditWriter(AppDbContext db, ILogger<McpAuditWriter> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task WriteAsync(McpAuditWriteRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        await ChainLock.WaitAsync(ct);
        try
        {
            var entry = await BuildAndTrackAsync(request, ct);
            await _db.SaveChangesAsync(ct);
            _logger.LogDebug(
                "MCP audit entry {EntryId} written (chain {Owner}, seq {Seq}, {Category}/{EventType}/{Result})",
                entry.Id, request.OwnerUserId, entry.SequenceNo, entry.Category, entry.EventType, entry.ResultStatus);
        }
        finally
        {
            ChainLock.Release();
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// Staging happens inside the shared change tracker; the caller's
    /// <c>SaveChangesAsync</c> commits the bundle together with the business effect.
    /// The chain head is read under <see cref="ChainLock"/>; a colliding concurrent
    /// insert surfaces as a unique-constraint failure of the caller's save, failing
    /// effect and record together — the required fail behaviour.
    /// </remarks>
    public async Task<McpAuditEntry?> StageAsync(McpAuditWriteRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        await ChainLock.WaitAsync(ct);
        try
        {
            return await BuildAndTrackAsync(request, ct);
        }
        finally
        {
            ChainLock.Release();
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// The check expects a full chain starting at <c>SequenceNo</c> 1 — or a documented
    /// truncation: sequence gaps that are fully covered by the <c>removedRanges</c>
    /// payload of <c>RetentionPurged</c> anchor entries (written by the retention
    /// executor, F-1104) are excused. The first entry after a documented gap anchors the
    /// remaining chain: its <c>PrevHash</c> references the deleted predecessor by design
    /// and is accepted as the new anchor ("ab diesem Eintrag" verify mode). A gap without
    /// covering marker — including a chain that starts above 1 without a marker — is
    /// still reported as a breach.
    /// </remarks>
    public async Task<McpAuditChainVerificationResult> VerifyAsync(Guid? ownerUserId, CancellationToken ct)
    {
        var entries = await _db.McpAuditEntries.AsNoTracking()
            .Include(e => e.Changes)
            .Where(e => e.OwnerUserId == ownerUserId)
            .OrderBy(e => e.SequenceNo)
            .ToListAsync(ct);

        var removedRanges = CollectDocumentedRanges(entries);
        var gaps = new List<McpAuditChainGap>();
        string? failure = null;
        long? brokenAt = null;
        var expected = 1L;
        string? prevHash = null;
        var anchoredByCut = false;

        foreach (var entry in entries)
        {
            if (entry.SequenceNo < expected)
            {
                failure = $"Duplicate or out-of-order sequence number {entry.SequenceNo} (expected {expected}).";
                brokenAt ??= entry.SequenceNo;
                break;
            }
            if (entry.SequenceNo > expected)
            {
                if (IsCoveredByDocumentedRanges(expected, entry.SequenceNo - 1, removedRanges))
                {
                    // Documented retention truncation: the entry anchors the remaining
                    // chain — its PrevHash points into the removed range by design.
                    anchoredByCut = true;
                }
                else
                {
                    gaps.Add(new McpAuditChainGap(expected, entry.SequenceNo - 1));
                }
                expected = entry.SequenceNo + 1;
            }
            else
            {
                expected++;
            }

            if (!anchoredByCut && !string.Equals(entry.PrevHash, prevHash, StringComparison.Ordinal))
            {
                failure = brokenAt is null
                    ? $"Entry {entry.SequenceNo} has a dangling PrevHash (chain head missing or predecessor tampered)."
                    : failure;
                brokenAt ??= entry.SequenceNo;
            }
            anchoredByCut = false;

            var recomputed = ComputeHash(
                entry.Utc, entry.SequenceNo, entry.Category, entry.EventType, entry.ResultStatus,
                entry.OwnerUserId, entry.McpAccessId, entry.McpAccessName, entry.ToolName, entry.CatalogVersion,
                entry.Context, entry.CorrelationId, entry.SourceIp, entry.DurationMs, entry.AffectedCount,
                entry.PayloadJson, entry.RelatedRequestId, entry.PrevHash,
                entry.Changes.Select(c => new CanonicalChange(c.EntityType, c.EntityId, c.Field, c.OldValue, c.NewValue)));
            if (!string.Equals(entry.EntryHash, recomputed, StringComparison.Ordinal))
            {
                failure ??= $"Entry {entry.SequenceNo} failed the hash recomputation (content tampered).";
                brokenAt ??= entry.SequenceNo;
            }

            prevHash = entry.EntryHash;
        }

        var intact = brokenAt is null && gaps.Count == 0;
        return new McpAuditChainVerificationResult(
            ownerUserId, entries.Count, intact, brokenAt, failure, gaps);
    }

    /// <summary>
    /// Collects the union of sequence ranges documented as removed by
    /// <c>RetentionPurged</c> anchor entries of this chain (merged, ascending).
    /// Unparseable payloads are ignored — a malformed marker excuses nothing.
    /// </summary>
    private static List<(long From, long To)> CollectDocumentedRanges(IReadOnlyList<McpAuditEntry> entries)
    {
        var raw = new List<(long From, long To)>();
        foreach (var entry in entries)
        {
            if (!string.Equals(entry.EventType, McpAuditRetentionExecutor.CutEventType, StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(entry.PayloadJson))
            {
                continue;
            }
            try
            {
                using var doc = JsonDocument.Parse(entry.PayloadJson);
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object
                    || !root.TryGetProperty("retentionCut", out var flag)
                    || flag.ValueKind != JsonValueKind.True
                    || !root.TryGetProperty("removedRanges", out var ranges)
                    || ranges.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }
                foreach (var range in ranges.EnumerateArray())
                {
                    if (range.ValueKind == JsonValueKind.Object
                        && range.TryGetProperty("from", out var from) && from.ValueKind == JsonValueKind.Number
                        && range.TryGetProperty("to", out var to) && to.ValueKind == JsonValueKind.Number)
                    {
                        var f = from.GetInt64();
                        var t = to.GetInt64();
                        if (f <= t)
                        {
                            raw.Add((f, t));
                        }
                    }
                }
            }
            catch (JsonException)
            {
                // A malformed marker is ignored — it excuses nothing.
            }
        }

        // Merge overlapping/adjacent ranges so a gap covered by contiguous markers counts.
        raw.Sort((a, b) => a.From != b.From ? a.From.CompareTo(b.From) : a.To.CompareTo(b.To));
        var merged = new List<(long From, long To)>();
        foreach (var (from, to) in raw)
        {
            if (merged.Count > 0 && from <= merged[^1].To + 1)
            {
                merged[^1] = (merged[^1].From, Math.Max(merged[^1].To, to));
            }
            else
            {
                merged.Add((from, to));
            }
        }
        return merged;
    }

    /// <summary>
    /// Determines whether the whole closed sequence interval <paramref name="from"/>..
    /// <paramref name="to"/> is covered by the documented removed ranges.
    /// </summary>
    private static bool IsCoveredByDocumentedRanges(long from, long to, IReadOnlyList<(long From, long To)> ranges)
    {
        var cursor = from;
        foreach (var (rf, rt) in ranges)
        {
            if (rt < cursor)
            {
                continue;
            }
            if (rf > cursor)
            {
                return false;
            }
            if (rt >= to)
            {
                return true;
            }
            cursor = rt + 1;
        }
        return false;
    }

    /// <summary>
    /// Builds the sanitized, hash-anchored entry bundle and tracks it in the shared
    /// context. Must be called under <see cref="ChainLock"/>.
    /// </summary>
    private async Task<McpAuditEntry> BuildAndTrackAsync(McpAuditWriteRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.EventType) || request.EventType.Length > 100)
        {
            throw new ArgumentException("EventType is required and must be at most 100 characters.", nameof(request));
        }
        if (string.IsNullOrWhiteSpace(request.ResultStatus) || request.ResultStatus.Length > 50)
        {
            throw new ArgumentException("ResultStatus is required and must be at most 50 characters.", nameof(request));
        }

        var head = await _db.McpAuditEntries.AsNoTracking()
            .Where(e => e.OwnerUserId == request.OwnerUserId)
            .OrderByDescending(e => e.SequenceNo)
            .Select(e => new { e.SequenceNo, e.EntryHash })
            .FirstOrDefaultAsync(ct);

        var utc = DateTime.UtcNow; // server-side timestamp only — client times are never taken
        var sequenceNo = (head?.SequenceNo ?? 0) + 1;
        var prevHash = head?.EntryHash;
        var payload = SanitizePayload(request.PayloadJson);

        var changes = (request.Changes ?? (IReadOnlyList<McpAuditFieldChange>)Array.Empty<McpAuditFieldChange>())
            .Select(c => new CanonicalChange(
                c.EntityType, c.EntityId, c.Field,
                SanitizeValue(c.OldValue), SanitizeValue(c.NewValue)))
            .ToList();

        // The hash covers the semantic entry content; the entity Id is assigned inside
        // the McpAuditEntry constructor and is identity, not content.
        var hash = ComputeHash(
            utc, sequenceNo, request.Category, request.EventType.Trim(), request.ResultStatus.Trim(),
            request.OwnerUserId, request.McpAccessId, request.McpAccessName, request.ToolName,
            request.CatalogVersion, request.Context, request.CorrelationId, request.SourceIp,
            request.DurationMs, request.AffectedCount, payload, request.RelatedRequestId, prevHash,
            changes);

        var entry = new McpAuditEntry(
            utc, sequenceNo, request.Category, request.EventType.Trim(), request.ResultStatus.Trim(), hash,
            request.OwnerUserId, request.McpAccessId, request.McpAccessName, request.ToolName,
            request.CatalogVersion, request.Context, request.CorrelationId, request.SourceIp,
            request.DurationMs, request.AffectedCount, payload, request.RelatedRequestId, prevHash);

        foreach (var change in changes)
        {
            entry.AddChange(new McpAuditEntryChange(
                entry.Id, change.EntityType, change.EntityId, change.Field, change.OldValue, change.NewValue));
        }

        _db.McpAuditEntries.Add(entry);
        return entry;
    }

    /// <summary>
    /// Redacts token/secret-shaped substrings. Applied to every free-text fragment
    /// before storage so the audit trail itself never contains credential material
    /// (audit-konzept.md §4.2 — "Token/Secrets: niemals").
    /// </summary>
    private static string RedactSecrets(string value)
        => SecretPatterns.Replace(value, "[redacted]");

    /// <summary>
    /// Sanitizes <c>PayloadJson</c>: secret redaction plus a length cap that keeps the
    /// stored value valid JSON (truncated payloads are wrapped into a metadata object
    /// carrying the first <see cref="MaxPayloadChars"/> characters).
    /// </summary>
    private static string? SanitizePayload(string? payload)
    {
        if (string.IsNullOrEmpty(payload))
        {
            return null;
        }
        var redacted = RedactSecrets(payload);
        if (redacted.Length <= MaxPayloadChars)
        {
            return redacted;
        }
        var head = redacted[..MaxPayloadChars];
        return "{\"head\":" + JsonSerializer.Serialize(head)
            + ",\"truncated\":true,\"originalChars\":" + redacted.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}";
    }

    /// <summary>Sanitizes one field-delta value: secret redaction plus length cap.</summary>
    private static string? SanitizeValue(string? value)
    {
        if (value is null)
        {
            return null;
        }
        var redacted = RedactSecrets(value);
        return redacted.Length <= MaxChangeValueChars
            ? redacted
            : redacted[..MaxChangeValueChars] + TruncatedSuffix;
    }

    /// <summary>Canonical projection of one field delta used for hashing.</summary>
    private readonly record struct CanonicalChange(
        string EntityType, Guid EntityId, string Field, string? OldValue, string? NewValue);

    /// <summary>
    /// Computes the chain anchor hash over the canonicalized entry content
    /// (deterministic: fixed JSON property order, UTC ticks, lowercase GUIDs, field
    /// deltas sorted canonically; SHA-256 rendered as lowercase hex).
    /// </summary>
    private static string ComputeHash(
        DateTime utc, long sequenceNo, McpAuditCategory category, string eventType,
        string resultStatus, Guid? ownerUserId, Guid? mcpAccessId, string? mcpAccessName,
        string? toolName, string? catalogVersion, string? context, Guid? correlationId,
        string? sourceIp, int? durationMs, int? affectedCount, string? payloadJson,
        Guid? relatedRequestId, string? prevHash, IEnumerable<CanonicalChange> changes)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var w = new Utf8JsonWriter(buffer))
        {
            w.WriteStartObject();
            w.WriteNumber("v", 1); // canonicalization version — bump on any format change
            w.WriteNumber("utc", utc.Ticks);
            w.WriteNumber("seq", sequenceNo);
            w.WriteNumber("cat", (short)category);
            w.WriteString("evt", eventType);
            w.WriteString("res", resultStatus);
            WriteNullable(w, "own", ownerUserId);
            WriteNullable(w, "acc", mcpAccessId);
            WriteNullable(w, "accName", mcpAccessName);
            WriteNullable(w, "tool", toolName);
            WriteNullable(w, "catVer", catalogVersion);
            WriteNullable(w, "ctx", context);
            WriteNullable(w, "corr", correlationId);
            WriteNullable(w, "ip", sourceIp);
            WriteNullable(w, "dur", durationMs);
            WriteNullable(w, "aff", affectedCount);
            WriteNullable(w, "payload", payloadJson);
            WriteNullable(w, "req", relatedRequestId);
            WriteNullable(w, "prev", prevHash);
            w.WritePropertyName("changes");
            w.WriteStartArray();
            foreach (var c in changes
                .OrderBy(c => c.EntityType, StringComparer.Ordinal)
                .ThenBy(c => c.EntityId)
                .ThenBy(c => c.Field, StringComparer.Ordinal)
                .ThenBy(c => c.OldValue, StringComparer.Ordinal)
                .ThenBy(c => c.NewValue, StringComparer.Ordinal))
            {
                w.WriteStartObject();
                w.WriteString("et", c.EntityType);
                w.WriteString("eid", c.EntityId);
                w.WriteString("f", c.Field);
                WriteNullable(w, "o", c.OldValue);
                WriteNullable(w, "n", c.NewValue);
                w.WriteEndObject();
            }
            w.WriteEndArray();
            w.WriteEndObject();
        }
        return Convert.ToHexString(SHA256.HashData(buffer.WrittenSpan)).ToLowerInvariant();
    }

    private static void WriteNullable(Utf8JsonWriter w, string name, Guid? value)
    {
        if (value.HasValue)
        {
            w.WriteString(name, value.Value);
        }
        else
        {
            w.WriteNull(name);
        }
    }

    private static void WriteNullable(Utf8JsonWriter w, string name, string? value)
    {
        if (value is not null)
        {
            w.WriteString(name, value);
        }
        else
        {
            w.WriteNull(name);
        }
    }

    private static void WriteNullable(Utf8JsonWriter w, string name, int? value)
    {
        if (value.HasValue)
        {
            w.WriteNumber(name, value.Value);
        }
        else
        {
            w.WriteNull(name);
        }
    }
}
