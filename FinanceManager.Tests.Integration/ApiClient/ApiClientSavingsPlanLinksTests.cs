using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace FinanceManager.Tests.Integration.ApiClient;

/// <summary>
/// End-to-end coverage for the savings-plan account-link endpoints and the actual-balance report
/// endpoint: link set/list/remove, the immediate switch of <c>accumulatedAmount</c> to the linked
/// account balances, and the "Istmeldung" correction posting which is rejected for linked plans.
/// </summary>
public class ApiClientSavingsPlanLinksTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    /// <summary>Initializes the test with the shared in-memory web application factory.</summary>
    /// <param name="factory">The shared in-memory test host used to spin up API clients.</param>
    public ApiClientSavingsPlanLinksTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private FinanceManager.Shared.ApiClient CreateClient()
    {
        var http = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
        return new FinanceManager.Shared.ApiClient(http);
    }

    private static async Task EnsureAuthenticatedAsync(FinanceManager.Shared.ApiClient api)
    {
        var username = $"user_{Guid.NewGuid():N}";
        await api.Auth_RegisterAsync(new RegisterRequest(username, "Secret123", PreferredLanguage: null, TimeZoneId: null));
    }

    /// <summary>
    /// Links an account to a plan, verifies listing, the analysis source switch and the removal
    /// fallback - the core REST contract of the multi-link feature.
    /// </summary>
    [Fact]
    public async Task SavingsPlanAccountLinks_Set_List_Remove_AndAnalysisSwitch()
    {
        var api = CreateClient();
        await EnsureAuthenticatedAsync(api);

        var plan = await api.SavingsPlans_CreateAsync(
            new FinanceManager.Shared.Dtos.SavingsPlans.SavingsPlanCreateRequest("Plan", FinanceManager.Shared.Dtos.SavingsPlans.SavingsPlanType.Open, null, null, null, null, null),
            TestContext.Current.CancellationToken);
        var account = await api.CreateAccountAsync(
            new AccountCreateRequest("Vertragskonto", AccountType.Savings, null, null, "Bank", null, SavingsPlanExpectation.None),
            TestContext.Current.CancellationToken);

        // an actual-balance report first so accumulated differs from the (empty) account balance
        var report = await api.SavingsPlans_ReportActualBalanceAsync(plan.Id, new FinanceManager.Shared.Dtos.SavingsPlans.SavingsPlanActualReportRequest(100m), TestContext.Current.CancellationToken);
        report.Should().NotBeNull();
        report!.AccumulatedAmount.Should().Be(100m);

        var linkedIds = await api.SavingsPlans_GetLinkedAccountsAsync(plan.Id, TestContext.Current.CancellationToken);
        linkedIds.Should().NotBeNull().And.BeEmpty();

        var added = await api.SavingsPlans_AddAccountLinkAsync(plan.Id, account.Id, TestContext.Current.CancellationToken);
        added.Should().BeTrue();

        linkedIds = await api.SavingsPlans_GetLinkedAccountsAsync(plan.Id, TestContext.Current.CancellationToken);
        linkedIds.Should().ContainSingle().Which.Should().Be(account.Id);

        // accumulated now follows the (empty) linked account balance, not the posting sum
        var analysis = await api.SavingsPlans_AnalyzeAsync(plan.Id, TestContext.Current.CancellationToken);
        analysis.AccumulatedAmount.Should().Be(0m);

        // actual-balance reports are rejected for linked plans
        var rejected = await api.SavingsPlans_ReportActualBalanceAsync(plan.Id, new FinanceManager.Shared.Dtos.SavingsPlans.SavingsPlanActualReportRequest(50m), TestContext.Current.CancellationToken);
        rejected.Should().BeNull();
        api.LastError.Should().NotBeNullOrEmpty();

        var removed = await api.SavingsPlans_RemoveAccountLinkAsync(plan.Id, account.Id, TestContext.Current.CancellationToken);
        removed.Should().BeTrue();

        // back to the posting-based evaluation after removing the last link
        analysis = await api.SavingsPlans_AnalyzeAsync(plan.Id, TestContext.Current.CancellationToken);
        analysis.AccumulatedAmount.Should().Be(100m);
    }

    /// <summary>
    /// Error handling of the link endpoints: unknown plans yield null/not-found semantics and a
    /// duplicate link is rejected.
    /// </summary>
    [Fact]
    public async Task SavingsPlanAccountLinks_Duplicate_AndNotFound()
    {
        var api = CreateClient();
        await EnsureAuthenticatedAsync(api);

        var plan = await api.SavingsPlans_CreateAsync(
            new FinanceManager.Shared.Dtos.SavingsPlans.SavingsPlanCreateRequest("Plan", FinanceManager.Shared.Dtos.SavingsPlans.SavingsPlanType.Open, null, null, null, null, null),
            TestContext.Current.CancellationToken);
        var account = await api.CreateAccountAsync(
            new AccountCreateRequest("Konto", AccountType.Savings, null, null, "Bank", null, SavingsPlanExpectation.None),
            TestContext.Current.CancellationToken);

        var missing = await api.SavingsPlans_GetLinkedAccountsAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);
        missing.Should().BeNull();

        (await api.SavingsPlans_AddAccountLinkAsync(plan.Id, account.Id, TestContext.Current.CancellationToken)).Should().BeTrue();
        // duplicate link rejected
        (await api.SavingsPlans_AddAccountLinkAsync(plan.Id, account.Id, TestContext.Current.CancellationToken)).Should().BeFalse();
        api.LastError.Should().NotBeNullOrEmpty();

        // removing a non-existing link yields false
        (await api.SavingsPlans_RemoveAccountLinkAsync(plan.Id, Guid.NewGuid(), TestContext.Current.CancellationToken)).Should().BeFalse();
    }
}
