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
    int AttemptCount);

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
    int AttemptCount);

/// <summary>
/// Request body of the owner decision endpoints (<c>approve</c>/<c>reject</c>): the optional
/// decision comment is stored as the request's <c>DecisionNote</c> and played back to the
/// client as decision feedback (a rejection reason is optional but recommended,
/// approval-konzept.md §7.3).
/// </summary>
/// <param name="DecisionComment">Optional decision note / rejection reason.</param>
public sealed record AiActionRequestDecisionRequest(string? DecisionComment);
