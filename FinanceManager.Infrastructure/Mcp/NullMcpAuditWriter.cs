using FinanceManager.Application.Mcp;
using FinanceManager.Domain.Mcp;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Infrastructure.Mcp;

/// <summary>
/// Documented no-op implementation of <see cref="IMcpAuditWriter"/>.
/// </summary>
/// <remarks>
/// The production registration is the persistent, hash-chained <see cref="McpAuditWriter"/>
/// (F-0301/F-0302). This implementation is kept for tests and reference: it accepts audit
/// write requests and logs them at diagnostic level without writing to
/// <c>McpAuditEntries</c> — useful in unit tests that must not produce a false sense of
/// revision-safe persistence.
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
        // Documented no-op: diagnostic trace only, no persistence.
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

    /// <inheritdoc />
    public Task<McpAuditEntry?> StageAsync(McpAuditWriteRequest request, CancellationToken ct)
    {
        // Documented no-op: nothing is staged — callers must tolerate a null staged entry.
        return Task.FromResult<McpAuditEntry?>(null);
    }

    /// <inheritdoc />
    public Task<McpAuditChainVerificationResult> VerifyAsync(Guid? ownerUserId, CancellationToken ct)
    {
        // Documented no-op: no persisted chain exists to verify.
        return Task.FromResult(new McpAuditChainVerificationResult(
            ownerUserId, EntriesChecked: 0, IsIntact: true,
            FirstBrokenSequenceNo: null, FailureReason: null,
            SequenceGaps: Array.Empty<McpAuditChainGap>()));
    }
}
