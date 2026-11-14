namespace FinanceManager.Domain.Mcp;

/// <summary>
/// An approval request of an MCP action (<c>AiActionRequest</c>): the persisted request of a
/// genehmigungspflichtige or approval-mode-gated tool call plus — at the same time — the
/// persistent execution queue of the MCP integration (approval-konzept.md §3/§5;
/// datenmodell-erweiterungen.md §5). The row carries the frozen parameter snapshot, the
/// decision and execution metadata, the idempotency key and the queue claim fields.
/// </summary>
/// <remarks>
/// <para>
/// <b>State machine (verbindlich, approval-konzept.md §3.3):</b>
/// <c>Pending → Approved | Rejected | Expired</c>;
/// <c>Approved → Executed | Failed | Rejected (system only) | Expired</c>;
/// <c>Executed</c>, <c>Failed</c>, <c>Rejected</c> and <c>Expired</c> are terminal.
/// Forbidden transitions (a rejected request never becomes approved; nothing leaves a
/// terminal state; no direct <c>Pending → Executed</c>) are technically excluded by the
/// guard checks of the transition methods, not just by convention.
/// </para>
/// <para>
/// <b>Queue claim:</b> the executor claims an approved request via a conditional update
/// (<c>WHERE Status = Approved</c>) that installs a <see cref="ClaimToken"/>/
/// <see cref="ClaimedUtc"/> pair — the same serialization pattern as
/// <c>StatementDraftBookingGuard</c>. A claim older than the executor's stale threshold may
/// be reclaimed (crash recovery of the single-instance runner).
/// </para>
/// <para>
/// <b>Snapshot fidelity:</b> <see cref="ParameterSnapshot"/> is frozen at
/// <see cref="RequestedUtc"/> and never changed afterwards; approval can only grant or
/// reject, never edit (bait-and-switch protection).
/// </para>
/// </remarks>
public sealed class AiActionRequest : Entity, IAggregateRoot
{
    /// <summary>
    /// Parameterless constructor for persistence/ORM and deserialization.
    /// </summary>
    private AiActionRequest() { }

    /// <summary>
    /// Creates a new approval request in <see cref="AiActionRequestStatus.Pending"/>.
    /// </summary>
    /// <param name="ownerUserId">Owner user id (= owner of the requesting access). Must not be empty.</param>
    /// <param name="mcpAccessId">Requesting access id (soft reference). Must not be empty.</param>
    /// <param name="mcpAccessName">Access name snapshot; stays readable after access deletion.</param>
    /// <param name="toolName">The <c>fm_*</c> tool name. Must not be null or whitespace.</param>
    /// <param name="actionKey">Sub-operation discriminator of the tool call (e.g. <c>merge</c>, <c>delete</c>).</param>
    /// <param name="catalogVersion">Catalog version (<c>katalog-v&lt;n&gt;</c>) at request time.</param>
    /// <param name="parameterSnapshot">Frozen JSON parameter snapshot. Must not be null or whitespace.</param>
    /// <param name="reasonText">Optional client-provided reason text (untrusted content).</param>
    /// <param name="expiresUtc">Decision deadline; after it the request expires.</param>
    /// <param name="correlationId">Correlation id linking tool call, request and audit entries.</param>
    /// <param name="idempotencyKey">Deduplication key (unique per owner). Must not be null or whitespace.</param>
    /// <exception cref="ArgumentException">Thrown when a required argument is empty or whitespace.</exception>
    public AiActionRequest(
        Guid ownerUserId,
        Guid mcpAccessId,
        string mcpAccessName,
        string toolName,
        string actionKey,
        string catalogVersion,
        string parameterSnapshot,
        string? reasonText,
        DateTime expiresUtc,
        Guid correlationId,
        string idempotencyKey)
    {
        OwnerUserId = Guards.NotEmpty(ownerUserId, nameof(ownerUserId));
        McpAccessId = Guards.NotEmpty(mcpAccessId, nameof(mcpAccessId));
        McpAccessName = Guards.NotNullOrWhiteSpace(mcpAccessName, nameof(mcpAccessName));
        ToolName = Guards.NotNullOrWhiteSpace(toolName, nameof(toolName));
        ActionKey = Guards.NotNullOrWhiteSpace(actionKey, nameof(actionKey));
        CatalogVersion = Guards.NotNullOrWhiteSpace(catalogVersion, nameof(catalogVersion));
        ParameterSnapshot = Guards.NotNullOrWhiteSpace(parameterSnapshot, nameof(parameterSnapshot));
        ReasonText = reasonText;
        Status = AiActionRequestStatus.Pending;
        RequestedUtc = DateTime.UtcNow;
        ExpiresUtc = expiresUtc;
        CorrelationId = correlationId;
        IdempotencyKey = Guards.NotNullOrWhiteSpace(idempotencyKey, nameof(idempotencyKey));
    }

