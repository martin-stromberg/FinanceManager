using Xunit;

namespace FinanceManager.Tests.Integration;

/// <summary>
/// Integration coverage for the global security response headers applied to every request and
/// the stricter help-surface Content-Security-Policy that keeps winning on help routes.
/// </summary>
public sealed class SecurityHeadersTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    /// <summary>Initializes the test with the shared in-memory application host.</summary>
    /// <param name="factory">The shared web application factory.</param>
    public SecurityHeadersTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    /// <summary>Every response carries the global baseline security headers.</summary>
    [Fact]
    public async Task Responses_IncludeGlobalSecurityHeaders()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/api/users/exists", TestContext.Current.CancellationToken);

        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
        Assert.Equal("strict-origin-when-cross-origin", Assert.Single(response.Headers.GetValues("Referrer-Policy")));
        Assert.Equal("SAMEORIGIN", Assert.Single(response.Headers.GetValues("X-Frame-Options")));
        Assert.True(response.Headers.Contains("Permissions-Policy"));
        var csp = Assert.Single(response.Headers.GetValues("Content-Security-Policy"));
        Assert.Contains("default-src 'self'", csp, StringComparison.Ordinal);
        Assert.Contains("frame-ancestors 'self'", csp, StringComparison.Ordinal);
    }

    /// <summary>The global CSP deliberately allows LAN-HTTP deployments by omitting upgrade-insecure-requests.</summary>
    [Fact]
    public async Task Csp_HasNoUpgradeInsecureRequests()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/api/users/exists", TestContext.Current.CancellationToken);

        var csp = Assert.Single(response.Headers.GetValues("Content-Security-Policy"));
        Assert.DoesNotContain("upgrade-insecure-requests", csp, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Help routes keep the stricter help CSP without inline allowances.</summary>
    [Fact]
    public async Task HelpPaths_KeepStricterCsp()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/help", TestContext.Current.CancellationToken);

        var csp = Assert.Single(response.Headers.GetValues("Content-Security-Policy"));
        Assert.Contains("script-src 'self'", csp, StringComparison.Ordinal);
        Assert.DoesNotContain("'unsafe-inline'", csp, StringComparison.Ordinal);
        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
    }
}
