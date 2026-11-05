using FinanceManager.Application.Backups;
using FinanceManager.Application.Mcp;
using FinanceManager.Domain.Mcp;
using FinanceManager.Web.Infrastructure;
using System.Text.Json;
using System.Threading.Channels;

namespace FinanceManager.Web.Mcp;

/// <summary>
/// Endpoint mapping of the <c>/mcp</c> route (F-0103/F-1101): a single POST endpoint
/// accepting JSON-RPC 2.0 messages (<c>initialize</c>, <c>ping</c>, <c>tools/list</c>,
/// <c>tools/call</c>, notifications), GET with <c>Accept: text/event-stream</c> opens the
/// SSE stream of the streamable-HTTP upgrade (server-initiated request-status
/// notifications of the calling access; GET without the SSE accept header → 405) and
/// DELETE terminates the transport session. Authentication runs exclusively via the
/// <c>McpPat</c> scheme — no cookie/JWT path.
/// </summary>
public static class McpEndpointExtensions
{
    private const int ProtocolError = -32000;
    private const int ParseError = -32700;
    private const int InvalidRequest = -32600;
    private const int MethodNotFound = -32601;
    private const int InvalidParams = -32602;
    private const int InternalError = -32603;
    private const long MaxBodyBytes = 1_048_576; // 1 MiB envelope limit

    private static readonly string[] SupportedProtocolVersions = ["2025-03-26", "2024-11-05", "2025-06-18"];

    /// <summary>
    /// Maps the MCP endpoint group.
    /// </summary>
    /// <param name="app">The endpoint route builder.</param>
    /// <returns>The route group builder.</returns>
    public static RouteGroupBuilder MapMcpEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/mcp");

        group.MapPost("", HandlePostAsync)
            .RequireAuthorization(McpAuthenticationDefaults.Policy)
            .RequireRateLimiting(RateLimitPolicies.McpEndpoint);

        group.MapGet("", HandleGetAsync)
            .RequireAuthorization(McpAuthenticationDefaults.Policy)
            .RequireRateLimiting(RateLimitPolicies.McpEndpoint);

        group.MapDelete("", (HttpContext ctx, McpSessionHub sessions) =>
        {
            var id = ctx.Request.Headers[McpSessionHub.SessionHeader].ToString();
            return sessions.Remove(id) ? Results.NoContent() : Results.NotFound();
        }).RequireAuthorization(McpAuthenticationDefaults.Policy)
          .RequireRateLimiting(RateLimitPolicies.McpEndpoint);

        // Short-lived, ticket-bound backup download (fm_admin_backups 'download'): the
        // signed/encrypted ticket itself is the credential — deliberately no PAT
        // authorization, so an agent can hand the URL to any plain HTTP client. The
        // endpoint still sits under the /mcp kill-switch branch and the per-IP
        // unauthenticated rate-limit partition.
        group.MapGet("/download/{token}", HandleDownloadAsync)
            .RequireRateLimiting(RateLimitPolicies.McpEndpoint);

