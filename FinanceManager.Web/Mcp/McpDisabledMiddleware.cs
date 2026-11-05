using FinanceManager.Application.Mcp;
using System.Text.Json;

namespace FinanceManager.Web.Mcp;

/// <summary>
/// Kill-switch middleware of the <c>/mcp</c> path (F-0107): when the database-stored
/// <c>McpSettings.Enabled</c> switch is off every MCP request is rejected with 503 before
/// any authentication work (pipeline position after the rate limiter, before
/// authentication). The flag is read per request from the scoped
/// <see cref="IMcpSettingsService"/> so an admin change takes effect immediately.
/// </summary>
public sealed class McpDisabledMiddleware
{
    private readonly RequestDelegate _next;

    /// <summary>Creates the middleware.</summary>
    public McpDisabledMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    /// <summary>Rejects the request with 503 when MCP is disabled.</summary>
    /// <param name="context">The http context.</param>
    /// <param name="settings">Scoped MCP settings service (request scope).</param>
    public async Task InvokeAsync(HttpContext context, IMcpSettingsService settings)
    {
        if (!await settings.IsEnabledAsync(context.RequestAborted))
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
