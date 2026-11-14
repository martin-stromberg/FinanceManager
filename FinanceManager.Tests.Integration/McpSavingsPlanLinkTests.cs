using FinanceManager.Application.Demo;
using FinanceManager.Infrastructure;
using FinanceManager.Shared;
using FinanceManager.Shared.Dtos.Mcp;
using FinanceManager.Shared.Dtos.Postings;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace FinanceManager.Tests.Integration;

/// <summary>
/// End-to-end coverage of the savings-plan account links through the real <c>/mcp</c> surface
/// on the real demo-data set: the demo seeds a savings account "Bausparkonto" linked to the
/// savings plan "Bausparen" — <c>fm_savings_plan_analysis</c> must report the plan's linked
/// accounts (<c>linkedAccountIds</c>) and the account-balance-based <c>accumulatedAmount</c>,
/// while <c>fm_savings_plans_manage</c> (<c>addAccountLink</c>/<c>removeAccountLink</c>)
/// maintains the link set with immediate effect.
/// </summary>
[Collection("DemoDataSerial")]
public sealed class McpSavingsPlanLinkTests : IClassFixture<TestWebApplicationFactory>
{
    private const string JwtKey = "PLEASE_REPLACE_WITH_LONG_RANDOM_256BIT_SECRET_BASE64";

    private readonly TestWebApplicationFactory _factory;

    /// <summary>Initializes the suite with a dedicated factory where MCP is enabled.</summary>
    /// <param name="factory">The shared web application factory.</param>
    public McpSavingsPlanLinkTests(TestWebApplicationFactory factory)
    {
        factory.ConfigurationOverrides["Mcp:Enabled"] = "true";
        _factory = factory;
    }

    private FinanceManager.Shared.ApiClient CreateApiClient()
    {
        var http = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
        return new FinanceManager.Shared.ApiClient(http);
    }

    /// <summary>
    /// Registers a fresh user, seeds the full demo data set for it and returns the
    /// username, user id and the ids/balances of the demo "Bausparen" plan, the linked
    /// "Bausparkonto" and an unlinked savings account of the same owner.
    /// </summary>
    /// <returns>The demo fixture identifiers and balances described in the summary.</returns>
    private async Task<(string Username, Guid UserId, Guid PlanId, Guid BausparAccountId, decimal BausparBalance, Guid OtherSavingsAccountId, decimal OtherSavingsBalance)>
        CreateUserWithDemoDataAsync()
    {
        var api = CreateApiClient();
        var username = $"mcpdemo_{Guid.NewGuid():N}";
        await api.Auth_RegisterAsync(new RegisterRequest(username, "Secret123", PreferredLanguage: null, TimeZoneId: null));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userId = await db.Users
            .Where(x => x.UserName == username)
            .Select(x => x.Id)
            .FirstAsync(TestContext.Current.CancellationToken);

        var demoDataService = scope.ServiceProvider.GetRequiredService<IDemoDataService>();
        await demoDataService.CreateDemoDataAsync(userId, true, TestContext.Current.CancellationToken);

        var plan = await db.SavingsPlans.AsNoTracking()
            .SingleAsync(x => x.OwnerUserId == userId && x.Name == "Bausparen", TestContext.Current.CancellationToken);
        var bauspar = await db.Accounts.AsNoTracking()
            .SingleAsync(x => x.OwnerUserId == userId && x.Name == "Bausparkonto", TestContext.Current.CancellationToken);
        var other = await db.Accounts.AsNoTracking()
            .SingleAsync(x => x.OwnerUserId == userId && x.Name == "Sparkonto Allgemein", TestContext.Current.CancellationToken);

        return (username, userId, plan.Id, bauspar.Id, bauspar.CurrentBalance, other.Id, other.CurrentBalance);
    }

    /// <summary>Creates an MCP access for the user and returns the plaintext PAT.</summary>
    /// <param name="client">HTTP client of the test host.</param>
    /// <param name="username">User the JWT and the access are issued for.</param>
    /// <param name="level">Access level (<see cref="FinanceManager.Domain.Mcp.McpAccessLevel"/> as short).</param>
    /// <param name="approvalMode">Approval mode (<see cref="FinanceManager.Domain.Mcp.McpApprovalMode"/> as short).</param>
    /// <returns>The plaintext personal access token.</returns>
    private async Task<string> CreateAccessTokenAsync(HttpClient client, string username, short level, short approvalMode)
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
    private async Task<JsonElement> CallToolAsync(HttpClient client, string toolName, object arguments, int id)
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

    /// <summary>
    /// Reads the single-plan <c>fm_savings_plan_analysis</c> result of the demo "Bausparen" plan.
    /// </summary>
    /// <param name="toolResult">The JSON-RPC <c>result</c> element of the tools/call response.</param>
    /// <returns>The first entry of the <c>savingsPlans</c> array in the structured content.</returns>
    private static JsonElement SinglePlan(JsonElement toolResult)
    {
        var content = toolResult.GetProperty("structuredContent");
        return content.GetProperty("savingsPlans")[0];
    }

