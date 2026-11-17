using System.Text.Json;

namespace FinanceManager.Application.Mcp;

/// <summary>
/// Optional enqueue-time validation hook of a tool handler (F-1003, approval-konzept.md
/// §4): a handler that can already tell a request is futile — e.g. reversing an already
/// reversed posting — implements this interface so the dispatcher rejects the call
/// classified <b>before</b> the <c>AiActionRequest</c> row is created instead of letting it
/// park as Pending and fail only after approval at execution time.
/// </summary>
/// <remarks>
/// The hook runs on the request-creation path only (gate redirect and the handler-level
/// <c>requiresApproval</c> fallback share it); it must be side-effect free — the normative
/// re-check at queue execution stays in <see cref="IMcpToolHandler.InvokeAsync"/>.
/// Implementations signal a classified rejection by throwing
/// <see cref="McpToolException"/>; any other exception propagates as an unexpected failure.
/// </remarks>
public interface IMcpEnqueueValidator
{
    /// <summary>
    /// Validates the frozen request arguments before the approval request is persisted.
    /// </summary>
    /// <param name="arguments">The <c>arguments</c> object of the <c>tools/call</c> request.</param>
    /// <param name="context">The call context (access, services, correlation).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A completed task when the request may be enqueued.</returns>
    /// <exception cref="McpToolException">
    /// Classified early rejection (<c>Err_*</c>) — the dispatcher answers the client with
    /// the code instead of a queued request id.
    /// </exception>
    Task ValidateRequestAsync(JsonElement arguments, McpToolCallContext context, CancellationToken ct);
}
