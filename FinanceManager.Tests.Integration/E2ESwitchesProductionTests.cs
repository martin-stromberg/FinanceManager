using System.Net;
using Xunit;

namespace FinanceManager.Tests.Integration;

/// <summary>
/// Integration coverage proving the E2E configuration switches (HTTPS-redirection toggle and
/// account-statistics fault injection) have no effect when the host runs outside Development.
/// </summary>
public sealed class E2ESwitchesProductionTests
{
    private const string ProductionJwtKey = "test-signing-key-with-sufficient-length-1234567890";

    private static TestWebApplicationFactory CreateProductionFactory()
    {
        var factory = new TestWebApplicationFactory();
        factory.HostEnvironment = "Production";
        factory.ConfigurationOverrides["Jwt:Key"] = ProductionJwtKey;
        // TestServer has no HTTPS endpoint to derive the redirect port from - pin it so
        // UseHttpsRedirection can actually emit a redirect instead of logging and passing through.
        factory.ConfigurationOverrides["https_port"] = "443";
        // The test host only trusts loopback as a reverse proxy so tests can emulate the
        // production reverse proxy via X-Forwarded-Proto.
        factory.ConfigurationOverrides["ReverseProxy:KnownProxies"] = "127.0.0.1;::1";
        factory.ConfigurationOverrides["ReverseProxy:KnownNetworks"] = "127.0.0.0/8;::1/128";
        return factory;
    }

    /// <summary>In a production-like environment the HTTPS-redirection E2E switch is ignored.</summary>
    [Fact]
    public async Task DisableHttpsRedirection_Ignored_InProduction()
    {
        using var factory = CreateProductionFactory();
        factory.ConfigurationOverrides["E2E:DisableHttpsRedirection"] = "true";
        using var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using var response = await client.GetAsync("/api/users/exists", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.TemporaryRedirect, response.StatusCode);
    }

    /// <summary>In a production-like environment the account-statistics fault injection is ignored.</summary>
    [Fact]
    public async Task FaultInjection_Ignored_InProduction()
    {
        var faultFile = Path.Combine(Path.GetTempPath(), $"fm-fault-{Guid.NewGuid():N}.flag");
        await File.WriteAllTextAsync(faultFile, "fail", TestContext.Current.CancellationToken);
        try
        {
            using var factory = CreateProductionFactory();
            factory.ConfigurationOverrides["E2E:AccountStatisticsFaultInjectionEnabled"] = "true";
            factory.ConfigurationOverrides["E2E:AccountStatisticsFaultFile"] = faultFile;
            using var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });
            var token = TestJwtTokens.Create(factory.Services, ProductionJwtKey, includeAdminRole: true);
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            // Simulate a request that already arrived via HTTPS at the reverse proxy.
            client.DefaultRequestHeaders.Add("X-Forwarded-Proto", "https");

            using var response = await client.GetAsync("/api/accounts/statistics", TestContext.Current.CancellationToken);
            var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.DoesNotContain("E2E_AccountStatisticsFault", body, StringComparison.Ordinal);
        }
        finally
        {
            if (File.Exists(faultFile))
            {
                File.Delete(faultFile);
            }
        }
    }
}
