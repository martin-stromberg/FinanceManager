using FinanceManager.Application.Mcp;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace FinanceManager.Web.Mcp;

/// <summary>
/// Kill-switch middleware of the <c>/mcp</c> path (F-0107): when <c>Mcp:Enabled=false</c>
/// every MCP request is rejected with 503 before any authentication work (pipeline position
/// after the rate limiter, before authentication). Bound via <see cref="IOptionsMonitor{T}"/>
/// so a configuration reload takes effect without a deployment.
/// </summary>
public sealed class McpDisabledMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IOptionsMonitor<McpOptions> _options;

    /// <summary>Creates the middleware.</summary>
    public McpDisabledMiddleware(RequestDelegate next, IOptionsMonitor<McpOptions> options)
    {
        _next = next;
        _options = options;
    }

    /// <summary>Rejects the request with 503 when MCP is disabled.</summary>
    public async Task InvokeAsync(HttpContext context)
    {
        if (!_options.CurrentValue.Enabled)
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            context.Response.ContentType = "application/json";
            var payload = new { origin = "API_Mcp", code = "Err_Mcp_Disabled", message = "The MCP endpoint is disabled." };
            await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
            return;
        }
        await _next(context);
    }
}
