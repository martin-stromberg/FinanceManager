using FinanceManager.Shared.Dtos.Mcp;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace FinanceManager.Tests.Integration;

/// <summary>
/// End-to-end coverage of the <c>/mcp</c> surface (F-0102/F-0103/F-0106): PAT issuance via the
/// administration API, PAT-authenticated JSON-RPC (<c>initialize</c>, <c>tools/list</c>,
/// <c>tools/call fm_capabilities_describe</c>), the uniform 401 path, GET → 405 and the
/// <c>Mcp:Enabled</c> kill switch → 503.
/// </summary>
public sealed class McpEndpointTests : IClassFixture<TestWebApplicationFactory>
{
    private const string JwtKey = "PLEASE_REPLACE_WITH_LONG_RANDOM_256BIT_SECRET_BASE64";

    private readonly TestWebApplicationFactory _factory;

    /// <summary>Initializes the suite with a dedicated factory where MCP is enabled.</summary>
    /// <param name="factory">The shared web application factory.</param>
    public McpEndpointTests(TestWebApplicationFactory factory)
    {
        factory.ConfigurationOverrides["Mcp:Enabled"] = "true";
        _factory = factory;
    }

    private async Task<McpAccessCreatedDto> CreateAccessAsync(HttpClient client, short approvalMode = 0)
    {
        var jwt = TestJwtTokens.Create(_factory.Services, JwtKey, includeAdminRole: true);
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/mcp-accesses")
        {
            Content = JsonContent.Create(new McpAccessCreateRequest(
                Name: $"it-{Guid.NewGuid():N}"[..20],
                Description: null,
                Level: 0, // Analyse
                ApprovalMode: approvalMode,
                ExpiresUtc: null,
                AllowedIpRanges: null,
                Scopes: null))
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<McpAccessCreatedDto>();
        Assert.NotNull(created);
        return created!;
    }

    private async Task<string> CreateAccessTokenAsync(HttpClient client)
        => (await CreateAccessAsync(client)).PlainTextToken;

    /// <summary>Test case.</summary>
    [Fact]
    public async Task PatFlow_Initialize_ToolsList_ToolsCall_Works()
    {
        using var client = _factory.CreateClient();
        var pat = await CreateAccessTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", pat);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        // initialize → protocol handshake + Mcp-Session-Id header.
        var init = await client.PostAsJsonAsync("/mcp", new
        {
            jsonrpc = "2.0",
            id = 1,
            method = "initialize",
            @params = new { protocolVersion = "2025-03-26", capabilities = new { }, clientInfo = new { name = "test", version = "1" } }
        }, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, init.StatusCode);
        Assert.True(init.Headers.Contains("Mcp-Session-Id"));
        var initDoc = await init.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal("FinanceManager", initDoc.GetProperty("result").GetProperty("serverInfo").GetProperty("name").GetString());
        Assert.Equal("katalog-v1", initDoc.GetProperty("result").GetProperty("serverInfo").GetProperty("catalogVersion").GetString());

        // tools/list → the two live meta tools with explicit input schemas.
        var list = await client.PostAsJsonAsync("/mcp", new { jsonrpc = "2.0", id = 2, method = "tools/list" }, cancellationToken: TestContext.Current.CancellationToken);
        var listDoc = await list.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: TestContext.Current.CancellationToken);
        var tools = listDoc.GetProperty("result").GetProperty("tools");
        var names = tools.EnumerateArray().Select(t => t.GetProperty("name").GetString()).ToList();
        Assert.Contains("fm_capabilities_describe", names);
        Assert.Contains("fm_task_status", names);
        Assert.All(tools.EnumerateArray(), t => Assert.True(t.TryGetProperty("inputSchema", out var s) && s.ValueKind == JsonValueKind.Object));

        // tools/call fm_capabilities_describe → effective profile of the access.
        var call = await client.PostAsJsonAsync("/mcp", new
        {
            jsonrpc = "2.0",
            id = 3,
            method = "tools/call",
            @params = new { name = "fm_capabilities_describe", arguments = new { } }
        }, cancellationToken: TestContext.Current.CancellationToken);
        var callDoc = await call.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: TestContext.Current.CancellationToken);
        var result = callDoc.GetProperty("result");
        var content = result.GetProperty("structuredContent");
        Assert.Equal("katalog-v1", content.GetProperty("catalogVersion").GetString());
        Assert.Equal("Analyse", content.GetProperty("access").GetProperty("level").GetString());
        Assert.Equal(19, content.GetProperty("effectiveScopes").GetArrayLength());

