using FinanceManager.Domain.Mcp;

namespace FinanceManager.Application.Mcp;

/// <summary>
/// Push notification of one <see cref="AiActionRequest"/> state change (F-1101,
/// approval-konzept.md §4 — the SSE channel of the <c>/mcp</c> endpoint). Published after
/// every persisted transition (<c>Pending → Approved/Rejected/Expired</c>,
/// <c>Approved → Executing/Executed/Failed/Rejected/Expired</c>); the payload carries only
/// status metadata — never the parameter snapshot, reason text or result payload
/// (untrusted content stays on the pull path <c>fm_task_status</c>).
/// </summary>
/// <param name="McpAccessId">Access whose request changed (subscription key — events never
/// cross access boundaries).</param>
/// <param name="OwnerUserId">Owner of the request.</param>
/// <param name="RequestId">The affected request.</param>
/// <param name="Status">Lifecycle status name; additionally <c>Executing</c> marks a claimed
/// queue entry whose persisted status is still <c>Approved</c> (queue progress hint).</param>
/// <param name="ToolName">Optional <c>fm_*</c> tool name.</param>
/// <param name="ActionKey">Optional sub-operation discriminator.</param>
/// <param name="CorrelationId">Optional correlation id of the effect chain.</param>
/// <param name="FailureReason">Optional classified <c>Err_*</c> failure reason.</param>
/// <param name="Utc">Timestamp of the transition.</param>
public sealed record McpRequestStatusEvent(
    Guid McpAccessId,
    Guid OwnerUserId,
    Guid RequestId,
    string Status,
    string? ToolName = null,
    string? ActionKey = null,
    Guid? CorrelationId = null,
    string? FailureReason = null,
    DateTime? Utc = null)
{
    /// <summary>Pseudo status emitted when an approved request is claimed for execution.</summary>
    public const string Executing = "Executing";

    /// <summary>
    /// Builds the status event of a persisted request transition.
    /// </summary>
    /// <param name="request">The request after its transition.</param>
    /// <param name="statusOverride">
    /// Optional pseudo status (<see cref="Executing"/>); when <see langword="null"/> the
    /// persisted <see cref="AiActionRequest.Status"/> is reported.
    /// </param>
    /// <returns>The status event.</returns>
    public static McpRequestStatusEvent FromRequest(AiActionRequest request, string? statusOverride = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new McpRequestStatusEvent(
            request.McpAccessId,
            request.OwnerUserId,
            request.Id,
            statusOverride ?? request.Status.ToString(),
            request.ToolName,
            request.ActionKey,
            request.CorrelationId,
            request.FailureReason,
            DateTime.UtcNow);
    }
}

/// <summary>
/// Push channel of request status changes to live agent connections (F-1101): the
/// <c>/mcp</c> SSE subscription of an access receives every event of that access —
/// <c>fm_task_status</c> polling stays the fallback for clients without SSE.
/// Implementations must be non-blocking (dropped events are tolerable; the request row is
/// the source of truth) and must never throw into the caller's unit of work.
/// </summary>
public interface IMcpRequestStatusPublisher
{
    /// <summary>
    /// Publishes one request status change to the subscribers of the owning access.
    /// </summary>
    /// <param name="statusEvent">The event payload (status metadata only).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the event was dispatched (best effort).</returns>
    Task PublishAsync(McpRequestStatusEvent statusEvent, CancellationToken ct);
}
