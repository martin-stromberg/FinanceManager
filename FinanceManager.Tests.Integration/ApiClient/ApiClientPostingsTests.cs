using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace FinanceManager.Tests.Integration.ApiClient;

/// <summary>
/// End-to-end coverage for the postings query endpoints (group links, per-account/contact/savings-plan/security
/// listings), verifying the ApiClient can call them successfully even for a fresh user with no owned data.
/// </summary>
public class ApiClientPostingsTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    /// <summary>Initializes the test with the shared in-memory web application factory.</summary>
    /// <param name="factory">The shared in-memory test host used to spin up API clients.</param>
    public ApiClientPostingsTests(TestWebApplicationFactory factory)
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

    private async Task EnsureAuthenticatedAsync(FinanceManager.Shared.ApiClient api)
    {
        var username = $"user_{Guid.NewGuid():N}";
        await api.Auth_RegisterAsync(new RegisterRequest(username, "Secret123", PreferredLanguage: null, TimeZoneId: null));
    }

    /// <summary>
    /// Verifies that requesting group links for a non-existent posting (<see cref="Guid.Empty"/>) returns null
    /// rather than throwing - guarding the "posting has no group" edge case that a naive lookup could
    /// otherwise turn into an unhandled exception.
    /// </summary>
    [Fact]
    public async Task Postings_GroupLinks_Should_Return_Null_For_Empty()
    {
        var api = CreateClient();
        await EnsureAuthenticatedAsync(api);
        var links = await api.Postings_GetGroupLinksAsync(Guid.Empty, TestContext.Current.CancellationToken);
        links.Should().BeNull();
    }

    /// <summary>
    /// Verifies that the various posting-listing endpoints (by account, contact, savings plan, security) all
    /// return an empty-but-non-null collection for random ids owned by nobody, rather than erroring - a smoke
    /// test that these query endpoints degrade gracefully instead of failing on missing/foreign entities.
    /// </summary>
    [Fact]
    public async Task Postings_List_Endpoints_Should_Not_Fail()
    {
        var api = CreateClient();
        await EnsureAuthenticatedAsync(api);

        // These will likely return empty due to no owned entities, but should succeed.
        var accList = await api.Postings_GetAccountAsync(Guid.NewGuid(), ct: TestContext.Current.CancellationToken);
        accList.Should().NotBeNull();

        var conList = await api.Postings_GetContactAsync(Guid.NewGuid(), ct: TestContext.Current.CancellationToken);
        conList.Should().NotBeNull();

        var spList = await api.Postings_GetSavingsPlanAsync(Guid.NewGuid(), ct: TestContext.Current.CancellationToken);
        spList.Should().NotBeNull();

        var secList = await api.Postings_GetSecurityAsync(Guid.NewGuid(), ct: TestContext.Current.CancellationToken);
        secList.Should().NotBeNull();
    }
}
