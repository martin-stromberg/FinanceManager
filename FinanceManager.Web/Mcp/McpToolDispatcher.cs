using FinanceManager.Application.Exceptions;
using FinanceManager.Application.Mcp;
using FinanceManager.Domain.Mcp;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text.Json;

namespace FinanceManager.Web.Mcp;

/// <summary>
/// Dispatcher of <c>tools/call</c> requests (F-0106/F-0902): routes by tool name, applies the
/// write-stop lid, the per-class rate limit, the scope evaluation (all declared tuples, AND),
/// the approval gate (pipeline position 8) and then invokes the adapter. Order per
/// architecture pipeline: known → WriteStop → class rate limit → scope → approval gate →
/// invoke.
/// </summary>
/// <remarks>
/// The approval gate only decides; the dispatcher performs the redirect itself:
/// <see cref="McpApprovalDecisionKind.RequiresApproval"/> parks the call as an
/// <see cref="AiActionRequest"/> via <see cref="IMcpApprovalService.EnqueueAsync"/> and
/// answers <c>{ queued: true, requestId, … }</c> instead of a tool result. As the documented
/// fallback the dispatcher also converts a handler-level <c>requiresApproval</c> result
/// (final/destructive sub-operations of the pflege tools T22–T29, which the class-level gate
/// lets through when the access approval mode allows direct execution) into the same queue
/// entry — an approval path never produces a synchronous effect. Before the request row is
/// persisted the dispatcher offers the handler an optional enqueue-time validation hook
/// (<see cref="IMcpEnqueueValidator"/>): a classified rejection (<see cref="McpToolException"/>)
/// is answered directly instead of a queued request id (F-1003).
/// </remarks>
public sealed class McpToolDispatcher
{
    private readonly IMcpToolRegistry _registry;
    private readonly IMcpScopeEvaluator _scopeEvaluator;
    private readonly IMcpAuditWriter _auditWriter;
    private readonly IMcpApprovalGate _approvalGate;
    private readonly IMcpApprovalService _approvalService;
    private readonly McpToolRateLimiter _rateLimiter;
    private readonly McpAnomalyTracker _anomalyTracker;
    private readonly IMcpSettingsService _settings;
    private readonly ILogger<McpToolDispatcher> _logger;

    /// <summary>Creates the dispatcher.</summary>
    public McpToolDispatcher(
        IMcpToolRegistry registry,
        IMcpScopeEvaluator scopeEvaluator,
        IMcpAuditWriter auditWriter,
        IMcpApprovalGate approvalGate,
        IMcpApprovalService approvalService,
        McpToolRateLimiter rateLimiter,
        McpAnomalyTracker anomalyTracker,
        IMcpSettingsService settings,
        ILogger<McpToolDispatcher> logger)
    {
        _registry = registry;
        _scopeEvaluator = scopeEvaluator;
        _auditWriter = auditWriter;
        _approvalGate = approvalGate;
        _approvalService = approvalService;
        _rateLimiter = rateLimiter;
        _anomalyTracker = anomalyTracker;
        _settings = settings;
        _logger = logger;
    }