    /// <summary>
    /// Creates a new request that is immediately <see cref="AiActionRequestStatus.Approved"/>
    /// (pre-approved scope, approval-konzept.md §3.4): the documented approval instance stays
    /// on the record — the request is enqueued into the execution queue without a pending phase.
    /// </summary>
    /// <param name="ownerUserId">Owner user id (= deciding owner). Must not be empty.</param>
    /// <param name="mcpAccessId">Requesting access id (soft reference).</param>
    /// <param name="mcpAccessName">Access name snapshot.</param>
    /// <param name="toolName">The <c>fm_*</c> tool name.</param>
    /// <param name="actionKey">Sub-operation discriminator of the tool call.</param>
    /// <param name="catalogVersion">Catalog version at request time.</param>
    /// <param name="parameterSnapshot">Frozen JSON parameter snapshot.</param>
    /// <param name="reasonText">Optional client-provided reason text (untrusted content).</param>
    /// <param name="expiresUtc">Decision deadline; after it the request expires.</param>
    /// <param name="correlationId">Correlation id of the effect chain.</param>
    /// <param name="idempotencyKey">Deduplication key (unique per owner).</param>
    /// <param name="decisionNote">Documented pre-approval note (e.g. "Vorab-Genehmigung Zugangseinstellung").</param>
    /// <returns>The pre-approved request, ready for the execution queue.</returns>
    public static AiActionRequest CreatePreApproved(
        Guid ownerUserId,
        Guid mcpAccessId,
        string mcpAccessName,
        string toolName,
        string actionKey,
        string catalogVersion,
        string parameterSnapshot,
        string? reasonText,
        DateTime expiresUtc,
        Guid correlationId,
        string idempotencyKey,
        string decisionNote)
    {
        var request = new AiActionRequest(
            ownerUserId, mcpAccessId, mcpAccessName, toolName, actionKey, catalogVersion,
            parameterSnapshot, reasonText, expiresUtc, correlationId, idempotencyKey);
        request.Approve(ownerUserId, decisionNote);
        return request;
    }

    /// <summary>Owner user identifier; all visibility and decision checks depend on it.</summary>
    public Guid OwnerUserId { get; private set; }

    /// <summary>
    /// Requesting access (soft reference — intentionally no FK constraint so the request
    /// stays readable as audit substrate after the access is deleted).
    /// </summary>
    public Guid McpAccessId { get; private set; }

    /// <summary>Access name at request time (readable after access deletion).</summary>
    public string McpAccessName { get; private set; } = null!;

    /// <summary>The <c>fm_*</c> tool name of the requested call.</summary>
    public string ToolName { get; private set; } = null!;

    /// <summary>
    /// Sub-operation discriminator of the tool call (e.g. <c>merge</c>, <c>delete</c>,
    /// <c>archive</c> for the pflege tools; a stable per-tool key otherwise).
    /// </summary>
    public string ActionKey { get; private set; } = null!;

