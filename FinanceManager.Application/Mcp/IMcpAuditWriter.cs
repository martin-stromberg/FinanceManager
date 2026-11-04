using FinanceManager.Domain.Mcp;

namespace FinanceManager.Application.Mcp;

/// <summary>
/// One field delta (before/after) attached to an audit entry.
/// </summary>
/// <param name="EntityType">Affected entity type (e.g. <c>Contact</c>).</param>
/// <param name="EntityId">Affected record id.</param>
/// <param name="Field">Changed field.</param>
/// <param name="OldValue">Value before (serialized, length-limited).</param>
/// <param name="NewValue">Value after (serialized, length-limited).</param>
public sealed record McpAuditFieldChange(
    string EntityType,
    Guid EntityId,
    string Field,
    string? OldValue,
    string? NewValue);

/// <summary>
/// Write request of one MCP audit entry (grundgerüst of audit-konzept.md §4.1). Token
/// material and binary content must never be passed (<see cref="PayloadJson"/> carries
/// graduated detail only).
/// </summary>
/// <param name="Category">Event category.</param>
/// <param name="EventType">Stable event type code (e.g. <c>ToolCall</c>, <c>AuthFailed</c>, <c>TokenRotated</c>).</param>
/// <param name="ResultStatus">Stable result code (<c>Success</c>, <c>Denied</c>, <c>Failed</c>, <c>RateLimited</c>).</param>
/// <param name="OwnerUserId">Affected owner; <see langword="null"/> for global events.</param>
/// <param name="McpAccessId">Optional soft reference to the causing access.</param>
/// <param name="McpAccessName">Optional access name snapshot.</param>
/// <param name="ToolName">Optional <c>fm_*</c> tool name.</param>
/// <param name="CatalogVersion">Optional catalog version (<c>katalog-v&lt;n&gt;</c>).</param>
/// <param name="Context">Optional administrative context (<c>Ui</c>, <c>McpEndpoint</c>, <c>System/Job</c>, <c>Admin</c>).</param>
/// <param name="CorrelationId">Optional correlation id of the effect chain.</param>
/// <param name="SourceIp">Optional source IP of client calls.</param>
/// <param name="DurationMs">Optional duration of the tool call.</param>
/// <param name="AffectedCount">Optional number of affected records.</param>
/// <param name="PayloadJson">Optional graduated payload (never secrets/binary).</param>
/// <param name="RelatedRequestId">Optional link to an approval request.</param>
/// <param name="Changes">Optional field deltas of the entry.</param>
public sealed record McpAuditWriteRequest(
    McpAuditCategory Category,
    string EventType,
    string ResultStatus,
    Guid? OwnerUserId = null,
    Guid? McpAccessId = null,
    string? McpAccessName = null,
    string? ToolName = null,
    string? CatalogVersion = null,
    string? Context = null,
    Guid? CorrelationId = null,
    string? SourceIp = null,
    int? DurationMs = null,
    int? AffectedCount = null,
    string? PayloadJson = null,
    Guid? RelatedRequestId = null,
    IReadOnlyList<McpAuditFieldChange>? Changes = null);

/// <summary>
/// Revision-safe audit write path of the MCP integration (audit-konzept.md, F-0301/F-0302).
/// Calls are issued at the pipeline points (authentication, scope decisions, tool calls,
/// access events, security events). The implementation is responsible for chain anchoring
/// (<c>SequenceNo</c>, <c>PrevHash</c>, <c>EntryHash</c>) and the transactional coupling to
/// the business effect.
/// </summary>
/// <remarks>
/// The registered implementation in this step is the documented no-op
/// <c>NullMcpAuditWriter</c> — the persistent, hash-chained writer is scheduled with
/// F-0301/F-0302 (step 3 of the roadmap). The interface and all call sites are already live.
/// </remarks>
public interface IMcpAuditWriter
{
    /// <summary>
    /// Writes one audit entry bundle (entry plus optional field deltas).
    /// </summary>
    /// <param name="request">The write request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the entry has been accepted.</returns>
    Task WriteAsync(McpAuditWriteRequest request, CancellationToken ct);
}
