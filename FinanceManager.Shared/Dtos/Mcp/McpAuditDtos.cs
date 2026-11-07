namespace FinanceManager.Shared.Dtos.Mcp;

/// <summary>
/// List item of one MCP audit entry (<c>GET /api/mcp-audit</c>). Enum values are transported
/// as <see langword="short"/> following the DTO convention; Category = McpAuditCategory.
/// The list projection carries no payload and no field deltas — drilling loads
/// <see cref="McpAuditEntryDetailDto"/> per entry.
/// </summary>
/// <param name="Id">Entry identifier.</param>
/// <param name="Utc">Server-side timestamp of the event.</param>
/// <param name="SequenceNo">Monotonic sequence number of the entry within its chain.</param>
/// <param name="Category">Event category (McpAuditCategory as short).</param>
/// <param name="EventType">Stable event type code (e.g. <c>ToolCall</c>, <c>AuthFailed</c>).</param>
/// <param name="ResultStatus">Stable result code (<c>Success</c>, <c>Denied</c>, <c>Failed</c>, …).</param>
/// <param name="McpAccessId">Optional soft reference to the causing access.</param>
/// <param name="McpAccessName">Optional snapshot of the access name.</param>
/// <param name="ToolName">Optional <c>fm_*</c> tool name.</param>
/// <param name="CorrelationId">Optional correlation id of the effect chain.</param>
/// <param name="Context">Optional administrative context (<c>Ui</c>, <c>McpEndpoint</c>, …).</param>
/// <param name="DurationMs">Optional tool call duration in milliseconds.</param>
/// <param name="AffectedCount">Optional number of affected records.</param>
/// <param name="ChangeCount">Number of attached field deltas (0 = none).</param>
public sealed record McpAuditEntryListItemDto(
    Guid Id,
    DateTime Utc,
    long SequenceNo,
    short Category,
    string EventType,
    string ResultStatus,
    Guid? McpAccessId,
    string? McpAccessName,
    string? ToolName,
    Guid? CorrelationId,
    string? Context,
    int? DurationMs,
    int? AffectedCount,
    int ChangeCount);

/// <summary>
/// One field delta (before/after) of an audit entry (drilling target, audit-konzept.md §3.2).
/// </summary>
/// <param name="EntityType">Affected entity type name (e.g. <c>Posting</c>, <c>Contact</c>).</param>
/// <param name="EntityId">Identifier of the affected record.</param>
/// <param name="Field">Name of the changed field.</param>
/// <param name="OldValue">Serialized previous value, or <see langword="null"/>.</param>
/// <param name="NewValue">Serialized new value, or <see langword="null"/>.</param>
public sealed record McpAuditChangeDto(
    string EntityType,
    Guid EntityId,
    string Field,
    string? OldValue,
    string? NewValue);

/// <summary>
/// Detail view of one audit entry (<c>GET /api/mcp-audit/{id}</c>) including the graduated
/// payload, the chain anchor fields and the field deltas. Audit rows are append-only —
/// this DTO is strictly read-only data.
/// </summary>
/// <param name="Id">Entry identifier.</param>
/// <param name="Utc">Server-side timestamp of the event.</param>
/// <param name="SequenceNo">Monotonic sequence number of the entry within its chain.</param>
/// <param name="Category">Event category (McpAuditCategory as short).</param>
/// <param name="EventType">Stable event type code.</param>
/// <param name="ResultStatus">Stable result code.</param>
/// <param name="McpAccessId">Optional soft reference to the causing access.</param>
/// <param name="McpAccessName">Optional snapshot of the access name.</param>
/// <param name="ToolName">Optional <c>fm_*</c> tool name.</param>
/// <param name="CatalogVersion">Optional tool catalog version (<c>katalog-v&lt;n&gt;</c>).</param>
/// <param name="Context">Optional administrative context.</param>
/// <param name="CorrelationId">Optional correlation id of the effect chain.</param>
/// <param name="SourceIp">Optional source IP of client calls.</param>
/// <param name="DurationMs">Optional tool call duration in milliseconds.</param>
/// <param name="AffectedCount">Optional number of affected records.</param>
/// <param name="PayloadJson">Optional graduated payload (never secrets/binary).</param>
/// <param name="RelatedRequestId">Optional link to an approval/action request.</param>
/// <param name="PrevHash">Hash of the predecessor entry of the same chain.</param>
/// <param name="EntryHash">Hash over the canonicalized entry content.</param>
/// <param name="Changes">Field deltas (before/after) of this entry.</param>
/// <param name="CreatedUtc">Persist timestamp of the entry row.</param>
public sealed record McpAuditEntryDetailDto(
    Guid Id,
    DateTime Utc,
    long SequenceNo,
    short Category,
    string EventType,
    string ResultStatus,
    Guid? McpAccessId,
    string? McpAccessName,
    string? ToolName,
    string? CatalogVersion,
    string? Context,
    Guid? CorrelationId,
    string? SourceIp,
    int? DurationMs,
    int? AffectedCount,
    string? PayloadJson,
    Guid? RelatedRequestId,
    string? PrevHash,
    string EntryHash,
    IReadOnlyList<McpAuditChangeDto> Changes,
    DateTime CreatedUtc);

