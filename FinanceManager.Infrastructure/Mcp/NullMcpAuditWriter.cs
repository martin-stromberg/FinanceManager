using FinanceManager.Application.Mcp;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Infrastructure.Mcp;

/// <summary>
/// Documented no-op implementation of <see cref="IMcpAuditWriter"/>.
/// </summary>
/// <remarks>
/// The persistent, hash-chained <c>McpAuditWriter</c> is scheduled with F-0301/F-0302
/// (step 3 of the MCP roadmap). Until then this registered implementation accepts and
/// logs audit write requests at diagnostic level so every pipeline call site is already
/// live without producing a false sense of revision-safe persistence. It intentionally
/// does <b>not</b> write to <c>McpAuditEntries</c>.
/// </remarks>
public sealed class NullMcpAuditWriter : IMcpAuditWriter
{
    private readonly ILogger<NullMcpAuditWriter> _logger;

    /// <summary>
    /// Creates a new instance of <see cref="NullMcpAuditWriter"/>.
    /// </summary>
    /// <param name="logger">Logger for diagnostic traces of dropped audit events.</param>
    public NullMcpAuditWriter(ILogger<NullMcpAuditWriter> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task WriteAsync(McpAuditWriteRequest request, CancellationToken ct)
    {
        // Documented no-op (F-0301/F-0302): diagnostic trace only, no persistence.
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug(
                "McpAudit (no-op writer) Category={Category} EventType={EventType} Result={Result} Access={AccessId} Tool={Tool} Correlation={CorrelationId}",
                request.Category,
                request.EventType,
                request.ResultStatus,
                request.McpAccessId,
                request.ToolName,
                request.CorrelationId);
        }
        return Task.CompletedTask;
    }
}
