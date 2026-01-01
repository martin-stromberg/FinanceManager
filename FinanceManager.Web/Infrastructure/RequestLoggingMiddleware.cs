using System.Diagnostics;

namespace FinanceManager.Web.Infrastructure;

/// <summary>
/// Middleware that logs HTTP request execution time and outcome.
/// Logs a Debug level entry for successful responses (status &lt; 400) and a Warning for error responses.
/// Exceptions thrown by downstream middleware are logged as warnings and rethrown.
/// </summary>
public sealed class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="RequestLoggingMiddleware"/>.
    /// </summary>
    /// <param name="next">Next middleware delegate in the pipeline. Must not be <c>null</c>.</param>
    /// <param name="logger">Logger used to record request timings and failures. Must not be <c>null</c>.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="next"/> or <paramref name="logger"/> is <c>null</c>.</exception>
    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Invokes the middleware for the given HTTP context. Measures elapsed time and logs request path,
    /// HTTP method, response status code and elapsed milliseconds. Exceptions thrown by downstream
    /// middleware are logged and rethrown.
    /// </summary>
    /// <param name="context">HTTP context for the current request. Must not be <c>null</c>.</param>
    /// <returns>A task that completes when the middleware and downstream pipeline have finished processing.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is <c>null</c>.</exception>
    /// <remarks>
    /// This middleware does not swallow exceptions; any exception from the downstream pipeline will be logged and rethrown.
    /// Logging avoids including sensitive payload content.
    /// </remarks>
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var sw = Stopwatch.StartNew();
        try
        {
            await _next(context);
            sw.Stop();

            var status = context.Response?.StatusCode ?? 0;
            var level = status < 400 ? LogLevel.Debug : LogLevel.Warning;
            var method = context.Request.Method;
            var path = context.Request.Path + context.Request.QueryString;
            var elapsedMs = sw.ElapsedMilliseconds;
            var traceId = context.TraceIdentifier;

            _logger.Log(level,
                "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs} ms (TraceId: {TraceId})",
                method, path, status, elapsedMs, traceId);
        }
        catch (Exception ex)
        {
            sw.Stop();
            var method = context.Request.Method;
            var path = context.Request.Path + context.Request.QueryString;
            var elapsedMs = sw.ElapsedMilliseconds;
            var traceId = context.TraceIdentifier;

            _logger.LogWarning(ex,
                "HTTP {Method} {Path} threw in {ElapsedMs} ms (TraceId: {TraceId})",
                method, path, elapsedMs, traceId);

            throw;
        }
    }
}