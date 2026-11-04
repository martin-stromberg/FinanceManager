using FinanceManager.Application.Mcp;
using FinanceManager.Domain.Mcp;

namespace FinanceManager.Web.Mcp;

/// <summary>
/// <see cref="IMcpRequestStatusPublisher"/> of the <c>/mcp</c> transport (F-1101): fans
/// request status events out to the SSE subscriptions of the owning access via
/// <see cref="McpSessionHub"/> and additionally feeds failed executions into the
/// <see cref="McpAnomalyTracker"/> agent-workload alert (F-1103). Fully non-blocking and
/// best effort — the request row plus <c>fm_task_status</c> polling stay the source of
/// truth, so a dropped event is tolerable by design.
/// </summary>
public sealed class McpRequestStatusPublisher : IMcpRequestStatusPublisher
{
    private readonly McpSessionHub _hub;
    private readonly McpAnomalyTracker _anomaly;

    /// <summary>Creates the publisher.</summary>
    /// <param name="hub">Session hub holding the per-access SSE subscriptions.</param>
    /// <param name="anomaly">Anomaly tracker for the execution-failure series alert.</param>
    public McpRequestStatusPublisher(McpSessionHub hub, McpAnomalyTracker anomaly)
    {
        _hub = hub;
        _anomaly = anomaly;
    }

    /// <inheritdoc />
    public async Task PublishAsync(McpRequestStatusEvent statusEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(statusEvent);
        _hub.Publish(statusEvent);

        // A failed execution additionally counts into the agent-workload failure series
        // (F-1103: alerting on a run of failed runs of one access).
        if (string.Equals(statusEvent.Status, AiActionRequestStatus.Failed.ToString(), StringComparison.Ordinal))
        {
            await _anomaly.RecordExecutionFailureAsync(
                statusEvent.McpAccessId, statusEvent.OwnerUserId, statusEvent.ToolName, ct);
        }
    }
}
