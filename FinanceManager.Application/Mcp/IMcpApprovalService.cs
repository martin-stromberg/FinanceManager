using FinanceManager.Domain.Mcp;
using FinanceManager.Shared.Dtos.Mcp;

namespace FinanceManager.Application.Mcp;

/// <summary>
/// Application service of the approval request queue (F-0902): creates
/// <see cref="AiActionRequest"/> rows with idempotency deduplication, owner-scoped decision
/// transitions (approve/reject/cancel), owner-isolated read paths and the system-side
/// withdrawal of open requests. Every mutating method couples its
/// <see cref="McpAuditCategory.ApprovalEvent"/> record transactionally to the state
/// transition (one <c>SaveChanges</c>, F-0301 rule).
/// </summary>
/// <remarks>
/// Owner scoping is strict: a foreign request id behaves like a missing one
/// (404 semantics). System transitions carry <see langword="null"/> as deciding user and a
/// documented reason in <see cref="AiActionRequest.DecisionNote"/>.
/// </remarks>
public interface IMcpApprovalService
{
    /// <summary>
    /// Enqueues a new approval request for a tool call. The request starts
    /// <see cref="AiActionRequestStatus.Pending"/> — or already
    /// <see cref="AiActionRequestStatus.Approved"/> when the access runs in the
    /// <see cref="McpApprovalMode.PreApproved"/> mode (documented pre-approval,
    /// approval-konzept.md §3.4).
    /// </summary>
    /// <param name="access">The authenticated access entity requesting the action.</param>
    /// <param name="toolName">The <c>fm_*</c> tool name.</param>
    /// <param name="actionKey">Sub-operation discriminator of the call (e.g. <c>merge</c>).</param>
    /// <param name="catalogVersion">Catalog version (<c>katalog-v&lt;n&gt;</c>) at request time.</param>
    /// <param name="parameterSnapshotJson">Frozen JSON parameter snapshot.</param>
    /// <param name="reasonText">Optional client reason text (untrusted content, length-capped).</param>
    /// <param name="correlationId">Correlation id of the effect chain.</param>
    /// <param name="clientIdempotencyKey">Optional client-side deduplication key; a repeated call
    /// with the same key returns the existing request instead of a duplicate.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created (or deduplicated existing) request.</returns>
    /// <exception cref="FinanceManager.Application.Exceptions.DomainValidationException">
    /// <c>Err_Mcp_PendingLimit</c> when the per-access pending-request limit is reached.
    /// </exception>
    Task<AiActionRequest> EnqueueAsync(
        McpAccess access,
        string toolName,
        string actionKey,
        string catalogVersion,
        string parameterSnapshotJson,
        string? reasonText,
        Guid correlationId,
        string? clientIdempotencyKey,
        CancellationToken ct);

    /// <summary>
    /// Approves a pending request of the owner (<c>Pending → Approved</c>).
    /// </summary>
    /// <param name="ownerUserId">Owner user id (must own the request).</param>
    /// <param name="requestId">Request identifier.</param>
    /// <param name="note">Optional decision note.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see langword="true"/> when the request was approved; <see langword="false"/> when not found/not owned.</returns>
    /// <exception cref="FinanceManager.Application.Exceptions.DomainValidationException">
    /// <c>Err_Mcp_InvalidState</c> when the request is not pending.
    /// </exception>
    Task<bool> ApproveAsync(Guid ownerUserId, Guid requestId, string? note, CancellationToken ct);

    /// <summary>
    /// Rejects a pending request of the owner (<c>Pending → Rejected</c>).
    /// </summary>
    /// <param name="ownerUserId">Owner user id (must own the request).</param>
    /// <param name="requestId">Request identifier.</param>
    /// <param name="note">Optional rejection reason (played back to the client as decision feedback).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see langword="true"/> when rejected; <see langword="false"/> when not found/not owned.</returns>
    /// <exception cref="FinanceManager.Application.Exceptions.DomainValidationException">
    /// <c>Err_Mcp_InvalidState</c> when the request is not pending.
    /// </exception>
    Task<bool> RejectAsync(Guid ownerUserId, Guid requestId, string? note, CancellationToken ct);

    /// <summary>
    /// Withdraws a still-pending request of the owner (<c>Pending → Rejected</c> with an
    /// owner-withdrawal note): the owner-side cancel of an open request.
    /// </summary>
    /// <param name="ownerUserId">Owner user id (must own the request).</param>
    /// <param name="requestId">Request identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see langword="true"/> when withdrawn; <see langword="false"/> when not found/not owned.</returns>
    /// <exception cref="FinanceManager.Application.Exceptions.DomainValidationException">
    /// <c>Err_Mcp_InvalidState</c> when the request is not pending.
    /// </exception>
    Task<bool> CancelAsync(Guid ownerUserId, Guid requestId, CancellationToken ct);

