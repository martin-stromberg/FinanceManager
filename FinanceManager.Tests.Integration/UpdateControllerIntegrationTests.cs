using System.Net;
using System.Net.Http.Json;
using FinanceManager.Shared.Dtos.Update;
using FluentAssertions;
using Xunit;

namespace FinanceManager.Tests.Integration;

public sealed class UpdateControllerIntegrationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public UpdateControllerIntegrationTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Health_IsAnonymous()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UpdateStatus_RequiresAdmin()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/setup/update/status");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateSettings_RoundTripsForAdmin()
    {
        var client = _factory.CreateClient();
        await AuthenticateAdminAsync(client);

        var update = new UpdateSettingsUpdateRequest(
            true,
            15,
            "martin-stromberg",
            "FinanceManager",
            "update.json",
            new TimeOnly(3, 30),
            "FinanceManagerService",
            "financemanager",
            null,
            "updates",
            120);

        var put = await client.PutAsJsonAsync("/api/setup/update/settings", update);
        put.EnsureSuccessStatusCode();
        var settings = await put.Content.ReadFromJsonAsync<UpdateSettingsDto>();

        settings!.Enabled.Should().BeTrue();
        settings.CheckIntervalMinutes.Should().Be(15);
        settings.RepositoryOwner.Should().Be("martin-stromberg");
    }

    private static async Task AuthenticateAdminAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            username = TestWebApplicationFactory.BootstrapAdminUsername,
            password = TestWebApplicationFactory.BootstrapAdminPassword
        });
        response.EnsureSuccessStatusCode();
    }
}
