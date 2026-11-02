using System.Net;
using System.Text.RegularExpressions;
using Xunit;

namespace FinanceManager.Tests.Integration;

/// <summary>
/// Integration coverage for the cookie-CSRF middleware: unsafe cookie-authenticated requests to
/// authorized endpoints require a valid antiforgery token, while bearer-authenticated requests
/// and anonymous endpoints stay untouched.
/// </summary>
public sealed partial class CookieCsrfProtectionTests : IClassFixture<TestWebApplicationFactory>
{
    private const string JwtKey = "PLEASE_REPLACE_WITH_LONG_RANDOM_256BIT_SECRET_BASE64";

    private readonly TestWebApplicationFactory _factory;

    /// <summary>Initializes the test with the shared in-memory application host.</summary>
    /// <param name="factory">The shared web application factory.</param>
    public CookieCsrfProtectionTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    /// <summary>An unsafe cookie-authenticated request without antiforgery token is rejected with 403.</summary>
    [Fact]
    public async Task CookieAuth_UnsafeRequest_WithoutToken_IsForbidden()
    {
        var token = TestJwtTokens.Create(_factory.Services, JwtKey, includeAdminRole: true);
        using var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false
        });
        client.DefaultRequestHeaders.Add("Cookie", $"FinanceManager.Auth={token}");

        using var response = await client.PostAsync("/api/statement-drafts/classify", null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>An unsafe bearer-authenticated request needs no antiforgery token.</summary>
    [Fact]
    public async Task Bearer_UnsafeRequest_WithoutToken_IsAllowed()
    {
        var token = TestJwtTokens.Create(_factory.Services, JwtKey, includeAdminRole: true);
        using var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        using var response = await client.PostAsync("/api/statement-drafts/classify", null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    /// <summary>A cookie-authenticated request carrying the meta-tag antiforgery token passes validation.</summary>
    [Fact]
    public async Task CookieAuth_WithToken_IsAllowed()
    {
        var token = TestJwtTokens.Create(_factory.Services, JwtKey, includeAdminRole: true);
        using var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        client.DefaultRequestHeaders.Add("Cookie", $"FinanceManager.Auth={token}");

        using var page = await client.GetAsync("/", TestContext.Current.CancellationToken);
        var html = await page.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var match = XsrfMetaRegex().Match(html);
        Assert.True(match.Success, "the prerendered page should carry the fm-xsrf-token meta tag");
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/statement-drafts/classify");
        request.Headers.Add("RequestVerificationToken", match.Groups[1].Value);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    /// <summary>An anonymous endpoint is never blocked by antiforgery validation.</summary>
    [Fact]
    public async Task Anonymous_UnsafeRequest_WithoutToken_IsNotForbidden()
    {
        using var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false
        });

        using var response = await client.PostAsync("/api/auth/logout", null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [GeneratedRegex("name=\"fm-xsrf-token\"\\s+content=\"([^\"]+)\"")]
    private static partial Regex XsrfMetaRegex();
}