    /// <summary>Catalog version (<c>katalog-v&lt;n&gt;</c>) at request time.</summary>
    public string CatalogVersion { get; private set; } = null!;

    /// <summary>
    /// Frozen JSON parameter snapshot — basis of the effect preview and the execution;
    /// immutable (no setter path, snapshot fidelity rule).
    /// </summary>
    public string ParameterSnapshot { get; private set; } = null!;

    /// <summary>Optional client-provided reason text (untrusted content, display-as-data).</summary>
    public string? ReasonText { get; private set; }

    /// <summary>Lifecycle state of the request.</summary>
    public AiActionRequestStatus Status { get; private set; }

    /// <summary>Server-side request timestamp.</summary>
    public DateTime RequestedUtc { get; private set; }

    /// <summary>Decision deadline; requests past it expire without a decision.</summary>
    public DateTime ExpiresUtc { get; private set; }

    /// <summary>Decision timestamp (approve/reject/pre-approve), or <see langword="null"/> while undecided.</summary>
    public DateTime? DecidedUtc { get; private set; }

    /// <summary>
    /// Deciding user — always the owner on user decisions; <see langword="null"/> on
    /// system-side transitions (withdrawal, expiry).
    /// </summary>
    public Guid? DecidedByUserId { get; private set; }

    /// <summary>Optional decision note (rejection reason, pre-approval marker, system reason).</summary>
    public string? DecisionNote { get; private set; }

    /// <summary>Execution timestamp, or <see langword="null"/> while not executed.</summary>
    public DateTime? ExecutedUtc { get; private set; }

    /// <summary>
    /// JSON result reference of a successful execution (created ids, result summary) —
    /// the link path into the audit chain. A failed execution carries the classified
    /// error envelope (<c>{"error":{…}}</c>) here instead — the diagnostic channel that
    /// <c>fm_task_status</c> surfaces as <c>untrusted_data.error</c>; it only ever contains
    /// defined, safe detail (defined code/message pairs or the bare exception type name,
    /// never internals, never stack traces).
    /// </summary>
    public string? ExecutionResult { get; private set; }

    /// <summary>Classified <c>Err_*</c> failure reason of a failed execution.</summary>
    public string? FailureReason { get; private set; }

    /// <summary>Correlation id linking tool call ↔ request ↔ audit entries.</summary>
    public Guid CorrelationId { get; private set; }

    /// <summary>Deduplication key (unique per owner) — a retry returns the same request.</summary>
    public string IdempotencyKey { get; private set; } = null!;

    /// <summary>Token of the active execution claim (<see langword="null"/> = not claimed).</summary>
    public Guid? ClaimToken { get; private set; }

    /// <summary>Timestamp of the active execution claim.</summary>
    public DateTime? ClaimedUtc { get; private set; }

    /// <summary>
    /// Number of execution attempts (diagnostic counter — there is deliberately no
    /// automatic retry of failed executions).
    /// </summary>
    public int AttemptCount { get; private set; }

    // ------------------------------------------------------------------
    // Decisions (Pending → …)
    // ------------------------------------------------------------------

    /// <summary>
    /// Approves the request (<c>Pending → Approved</c>) and places it into the execution queue.
    /// </summary>
    /// <param name="decidedByUserId">Deciding user — always the owner. Must not be empty.</param>
    /// <param name="note">Optional decision note.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="decidedByUserId"/> is empty.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the request is not <see cref="AiActionRequestStatus.Pending"/>.</exception>
    public void Approve(Guid decidedByUserId, string? note)
    {
        EnsureStatus(AiActionRequestStatus.Pending, "Only a pending request can be approved.");
        Status = AiActionRequestStatus.Approved;
        DecidedUtc = DateTime.UtcNow;
        DecidedByUserId = Guards.NotEmpty(decidedByUserId, nameof(decidedByUserId));
        DecisionNote = note;
        Touch();
    }

