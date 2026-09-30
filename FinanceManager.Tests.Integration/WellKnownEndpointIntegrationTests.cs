using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace FinanceManager.Tests.Integration;

/// <summary>
/// End-to-end coverage for the well-known endpoints: the anonymous
/// <c>/.well-known/change-password</c> redirect (default and admin-configured targets) and the
/// admin-only settings round trip including the non-admin authorization rejection.
/// </summary>
public sealed class WellKnownEndpointIntegrationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    /// <summary>
    /// Initializes the test with the shared <see cref="TestWebApplicationFactory"/>, which hosts the
    /// application in-memory for the duration of the test class.
    /// </summary>
    /// <param name="factory">The shared in-memory application host injected by xUnit's class fixture.</param>
    public WellKnownEndpointIntegrationTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CreateHttpClient()
        => _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private FinanceManager.Shared.ApiClient CreateApiClient()
        => new(CreateHttpClient());

    /// <summary>
    /// Verifies that the anonymous well-known endpoint answers with an HTTP 302 redirect to the
    /// default change-password page — the machine-readable discovery entry point that password
    /// managers resolve before any session exists.
    /// </summary>
    [Fact]
    public async Task WellKnownChangePassword_ReturnsRedirect()
    {
        var http = CreateHttpClient();

        var response = await http.GetAsync("/.well-known/change-password", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.OriginalString.Should().Be("/change-password");
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().BeEmpty();
    }

    /// <summary>
    /// Verifies the admin settings round trip: an authenticated admin can read and update the
    /// configured change-password URL and the public endpoint then redirects to the configured target.
    /// </summary>
    [Fact]
    public async Task WellKnownSettings_AdminRoundtrip()
    {
        var api = CreateApiClient();
        await api.Auth_LoginAsync(new LoginRequest(TestWebApplicationFactory.BootstrapAdminUsername, TestWebApplicationFactory.BootstrapAdminPassword, null, null), TestContext.Current.CancellationToken);

        try
        {
            await api.UpdateWellKnownSettingsAsync(new WellKnownSettingsUpdateRequest("/account/password"), TestContext.Current.CancellationToken);

            var dto = await api.GetWellKnownSettingsAsync(TestContext.Current.CancellationToken);
            dto.Should().NotBeNull();
            dto!.ChangePasswordUrl.Should().Be("/account/password");

            var http = CreateHttpClient();
            var response = await http.GetAsync("/.well-known/change-password", TestContext.Current.CancellationToken);
            response.StatusCode.Should().Be(HttpStatusCode.Redirect);
            response.Headers.Location.Should().NotBeNull();
            response.Headers.Location!.OriginalString.Should().Be("/account/password");
        }
        finally
        {
            // Restore the default so the shared test database does not leak the configured
            // target into other tests of this class.
            await api.UpdateWellKnownSettingsAsync(new WellKnownSettingsUpdateRequest("/change-password"), CancellationToken.None);
        }
    }

    /// <summary>
    /// Verifies that a registered non-admin user is rejected with 403 when attempting to update
    /// the well-known configuration.
    /// </summary>
    [Fact]
    public async Task WellKnownSettings_NonAdminUpdate_Returns403()
    {
        var userApi = CreateApiClient();
        var username = $"user_{Guid.NewGuid():N}";
        await userApi.Auth_RegisterAsync(new RegisterRequest(username, "Secret123", null, null), TestContext.Current.CancellationToken);
        Func<Task> forbidden = () => userApi.UpdateWellKnownSettingsAsync(new WellKnownSettingsUpdateRequest("/other"), TestContext.Current.CancellationToken);
        var exception = await forbidden.Should().ThrowAsync<HttpRequestException>();
        exception.Which.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
