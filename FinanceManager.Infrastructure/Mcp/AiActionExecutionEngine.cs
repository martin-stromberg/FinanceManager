using FinanceManager.Application.Mcp;
using FinanceManager.Application.Notifications;
using FinanceManager.Domain.Mcp;
using FinanceManager.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace FinanceManager.Infrastructure.Mcp;

/// <summary>
/// Execution engine of the persistent approval queue (F-0903/F-0904; approval-konzept.md §5,
/// architekturkonzept.md §6.2). The <see cref="AiActionRequest"/> table itself is the queue:
/// due approved requests are claimed by a conditional, atomic update
/// (<c>WHERE Status = Approved AND (ClaimToken IS NULL OR ClaimedUtc &lt;= stale)</c>) — the
/// same serialization pattern as <c>StatementDraftBookingGuard</c> — and then executed via
/// the original <see cref="IMcpToolHandler"/> in
/// <see cref="McpToolExecutionMode.ApprovedExecution"/> mode.
/// </summary>
/// <remarks>
/// <para>
/// <b>Guard re-evaluation (sicherheitskonzept.md §7):</b> between approval and execution the
/// data and rights situation may have changed, so the engine re-checks — before invoking the
/// handler — the request deadline, the access existence/usability (status, expiry; a revoked
/// or deactivated access withdraws the request), the write-stop lid (which defers the
/// request back into the queue instead of executing) and every declared scope tuple of the
/// tool descriptor. Inside the handler the whole tool-guard layer runs again on the frozen
/// parameter snapshot (argument validation, record-level scope filters, service guards).
/// </para>
/// <para>
/// <b>Single-shot semantics:</b> only <c>Approved → Executed|Failed</c> is reachable; there
/// is deliberately no automatic retry — <see cref="AiActionRequest.AttemptCount"/> is a
/// diagnostic counter. A crashed executor leaves a stale claim that becomes reclaimable
/// after <c>Mcp:Approval:ClaimStaleMinutes</c>.
/// </para>
/// </remarks>
public sealed class AiActionExecutionEngine : IAiActionExecutionEngine
{
    /// <summary>Maximum characters of a serialized execution result kept on the request.</summary>
    private const int MaxResultChars = 8000;

    private readonly AppDbContext _db;
    private readonly IMcpScopeEvaluator _scopeEvaluator;
    private readonly IMcpAuditWriter _auditWriter;
    private readonly IMcpToolRegistry _registry;
    private readonly INotificationWriter _notifications;
    private readonly IServiceProvider _services;
    private readonly IOptionsMonitor<McpOptions> _options;
    private readonly IMcpSettingsService _settings;
    private readonly IMcpRequestStatusPublisher? _statusPublisher;
    private readonly ILogger<AiActionExecutionEngine> _logger;

    /// <summary>Creates the engine.</summary>
    /// <param name="db">Shared scoped database context.</param>
    /// <param name="scopeEvaluator">Scope evaluator for the execution-time re-check.</param>
    /// <param name="auditWriter">Hash-chained audit writer (shared unit of work).</param>
    /// <param name="registry">Compiled tool registry (descriptor lookup for scope re-checks).</param>
    /// <param name="notifications">Owner notification writer.</param>
    /// <param name="services">Scope service provider for handler resolution.</param>
    /// <param name="options">MCP options (executor timeouts/batch).</param>
    /// <param name="settings">Database-stored MCP switches (write-stop lid).</param>
    /// <param name="logger">Logger.</param>
    /// <param name="statusPublisher">Optional SSE status publisher (F-1101) — after-commit,
    /// best effort; <see langword="null"/> disables the push channel (polling fallback).</param>
    public AiActionExecutionEngine(
        AppDbContext db,
        IMcpScopeEvaluator scopeEvaluator,
        IMcpAuditWriter auditWriter,
        IMcpToolRegistry registry,
        INotificationWriter notifications,
        IServiceProvider services,
        IOptionsMonitor<McpOptions> options,
        IMcpSettingsService settings,
        ILogger<AiActionExecutionEngine> logger,
        IMcpRequestStatusPublisher? statusPublisher = null)
    {
        _db = db;
        _scopeEvaluator = scopeEvaluator;
        _auditWriter = auditWriter;
        _registry = registry;
        _notifications = notifications;
        _services = services;
        _options = options;
        _settings = settings;
        _logger = logger;
        _statusPublisher = statusPublisher;
    }

