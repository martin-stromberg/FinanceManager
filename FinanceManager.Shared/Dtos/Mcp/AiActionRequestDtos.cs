namespace FinanceManager.Shared.Dtos.Mcp;

/// <summary>
/// List item of an approval request (<c>AiActionRequest</c>) — inbox and history rows of the
/// approval center and of <c>fm_task_status</c>. <see cref="Status"/> is transported as
/// <see langword="short"/> (AiActionRequestStatus) following the DTO convention.
/// </summary>
/// <param name="Id">Request identifier (the client-facing reference).</param>
/// <param name="McpAccessId">Requesting access identifier (soft reference).</param>
/// <param name="McpAccessName">Access name snapshot at request time.</param>
/// <param name="ToolName">The <c>fm_*</c> tool name.</param>
/// <param name="ActionKey">Sub-operation discriminator (e.g. <c>merge</c>, <c>delete</c>).</param>
/// <param name="Status">Lifecycle state (AiActionRequestStatus as short).</param>
/// <param name="RequestedUtc">Request timestamp.</param>
/// <param name="ExpiresUtc">Decision deadline.</param>
/// <param name="DecidedUtc">Decision timestamp, or <see langword="null"/>.</param>
/// <param name="ExecutedUtc">Execution timestamp, or <see langword="null"/>.</param>
/// <param name="ExecutionResult">JSON result reference of a successful execution, or <see langword="null"/>.</param>
/// <param name="FailureReason">Classified <c>Err_*</c> failure reason, or <see langword="null"/>.</param>
/// <param name="CorrelationId">Correlation id of the effect chain.</param>
/// <param name="AttemptCount">Execution attempt counter.</param>
/// <param name="EffectPreview">Server-side effect preview of the request (same classification
/// as the detail projection) — the approval center renders the compact localized effect
/// statement and the destructive badge of the inbox row from it (ui-konzept.md §4.6).</param>
public sealed record AiActionRequestListItemDto(
    Guid Id,
    Guid McpAccessId,
    string McpAccessName,
    string ToolName,
    string ActionKey,
    short Status,
    DateTime RequestedUtc,
    DateTime ExpiresUtc,
    DateTime? DecidedUtc,
    DateTime? ExecutedUtc,
    string? ExecutionResult,
    string? FailureReason,
    Guid CorrelationId,
    int AttemptCount,
    AiActionRequestEffectPreviewDto? EffectPreview);

/// <summary>
/// Detail projection of one approval request: everything of the list item plus the frozen
/// parameter snapshot (basis of the server-side effect preview), the client reason text
/// (untrusted content — display as data), the decision metadata and the execution result.
/// </summary>
/// <param name="Id">Request identifier.</param>
/// <param name="McpAccessId">Requesting access identifier.</param>
/// <param name="McpAccessName">Access name snapshot.</param>
/// <param name="ToolName">The <c>fm_*</c> tool name.</param>
/// <param name="ActionKey">Sub-operation discriminator.</param>
/// <param name="CatalogVersion">Catalog version at request time.</param>
/// <param name="ParameterSnapshot">Frozen JSON parameter snapshot.</param>
/// <param name="ReasonText">Optional client reason text (untrusted content).</param>
/// <param name="Status">Lifecycle state (AiActionRequestStatus as short).</param>
/// <param name="RequestedUtc">Request timestamp.</param>
/// <param name="ExpiresUtc">Decision deadline.</param>
/// <param name="DecidedUtc">Decision timestamp, or <see langword="null"/>.</param>
/// <param name="DecidedByUserId">Deciding user, or <see langword="null"/> for system transitions.</param>
/// <param name="DecisionNote">Decision note / rejection reason.</param>
/// <param name="ExecutedUtc">Execution timestamp, or <see langword="null"/>.</param>
/// <param name="ExecutionResult">Execution result reference (JSON), or <see langword="null"/>.</param>
/// <param name="FailureReason">Classified <c>Err_*</c> failure reason, or <see langword="null"/>.</param>
/// <param name="CorrelationId">Correlation id of the effect chain.</param>
/// <param name="IdempotencyKey">Deduplication key.</param>
/// <param name="AttemptCount">Execution attempt counter.</param>
/// <param name="EffectPreview">Server-side effect preview derived from the frozen parameter
/// snapshot (approval-konzept.md §7.2 / ui-konzept.md §4.6), or <see langword="null"/> when
/// the snapshot cannot be classified.</param>
public sealed record AiActionRequestDetailDto(
    Guid Id,
    Guid McpAccessId,
    string McpAccessName,
    string ToolName,
    string ActionKey,
    string CatalogVersion,
    string ParameterSnapshot,
    string? ReasonText,
    short Status,
    DateTime RequestedUtc,
    DateTime ExpiresUtc,
    DateTime? DecidedUtc,
    Guid? DecidedByUserId,
    string? DecisionNote,
    DateTime? ExecutedUtc,
    string? ExecutionResult,
    string? FailureReason,
    Guid CorrelationId,
    string IdempotencyKey,
    int AttemptCount,
    AiActionRequestEffectPreviewDto? EffectPreview);