    /// <summary>
    /// Executes one <c>tools/call</c> invocation.
    /// </summary>
    /// <param name="access">The authenticated access context.</param>
    /// <param name="toolName">Requested tool name.</param>
    /// <param name="arguments">Tool arguments (JSON).</param>
    /// <param name="services">Request service provider.</param>
    /// <param name="sourceIp">Client IP.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The dispatch result (structured result or defined error).</returns>
    public async Task<McpDispatchResult> DispatchAsync(
        IMcpAccessContext access,
        string toolName,
        JsonElement arguments,
        IServiceProvider services,
        string? sourceIp,
        CancellationToken ct)
    {
        var correlationId = Guid.NewGuid();
        var sw = Stopwatch.StartNew();
        var entity = access.Access;

        // 1) Tool known?
        var descriptor = _registry.Find(toolName);
        if (descriptor is null)
        {
            return McpDispatchResult.ProtocolError("Err_Mcp_UnknownTool", $"Unknown tool '{toolName}'.");
        }

        // 2) Global write-stop lid (database-stored McpSettings.WriteAccessEnabled=false).
        if (!await _settings.IsWriteAccessEnabledAsync(ct)
            && IsWriteClass(descriptor.SecurityClass))
        {
            await AuditAsync(entity, toolName, "Denied", correlationId, sourceIp, arguments, sw, ct);
            return McpDispatchResult.DefinedError("Err_Mcp_WriteAccessDisabled", "Write access via MCP is currently disabled.");
        }

        // 3) Per-class rate limit (partition = access).
        if (entity is not null && !_rateLimiter.TryAcquire(entity.Id, descriptor))
        {
            await AuditAsync(entity, toolName, "RateLimited", correlationId, sourceIp, arguments, sw, ct);
            return McpDispatchResult.DefinedError("Err_Mcp_RateLimited", "Rate limit for this tool class exceeded.");
        }

        // 4) Scope check (meta tools only need ownership — guaranteed by authentication).
        if (entity is not null && descriptor.MetaScope is null)
        {
            foreach (var scope in descriptor.RequiredScopes)
            {
                if (!await _scopeEvaluator.IsAllowedAsync(entity, scope.DataType, scope.Right, ct))
                {
                    await AuditAsync(entity, toolName, "Denied", correlationId, sourceIp, arguments, sw, ct);
                    await _anomalyTracker.RecordScopeDenialAsync(entity.Id, entity.OwnerUserId, entity.Name, ct);
                    return McpDispatchResult.DefinedError("Err_Mcp_ScopeDenied",
                        $"The access does not grant the required scope ({scope.DataType}, {scope.Right}).");
                }
            }
        }

        // 5) Approval gate (pipeline position 8, F-0902): security class × approval mode
        //    decide whether the call runs now or parks as an AiActionRequest; the gate
        //    additionally caps the per-access pending limit (approval-spam protection).
        if (entity is not null)
        {
            var decision = await _approvalGate.EvaluateAsync(entity, descriptor, ct);
            if (decision.Kind == McpApprovalDecisionKind.Denied)
            {
                await AuditAsync(entity, toolName, "Denied", correlationId, sourceIp, arguments, sw, ct);
                return McpDispatchResult.DefinedError(
                    decision.ErrorCode ?? "Err_Mcp_ScopeDenied",
                    decision.ErrorMessage ?? "The approval gate denied this call.");
            }
            if (decision.Kind == McpApprovalDecisionKind.RequiresApproval)
            {
                return await EnqueueAsync(access, entity, descriptor, toolName, arguments, correlationId, sourceIp, sw, services, ct);
            }
        }

        // 6) Invoke adapter.
        var handler = services.GetServices<IMcpToolHandler>().FirstOrDefault(h => h.ToolName == toolName);
        if (handler is null)
        {
            return McpDispatchResult.ProtocolError("Err_Mcp_ToolNotImplemented", $"Tool '{toolName}' has no handler.");
        }

        try
        {
            var result = await handler.InvokeAsync(arguments,
                new McpToolCallContext(access, services, correlationId, sourceIp), ct);

            // 7) Handler-level approval fallback: final/destructive sub-operations of the
            //    write-preparing tools answer requiresApproval even when the class-level gate
            //    allowed direct execution (e.g. PreApproved access mode — those requests start
            //    directly approved, approval-konzept.md §3.4). The dispatcher turns that answer
            //    into the same queue entry; no approval path yields a synchronous effect.
            if (entity is not null && IsApprovalRequiredResult(result))
            {
                return await EnqueueAsync(access, entity, descriptor, toolName, arguments, correlationId, sourceIp, sw, services, ct);
            }

            var audited = await AuditAsync(entity, toolName, "Success", correlationId, sourceIp, arguments, sw, ct);
            // A write-class call without its audit record must fail (F-0301). Write tools
            // additionally couple their DataChange/ApprovalEvent records inside the effect
            // transaction via IMcpAuditWriter.StageAsync — this guard covers the ToolCall
            // record itself.
            if (!audited && IsWriteClass(descriptor.SecurityClass))
            {
                return McpDispatchResult.DefinedError("Err_Mcp_AuditFailed",
                    "The audit record of this write call could not be persisted.");
            }
            return McpDispatchResult.Success(result);
        }
        catch (McpToolException ex)
        {
            await AuditAsync(entity, toolName, "Failed", correlationId, sourceIp, arguments, sw, ct);
            return McpDispatchResult.ToolError(ex.Code, ex.Message);
        }
    }