    /// <inheritdoc />
    public async Task<int> ExecuteDueRequestsAsync(int batchSize, CancellationToken ct)
    {
        var approval = _options.CurrentValue.Approval;
        var now = DateTime.UtcNow;
        var staleBefore = now.AddMinutes(-approval.ClaimStaleMinutes);
        var take = Math.Clamp(batchSize, 1, approval.MaxExecutionBatchSize);

        // Candidates: approved, not expired, not claimed (or stale claim from a crashed run).
        var candidateIds = await _db.AiActionRequests.AsNoTracking()
            .Where(r => r.Status == AiActionRequestStatus.Approved
                && r.ExpiresUtc > now
                && (r.ClaimToken == null || r.ClaimedUtc <= staleBefore))
            .OrderBy(r => r.RequestedUtc)
            .Select(r => r.Id)
            .Take(take)
            .ToListAsync(ct);

        var completed = 0;
        foreach (var id in candidateIds)
        {
            ct.ThrowIfCancellationRequested();
            var token = await TryClaimAsync(id, now, staleBefore, ct);
            if (token is null)
            {
                continue; // lost the race or state changed — not ours.
            }
            if (await ExecuteClaimedAsync(id, token.Value, now, staleBefore, ct))
            {
                completed++;
            }
        }
        return completed;
    }