/// <summary>
/// Server-side effect preview of one approval request (F-1001): a stable classification of
/// the requested effect (<see cref="Kind"/>), the destruktiv flag driving the critical
/// confirmation step of the approval center, and the relevant scalar fields of the frozen
/// parameter snapshot so the UI can render a concrete "what would happen" view without
/// interpreting raw JSON.
/// </summary>
/// <param name="Kind">Stable effect kind: <c>book</c>, <c>bookAll</c>, <c>commit</c>,
/// <c>discard</c>, <c>discardAll</c>, <c>reverse</c>, <c>merge</c>, <c>delete</c>,
/// <c>archive</c> or <c>generic</c>.</param>
/// <param name="IsDestructive"><see langword="true"/> for final/irreversible effects —
/// the approval center confirms these via the critical severity of the
/// <c>IConfirmationService</c>.</param>
/// <param name="Fields">Relevant scalar snapshot parameters (name/value pairs, untrusted
/// content — display as data).</param>
public sealed record AiActionRequestEffectPreviewDto(
    string Kind,
    bool IsDestructive,
    IReadOnlyList<AiActionRequestEffectFieldDto> Fields);

/// <summary>
/// One scalar field of the effect preview (a flattened parameter-snapshot entry).
/// </summary>
/// <param name="Name">Snapshot property name.</param>
/// <param name="Value">Scalar value rendered as text (untrusted content).</param>
public sealed record AiActionRequestEffectFieldDto(string Name, string? Value);

/// <summary>
/// Request body of <c>POST /api/mcp-approvals/batch</c>: a multi decision over own pending
/// requests. Every request keeps its own state transition — partial success is reported
/// per item (approval-konzept.md §7.4, F-1001).
/// </summary>
/// <param name="RequestIds">Requests to decide (server-clamped).</param>
/// <param name="Approve"><see langword="true"/> approves, <see langword="false"/> rejects.</param>
/// <param name="DecisionComment">Optional decision note applied to every decided request.</param>
public sealed record AiActionRequestBatchRequest(
    IReadOnlyList<Guid> RequestIds,
    bool Approve,
    string? DecisionComment);

/// <summary>
/// Per-request outcome of a batch decision.
/// </summary>
/// <param name="RequestId">The addressed request.</param>
/// <param name="Succeeded"><see langword="true"/> when the transition was applied.</param>
/// <param name="ErrorCode">Stable <c>Err_*</c> code of a failed item, or <see langword="null"/>.</param>
public sealed record AiActionRequestBatchItemDto(Guid RequestId, bool Succeeded, string? ErrorCode);

/// <summary>
/// Request body of the owner decision endpoints (<c>approve</c>/<c>reject</c>): the optional
/// decision comment is stored as the request's <c>DecisionNote</c> and played back to the
/// client as decision feedback (a rejection reason is optional but recommended,
/// approval-konzept.md §7.3).
/// </summary>
/// <param name="DecisionComment">Optional decision note / rejection reason.</param>
public sealed record AiActionRequestDecisionRequest(string? DecisionComment);

/// <summary>
/// Monitoring overview of one MCP access over its <c>AiActionRequest</c> workload
/// (F-1103 — the agent runbook view): queue depth, pending age and the 24-hour outcome
/// counters of the agent's own runs. Owner-scoped; a foreign access id behaves like a
/// missing record (404 semantics).
/// </summary>
/// <param name="McpAccessId">Access identifier.</param>
/// <param name="PendingCount">Requests currently waiting for a decision.</param>
/// <param name="ApprovedCount">Requests approved and waiting in the execution queue.</param>
/// <param name="OldestPendingUtc">Timestamp of the oldest pending request, or
/// <see langword="null"/> when the inbox is empty (pending-age indicator).</param>
/// <param name="RequestsLast24h">Requests created within the last 24 hours.</param>
/// <param name="ExecutedLast24h">Requests that reached <c>Executed</c> within the last 24 hours.</param>
/// <param name="FailedLast24h">Requests that reached <c>Failed</c> within the last 24 hours.</param>
/// <param name="RejectedLast24h">Requests decided <c>Rejected</c> within the last 24 hours.</param>
/// <param name="ExpiredLast24h">Requests that reached <c>Expired</c> within the last 24 hours.</param>
/// <param name="FailureRateLast24h">Share of failed executions among the requests that
/// reached a terminal execution state (Executed/Failed) within the last 24 hours
/// (0 when none).</param>
/// <param name="LastRequestUtc">Timestamp of the most recent request of the access, or
/// <see langword="null"/>.</param>
public sealed record McpAccessMonitoringDto(
    Guid McpAccessId,
    int PendingCount,
    int ApprovedCount,
    DateTime? OldestPendingUtc,
    int RequestsLast24h,
    int ExecutedLast24h,
    int FailedLast24h,
    int RejectedLast24h,
    int ExpiredLast24h,
    double FailureRateLast24h,
    DateTime? LastRequestUtc);
