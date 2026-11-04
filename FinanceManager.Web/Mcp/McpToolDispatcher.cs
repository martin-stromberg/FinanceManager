using FinanceManager.Application.Mcp;
using FinanceManager.Domain.Mcp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Text.Json;

namespace FinanceManager.Web.Mcp;

/// <summary>
/// Dispatcher of <c>tools/call</c> requests (F-0106): routes by tool name, applies the
/// write-stop lid, the per-class rate limit, the scope evaluation (all declared tuples, AND)
/// and then invokes the adapter. Order per architecture pipeline: known → WriteStop →
/// class rate limit → scope → invoke.
/// </summary>
public sealed class McpToolDispatcher
{
    private readonly IMcpToolRegistry _registry;
    private readonly IMcpScopeEvaluator _scopeEvaluator;
    private readonly IMcpAuditWriter _auditWriter;
    private readonly McpToolRateLimiter _rateLimiter;
    private readonly McpAnomalyTracker _anomalyTracker;
    private readonly IOptionsMonitor<McpOptions> _options;
    private readonly ILogger<McpToolDispatcher> _logger;

    /// <summary>Creates the dispatcher.</summary>
    public McpToolDispatcher(
        IMcpToolRegistry registry,
        IMcpScopeEvaluator scopeEvaluator,
        IMcpAuditWriter auditWriter,
        McpToolRateLimiter rateLimiter,
        McpAnomalyTracker anomalyTracker,
        IOptionsMonitor<McpOptions> options,
        ILogger<McpToolDispatcher> logger)
    {
        _registry = registry;
        _scopeEvaluator = scopeEvaluator;
        _auditWriter = auditWriter;
        _rateLimiter = rateLimiter;
        _anomalyTracker = anomalyTracker;
        _options = options;
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

        // 2) Global write-stop lid (Mcp:WriteAccess:Enabled=false).
        if (!_options.CurrentValue.WriteAccess.Enabled
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

        // 5) Invoke adapter.
        var handler = services.GetServices<IMcpToolHandler>().FirstOrDefault(h => h.ToolName == toolName);
        if (handler is null)
        {
            return McpDispatchResult.ProtocolError("Err_Mcp_ToolNotImplemented", $"Tool '{toolName}' has no handler.");
        }

        try
        {
            var result = await handler.InvokeAsync(arguments,
                new McpToolCallContext(access, services, correlationId, sourceIp), ct);
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