    /// <summary>
    /// Rejects the request (<c>Pending → Rejected</c>) — owner decision or owner-side
    /// withdrawal of an open request.
    /// </summary>
    /// <param name="decidedByUserId">Deciding user — always the owner. Must not be empty.</param>
    /// <param name="note">Optional rejection reason (played back to the client as decision feedback).</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="decidedByUserId"/> is empty.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the request is not <see cref="AiActionRequestStatus.Pending"/>.</exception>
    public void Reject(Guid decidedByUserId, string? note)
    {
        EnsureStatus(AiActionRequestStatus.Pending, "Only a pending request can be rejected.");
        Status = AiActionRequestStatus.Rejected;
        DecidedUtc = DateTime.UtcNow;
        DecidedByUserId = Guards.NotEmpty(decidedByUserId, nameof(decidedByUserId));
        DecisionNote = note;
        Touch();
    }

    /// <summary>
    /// System-side withdrawal (<c>Pending → Rejected</c> or <c>Approved → Rejected</c>):
    /// allowed only before execution starts, when a hard lid hits (access deactivated or
    /// revoked, required scope lost). The reason is documented in <see cref="DecisionNote"/>;
    /// <see cref="DecidedByUserId"/> stays <see langword="null"/> (system actor).
    /// </summary>
    /// <param name="reason">Documented system reason. Must not be null or whitespace.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="reason"/> is null or whitespace.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the request is neither pending nor approved.</exception>
    public void WithdrawBySystem(string reason)
    {
        if (Status is not (AiActionRequestStatus.Pending or AiActionRequestStatus.Approved))
        {
            throw new InvalidOperationException(
                $"A request in state {Status} cannot be withdrawn by the system.");
        }
        Status = AiActionRequestStatus.Rejected;
        DecidedUtc = DateTime.UtcNow;
        DecidedByUserId = null;
        DecisionNote = Guards.NotNullOrWhiteSpace(reason, nameof(reason));
        ClaimToken = null;
        ClaimedUtc = null;
        Touch();
    }

    /// <summary>
    /// Expires the request (<c>Pending → Expired</c> or <c>Approved → Expired</c>) once the
    /// decision deadline has passed without completion — an approved-but-held request
    /// (e.g. under the write-stop lid) expires likewise.
    /// </summary>
    /// <param name="utcNow">The current UTC timestamp to evaluate the deadline against.</param>
    /// <exception cref="InvalidOperationException">Thrown when the request is terminal or the deadline has not passed.</exception>
    public void Expire(DateTime utcNow)
    {
        if (Status is not (AiActionRequestStatus.Pending or AiActionRequestStatus.Approved))
        {
            throw new InvalidOperationException(
                $"A request in state {Status} cannot expire.");
        }
        if (ExpiresUtc > utcNow)
        {
            throw new InvalidOperationException("The request has not reached its expiry deadline yet.");
        }
        Status = AiActionRequestStatus.Expired;
        DecidedUtc = utcNow;
        DecidedByUserId = null;
        ClaimToken = null;
        ClaimedUtc = null;
        Touch();
    }

    // ------------------------------------------------------------------
    // Queue claim and execution (Approved → …)
    // ------------------------------------------------------------------

    /// <summary>
    /// Records the queue claim of this request (called after the atomic conditional update
    /// won, or mirrored by it). Only an <see cref="AiActionRequestStatus.Approved"/> request
    /// can be claimed.
    /// </summary>
    /// <param name="claimToken">The claim token identifying this execution attempt. Must not be empty.</param>
    /// <param name="claimedUtc">The claim timestamp.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="claimToken"/> is empty.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the request is not approved.</exception>
    public void MarkClaimed(Guid claimToken, DateTime claimedUtc)
    {
        EnsureStatus(AiActionRequestStatus.Approved, "Only an approved request can be claimed.");
        ClaimToken = Guards.NotEmpty(claimToken, nameof(claimToken));
        ClaimedUtc = claimedUtc;
        AttemptCount++;
        Touch();
    }