        return group;
    }

    /// <summary>
    /// Serves one backup zip behind a short-lived download ticket issued by the
    /// <c>fm_admin_backups</c> <c>download</c> sub-operation. The ticket binds owner +
    /// backup id, so no request-side identity is evaluated: an authentic, unexpired token
    /// grants exactly the file it was minted for. Invalid tokens answer 404, expired ones
    /// 410 (uniform, no detail leak); every outcome is recorded as a
    /// <see cref="McpAuditCategory.SecurityEvent"/> (<c>BackupDownloaded</c>/
    /// <c>BackupDownloadDenied</c>).
    /// </summary>
    private static async Task<IResult> HandleDownloadAsync(
        string token,
        HttpContext http,
        IMcpDownloadTicketService tickets,
        IBackupService backups,
        IMcpAuditWriter auditWriter,
        ILoggerFactory loggerFactory)
    {
        var ct = http.RequestAborted;
        var sourceIp = http.Connection.RemoteIpAddress?.ToString();
        var validation = tickets.Validate(token);
        var grant = validation.Grant;
        if (grant is null || validation.Result is not McpDownloadTicketValidationResult.Valid)
        {
            await WriteDownloadAuditAsync(auditWriter, loggerFactory, "BackupDownloadDenied",
                "Denied", grant, sourceIp, payloadJson: null, ct);
            return validation.Result == McpDownloadTicketValidationResult.Expired
                ? Results.StatusCode(StatusCodes.Status410Gone)
                : Results.NotFound();
        }

        var meta = await backups.GetAsync(grant.OwnerUserId, grant.BackupId, ct);
        var stream = meta is null
            ? null
            : await backups.OpenDownloadAsync(grant.OwnerUserId, grant.BackupId, ct);
        if (meta is null || stream is null)
        {
            await WriteDownloadAuditAsync(auditWriter, loggerFactory, "BackupDownloadDenied",
                "Denied", grant, sourceIp, payloadJson: null, ct);
            return Results.NotFound();
        }

        await WriteDownloadAuditAsync(auditWriter, loggerFactory, "BackupDownloaded",
            "Success", grant, sourceIp,
            JsonSerializer.Serialize(new { backupId = grant.BackupId, fileName = meta.FileName }), ct);

        return Results.File(stream, "application/zip", fileDownloadName: SanitizeDownloadName(meta.FileName));
    }

    /// <summary>
    /// Defense-in-depth cleanup of the database-stored file name before it goes into the
    /// <c>Content-Disposition</c> header: strips path segments, control characters and
    /// quotes (the value is untrusted metadata, never a storage path).
    /// </summary>
    private static string SanitizeDownloadName(string? fileName)
    {
        var name = Path.GetFileName(fileName ?? string.Empty);
        var cleaned = new string(name.Where(c => !char.IsControl(c) && c != '"').ToArray());
        return string.IsNullOrWhiteSpace(cleaned) ? "backup.zip" : cleaned;
    }

    /// <summary>
    /// Best-effort <see cref="McpAuditCategory.SecurityEvent"/> record of a ticket
    /// redemption attempt. Audit failures are logged but never change the uniform
    /// response (same convention as the auth-failure path).
    /// </summary>
    private static async Task WriteDownloadAuditAsync(
        IMcpAuditWriter auditWriter, ILoggerFactory loggerFactory, string eventType,
        string resultStatus, McpDownloadTicketGrant? grant, string? sourceIp,
        string? payloadJson, CancellationToken ct)
    {
        try
        {
            await auditWriter.WriteAsync(new McpAuditWriteRequest(
                McpAuditCategory.SecurityEvent, eventType, resultStatus,
                OwnerUserId: grant?.OwnerUserId, McpAccessId: grant?.McpAccessId,
                Context: "McpEndpoint", SourceIp: sourceIp, PayloadJson: payloadJson), ct);
        }
        catch (Exception ex)
        {
            loggerFactory.CreateLogger("FinanceManager.Web.Mcp.DownloadEndpoint")
                .LogWarning(ex, "Failed to write MCP audit record {EventType}", eventType);
        }
    }

    /// <summary>
    /// SSE stream of the streamable-HTTP upgrade (F-1101, architekturkonzept.md §3):
    /// <c>GET /mcp</c> with <c>Accept: text/event-stream</c> and a live
    /// <c>Mcp-Session-Id</c> opens a server-sent-events stream that delivers the request
    /// status changes of the authenticated access (<c>request-status</c> events) plus a
    /// periodic keep-alive comment. Only the access's own requests are emitted — the
    /// subscription key is the access id, never the session. The stream is bound to the
    /// same PAT authentication and kill switch as the POST endpoint;
    /// <c>fm_task_status</c> polling stays the documented fallback for clients without
    /// SSE support.
    /// </summary>
    private static async Task HandleGetAsync(
        HttpContext http,
        McpSessionHub sessions,
        IMcpAccessContext access,
        Microsoft.Extensions.Options.IOptionsMonitor<McpOptions> options)
    {
        var acceptsSse = http.Request.Headers.Accept
            .Any(v => v?.Contains("text/event-stream", StringComparison.OrdinalIgnoreCase) == true);
        if (!acceptsSse)
        {
            http.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
            return;
        }

        // The stream belongs to a live transport session (streamable HTTP).
        var sessionId = http.Request.Headers[McpSessionHub.SessionHeader].ToString();
        if (string.IsNullOrEmpty(sessionId) || !sessions.Touch(sessionId))
        {
            http.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        var accessId = access.McpAccessId;
        var subscription = sessions.Subscribe(accessId, options.CurrentValue.Sse.MaxSubscribersPerAccess);
        if (subscription is null)
        {
            // Unknown access context or the per-access subscriber cap is reached.
            http.Response.StatusCode = accessId == Guid.Empty
                ? StatusCodes.Status401Unauthorized
                : StatusCodes.Status429TooManyRequests;
            return;
        }

        var ct = http.RequestAborted;
        var heartbeat = TimeSpan.FromSeconds(Math.Max(5, options.CurrentValue.Sse.HeartbeatSeconds));
        try
        {
            http.Response.StatusCode = StatusCodes.Status200OK;
            http.Response.ContentType = "text/event-stream";
            http.Response.Headers.CacheControl = "no-cache";
            http.Response.Headers["X-Accel-Buffering"] = "no"; // disable proxy buffering
            await http.Response.Body.FlushAsync(ct);

            var eventId = 0L;
            while (!ct.IsCancellationRequested)
            {
                McpRequestStatusEvent? next;
                try
                {
                    next = await subscription.Reader.ReadAsync(ct)
                        .AsTask().WaitAsync(heartbeat, ct);
                }
                catch (TimeoutException)
                {
                    await http.Response.WriteAsync(": heartbeat\n\n", ct);
                    await http.Response.Body.FlushAsync(ct);
                    continue;
                }
                catch (ChannelClosedException)
                {
                    break;
                }

                if (next is null)
                {
                    continue;
                }
                var payload = JsonSerializer.Serialize(new
                {
                    type = "request-status",
                    requestId = next.RequestId,
                    status = next.Status,
                    toolName = next.ToolName,
                    actionKey = next.ActionKey,
                    correlationId = next.CorrelationId,
                    failureReason = next.FailureReason,
                    utc = next.Utc
                });
                await http.Response.WriteAsync(
                    $"id: {++eventId}\nevent: request-status\ndata: {payload}\n\n", ct);
                await http.Response.Body.FlushAsync(ct);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Client disconnect / server shutdown — clean stream end.
        }
        finally
        {
            subscription.Dispose();
        }
    }

    private static async Task<IResult> HandlePostAsync(
        HttpContext http,
        McpSessionHub sessions,
        IMcpToolRegistry registry,
        McpToolDispatcher dispatcher,
        IMcpAccessContext access)
    {
        var ct = http.RequestAborted;

        // Session semantics: an unknown/expired session id is rejected (transport state only).
        var sessionId = http.Request.Headers[McpSessionHub.SessionHeader].ToString();
        if (!string.IsNullOrEmpty(sessionId) && !sessions.Touch(sessionId))
        {
            return Results.StatusCode(StatusCodes.Status404NotFound);
        }

        if (http.Request.ContentLength is > MaxBodyBytes)
        {
            return Results.Json(ErrorObject(null, InvalidRequest, "Request too large.", null, null));
        }

        JsonDocument doc;
        try
        {
            doc = await JsonDocument.ParseAsync(http.Request.Body, cancellationToken: ct);
        }
        catch (JsonException)
        {
            return Results.Json(ErrorObject(null, ParseError, "Parse error.", null, null));
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Array)
            {
                if (root.GetArrayLength() == 0)
                {
                    return Results.Json(ErrorObject(null, InvalidRequest, "Invalid request.", null, null));
                }
                var results = new List<object?>();
                foreach (var msg in root.EnumerateArray())
                {
                    var response = await HandleMessageAsync(msg, http, sessions, registry, dispatcher, access, ct);
                    if (response is not null)
                    {
                        results.Add(response);
                    }
                }
                return results.Count == 0 ? Results.Accepted() : Results.Json(results);
            }

            var single = await HandleMessageAsync(root, http, sessions, registry, dispatcher, access, ct);
            return single is null ? Results.Accepted() : Results.Json(single);
        }
    }

    private static async Task<object?> HandleMessageAsync(
        JsonElement msg,
        HttpContext http,
        McpSessionHub sessions,
        IMcpToolRegistry registry,
        McpToolDispatcher dispatcher,
        IMcpAccessContext access,
        CancellationToken ct)
    {
        if (msg.ValueKind != JsonValueKind.Object
            || !msg.TryGetProperty("jsonrpc", out var rpcEl)
            || rpcEl.GetString() != "2.0"
            || !msg.TryGetProperty("method", out var methodEl)
            || methodEl.GetString() is not { } method)
        {
            return Error(null, InvalidRequest, "Invalid request.");
        }

        var isNotification = !msg.TryGetProperty("id", out var idEl);
        var method2 = method;
        if (isNotification)
        {
            // Notifications (e.g. notifications/initialized) are accepted silently.
            return null;
        }

        var id = idEl.Clone();
        try
        {
            switch (method2)
            {
                case "initialize":
                    {
                        var newSession = sessions.Create();
                        http.Response.Headers[McpSessionHub.SessionHeader] = newSession;
                        var requested = msg.TryGetProperty("params", out var p)
                            && p.ValueKind == JsonValueKind.Object
                            && p.TryGetProperty("protocolVersion", out var pv)
                                ? pv.GetString()
                                : null;
                        var version = requested is not null && SupportedProtocolVersions.Contains(requested)
                            ? requested
                            : SupportedProtocolVersions[0];
                        return Result(id, new
                        {
                            protocolVersion = version,
                            capabilities = new { tools = new { listChanged = false } },
                            serverInfo = new { name = "FinanceManager", version = "1.0", catalogVersion = registry.CatalogVersion }
                        });
                    }
                case "ping":
                    return Result(id, new { });
                case "tools/list":
                    return Result(id, new
                    {
                        tools = registry.Tools.Select(t => (object)new
                        {
                            name = t.Name,
                            description = t.Description
                                + (t.IsDeprecated ? $" [deprecated{(t.ReplacedBy is null ? string.Empty : $" — use {t.ReplacedBy}")}]" : string.Empty),
                            inputSchema = t.InputSchema.RootElement.Clone(),
                            deprecated = t.IsDeprecated,
                            replacedBy = t.ReplacedBy,
                            sinceVersion = t.SinceVersion
                        }).ToList()
                    });
                case "tools/call":
                    {
                        if (!msg.TryGetProperty("params", out var callParams)
                            || callParams.ValueKind != JsonValueKind.Object
                            || !callParams.TryGetProperty("name", out var nameEl)
                            || nameEl.GetString() is not { } toolName)
                        {
                            return Error(id, InvalidParams, "Missing tool name.");
                        }
                        var arguments = callParams.TryGetProperty("arguments", out var argsEl)
                            && argsEl.ValueKind == JsonValueKind.Object
                                ? argsEl.Clone()
                                : JsonDocument.Parse("{}").RootElement;

                        var outcome = await dispatcher.DispatchAsync(
                            access, toolName, arguments, http.RequestServices,
                            http.Connection.RemoteIpAddress?.ToString(), ct);

                        return outcome.Kind switch
                        {
                            McpDispatchResultKind.Success => Result(id, new
                            {
                                content = new[] { new { type = "text", text = JsonSerializer.Serialize(outcome.Result) } },
                                structuredContent = outcome.Result
                            }),
                            McpDispatchResultKind.ToolError => Result(id, new
                            {
                                content = new[] { new { type = "text", text = outcome.ErrorMessage } },
                                isError = true
                            }),
                            _ => Error(id, ProtocolError, outcome.ErrorMessage ?? "MCP error.", outcome.ErrorCode)
                        };
                    }
                default:
                    return Error(id, MethodNotFound, $"Method '{method2}' not found.");
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Error(id, InternalError, "Internal error.", detail: ex.GetType().Name);
        }
    }

    private static object Result(JsonElement id, object result)
        => new { jsonrpc = "2.0", id, result };

    private static object Error(JsonElement? id, int code, string message, string? errCode = null, string? detail = null)
        => ErrorObject(id, code, message, errCode, detail);

    private static object ErrorObject(JsonElement? id, int code, string message, string? errCode, string? detail)
    {
        object? data = errCode is null && detail is null
            ? null
            : new { code = errCode, detail };
        return new { jsonrpc = "2.0", id = id?.Clone(), error = new { code, message, data } };
    }
}