    /// <inheritdoc />
    public async Task<int> ExpireOverdueRequestsAsync(DateTime utcNow, int batchSize, CancellationToken ct)
    {
        var ids = await _db.AiActionRequests.AsNoTracking()
            .Where(r => (r.Status == AiActionRequestStatus.Pending || r.Status == AiActionRequestStatus.Approved)
                && r.ExpiresUtc <= utcNow
                && r.ClaimToken == null)
            .OrderBy(r => r.ExpiresUtc)
            .Select(r => r.Id)
            .Take(Math.Clamp(batchSize, 1, 200))
            .ToListAsync(ct);

        var expired = 0;
        foreach (var id in ids)
        {
            // Conditional transition — atomic against a concurrent claim/decision.
            var rows = await _db.AiActionRequests
                .Where(r => r.Id == id
                    && (r.Status == AiActionRequestStatus.Pending || r.Status == AiActionRequestStatus.Approved)
                    && r.ClaimToken == null)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(r => r.Status, AiActionRequestStatus.Expired)
                    .SetProperty(r => r.DecidedUtc, utcNow)
                    .SetProperty(r => r.DecidedByUserId, (Guid?)null)
                    .SetProperty(r => r.ModifiedUtc, utcNow), ct);
            if (rows == 0)
            {
                continue;
            }
            var snapshot = await _db.AiActionRequests.AsNoTracking()
                .Where(r => r.Id == id)
                .Select(r => new { r.OwnerUserId, r.McpAccessId, r.McpAccessName, r.ToolName, r.ActionKey, r.CatalogVersion, r.CorrelationId })
                .FirstAsync(ct);
            await _auditWriter.WriteAsync(new McpAuditWriteRequest(
                McpAuditCategory.ApprovalEvent, "ApprovalExpired", "Success",
                OwnerUserId: snapshot.OwnerUserId, McpAccessId: snapshot.McpAccessId,
                McpAccessName: snapshot.McpAccessName, ToolName: snapshot.ToolName,
                CatalogVersion: snapshot.CatalogVersion, Context: "System/Job",
                CorrelationId: snapshot.CorrelationId, RelatedRequestId: id,
                PayloadJson: JsonSerializer.Serialize(new { requestId = id })), ct);
            await TryPublishEventAsync(new McpRequestStatusEvent(
                snapshot.McpAccessId, snapshot.OwnerUserId, id,
                AiActionRequestStatus.Expired.ToString(), snapshot.ToolName,
                snapshot.ActionKey, snapshot.CorrelationId, FailureReason: null, utcNow), ct);
            expired++;
        }
        return expired;
    }

    /// <summary>
    /// Atomically claims one approved request. Returns the claim token when the conditional
    /// update won, otherwise <see langword="null"/>.
    /// </summary>
    private async Task<Guid?> TryClaimAsync(Guid requestId, DateTime now, DateTime staleBefore, CancellationToken ct)
    {
        var token = Guid.NewGuid();
        var rows = await _db.AiActionRequests
            .Where(r => r.Id == requestId
                && r.Status == AiActionRequestStatus.Approved
                && (r.ClaimToken == null || r.ClaimedUtc <= staleBefore))
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.ClaimToken, token)
                .SetProperty(r => r.ClaimedUtc, now)
                .SetProperty(r => r.AttemptCount, r => r.AttemptCount + 1)
                .SetProperty(r => r.ModifiedUtc, now), ct);
        return rows == 1 ? token : null;
    }

    /// <summary>
    /// Executes one claimed request: re-checks all guards at execution time, invokes the
    /// original tool handler in approved-execution mode and writes the terminal state plus
    /// its audit record. Returns <see langword="true"/> when a terminal state was reached.
    /// </summary>
    private async Task<bool> ExecuteClaimedAsync(Guid requestId, Guid claimToken, DateTime now, DateTime staleBefore, CancellationToken ct)
    {
        var request = await _db.AiActionRequests.FirstOrDefaultAsync(r => r.Id == requestId, ct);
        if (request is null)
        {
            return false;
        }
        // The claim ran as a conditional bulk update below the change tracker — refresh a
        // possibly already-tracked instance so the token check sees the claimed state.
        await _db.Entry(request).ReloadAsync(ct);
        if (request.Status != AiActionRequestStatus.Approved
            || request.ClaimToken != claimToken)
        {
            return false;
        }

        // Queue progress hint (F-1101): the claim won — subscribers see "Executing" while
        // the persisted status is still Approved (the pseudo status is documented on
        // McpRequestStatusEvent.Executing).
        await TryPublishEventAsync(McpRequestStatusEvent.FromRequest(request, McpRequestStatusEvent.Executing), ct);

        // 1) Deadline check — an approved request that passed its expiry is expired, not executed.
        if (request.ExpiresUtc <= now)
        {
            request.Expire(now);
            await StageRequestAuditAsync(request, "ApprovalExpired", "Success", null, ct);
            await _db.SaveChangesAsync(ct);
            await TryPublishEventAsync(McpRequestStatusEvent.FromRequest(request), ct);
            return true;
        }

        // 2) Access re-check — a gone/deactivated/revoked/expired access withdraws the request
        //    (system transition before execution start, approval-konzept.md §6.3).
        var access = await _db.McpAccesses.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == request.McpAccessId, ct);
        if (access is null || !access.IsCurrentlyUsable(now))
        {
            request.WithdrawBySystem("Zugang entzogen oder nicht mehr verwendbar");
            await StageRequestAuditAsync(request, "ApprovalWithdrawn", "Success", null, ct);
            await _db.SaveChangesAsync(ct);
            await TryPublishEventAsync(McpRequestStatusEvent.FromRequest(request), ct);
            return true;
        }

        var descriptor = _registry.Find(request.ToolName);
        if (descriptor is null)
        {
            return await FailAsync(request, "Err_Mcp_UnknownTool", null, ct);
        }

        // 3) Write-stop lid — approved requests stay in the queue while it is active
        //    (they are released, not executed; the expiry deadline still applies).
        if (!await _settings.IsWriteAccessEnabledAsync(ct)
            && descriptor.SecurityClass is McpToolSecurityClass.Schreibvorbereitend or McpToolSecurityClass.Genehmigungspflichtig)
        {
            request.ReleaseClaim();
            // AttemptCount deliberately keeps the deferred attempt for diagnostics.
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Approval request {RequestId} deferred — MCP write access is stopped", request.Id);
            return false;
        }

        // 4) Scope re-check (Prüfstufen 1–3 at execution time; record-sharp checks run again
        //    inside the handler). A lost right fails the request classified.
        if (descriptor.MetaScope is null)
        {
            foreach (var scope in descriptor.RequiredScopes)
            {
                if (!await _scopeEvaluator.IsAllowedAsync(access, scope.DataType, scope.Right, ct))
                {
                    return await FailAsync(request, "Err_RightsRevoked", null, ct);
                }
            }
        }

        // 5) Invoke the original tool handler in approved-execution mode — the guard layer
        //    (argument validation, record filters, service guards) runs again on the frozen
        //    parameter snapshot; approval never bypasses it.
        var handler = _services.GetServices<IMcpToolHandler>()
            .FirstOrDefault(h => h.ToolName == request.ToolName);
        if (handler is null)
        {
            return await FailAsync(request, "Err_Mcp_ToolNotImplemented", null, ct);
        }

        JsonDocument parameters;
        try
        {
            parameters = JsonDocument.Parse(request.ParameterSnapshot);
        }
        catch (JsonException)
        {
            return await FailAsync(request, "Err_Internal", new { type = nameof(JsonException) }, ct);
        }

        await StageRequestAuditAsync(request, "ApprovalExecutionStarted", "Success", null, ct);
        await _db.SaveChangesAsync(ct);

        var timeout = TimeSpan.FromSeconds(Math.Max(1, _options.CurrentValue.Approval.ExecutionTimeoutSeconds));
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(timeout);
        using (parameters)
        {
            try
            {
                var result = await handler.InvokeAsync(parameters.RootElement,
                    new McpToolCallContext(new McpExecutorAccessContext(access), _services,
                        request.CorrelationId, SourceIp: null, Mode: McpToolExecutionMode.ApprovedExecution),
                    timeoutCts.Token);
                return await CompleteAsync(request, JsonSerializer.Serialize(result), ct);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                return await FailAsync(request, "Err_ExecutionTimeout",
                    new { type = "ExecutionTimeout", timeoutSeconds = (int)timeout.TotalSeconds }, ct);
            }
            catch (McpToolException ex)
            {
                // Defined error: the declared code and its (interna-free) client message
                // may be surfaced — the tool contract guarantees safe texts.
                return await FailAsync(request, ClassifyFailure(ex.Code),
                    new { code = ex.Code, message = ex.Message }, ct);
            }
            catch (FinanceManager.Application.Exceptions.DomainValidationException ex)
            {
                // Defined error: code + defined validation message (may name the affected
                // arguments — the message is authored for client visibility).
                return await FailAsync(request, ClassifyFailure(ex.Code),
                    new { code = ex.Code, message = ex.Message }, ct);
            }
            catch (Exception ex)
            {
                // Foreign exception: only the bare type name is a safe detail — never the
                // message or a stack trace (both can carry internals).
                _logger.LogError(ex, "Approval request {RequestId} execution failed with an unhandled error", request.Id);
                return await FailAsync(request, "Err_Internal", new { type = ex.GetType().Name }, ct);
            }
        }
    }

    /// <summary>
    /// Marks a claimed request executed, stages the <c>ApprovalExecuted</c> record and
    /// notifies the owner — request state + audit commit in one save.
    /// </summary>
    private async Task<bool> CompleteAsync(AiActionRequest request, string resultJson, CancellationToken ct)
    {
        if (resultJson.Length > MaxResultChars)
        {
            resultJson = resultJson[..MaxResultChars];
        }
        request.MarkExecuted(resultJson);
        await StageRequestAuditAsync(request, "ApprovalExecuted", "Success",
            JsonSerializer.Serialize(new { requestId = request.Id }), ct);
        await _db.SaveChangesAsync(ct);
        await TryPublishEventAsync(McpRequestStatusEvent.FromRequest(request), ct);
        await _notifications.CreateForUserAsync(
            request.OwnerUserId, "MCP-Aktion ausgeführt",
            $"Die genehmigte Aktion '{request.ToolName}' wurde ausgeführt.",
            NotificationType.EventDriven, NotificationTarget.Dashboard, DateTime.UtcNow,
            $"mcp-approval-result:{request.Id}", ct);
        _logger.LogInformation("Approval request {RequestId} executed", request.Id);
        return true;
    }

    /// <summary>
    /// Marks a claimed request failed with a classified reason, stages the
    /// <c>ApprovalFailed</c> record and notifies the owner.
    /// </summary>
    /// <param name="request">The claimed request.</param>
    /// <param name="failureReason">Classified <c>Err_*</c> failure reason.</param>
    /// <param name="errorDetail">Optional safe diagnostic detail stored as the request's
    /// result payload (<c>{"error":{…}}</c>) so <c>fm_task_status</c> can surface it as
    /// <c>untrusted_data.error</c>: the declared <c>code</c>/<c>message</c> pair of defined
    /// exceptions or the bare type name of a foreign exception — never internals.
    /// <see cref="AiActionRequest.ReasonText"/> is client input and deliberately not used.</param>
    /// <param name="ct">Cancellation token.</param>
    private async Task<bool> FailAsync(AiActionRequest request, string failureReason,
        object? errorDetail, CancellationToken ct)
    {
        string? resultJson = null;
        if (errorDetail is not null)
        {
            resultJson = JsonSerializer.Serialize(new { error = errorDetail });
            if (resultJson.Length > MaxResultChars)
            {
                resultJson = resultJson[..MaxResultChars];
            }
        }
        request.MarkFailed(failureReason, resultJson);
        await StageRequestAuditAsync(request, "ApprovalFailed", "Failed",
            JsonSerializer.Serialize(new { requestId = request.Id, failureReason, error = errorDetail }), ct);
        await _db.SaveChangesAsync(ct);
        await TryPublishEventAsync(McpRequestStatusEvent.FromRequest(request), ct);
        await _notifications.CreateForUserAsync(
            request.OwnerUserId, "MCP-Aktion fehlgeschlagen",
            $"Die genehmigte Aktion '{request.ToolName}' ist fehlgeschlagen ({failureReason}).",
            NotificationType.EventDriven, NotificationTarget.Dashboard, DateTime.UtcNow,
            $"mcp-approval-result:{request.Id}", ct);
        _logger.LogWarning("Approval request {RequestId} failed: {Reason}", request.Id, failureReason);
        return true;
    }

    /// <summary>
    /// Best-effort push of one request status event to the SSE subscribers of the access
    /// (F-1101): invoked strictly after the state change so a failing channel can never
    /// roll back the transition; a failing publisher is logged and swallowed — the
    /// request row plus <c>fm_task_status</c> polling stay the source of truth.
    /// </summary>
    private async Task TryPublishEventAsync(McpRequestStatusEvent statusEvent, CancellationToken ct)
    {
        if (_statusPublisher is null)
        {
            return;
        }
        try
        {
            await _statusPublisher.PublishAsync(statusEvent, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish the MCP request status of {RequestId}", statusEvent.RequestId);
        }
    }

    /// <summary>Stages an approval-event record linked to the request.</summary>
    private Task<McpAuditEntry?> StageRequestAuditAsync(AiActionRequest request, string eventType,
        string resultStatus, string? payloadJson, CancellationToken ct)
        => _auditWriter.StageAsync(new McpAuditWriteRequest(
            McpAuditCategory.ApprovalEvent, eventType, resultStatus,
            OwnerUserId: request.OwnerUserId, McpAccessId: request.McpAccessId,
            McpAccessName: request.McpAccessName, ToolName: request.ToolName,
            CatalogVersion: request.CatalogVersion, Context: "System/Job",
            CorrelationId: request.CorrelationId, RelatedRequestId: request.Id,
            PayloadJson: payloadJson), ct);

    /// <summary>
    /// Keeps stable <c>Err_*</c> codes of the called layers; anything else is classified as
    /// <c>Err_Internal</c> (klassifizierte FailureReason-Liste, approval-konzept.md §6.2).
    /// </summary>
    private static string ClassifyFailure(string? code)
        => !string.IsNullOrWhiteSpace(code) && code.StartsWith("Err_", StringComparison.Ordinal)
            ? code
            : "Err_Internal";
}
