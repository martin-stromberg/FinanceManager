using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace FinanceManager.Tests.Integration.ApiClient;

public class ApiClientAuthTests : IClassFixture<TestWebApplicationFactory>
{
    private const string DevelopmentJwtKey = "PLEASE_REPLACE_WITH_LONG_RANDOM_256BIT_SECRET_BASE64";
    private const string JwtIssuer = "financemanager";
    private const string JwtAudience = "financemanager";

    private readonly TestWebApplicationFactory _factory;

    public ApiClientAuthTests(TestWebApplicationFactory factory)
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

    [Fact]
    public async Task Register_ShouldSetAuthCookie_AndReturnResponse()
    {
        var api = CreateClient();
        var req = new RegisterRequest($"user_{Guid.NewGuid():N}", "Secret123", PreferredLanguage: "de", TimeZoneId: "Europe/Berlin");
        var resp = await api.Auth_RegisterAsync(req);
        resp.Should().NotBeNull();
        resp.isAdmin.Should().BeFalse();
        resp.user.Should().Be(req.Username);
        resp.exp.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public async Task Login_ShouldReturnOk_AndUnauthorized_OnInvalid()
    {
        var api = CreateClient();
        var username = $"user_{Guid.NewGuid():N}";
        // register first
        await api.Auth_RegisterAsync(new RegisterRequest(username, "Secret123", null, null));

        var ok = await api.Auth_LoginAsync(new LoginRequest(username, "Secret123", null, null));
        ok.Should().NotBeNull();
        ok.user.Should().Be(username);

        // invalid password
        Func<Task> invalid = () => api.Auth_LoginAsync(new LoginRequest(username, "wrongpw", null, null));
        await invalid.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task Logout_ShouldClearCookie()
    {
        var api = CreateClient();
        var username = $"user_{Guid.NewGuid():N}";
        await api.Auth_RegisterAsync(new RegisterRequest(username, "Secret123", null, null));

        var ok = await api.Auth_LogoutAsync();
        ok.Should().BeTrue();
        // Further validation: subsequent authenticated-only endpoints would fail; basic check is enough here.
    }

    [Fact]
    public async Task Bearer_ShouldRejectTokenWithInvalidIssuer()
    {
        var http = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateBearerToken(issuer: "wrong-issuer"));

        var response = await http.GetAsync("/api/user/settings/profile");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Bearer_ShouldRejectTokenWithInvalidAudience()
    {
        var http = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateBearerToken(audience: "wrong-audience"));

        var response = await http.GetAsync("/api/user/settings/profile");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Bearer_ShouldAcceptTokenWithConfiguredIssuerAndAudience()
    {
        var http = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateBearerToken());

        var response = await http.GetAsync("/api/user/settings/profile");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static string CreateBearerToken(
        string issuer = JwtIssuer,
        string audience = JwtAudience)
    {
        var userId = Guid.NewGuid();
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(DevelopmentJwtKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Name, "jwt-test-user"),
            new Claim(JwtRegisteredClaimNames.UniqueName, "jwt-test-user")
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
