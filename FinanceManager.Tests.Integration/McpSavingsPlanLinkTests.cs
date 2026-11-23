using FinanceManager.Application.Demo;
using FinanceManager.Infrastructure;
using FinanceManager.Shared;
using FinanceManager.Shared.Dtos.Postings;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Headers;
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
public sealed class McpSavingsPlanLinkTests : McpIntegrationTestBase
{
    /// <summary>Initializes the suite with a dedicated factory where MCP is enabled.</summary>
    /// <param name="factory">The shared web application factory.</param>
    public McpSavingsPlanLinkTests(TestWebApplicationFactory factory) : base(factory)
    {
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
