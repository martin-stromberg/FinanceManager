using FinanceManager.Shared.Dtos.Mcp;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace FinanceManager.Tests.Integration;

/// <summary>
/// Shared wire-level plumbing of the <c>/mcp</c> integration suites: factory bootstrap with
/// MCP enabled, PAT issuance via the administration API and the <c>tools/call</c> JSON-RPC
/// round-trip with error unwrapping.
/// </summary>
public abstract class McpIntegrationTestBase : IClassFixture<TestWebApplicationFactory>
{
    /// <summary>JWT signing key the test host is configured with.</summary>
    protected const string JwtKey = "PLEASE_REPLACE_WITH_LONG_RANDOM_256BIT_SECRET_BASE64";

    /// <summary>The shared web application factory of the suite.</summary>
    protected readonly TestWebApplicationFactory _factory;

    /// <summary>Initializes the suite with a dedicated factory where MCP is enabled.</summary>
    /// <param name="factory">The shared web application factory.</param>
    protected McpIntegrationTestBase(TestWebApplicationFactory factory)
    {
        factory.ConfigurationOverrides["Mcp:Enabled"] = "true";
        _factory = factory;
    }

    /// <summary>Creates an API client bound to a non-redirecting HTTP client of the test host.</summary>
    /// <returns>The shared <see cref="FinanceManager.Shared.ApiClient"/>.</returns>
    protected FinanceManager.Shared.ApiClient CreateApiClient()
    {
        var http = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
        return new FinanceManager.Shared.ApiClient(http);
    }

    /// <summary>Creates an MCP access for the user and returns the plaintext PAT.</summary>
    /// <param name="client">HTTP client of the test host.</param>
    /// <param name="username">User the JWT and the access are issued for.</param>
    /// <param name="level">Access level (<see cref="FinanceManager.Domain.Mcp.McpAccessLevel"/> as short).</param>
    /// <param name="approvalMode">Approval mode (<see cref="FinanceManager.Domain.Mcp.McpApprovalMode"/> as short).</param>
    /// <returns>The plaintext personal access token.</returns>
    protected async Task<string> CreateAccessTokenAsync(HttpClient client, string username, short level, short approvalMode)
    {
        var jwt = TestJwtTokens.Create(_factory.Services, JwtKey, username: username);
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/mcp-accesses")
        {
            Content = JsonContent.Create(new McpAccessCreateRequest(
                Name: $"it-{Guid.NewGuid():N}"[..20],
                Description: null,
                Level: level,
                ApprovalMode: approvalMode,
                ExpiresUtc: null,
                AllowedIpRanges: null,
                Scopes: null))
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<McpAccessCreatedDto>(TestContext.Current.CancellationToken);
        Assert.NotNull(created);
        return created!.PlainTextToken;
    }

    /// <summary>
    /// Invokes one <c>tools/call</c> JSON-RPC request against the live <c>/mcp</c> endpoint
    /// and fails the test when the response carries a JSON-RPC error object.
    /// </summary>
    /// <param name="client">HTTP client authenticated with the PAT.</param>
    /// <param name="toolName">Name of the MCP tool to call.</param>
    /// <param name="arguments">Tool arguments (serialized into the <c>arguments</c> parameter).</param>
    /// <param name="id">JSON-RPC request id.</param>
    /// <returns>The <c>result</c> element of the JSON-RPC response.</returns>
    protected async Task<JsonElement> CallToolAsync(HttpClient client, string toolName, object arguments, int id)
    {
        var response = await client.PostAsJsonAsync("/mcp", new
        {
            jsonrpc = "2.0",
            id,
            method = "tools/call",
            @params = new { name = toolName, arguments }
        }, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var doc = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: TestContext.Current.CancellationToken);
        if (doc.TryGetProperty("error", out var error))
        {
            throw new Xunit.Sdk.XunitException($"tools/call {toolName} failed: {error.GetRawText()}");
        }
        return doc.GetProperty("result");
    }
}
