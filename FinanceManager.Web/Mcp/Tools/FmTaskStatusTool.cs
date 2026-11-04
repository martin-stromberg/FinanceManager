using FinanceManager.Application;
using FinanceManager.Application.Mcp;
using FinanceManager.Domain.Mcp;
using FinanceManager.Shared.Dtos.Mcp;
using System.Text.Json;

namespace FinanceManager.Web.Mcp.Tools;

/// <summary>
/// Descriptor of the meta tool <c>fm_task_status</c> (T33, scope-und-toolkatalog.md §3.6/§4):
/// reports the status of the owner's own asynchronous executions — background tasks plus the
/// <see cref="AiActionRequest"/> states of the calling access. Carries the closed meta-scope
/// <c>Ausführungsstatus</c>.
/// </summary>
public sealed class FmTaskStatusDescriptor : McpToolDescriptorBase
{
    /// <summary>Tool name.</summary>
    public const string ToolName = "fm_task_status";

    /// <summary>Creates the descriptor.</summary>
    public FmTaskStatusDescriptor() : base(
        "{\"type\":\"object\",\"properties\":{\"taskId\":{\"type\":\"string\",\"format\":\"uuid\",\"description\":\"Optional background task id; without it, all own tasks are listed.\"},\"requestId\":{\"type\":\"string\",\"format\":\"uuid\",\"description\":\"Optional AiActionRequest id — returns the full detail of that request (snapshot, decision, result) instead of the list.\"},\"status\":{\"type\":\"string\",\"enum\":[\"Pending\",\"Approved\",\"Rejected\",\"Executed\",\"Failed\",\"Expired\"],\"description\":\"Optional request status filter for the request list.\"},\"take\":{\"type\":\"integer\",\"description\":\"Maximum number of requests returned (default 100, clamped 1-200).\"}},\"additionalProperties\":false}")
    { }

    /// <inheritdoc />
    public override string Name => ToolName;

    /// <inheritdoc />
    public override string Description
        => "Returns the status of own asynchronous executions: background tasks of the access owner plus the AiActionRequest states (Pending/Approved/Rejected/Executed/Failed/Expired) of the calling access. Meta-scope: Ausfuehrungsstatus; only own tasks and requests are visible.";

    /// <inheritdoc />
    public override McpToolSecurityClass SecurityClass => McpToolSecurityClass.Lesend;

    /// <inheritdoc />
    public override IReadOnlyList<McpToolScope> RequiredScopes => Array.Empty<McpToolScope>();

    /// <inheritdoc />
    public override string? MetaScope => "Ausführungsstatus";
}

/// <summary>
/// Handler of <c>fm_task_status</c> (T33 full form, F-0905): lists only the owner's own
/// background tasks from <see cref="IBackgroundTaskManager"/> and the
/// <see cref="AiActionRequest"/>s of the calling access via
/// <see cref="IMcpApprovalService"/> — never foreign tasks or requests (ownership check;
/// a request id of another access behaves like a missing one).
/// </summary>
public sealed class FmTaskStatusHandler : IMcpToolHandler
{
    private readonly IBackgroundTaskManager _tasks;
    private readonly IMcpApprovalService _approvals;

    /// <summary>Creates the handler.</summary>
    public FmTaskStatusHandler(IBackgroundTaskManager tasks, IMcpApprovalService approvals)
    {
        _tasks = tasks;
        _approvals = approvals;
    }

    /// <inheritdoc />
    public string ToolName => FmTaskStatusDescriptor.ToolName;