    /// <summary>
    /// Releases an active claim without a state change — the request stays approved
    /// (execution deferred, e.g. while the write-stop lid is active).
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the request is not approved.</exception>
    public void ReleaseClaim()
    {
        EnsureStatus(AiActionRequestStatus.Approved, "Only an approved request can release a claim.");
        ClaimToken = null;
        ClaimedUtc = null;
        Touch();
    }

    /// <summary>
    /// Marks the request as successfully executed (<c>Approved → Executed</c>, terminal).
    /// The request must be claimed — unclaimed execution is impossible.
    /// </summary>
    /// <param name="resultJson">JSON result reference (created ids, result summary).</param>
    /// <exception cref="InvalidOperationException">Thrown when the request is not an approved, claimed one.</exception>
    public void MarkExecuted(string? resultJson)
    {
        EnsureClaimed("Only a claimed, approved request can be marked executed.");
        Status = AiActionRequestStatus.Executed;
        ExecutedUtc = DateTime.UtcNow;
        ExecutionResult = resultJson;
        ClaimToken = null;
        ClaimedUtc = null;
        Touch();
    }

    /// <summary>
    /// Marks the request as failed (<c>Approved → Failed</c>, terminal). There is no
    /// automatic retry — a new attempt is a conscious new client request.
    /// </summary>
    /// <param name="failureReason">Classified <c>Err_*</c> failure reason. Must not be null or whitespace.</param>
    /// <param name="failureResultJson">Optional classified error envelope
    /// (<c>{"error":{…}}</c>) stored as the execution result reference — defined, safe
    /// diagnostic detail only (never internals/stack traces of foreign exceptions).</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="failureReason"/> is null or whitespace.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the request is not an approved, claimed one.</exception>
    public void MarkFailed(string failureReason, string? failureResultJson = null)
    {
        EnsureClaimed("Only a claimed, approved request can be marked failed.");
        Status = AiActionRequestStatus.Failed;
        ExecutedUtc = DateTime.UtcNow;
        FailureReason = Guards.NotNullOrWhiteSpace(failureReason, nameof(failureReason));
        ExecutionResult = failureResultJson;
        ClaimToken = null;
        ClaimedUtc = null;
        Touch();
    }

    /// <summary>
    /// Determines whether the request is in a terminal state (no further transitions).
    /// </summary>
    /// <returns><see langword="true"/> for Executed, Failed, Rejected and Expired.</returns>
    public bool IsTerminal()
        => Status is AiActionRequestStatus.Executed or AiActionRequestStatus.Failed
            or AiActionRequestStatus.Rejected or AiActionRequestStatus.Expired;

    private void EnsureStatus(AiActionRequestStatus expected, string message)
    {
        if (Status != expected)
        {
            throw new InvalidOperationException($"{message} Current state: {Status}.");
        }
    }

    private void EnsureClaimed(string message)
    {
        if (Status != AiActionRequestStatus.Approved || ClaimToken is null)
        {
            throw new InvalidOperationException($"{message} Current state: {Status}, claimed: {ClaimToken is not null}.");
        }
    }

    // ------------------------------------------------------------------
    // Backup
    // ------------------------------------------------------------------