        // unknown tool → defined error, not a crash.
        var unknown = await client.PostAsJsonAsync("/mcp", new
        {
            jsonrpc = "2.0",
            id = 4,
            method = "tools/call",
            @params = new { name = "fm_no_such_tool", arguments = new { } }
        }, cancellationToken: TestContext.Current.CancellationToken);
        var unknownDoc = await unknown.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: TestContext.Current.CancellationToken);
        var unknownRaw = unknownDoc.GetRawText();
        Assert.True(unknownDoc.TryGetProperty("error", out var unkErr), $"expected error, got {unknownRaw}");
        Assert.True(unkErr.TryGetProperty("data", out var unkData), $"expected data, got {unknownRaw}");
        Assert.Equal("Err_Mcp_UnknownTool", unkData.GetProperty("code").GetString());

        // unknown JSON-RPC method → -32601.
        var method = await client.PostAsJsonAsync("/mcp", new { jsonrpc = "2.0", id = 5, method = "resources/list" }, cancellationToken: TestContext.Current.CancellationToken);
        var methodDoc = await method.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(-32601, methodDoc.GetProperty("error").GetProperty("code").GetInt32());
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task Get_WithoutSseAccept_Returns405()
    {
        using var client = _factory.CreateClient();
        var pat = await CreateAccessTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", pat);
        var response = await client.GetAsync("/mcp", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task Get_SseStream_OpensAndDeliversOwnRequestStatus()
    {
        using var client = _factory.CreateClient();
        var created = await CreateAccessAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", created.PlainTextToken);

        // initialize → live session id the stream binds to.
        var init = await client.PostAsJsonAsync("/mcp", new { jsonrpc = "2.0", id = 1, method = "initialize" }, cancellationToken: TestContext.Current.CancellationToken);
        var sessionId = init.Headers.GetValues("Mcp-Session-Id").Single();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/mcp");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        request.Headers.Add("Mcp-Session-Id", sessionId);
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.StartsWith("text/event-stream", response.Content.Headers.ContentType?.ToString());

        // Push a status event of the access through the hub — the stream must deliver it
        // as a request-status SSE frame.
        var hub = _factory.Services.GetRequiredService<FinanceManager.Web.Mcp.McpSessionHub>();
        var requestId = Guid.NewGuid();
        hub.Publish(new FinanceManager.Application.Mcp.McpRequestStatusEvent(
            created.Access.Id, Guid.NewGuid(), requestId, "Executed", "fm_test_tool"));

        await using var stream = await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken);
        using var reader = new StreamReader(stream);
        var frame = await ReadFrameAsync(reader, TimeSpan.FromSeconds(15));
        Assert.Contains("request-status", frame);
        Assert.Contains(requestId.ToString(), frame);
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task Get_SseWithoutSession_Returns404()
    {
        using var client = _factory.CreateClient();
        var pat = await CreateAccessTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", pat);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/mcp");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>Reads one complete SSE frame (until its terminating blank line).</summary>
    private static async Task<string> ReadFrameAsync(StreamReader reader, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        var frame = new System.Text.StringBuilder();
        while (!cts.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cts.Token);
            if (line is null)
            {
                break;
            }
            if (line.Length == 0)
            {
                return frame.ToString();
            }
            frame.AppendLine(line);
        }
        throw new Xunit.Sdk.XunitException("No complete SSE frame received within the timeout.");
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task MissingOrInvalidPat_ReturnsUniform401()
    {
        using var client = _factory.CreateClient();
        var missing = await client.PostAsJsonAsync("/mcp", new { jsonrpc = "2.0", id = 1, method = "ping" }, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);
        var body = await missing.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("Err_Mcp_Unauthorized", body);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "fm_mcp_AAAA_this_secret_does_not_exist_0123456789");
        var invalid = await client.PostAsJsonAsync("/mcp", new { jsonrpc = "2.0", id = 1, method = "ping" }, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, invalid.StatusCode);
        var body2 = await invalid.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("Err_Mcp_Unauthorized", body2);
        // Uniformity: same error payload for unknown and malformed credentials.
        Assert.Equal(body, body2);
    }

    /// <summary>
    /// End-to-end of the <c>fm_admin_backups</c> <c>download</c> sub-operation: the tool
    /// answers a short-lived, ticket-bound <c>/mcp/download/{token}</c> URL which an
    /// unauthenticated client can redeem within its ~10-minute lifetime for the backup zip;
    /// invalid tokens answer a uniform 404.
    /// </summary>
    [Fact]
    public async Task AdminBackups_DownloadTicket_ServesZipAndRejectsBadTokens()
    {
        using var client = _factory.CreateClient();
        var jwt = TestJwtTokens.Create(_factory.Services, JwtKey, includeAdminRole: true);

        // A real backup of the bootstrap admin (same owner as the PAT below).
        Guid backupId;
        using (var backupReq = new HttpRequestMessage(HttpMethod.Post, "/api/setup/backups"))
        {
            backupReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
            var backupRes = await client.SendAsync(backupReq, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, backupRes.StatusCode);
            var backupDoc = await backupRes.Content.ReadFromJsonAsync<JsonElement>(
                cancellationToken: TestContext.Current.CancellationToken);
            backupId = backupDoc.GetProperty("id").GetGuid();
        }

        try
        {
            // PreApproved access → the write-preparing tool class runs directly; the
            // download sub-op itself needs no handler-level approval.
            var pat = (await CreateAccessAsync(client, approvalMode: 1)).PlainTextToken;
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", pat);
            var call = await client.PostAsJsonAsync("/mcp", new
            {
                jsonrpc = "2.0",
                id = 1,
                method = "tools/call",
                @params = new
                {
                    name = "fm_admin_backups",
                    arguments = new { operation = "download", id = backupId.ToString() }
                }
            }, cancellationToken: TestContext.Current.CancellationToken);
            var callDoc = await call.Content.ReadFromJsonAsync<JsonElement>(
                cancellationToken: TestContext.Current.CancellationToken);
            var content = callDoc.GetProperty("result").GetProperty("structuredContent");
            Assert.Equal("download", content.GetProperty("operation").GetString());
            var url = content.GetProperty("downloadUrl").GetString();
            Assert.NotNull(url);
            Assert.Contains("/mcp/download/", url);

            // The ticket is the credential — no PAT needed on the download itself.
            using var anon = _factory.CreateClient();
            var file = await anon.GetAsync(url, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, file.StatusCode);
            Assert.Equal("application/zip", file.Content.Headers.ContentType?.MediaType);
            Assert.True((await file.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken)).Length > 0);

            // Unknown/garbage ticket → uniform 404 (no existence oracle).
            var bad = await anon.GetAsync("/mcp/download/not-a-ticket", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.NotFound, bad.StatusCode);
        }
        finally
        {
            // Best-effort cleanup of the backup file under the content root.
            using var del = new HttpRequestMessage(HttpMethod.Delete, $"/api/setup/backups/{backupId}");
            del.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
            await client.SendAsync(del, TestContext.Current.CancellationToken);
        }
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task JwtToken_DoesNotAuthenticate_Mcp()
    {
        using var client = _factory.CreateClient();
        var jwt = TestJwtTokens.Create(_factory.Services, JwtKey, includeAdminRole: true);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
        var response = await client.PostAsJsonAsync("/mcp", new { jsonrpc = "2.0", id = 1, method = "ping" }, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task DisabledServer_Returns503()
    {
        using var factory = new TestWebApplicationFactory();
        // no Mcp:Enabled override → default false from appsettings.json.
        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/mcp", new { jsonrpc = "2.0", id = 1, method = "ping" }, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var get = await client.GetAsync("/mcp", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, get.StatusCode);
    }
}