    /// <inheritdoc />
    public async Task<object> InvokeAsync(JsonElement arguments, McpToolCallContext context, CancellationToken ct)
    {
        var ownerUserId = context.Access.OwnerUserId;
        var accessId = context.Access.McpAccessId;
        Guid? taskId = null;
        if (arguments.TryGetProperty("taskId", out var idEl) && idEl.ValueKind == JsonValueKind.String
            && Guid.TryParse(idEl.GetString(), out var parsed))
        {
            taskId = parsed;
        }
        var requestId = McpToolArguments.OptionalGuid(arguments, "requestId");
        var statusFilter = McpToolArguments.OptionalEnum<AiActionRequestStatus>(arguments, "status");
        var take = McpToolArguments.Take(arguments, fallback: 100);

        var tasks = _tasks.GetAll()
            .Where(t => t.UserId == ownerUserId && (taskId is null || t.Id == taskId))
            .Select(t => new
            {
                id = t.Id,
                type = t.Type.ToString(),
                status = t.Status.ToString(),
                processed = t.Processed,
                total = t.Total,
                enqueuedUtc = t.EnqueuedUtc,
                startedUtc = t.StartedUtc,
                finishedUtc = t.FinishedUtc,
                // Task texts and counters carry pipeline output that may embed user data
                // (file names, entry texts) — untrusted content (sicherheitskonzept.md §5.2),
                // separated into the marked wrapper.
                untrusted_data = new
                {
                    message = t.Message,
                    errorDetail = t.ErrorDetail,
                    warnings = t.Warnings,
                    errors = t.Errors
                }
            })
            .ToList();

        var requests = requestId.HasValue
            ? await RequestDetailAsync(ownerUserId, accessId, requestId.Value, ct)
            : await RequestListAsync(ownerUserId, accessId, statusFilter, take, ct);

        return new { tasks, requests };
    }

    /// <summary>
    /// Detail of one request — restricted to requests of the calling access (a foreign or
    /// unknown id yields an empty list, no forbidden/not-found distinction).
    /// </summary>
    private async Task<IReadOnlyList<object>> RequestDetailAsync(
        Guid ownerUserId, Guid accessId, Guid requestId, CancellationToken ct)
    {
        var d = await _approvals.GetAsync(ownerUserId, requestId, ct);
        if (d is null || d.McpAccessId != accessId)
        {
            return Array.Empty<object>();
        }
        return new object[]
        {
            new
            {
                requestId = d.Id,
                mcpAccessId = d.McpAccessId,
                toolName = d.ToolName,
                actionKey = d.ActionKey,
                catalogVersion = d.CatalogVersion,
                status = ((AiActionRequestStatus)d.Status).ToString(),
                requestedUtc = d.RequestedUtc,
                expiresUtc = d.ExpiresUtc,
                decidedUtc = d.DecidedUtc,
                decidedByUserId = d.DecidedByUserId,
                executedUtc = d.ExecutedUtc,
                failureReason = d.FailureReason,
                correlationId = d.CorrelationId,
                idempotencyKey = d.IdempotencyKey,
                attemptCount = d.AttemptCount,
                // Parameter snapshot, client reason text, decision note and the execution
                // result reference carry user/client content — untrusted_data wrapper
                // (sicherheitskonzept.md §5.2).
                untrusted_data = new
                {
                    mcpAccessName = d.McpAccessName,
                    parameterSnapshot = ParseSnapshot(d.ParameterSnapshot),
                    reasonText = d.ReasonText,
                    decisionNote = d.DecisionNote,
                    resultRef = ParseSnapshot(d.ExecutionResult)
                }
            }
        };
    }

    /// <summary>List of the access's own requests (optionally status-filtered).</summary>
    private async Task<IReadOnlyList<object>> RequestListAsync(
        Guid ownerUserId, Guid accessId, AiActionRequestStatus? status, int take, CancellationToken ct)
    {
        var items = await _approvals.ListAsync(ownerUserId, status, accessId, 0, take, ct);
        return items.Select(r => (object)new
        {
            requestId = r.Id,
            mcpAccessId = r.McpAccessId,
            toolName = r.ToolName,
            actionKey = r.ActionKey,
            status = ((AiActionRequestStatus)r.Status).ToString(),
            requestedUtc = r.RequestedUtc,
            expiresUtc = r.ExpiresUtc,
            decidedUtc = r.DecidedUtc,
            executedUtc = r.ExecutedUtc,
            failureReason = r.FailureReason,
            correlationId = r.CorrelationId,
            attemptCount = r.AttemptCount,
            untrusted_data = new
            {
                mcpAccessName = r.McpAccessName,
                resultRef = ParseSnapshot(r.ExecutionResult)
            }
        }).ToList();
    }

    /// <summary>Returns the stored JSON as a structured element (or raw string when not JSON).</summary>
    private static object? ParseSnapshot(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }
        try
        {
            return JsonDocument.Parse(json).RootElement.Clone();
        }
        catch (JsonException)
        {
            return json;
        }
    }
}
