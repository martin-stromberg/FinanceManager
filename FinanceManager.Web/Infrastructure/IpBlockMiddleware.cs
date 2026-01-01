using FinanceManager.Infrastructure;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace FinanceManager.Web.Infrastructure;

/// <summary>
/// Middleware that denies requests originating from IP addresses that are present and marked as blocked in the database.
/// When a request is blocked a 403 Forbidden JSON response is returned and the request pipeline is not continued.
/// </summary>
public sealed class IpBlockMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<IpBlockMiddleware> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="IpBlockMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next middleware delegate in the pipeline. Must not be <c>null</c>.</param>
    /// <param name="logger">Logger used to record blocked requests and unexpected conditions. Must not be <c>null</c>.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="next"/> or <paramref name="logger"/> is <c>null</c>.</exception>
    public IpBlockMiddleware(RequestDelegate next, ILogger<IpBlockMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Invokes the middleware. The method checks the remote IP address of the incoming HTTP context against the
    /// <c>IpBlocks</c> table in the provided <see cref="AppDbContext"/>. If the IP address is marked as blocked a
    /// 403 Forbidden JSON response is written and the request pipeline is not continued. Otherwise the request is
    /// forwarded to the next middleware.
    /// </summary>
    /// <param name="context">HTTP context for the current request. Cannot be <c>null</c>.</param>
    /// <param name="db">Database context used to query the IP block list. The instance is expected to be scoped to the request.</param>
    /// <returns>A task that completes when the middleware has finished processing the request.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is <c>null</c>.</exception>
    public async Task InvokeAsync(HttpContext context, AppDbContext db)
    {
        ArgumentNullException.ThrowIfNull(context);
        var ip = context.Connection.RemoteIpAddress?.ToString();

        if (!string.IsNullOrWhiteSpace(ip))
        {
            bool isBlocked = await db.IpBlocks.AsNoTracking()
                .AnyAsync(b => b.IpAddress == ip && b.IsBlocked, context.RequestAborted);

            if (isBlocked)
            {
                _logger.LogWarning("Blocked request from IP {Ip} to {Path}", ip, context.Request.Path);

                if (!context.Response.HasStarted)
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    context.Response.ContentType = "application/json";
                    var payload = new
                    {
                        title = "IP blocked",
                        status = StatusCodes.Status403Forbidden,
                        detail = "This IP address is currently blocked.",
                        ip,
                        traceId = context.TraceIdentifier
                    };
                    await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
                }
                return;
            }
        }

        await _next(context);
    }
}
