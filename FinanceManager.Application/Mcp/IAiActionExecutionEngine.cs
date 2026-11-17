namespace FinanceManager.Application.Mcp;

/// <summary>
/// The execution engine of the persistent approval queue (F-0903/F-0904;
/// architekturkonzept.md §6.2): the <see cref="FinanceManager.Domain.Mcp.AiActionRequest"/>
/// table itself is the queue — no separate store. The engine polls due
/// <see cref="Domain.Mcp.AiActionRequestStatus.Approved"/> requests, claims each atomically
/// (conditional update on the status), re-runs all guards at execution time (access state,
/// write-stop lid, scope profile — never a blind snapshot replay) and invokes the original
/// tool handler in <see cref="McpToolExecutionMode.ApprovedExecution"/> mode, then writes
/// the terminal state (<c>Executed</c>/<c>Failed</c>) together with its audit record.
/// It also performs the expiry sweep (<c>Pending/Approved → Expired</c>).
/// </summary>
/// <remarks>
/// Scoped service — the hosted runner (<c>AiActionExecutorService</c>/<c>AiActionExpiryService</c>)
/// creates a scope per pass and calls these methods. Exactly-once execution is guaranteed by
/// the atomic claim (single-instance SQLite deployment, same pattern as
/// <c>StatementDraftBookingGuard</c>).
/// </remarks>
public interface IAiActionExecutionEngine
{
    /// <summary>
    /// Claims and executes due approved requests (batch of at most
    /// <paramref name="batchSize"/>, oldest first). Requests held by the write-stop lid are
    /// released back to the queue untouched; requests whose access or scope was revoked are
    /// failed/withdrawn per the transition rules.
    /// </summary>
    /// <param name="batchSize">Maximum requests processed in this pass.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Number of requests that reached a terminal state in this pass.</returns>
    Task<int> ExecuteDueRequestsAsync(int batchSize, CancellationToken ct);

    /// <summary>
    /// Marks pending and approved requests whose decision deadline passed as
    /// <see cref="Domain.Mcp.AiActionRequestStatus.Expired"/> (with an audit event per request).
    /// Requests under an active claim are skipped (the running execution decides).
    /// </summary>
    /// <param name="utcNow">The current UTC timestamp.</param>
    /// <param name="batchSize">Maximum requests processed in this pass.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Number of expired requests.</returns>
    Task<int> ExpireOverdueRequestsAsync(DateTime utcNow, int batchSize, CancellationToken ct);
}