    /// <summary>
    /// The real demo-data set ("Bausparkonto" linked to "Bausparen") drives the account-link
    /// mode of <c>fm_savings_plan_analysis</c>: <c>linkedAccountIds</c> exposes the link and
    /// <c>analysis.accumulatedAmount</c> equals the sum of the linked accounts' balances —
    /// not the sum of plan postings.
    /// </summary>
    [Fact]
    public async Task Analysis_LinkedPlan_ReportsLinkedAccountsAndAccountBasedBalance()
    {
        var (username, _, planId, bausparAccountId, bausparBalance, _, _) = await CreateUserWithDemoDataAsync();

        using var client = _factory.CreateClient();
        var pat = await CreateAccessTokenAsync(client, username, level: 0 /* Analyse */, approvalMode: 0);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", pat);

        var result = await CallToolAsync(client, "fm_savings_plan_analysis",
            new { savingsPlanId = planId.ToString() }, id: 1);
        var plan = SinglePlan(result);

        plan.GetProperty("id").GetGuid().Should().Be(planId);
        plan.GetProperty("linkedAccountIds").EnumerateArray()
            .Select(x => x.GetGuid()).Should().Equal(bausparAccountId);

        // Konto-basierter Saldo: Summe der verknüpften Kontosalden — hier genau das Bausparkonto.
        var accumulated = plan.GetProperty("analysis").GetProperty("accumulatedAmount").GetDecimal();
        accumulated.Should().Be(bausparBalance);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var planPostingSum = await db.Postings.AsNoTracking()
            .Where(p => p.SavingsPlanId == planId && p.Kind == PostingKind.SavingsPlan && p.BookingDate <= DateTime.Today)
            .SumAsync(p => (decimal?)p.Amount, TestContext.Current.CancellationToken) ?? 0m;

        // The demo data holds real plan postings (informal funding marks): the account-balance
        // source must win over the posting sum — the posting sum itself counts the informal
        // funding marks, which are only documentation under account-link mode.
        planPostingSum.Should().BeGreaterThan(0m);
        accumulated.Should().Be(bausparBalance);
    }

    /// <summary>
    /// <c>fm_savings_plans_manage</c> exposes the link maintenance over MCP:
    /// <c>addAccountLink</c> extends the link set (the balance then follows the sum of both
    /// accounts) and <c>removeAccountLink</c> restores the single-link state — each with the
    /// current <c>linkedAccountIds</c> in the tool result.
    /// </summary>
    [Fact]
    public async Task Manage_AddAndRemoveAccountLink_WorksOverMcp()
    {
        var (username, _, planId, bausparAccountId, bausparBalance, otherId, otherBalance) = await CreateUserWithDemoDataAsync();

        using var client = _factory.CreateClient();
        // Buchhalter + durable pre-approval → write-preparing ops run directly.
        var pat = await CreateAccessTokenAsync(client, username, level: 3 /* Buchhalter */, approvalMode: 1 /* PreApproved */);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", pat);

        var added = await CallToolAsync(client, "fm_savings_plans_manage",
            new { operation = "addAccountLink", savingsPlanId = planId.ToString(), accountId = otherId.ToString() }, id: 1);
        var addedContent = added.GetProperty("structuredContent");
        addedContent.GetProperty("applied").GetBoolean().Should().BeTrue();
        addedContent.GetProperty("linkedAccountIds").EnumerateArray()
            .Select(x => x.GetGuid()).Should().BeEquivalentTo(new[] { bausparAccountId, otherId });

        // accumulatedAmount now sums both linked account balances
        var result = await CallToolAsync(client, "fm_savings_plan_analysis",
            new { savingsPlanId = planId.ToString() }, id: 2);
        var plan = SinglePlan(result);
        plan.GetProperty("linkedAccountIds").EnumerateArray()
            .Select(x => x.GetGuid()).Should().BeEquivalentTo(new[] { bausparAccountId, otherId });
        plan.GetProperty("analysis").GetProperty("accumulatedAmount").GetDecimal()
            .Should().Be(bausparBalance + otherBalance);

        var removed = await CallToolAsync(client, "fm_savings_plans_manage",
            new { operation = "removeAccountLink", savingsPlanId = planId.ToString(), accountId = otherId.ToString() }, id: 3);
        var removedContent = removed.GetProperty("structuredContent");
        removedContent.GetProperty("applied").GetBoolean().Should().BeTrue();
        removedContent.GetProperty("linkedAccountIds").EnumerateArray()
            .Select(x => x.GetGuid()).Should().Equal(bausparAccountId);

        var after = SinglePlan(await CallToolAsync(client, "fm_savings_plan_analysis",
            new { savingsPlanId = planId.ToString() }, id: 4));
        after.GetProperty("analysis").GetProperty("accumulatedAmount").GetDecimal()
            .Should().Be(bausparBalance);
    }
}
