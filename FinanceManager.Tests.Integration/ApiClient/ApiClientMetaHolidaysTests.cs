using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace FinanceManager.Tests.Integration.ApiClient;

/// <summary>
/// End-to-end coverage for the meta endpoints that expose holiday provider metadata (providers, countries,
/// subdivisions) used to configure holiday calendars, verifying the ApiClient contract stays in sync with the
/// server-side lookups.
/// </summary>
public class ApiClientMetaHolidaysTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    /// <summary>Initializes the test with the shared in-memory web application factory.</summary>
    /// <param name="factory">The shared in-memory test host used to spin up API clients.</param>
    public ApiClientMetaHolidaysTests(TestWebApplicationFactory factory)
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
    /// Verifies the holiday metadata endpoints work together as a drill-down chain - providers must be
    /// non-empty, the country list must include a known country (Germany), and subdivisions can be queried
    /// for a provider/country pair without erroring, even if the result set is empty for that combination.
    /// </summary>
    [Fact]
    public async Task Meta_HolidayProviders_Countries_Subdivisions_Work()
    {
        var api = CreateClient();
        await EnsureAuthenticatedAsync(api);
        // Providers
        var providers = await api.Meta_GetHolidayProvidersAsync(TestContext.Current.CancellationToken);
        providers.Should().NotBeNull();
        providers.Should().Contain(p => !string.IsNullOrWhiteSpace(p));

        // Countries
        var countries = await api.Meta_GetHolidayCountriesAsync(TestContext.Current.CancellationToken);
        countries.Should().NotBeNull();
        countries.Should().Contain("DE");

        // Subdivisions for valid provider + country
        // Use any provider returned, assume lowercase/uppercase tolerated by API
        var provider = providers.First();
        var subs = await api.Meta_GetHolidaySubdivisionsAsync(provider, "DE", TestContext.Current.CancellationToken);
        subs.Should().NotBeNull();
        // may be empty depending on provider implementation, but call should succeed
    }
}