    /// <summary>
    /// Enqueues the call as an <see cref="AiActionRequest"/> (gate redirect and handler-level
    /// fallback share this path) and builds the synchronous queued answer
    /// <c>{ queued: true, requestId, status, expiresUtc, … }</c> of approval-konzept.md §4 —
    /// the client polls the outcome via <c>fm_task_status</c>. The frozen parameter snapshot
    /// is the raw <c>arguments</c> object: the queue executor re-invokes the same tool handler
    /// on it with all guards re-evaluated. Deduplication (client <c>idempotencyKey</c> or the
    /// content hash the service derives) makes a retry return the same request id.
    /// </summary>
    private async Task<McpDispatchResult> EnqueueAsync(
        IMcpAccessContext access, McpAccess entity, IMcpToolDescriptor descriptor, string toolName,
        JsonElement arguments, Guid correlationId, string? sourceIp, Stopwatch sw,
        IServiceProvider services, CancellationToken ct)
    {
        // Enqueue-time pre-validation (F-1003): a handler implementing IMcpEnqueueValidator
        // (e.g. fm_posting_reverse with its CanReverseAsync check) rejects a futile request
        // classified before the queue entry is persisted — the request must not park as
        // Pending only to fail after approval at execution. GetService (not GetServices) so
        // providers that cannot resolve the enumerable degrade to "no validator".
        var enqueueHandler = services.GetService<IEnumerable<IMcpToolHandler>>()
            ?.FirstOrDefault(h => h.ToolName == toolName);
        if (enqueueHandler is IMcpEnqueueValidator validator)
        {
            try
            {
                await validator.ValidateRequestAsync(arguments,
                    new McpToolCallContext(access, services, correlationId, sourceIp), ct);
            }
            catch (McpToolException ex)
            {
                await AuditAsync(entity, toolName, "Failed", correlationId, sourceIp, arguments, sw, ct);
                return McpDispatchResult.ToolError(ex.Code, ex.Message);
            }
        }

        var snapshot = arguments.ValueKind == JsonValueKind.Object ? arguments.GetRawText() : "{}";
        // The sub-operation discriminator: the pflege tools carry an "operation" argument
        // (merge/delete/archive); other tools get a stable per-tool key.
        var actionKey = LenientString(arguments, "operation") ?? descriptor.Name;
        var reasonText = LenientString(arguments, "reasonText") ?? LenientString(arguments, "reason");
        var clientKey = LenientString(arguments, "idempotencyKey");

        AiActionRequest request;
        try
        {
            request = await _approvalService.EnqueueAsync(
                entity, toolName, actionKey, _registry.CatalogVersion,
                snapshot, reasonText, correlationId, clientKey, ct);
        }
        catch (DomainValidationException ex)
        {
            // The pending limit is re-applied at write time — it can lose the race against
            // the gate's earlier read (defense in depth, TOCTOU).
            await AuditAsync(entity, toolName, "Denied", correlationId, sourceIp, arguments, sw, ct);
            return McpDispatchResult.DefinedError(ex.Code ?? "Err_Mcp_PendingLimit", ex.Message);
        }

        await AuditAsync(entity, toolName, "RedirectedToApproval", correlationId, sourceIp, arguments, sw, ct);
        return McpDispatchResult.Success(new
        {
            queued = true,
            requestId = request.Id,
            toolName = request.ToolName,
            actionKey = request.ActionKey,
            status = request.Status.ToString(),
            expiresUtc = request.ExpiresUtc,
            correlationId = request.CorrelationId
        });
    }