    /// <summary>
    /// Backup DTO of <see cref="AiActionRequest"/>: open (non-terminal) requests survive a
    /// restore so an already granted approval is not lost (F-0901). No secret material is
    /// part of the record.
    /// </summary>
    /// <param name="Id">Request identifier.</param>
    /// <param name="OwnerUserId">Owner user identifier.</param>
    /// <param name="McpAccessId">Requesting access identifier (soft reference).</param>
    /// <param name="McpAccessName">Access name snapshot.</param>
    /// <param name="ToolName">Tool name.</param>
    /// <param name="ActionKey">Sub-operation discriminator.</param>
    /// <param name="CatalogVersion">Catalog version.</param>
    /// <param name="ParameterSnapshot">Frozen parameter snapshot (JSON).</param>
    /// <param name="ReasonText">Optional client reason text.</param>
    /// <param name="Status">Lifecycle state.</param>
    /// <param name="RequestedUtc">Request timestamp.</param>
    /// <param name="ExpiresUtc">Decision deadline.</param>
    /// <param name="DecidedUtc">Decision timestamp.</param>
    /// <param name="DecidedByUserId">Deciding user.</param>
    /// <param name="DecisionNote">Decision note.</param>
    /// <param name="ExecutedUtc">Execution timestamp.</param>
    /// <param name="ExecutionResult">Execution result reference (JSON).</param>
    /// <param name="FailureReason">Classified failure reason.</param>
    /// <param name="CorrelationId">Correlation id.</param>
    /// <param name="IdempotencyKey">Deduplication key.</param>
    /// <param name="AttemptCount">Execution attempt counter.</param>
    /// <param name="CreatedUtc">Creation timestamp.</param>
    /// <param name="ModifiedUtc">Last modification timestamp.</param>
    public sealed record AiActionRequestBackupDto(
        Guid Id,
        Guid OwnerUserId,
        Guid McpAccessId,
        string McpAccessName,
        string ToolName,
        string ActionKey,
        string CatalogVersion,
        string ParameterSnapshot,
        string? ReasonText,
        AiActionRequestStatus Status,
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
        DateTime CreatedUtc,
        DateTime? ModifiedUtc);

    /// <summary>
    /// Creates the backup DTO of this request. Claim fields are deliberately not exported —
    /// a restored open request is never claimed.
    /// </summary>
    /// <returns>A <see cref="AiActionRequestBackupDto"/> containing the backup data.</returns>
    public AiActionRequestBackupDto ToBackupDto()
        => new(
            Id, OwnerUserId, McpAccessId, McpAccessName, ToolName, ActionKey, CatalogVersion,
            ParameterSnapshot, ReasonText, Status, RequestedUtc, ExpiresUtc, DecidedUtc,
            DecidedByUserId, DecisionNote, ExecutedUtc, ExecutionResult, FailureReason,
            CorrelationId, IdempotencyKey, AttemptCount, CreatedUtc, ModifiedUtc);

    /// <summary>
    /// Applies a backup DTO to this request (restore path): state and timestamps are taken
    /// verbatim; any queue claim is cleared so a restored approved request is picked up by
    /// the executor again.
    /// </summary>
    /// <param name="dto">The backup DTO to apply. Must not be <c>null</c>.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="dto"/> is <c>null</c>.</exception>
    public void AssignBackupDto(AiActionRequestBackupDto dto)
    {
        if (dto == null) throw new ArgumentNullException(nameof(dto));
        McpAccessId = dto.McpAccessId;
        McpAccessName = dto.McpAccessName;
        ToolName = dto.ToolName;
        ActionKey = dto.ActionKey;
        CatalogVersion = dto.CatalogVersion;
        ParameterSnapshot = dto.ParameterSnapshot;
        ReasonText = dto.ReasonText;
        Status = dto.Status;
        RequestedUtc = dto.RequestedUtc;
        ExpiresUtc = dto.ExpiresUtc;
        DecidedUtc = dto.DecidedUtc;
        DecidedByUserId = dto.DecidedByUserId;
        DecisionNote = dto.DecisionNote;
        ExecutedUtc = dto.ExecutedUtc;
        ExecutionResult = dto.ExecutionResult;
        FailureReason = dto.FailureReason;
        CorrelationId = dto.CorrelationId;
        IdempotencyKey = dto.IdempotencyKey;
        AttemptCount = dto.AttemptCount;
        ClaimToken = null;
        ClaimedUtc = null;
        SetDates(dto.CreatedUtc, dto.ModifiedUtc);
    }
}
