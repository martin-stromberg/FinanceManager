using FinanceManager.Shared.Dtos.Mcp;
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

    private async Task<string> CreateAccessTokenAsync(HttpClient client)
    {
        var jwt = TestJwtTokens.Create(_factory.Services, JwtKey, includeAdminRole: true);
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/mcp-accesses")
        {
            Content = JsonContent.Create(new McpAccessCreateRequest(
                Name: $"it-{Guid.NewGuid():N}"[..20],
                Description: null,
                Level: 0, // Analyse
                ApprovalMode: 0, // PerAction
                ExpiresUtc: null,
                AllowedIpRanges: null,
                Scopes: null))
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<McpAccessCreatedDto>();
        Assert.NotNull(created);
        return created!.PlainTextToken;
    }

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
        });
        Assert.Equal(HttpStatusCode.OK, init.StatusCode);
        Assert.True(init.Headers.Contains("Mcp-Session-Id"));
        var initDoc = await init.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("FinanceManager", initDoc.GetProperty("result").GetProperty("serverInfo").GetProperty("name").GetString());
        Assert.Equal("katalog-v1", initDoc.GetProperty("result").GetProperty("serverInfo").GetProperty("catalogVersion").GetString());

        // tools/list → the two live meta tools with explicit input schemas.
        var list = await client.PostAsJsonAsync("/mcp", new { jsonrpc = "2.0", id = 2, method = "tools/list" });
        var listDoc = await list.Content.ReadFromJsonAsync<JsonElement>();
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
        });
        var callDoc = await call.Content.ReadFromJsonAsync<JsonElement>();
        var result = callDoc.GetProperty("result");
        var content = result.GetProperty("structuredContent");
        Assert.Equal("katalog-v1", content.GetProperty("catalogVersion").GetString());
        Assert.Equal("Analyse", content.GetProperty("access").GetProperty("level").GetString());
        Assert.Equal(18, content.GetProperty("effectiveScopes").GetArrayLength());

        // unknown tool → defined error, not a crash.
        var unknown = await client.PostAsJsonAsync("/mcp", new
        {
            jsonrpc = "2.0",
            id = 4,
            method = "tools/call",
            @params = new { name = "fm_no_such_tool", arguments = new { } }
        });
        var unknownDoc = await unknown.Content.ReadFromJsonAsync<JsonElement>();
        var unknownRaw = unknownDoc.GetRawText();
        Assert.True(unknownDoc.TryGetProperty("error", out var unkErr), $"expected error, got {unknownRaw}");
        Assert.True(unkErr.TryGetProperty("data", out var unkData), $"expected data, got {unknownRaw}");
        Assert.Equal("Err_Mcp_UnknownTool", unkData.GetProperty("code").GetString());

        // unknown JSON-RPC method → -32601.
        var method = await client.PostAsJsonAsync("/mcp", new { jsonrpc = "2.0", id = 5, method = "resources/list" });
        var methodDoc = await method.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(-32601, methodDoc.GetProperty("error").GetProperty("code").GetInt32());
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task Get_Returns405()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/mcp");
        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task MissingOrInvalidPat_ReturnsUniform401()
    {
        using var client = _factory.CreateClient();
        var missing = await client.PostAsJsonAsync("/mcp", new { jsonrpc = "2.0", id = 1, method = "ping" });
        Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);
        var body = await missing.Content.ReadAsStringAsync();
        Assert.Contains("Err_Mcp_Unauthorized", body);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "fm_mcp_AAAA_this_secret_does_not_exist_0123456789");
        var invalid = await client.PostAsJsonAsync("/mcp", new { jsonrpc = "2.0", id = 1, method = "ping" });
        Assert.Equal(HttpStatusCode.Unauthorized, invalid.StatusCode);
        var body2 = await invalid.Content.ReadAsStringAsync();
        Assert.Contains("Err_Mcp_Unauthorized", body2);
        // Uniformity: same error payload for unknown and malformed credentials.
        Assert.Equal(body, body2);
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task JwtToken_DoesNotAuthenticate_Mcp()
    {
        using var client = _factory.CreateClient();
        var jwt = TestJwtTokens.Create(_factory.Services, JwtKey, includeAdminRole: true);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
        var response = await client.PostAsJsonAsync("/mcp", new { jsonrpc = "2.0", id = 1, method = "ping" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task DisabledServer_Returns503()
    {
        using var factory = new TestWebApplicationFactory();
        // no Mcp:Enabled override → default false from appsettings.json.
        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/mcp", new { jsonrpc = "2.0", id = 1, method = "ping" });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var get = await client.GetAsync("/mcp");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, get.StatusCode);
    }
}