    /// <summary>
    /// Gets one request of the owner (full detail incl. parameter snapshot).
    /// </summary>
    /// <param name="ownerUserId">Owner user id.</param>
    /// <param name="requestId">Request identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The detail DTO or <see langword="null"/> when not found/not owned.</returns>
    Task<AiActionRequestDetailDto?> GetAsync(Guid ownerUserId, Guid requestId, CancellationToken ct);

    /// <summary>
    /// Lists requests of the owner, optionally filtered by status and/or requesting access.
    /// </summary>
    /// <param name="ownerUserId">Owner user id.</param>
    /// <param name="status">Optional status filter.</param>
    /// <param name="accessId">Optional access filter.</param>
    /// <param name="skip">Items to skip (≥ 0).</param>
    /// <param name="take">Items to take (server-clamped).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The matching requests ordered by <see cref="AiActionRequest.RequestedUtc"/> descending.</returns>
    Task<IReadOnlyList<AiActionRequestListItemDto>> ListAsync(
        Guid ownerUserId, AiActionRequestStatus? status, Guid? accessId, int skip, int take, CancellationToken ct);

    /// <summary>
    /// Counts the currently pending requests of one access (pending-limit evaluation).
    /// </summary>
    /// <param name="mcpAccessId">Access identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Number of pending requests.</returns>
    Task<int> CountPendingAsync(Guid mcpAccessId, CancellationToken ct);

    /// <summary>
    /// Counts the currently pending requests of an owner across all own accesses
    /// (F-1001: the navigation badge of the approval center).
    /// </summary>
    /// <param name="ownerUserId">Owner user id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Number of pending requests of the owner.</returns>
    Task<int> CountPendingForOwnerAsync(Guid ownerUserId, CancellationToken ct);

    /// <summary>
    /// Applies one decision (approve or reject) to a set of own pending requests — the
    /// batch operation of the approval center (F-1001, approval-konzept.md §7.4). Every
    /// request keeps its own state transition and its own
    /// <see cref="McpAuditCategory.ApprovalEvent"/> record; requests that are not pending,
    /// not owned or missing are reported per item and do not abort the batch. All applied
    /// transitions commit atomically in one save.
    /// </summary>
    /// <param name="ownerUserId">Owner user id.</param>
    /// <param name="requestIds">Requests to decide (server-clamped by the caller).</param>
    /// <param name="approve"><see langword="true"/> approves, <see langword="false"/> rejects.</param>
    /// <param name="note">Optional decision note applied to every decided request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>One outcome row per requested id.</returns>
    Task<IReadOnlyList<AiActionRequestBatchItemDto>> DecideBatchAsync(
        Guid ownerUserId, IReadOnlyList<Guid> requestIds, bool approve, string? note, CancellationToken ct);

    /// <summary>
    /// System-side withdrawal of all open (pending and approved-not-yet-executing) requests
    /// of one access — the revocation/deactivation transition of approval-konzept.md §6.3.
    /// Approved requests under an active claim are left untouched (a started execution
    /// finishes; the executor re-checks the access anyway).
    /// </summary>
    /// <param name="mcpAccessId">Access identifier.</param>
    /// <param name="reason">Documented system reason (e.g. "Zugang entzogen").</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Number of withdrawn requests.</returns>
    Task<int> WithdrawOpenForAccessAsync(Guid mcpAccessId, string reason, CancellationToken ct);

    /// <summary>
    /// System-side withdrawal of the open requests of one access whose declared tool scope
    /// the current effective profile no longer covers — the „Recht entfallen" transition of
    /// approval-konzept.md §3.3/§6.1 after a scope-profile or access-level change. The check
    /// is granular: a request is only withdrawn when its tool descriptor declares a
    /// (Datentyp × Rechteart) tuple missing from the new profile; still-covered requests,
    /// meta-scope tools (inherent scope) and requests under an active claim stay untouched.
    /// Called by the access administration after profile-changing writes
    /// (<c>SetScopes</c>, level-changing <c>Update</c>); the executor's execution-time
    /// re-check remains the safety net for every other lid.
    /// </summary>
    /// <param name="mcpAccessId">Access identifier.</param>
    /// <param name="reason">Documented system reason (e.g. "Recht entfallen").</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Number of withdrawn requests.</returns>
    Task<int> WithdrawScopeUncoveredForAccessAsync(Guid mcpAccessId, string reason, CancellationToken ct);
}