    /// <summary>
    /// Detects the structured <c>requiresApproval</c> result of the handler-level guard
    /// (<see cref="McpMasterDataGuards.ApprovalRequiredAsync"/>): the object carries a
    /// <c>requiresApproval: true</c> flag. Serialization failures are irrelevant — an
    /// unclassifiable result is not an approval marker.
    /// </summary>
    private static bool IsApprovalRequiredResult(object result)
    {
        try
        {
            var el = JsonSerializer.SerializeToElement(result);
            return el.ValueKind == JsonValueKind.Object
                && el.TryGetProperty("requiresApproval", out var flag)
                && flag.ValueKind == JsonValueKind.True;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>Reads a string argument defensively — never throws (queue extraction is lenient).</summary>
    private static string? LenientString(JsonElement args, string name)
        => args.ValueKind == JsonValueKind.Object
            && args.TryGetProperty(name, out var el)
            && el.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(el.GetString())
                ? el.GetString()
                : null;

    private static bool IsWriteClass(McpToolSecurityClass securityClass)
        => securityClass is McpToolSecurityClass.Schreibvorbereitend or McpToolSecurityClass.Genehmigungspflichtig;

    /// <summary>
    /// Writes the <see cref="McpAuditCategory.ToolCall"/> record of one invocation. The call
    /// parameters are stored in graduated detail (the writer truncates/redacts); result
    /// payloads of read calls are never recorded (audit-konzept.md §4.2).
    /// </summary>
    /// <returns><see langword="true"/> when the record was persisted; <see langword="false"/>
    /// when the audit write failed (logged — the read path must not be blocked).</returns>
    private async Task<bool> AuditAsync(McpAccess? access, string toolName, string result, Guid correlationId,
        string? sourceIp, JsonElement arguments, Stopwatch sw, CancellationToken ct)
    {
        try
        {
            await _auditWriter.WriteAsync(new McpAuditWriteRequest(
                McpAuditCategory.ToolCall, "ToolCall", result,
                OwnerUserId: access?.OwnerUserId, McpAccessId: access?.Id, McpAccessName: access?.Name,
                ToolName: toolName, CatalogVersion: _registry.CatalogVersion,
                Context: "McpEndpoint", CorrelationId: correlationId, SourceIp: sourceIp,
                DurationMs: (int)sw.ElapsedMilliseconds,
                PayloadJson: arguments.ValueKind is JsonValueKind.Object or JsonValueKind.Array
                    ? arguments.GetRawText()
                    : null), ct);
            return true;
        }
        catch (Exception ex)
        {
            // Read-path audit failures never block the call (F-0301); the missing record
            // becomes visible via the chain gap check.
            _logger.LogWarning(ex, "MCP audit write failed for tool {Tool} (result {Result}, correlation {CorrelationId})",
                toolName, result, correlationId);
            return false;
        }
    }
}

/// <summary>
/// Result of a dispatched tool call.
/// </summary>
public sealed class McpDispatchResult
{
    private McpDispatchResult() { }

    /// <summary>Kind of the result.</summary>
    public McpDispatchResultKind Kind { get; private init; }

    /// <summary>Structured result payload (when <see cref="Kind"/> is Success).</summary>
    public object? Result { get; private init; }

    /// <summary>Stable error code (when not success).</summary>
    public string? ErrorCode { get; private init; }

    /// <summary>Error message (when not success).</summary>
    public string? ErrorMessage { get; private init; }

    /// <summary>Successful structured result.</summary>
    public static McpDispatchResult Success(object result)
        => new() { Kind = McpDispatchResultKind.Success, Result = result };

    /// <summary>Defined domain error (scope denial, write-stop, rate limit) — JSON-RPC error.</summary>
    public static McpDispatchResult DefinedError(string code, string message)
        => new() { Kind = McpDispatchResultKind.DefinedError, ErrorCode = code, ErrorMessage = message };

    /// <summary>Protocol-level error (unknown tool, missing handler) — JSON-RPC error.</summary>
    public static McpDispatchResult ProtocolError(string code, string message)
        => new() { Kind = McpDispatchResultKind.ProtocolError, ErrorCode = code, ErrorMessage = message };

    /// <summary>Tool-level failure — reported as <c>isError</c> tool result.</summary>
    public static McpDispatchResult ToolError(string code, string message)
        => new() { Kind = McpDispatchResultKind.ToolError, ErrorCode = code, ErrorMessage = message };
}

/// <summary>Result kinds of <see cref="McpDispatchResult"/>.</summary>
public enum McpDispatchResultKind
{
    /// <summary>Successful tool result.</summary>
    Success,
    /// <summary>Defined error returned as JSON-RPC error object.</summary>
    DefinedError,
    /// <summary>Protocol error returned as JSON-RPC error object.</summary>
    ProtocolError,
    /// <summary>Tool failure returned as <c>isError</c> result.</summary>
    ToolError
}
