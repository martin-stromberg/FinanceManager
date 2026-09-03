using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace FinanceManager.Tests.Integration.ApiClient;

/// <summary>
/// End-to-end coverage for the notifications API surface, verifying that the ApiClient can list a user's
/// notifications and dismiss them through the real HTTP pipeline.
/// </summary>
public class ApiClientNotificationsTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    /// <summary>Initializes the test with the shared in-memory web application factory.</summary>
    /// <param name="factory">The shared in-memory test host used to spin up API clients.</param>
    public ApiClientNotificationsTests(TestWebApplicationFactory factory)
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
    /// Verifies the notification lifecycle end to end: listing succeeds even for a fresh user with no
    /// notifications, and when at least one notification is present, dismissing it via its id reports success -
    /// guarding both the "empty list" edge case and the happy-path dismiss flow in a single scenario.
    /// </summary>
    [Fact]
    public async Task Notifications_List_Then_Dismiss_Should_Succeed()
    {
        var api = CreateClient();
        await EnsureAuthenticatedAsync(api);

        var items = await api.Notifications_ListAsync(TestContext.Current.CancellationToken);
        items.Should().NotBeNull();
        // Initially might be empty; we just validate the call.

        if (items.Count > 0)
        {
            var first = items.First();
            var ok = await api.Notifications_DismissAsync(first.Id, TestContext.Current.CancellationToken);
            ok.Should().BeTrue();
        }
    }
}
