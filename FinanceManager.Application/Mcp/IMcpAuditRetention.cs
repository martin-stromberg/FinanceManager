namespace FinanceManager.Application.Mcp;

/// <summary>
/// Outcome of one retention pass (F-1104, audit-konzept.md §5).
/// </summary>
/// <param name="EntriesRemoved">Audit entries deleted across all chains.</param>
/// <param name="RequestsRemoved">Terminal <c>AiActionRequest</c> rows deleted.</param>
/// <param name="ChainsProcessed">Chains (owner + global) that were evaluated.</param>
/// <param name="CutMarkersWritten"><c>RetentionPurged</c> anchor entries appended.</param>
public sealed record McpAuditRetentionResult(
    int EntriesRemoved,
    int RequestsRemoved,
    int ChainsProcessed,
    int CutMarkersWritten);

/// <summary>
/// Retention executor of the MCP audit trail and the approval-request queue (F-1104,
/// audit-konzept.md §5 — the only deletion path of the application on the audit table).
/// The pass is tiered: <c>ToolCall</c>/<c>AccessEvent</c>/<c>SecurityEvent</c> entries older
/// than <c>Mcp:Retention:ToolCallMonths</c> and <c>ApprovalEvent</c>/<c>DataChange</c>
/// entries older than <c>Mcp:Retention:ApprovalMonths</c> are removed per chain; terminal
/// <c>AiActionRequest</c> rows older than <c>Mcp:Retention:RequestMonths</c> follow the same
/// sweep. Every purged chain first receives a <c>RetentionPurged</c> anchor entry
/// documenting the removed sequence ranges — <see cref="IMcpAuditWriter.VerifyAsync"/>
/// treats ranges covered by such a marker as documented truncation instead of a breach
/// (the chain stays verifiable "ab diesem Eintrag").
/// </summary>
public interface IMcpAuditRetention
{
    /// <summary>
    /// Runs one retention pass at the given reference time.
    /// </summary>
    /// <param name="utcNow">Reference timestamp (injected for determinism).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The pass outcome counters.</returns>
    Task<McpAuditRetentionResult> RunAsync(DateTime utcNow, CancellationToken ct);
}
