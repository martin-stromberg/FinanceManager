using System.Text.Json;

namespace FinanceManager.Application.Mcp;

/// <summary>
/// Per-call context handed to a tool handler by the dispatcher.
/// </summary>
/// <param name="Access">The authenticated MCP access context (owner resolution).</param>
/// <param name="Services">Request service provider for resolving scoped services.</param>
/// <param name="CorrelationId">Correlation id linking call, requests and audit entries.</param>
/// <param name="SourceIp">Source IP of the client call (security attribution).</param>
public sealed record McpToolCallContext(
    IMcpAccessContext Access,
    IServiceProvider Services,
    Guid CorrelationId,
    string? SourceIp);

/// <summary>
/// Exception thrown by tool handlers for a controlled, client-visible failure that must be
/// reported as an <c>isError</c> tool result (MCP convention) instead of a protocol error.
/// </summary>
public sealed class McpToolException : Exception
{
    /// <summary>
    /// Creates a new <see cref="McpToolException"/>.
    /// </summary>
    /// <param name="code">Stable <c>Err_Mcp_*</c> code.</param>
    /// <param name="message">Client-visible message (must not leak foreign data).</param>
    public McpToolException(string code, string message) : base(message)
    {
        Code = code;
    }

    /// <summary>Stable error code.</summary>
    public string Code { get; }
}

/// <summary>
/// Invocation side of an MCP tool: the thin adapter executing the business call.
/// Handlers are scoped services resolved per call by the dispatcher; metadata lives in the
/// matching <see cref="IMcpToolDescriptor"/> of the same <see cref="ToolName"/>.
/// </summary>
public interface IMcpToolHandler
{
    /// <summary>The <c>fm_*</c> tool name this handler implements.</summary>
    string ToolName { get; }

    /// <summary>
    /// Executes the tool with the validated arguments.
    /// </summary>
    /// <param name="arguments">The <c>arguments</c> object of the <c>tools/call</c> request (may be an empty object).</param>
    /// <param name="context">The call context (access, services, correlation).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The structured tool result (serialized as <c>structuredContent</c>).</returns>
    /// <exception cref="McpToolException">Controlled failure reported as <c>isError</c> result.</exception>
    Task<object> InvokeAsync(JsonElement arguments, McpToolCallContext context, CancellationToken ct);
}
