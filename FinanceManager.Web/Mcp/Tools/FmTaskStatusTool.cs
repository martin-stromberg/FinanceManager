using FinanceManager.Application;
using FinanceManager.Application.Mcp;
using FinanceManager.Domain.Mcp;
using System.Text.Json;

namespace FinanceManager.Web.Mcp.Tools;

/// <summary>
/// Descriptor of the meta tool <c>fm_task_status</c> (T33, basis without request support):
/// reports the status of the owner's own asynchronous executions. Carries the closed
/// meta-scope <c>Ausführungsstatus</c>.
/// </summary>
public sealed class FmTaskStatusDescriptor : McpToolDescriptorBase
{
    /// <summary>Tool name.</summary>
    public const string ToolName = "fm_task_status";

    /// <summary>Creates the descriptor.</summary>
    public FmTaskStatusDescriptor() : base(
        "{\"type\":\"object\",\"properties\":{\"taskId\":{\"type\":\"string\",\"format\":\"uuid\",\"description\":\"Optional background task id; without it, all own tasks are listed.\"}},\"additionalProperties\":false}") { }

    /// <inheritdoc />
    public override string Name => ToolName;

    /// <inheritdoc />
    public override string Description
        => "Returns the status of own asynchronous executions (background tasks of the access owner). Meta-scope: Ausfuehrungsstatus.";

    /// <inheritdoc />
    public override McpToolSecurityClass SecurityClass => McpToolSecurityClass.Lesend;

    /// <inheritdoc />
    public override IReadOnlyList<McpToolScope> RequiredScopes => Array.Empty<McpToolScope>();

    /// <inheritdoc />
    public override string? MetaScope => "Ausführungsstatus";
}

/// <summary>
/// Handler of <c>fm_task_status</c> (basis): lists only the owner's own background tasks
/// from <see cref="IBackgroundTaskManager"/> — never foreign tasks (ownership check).
/// Approval-request states are added with the AiActionRequest feature (later step).
/// </summary>
public sealed class FmTaskStatusHandler : IMcpToolHandler
{
    private readonly IBackgroundTaskManager _tasks;

    /// <summary>Creates the handler.</summary>
    public FmTaskStatusHandler(IBackgroundTaskManager tasks)
    {
        _tasks = tasks;
    }

    /// <inheritdoc />
    public string ToolName => FmTaskStatusDescriptor.ToolName;

    /// <inheritdoc />
    public Task<object> InvokeAsync(JsonElement arguments, McpToolCallContext context, CancellationToken ct)
    {
        var ownerUserId = context.Access.OwnerUserId;
        Guid? taskId = null;
        if (arguments.TryGetProperty("taskId", out var idEl) && idEl.ValueKind == JsonValueKind.String
            && Guid.TryParse(idEl.GetString(), out var parsed))
        {
            taskId = parsed;
        }

        var tasks = _tasks.GetAll()
            .Where(t => t.UserId == ownerUserId && (taskId is null || t.Id == taskId))
            .Select(t => new
            {
                id = t.Id,
                type = t.Type.ToString(),
                status = t.Status.ToString(),
                processed = t.Processed,
                total = t.Total,
                message = t.Message,
                warnings = t.Warnings,
                errors = t.Errors,
                enqueuedUtc = t.EnqueuedUtc,
                startedUtc = t.StartedUtc,
                finishedUtc = t.FinishedUtc
            })
            .ToList();

        return Task.FromResult<object>(new { tasks });
    }
}