/// <summary>
/// One contiguous run of missing sequence numbers inside an audit chain
/// (<see cref="McpAuditVerifyResultDto"/>).
/// </summary>
/// <param name="FirstMissingSequenceNo">First missing sequence number (inclusive).</param>
/// <param name="LastMissingSequenceNo">Last missing sequence number (inclusive).</param>
public sealed record McpAuditChainGapDto(long FirstMissingSequenceNo, long LastMissingSequenceNo);

/// <summary>
/// Result of the audit hash-chain verification (<c>GET /api/mcp-audit/verify</c>,
/// audit-konzept.md §7.2/§7.3).
/// </summary>
/// <param name="EntriesChecked">Number of chain entries that were loaded and checked.</param>
/// <param name="IsIntact"><see langword="true"/> when no breach and no gap was found.</param>
/// <param name="FirstBrokenSequenceNo">Sequence number of the first entry that failed
/// verification, or <see langword="null"/> when no breach was found.</param>
/// <param name="FailureReason">Human-readable description of the first breach.</param>
/// <param name="SequenceGaps">Runs of missing sequence numbers (Lücken).</param>
public sealed record McpAuditVerifyResultDto(
    long EntriesChecked,
    bool IsIntact,
    long? FirstBrokenSequenceNo,
    string? FailureReason,
    IReadOnlyList<McpAuditChainGapDto> SequenceGaps);

/// <summary>
/// Aggregated usage of one tool over one MCP access (F-0603, audit-konzept.md §6.1).
/// </summary>
/// <param name="ToolName"><c>fm_*</c> tool name.</param>
/// <param name="CallCount">Number of recorded calls.</param>
public sealed record McpAuditToolUsageDto(string ToolName, int CallCount);

/// <summary>
/// Usage overview of one MCP access aggregated from the audit trail (F-0603):
/// call counters, failure rate, top tools and the most recent calls.
/// </summary>
/// <param name="McpAccessId">Access identifier.</param>
/// <param name="TotalCalls">Total number of recorded tool calls of this access.</param>
/// <param name="SuccessCalls">Calls with result <c>Success</c>.</param>
/// <param name="FailedCalls">Calls with a non-success result (Denied, Failed, RateLimited, …).</param>
/// <param name="FailureRate">Share of failed calls (0..1; 0 when no calls).</param>
/// <param name="FirstCallUtc">Timestamp of the first recorded call, or <see langword="null"/>.</param>
/// <param name="LastCallUtc">Timestamp of the most recent recorded call, or <see langword="null"/>.</param>
/// <param name="TopTools">Most used tools (descending call count, capped server-side).</param>
/// <param name="RecentCalls">Most recent tool call entries (timeline excerpt).</param>
public sealed record McpAccessUsageDto(
    Guid McpAccessId,
    int TotalCalls,
    int SuccessCalls,
    int FailedCalls,
    double FailureRate,
    DateTime? FirstCallUtc,
    DateTime? LastCallUtc,
    IReadOnlyList<McpAuditToolUsageDto> TopTools,
    IReadOnlyList<McpAuditEntryListItemDto> RecentCalls);
