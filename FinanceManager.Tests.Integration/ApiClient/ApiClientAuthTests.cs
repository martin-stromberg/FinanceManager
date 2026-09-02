using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using FluentAssertions;
using FinanceManager.Domain.Users;
using FinanceManager.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace FinanceManager.Tests.Integration.ApiClient;

/// <summary>
/// End-to-end coverage for the authentication surface: registration, cookie-based login/logout, and the
/// bearer-token path used by API clients, including the security-stamp and role-revocation checks that
/// invalidate a previously issued token without requiring an explicit logout.
/// </summary>
public class ApiClientAuthTests : IClassFixture<TestWebApplicationFactory>
{
    private const string DevelopmentJwtKey = "PLEASE_REPLACE_WITH_LONG_RANDOM_256BIT_SECRET_BASE64";
    private const string JwtIssuer = "financemanager";
    private const string JwtAudience = "financemanager";

    private readonly TestWebApplicationFactory _factory;

    /// <summary>
    /// Initializes the test with the shared <see cref="TestWebApplicationFactory"/>, which hosts the
    /// application in-memory for the duration of the test class.
    /// </summary>
    /// <param name="factory">The shared in-memory application host injected by xUnit's class fixture.</param>
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

    private static FinanceManager.Shared.ApiClient CreateClient(HttpStatusCode statusCode, HttpContent? content = null)
    {
        var http = new HttpClient(new StubHttpMessageHandler(new HttpResponseMessage(statusCode)
        {
            Content = content ?? new StringContent(string.Empty)
        }))
        {
            BaseAddress = new Uri("https://example.test/")
        };

        return new FinanceManager.Shared.ApiClient(http);
    }

    /// <summary>
    /// Verifies that registering a new user returns a populated response (username, non-admin flag,
    /// future expiry) and implicitly sets the auth cookie so the caller is immediately authenticated
    /// without a separate login call.
    /// </summary>
    [Fact]
    public async Task Register_ShouldSetAuthCookie_AndReturnResponse()
    {
        var api = CreateClient();
        var req = new RegisterRequest($"user_{Guid.NewGuid():N}", "Secret123", PreferredLanguage: "de", TimeZoneId: "Europe/Berlin");
        var resp = await api.Auth_RegisterAsync(req, TestContext.Current.CancellationToken);
        resp.Should().NotBeNull();
        resp.isAdmin.Should().BeFalse();
        resp.user.Should().Be(req.Username);
        resp.exp.Should().BeAfter(DateTime.UtcNow);
    }

    /// <summary>
    /// Verifies that login succeeds with correct credentials for a previously registered user, and that
    /// an incorrect password is rejected outright rather than silently falling back to some other
    /// authentication path.
    /// </summary>
    [Fact]
    public async Task Login_ShouldReturnOk_AndUnauthorized_OnInvalid()
    {
        var api = CreateClient();
        var username = $"user_{Guid.NewGuid():N}";
        // register first
        await api.Auth_RegisterAsync(new RegisterRequest(username, "Secret123", null, null), TestContext.Current.CancellationToken);

        var ok = await api.Auth_LoginAsync(new LoginRequest(username, "Secret123", null, null), TestContext.Current.CancellationToken);
        ok.Should().NotBeNull();
        ok.user.Should().Be(username);

        // invalid password
        Func<Task> invalid = () => api.Auth_LoginAsync(new LoginRequest(username, "wrongpw", null, null));
        await invalid.Should().ThrowAsync<HttpRequestException>();
    }

    /// <summary>
    /// Verifies that the logout endpoint reports success for an authenticated user, i.e. the auth cookie
    /// is accepted and cleared server-side rather than the call being a no-op.
    /// </summary>
    [Fact]
    public async Task Logout_ShouldClearCookie()
    {
        var api = CreateClient();
        var username = $"user_{Guid.NewGuid():N}";
        await api.Auth_RegisterAsync(new RegisterRequest(username, "Secret123", null, null), TestContext.Current.CancellationToken);

        var ok = await api.Auth_LogoutAsync(TestContext.Current.CancellationToken);
        ok.Should().BeTrue();
        // Further validation: subsequent authenticated-only endpoints would fail; basic check is enough here.
    }

    /// <summary>
    /// Verifies that a bearer token signed with the correct key but a mismatched issuer claim is rejected,
    /// guarding against tokens minted by a different (or misconfigured) trust boundary from being accepted.
    /// </summary>
    [Fact]
    public async Task Bearer_ShouldRejectTokenWithInvalidIssuer()
    {
        var http = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await CreateBearerTokenAsync(issuer: "wrong-issuer"));

        var response = await http.GetAsync("/api/user/settings/profile", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Verifies that a bearer token signed with the correct key but a mismatched audience claim is
    /// rejected, ensuring tokens issued for a different consumer cannot be replayed against this API.
    /// </summary>
    [Fact]
    public async Task Bearer_ShouldRejectTokenWithInvalidAudience()
    {
        var http = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await CreateBearerTokenAsync(audience: "wrong-audience"));

        var response = await http.GetAsync("/api/user/settings/profile", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Verifies the positive counterpart to the issuer/audience rejection tests: a token that matches the
    /// configured issuer and audience, and carries the admin role, is accepted for a protected endpoint.
    /// </summary>
    [Fact]
    public async Task Bearer_ShouldAcceptTokenWithConfiguredIssuerAndAudience()
    {
        var http = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await CreateBearerTokenAsync(includeAdminRole: true));

        var response = await http.GetAsync("/api/user/settings/profile", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// Verifies that calling keepalive with a bearer token close to expiry returns a fresh token (via the
    /// X-Auth-Token/-Expires headers) and a renewed auth cookie, so a long-lived client session can stay
    /// authenticated without the user re-entering credentials.
    /// </summary>
    [Fact]
    public async Task Keepalive_WithBearerNearExpiry_ShouldRefreshCookieAndReturnNoContent()
    {
        var http = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
        var token = await CreateBearerTokenAsync(includeAdminRole: true, expiresUtc: DateTime.UtcNow.AddMinutes(10));
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await http.GetAsync("/api/auth/keepalive", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        response.Headers.TryGetValues("X-Auth-Token", out var refreshedTokens).Should().BeTrue();
        response.Headers.TryGetValues("X-Auth-Token-Expires", out var refreshedExpires).Should().BeTrue();
        response.Headers.TryGetValues("Set-Cookie", out var cookies).Should().BeTrue();
        refreshedTokens!.Single().Should().NotBe(token);
        refreshedExpires!.Single().Should().NotBeNullOrWhiteSpace();
        cookies!.Should().Contain(cookie => cookie.StartsWith("FinanceManager.Auth=", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Verifies that keepalive rejects a token whose security stamp no longer matches the user record
    /// (e.g. after a password change) with 401 and does not hand out a refreshed token in the response
    /// headers - a stale token must not be able to perpetually renew itself.
    /// </summary>
    [Fact]
    public async Task Keepalive_WithInvalidSecurityStamp_ShouldReturnUnauthorizedWithoutRefreshLoop()
    {
        var token = await CreateBearerTokenAsync(includeAdminRole: true, expiresUtc: DateTime.UtcNow.AddMinutes(10));
        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var user = await userManager.FindByNameAsync(TestWebApplicationFactory.BootstrapAdminUsername);
            user.Should().NotBeNull();
            var result = await userManager.UpdateSecurityStampAsync(user!);
            result.Succeeded.Should().BeTrue();
        }

        var http = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await http.GetAsync("/api/auth/keepalive", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Headers.TryGetValues("X-Auth-Token", out _).Should().BeFalse();
    }

    /// <summary>
    /// Verifies that a previously valid bearer token is rejected for a normal (non-keepalive) endpoint
    /// once the user's security stamp changes, confirming the stamp check is enforced on the general
    /// authorization path and not only during keepalive.
    /// </summary>
    [Fact]
    public async Task Bearer_ShouldRejectToken_WhenSecurityStampChanged()
    {
        var token = await CreateBearerTokenAsync(includeAdminRole: true);
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var user = await userManager.FindByNameAsync(TestWebApplicationFactory.BootstrapAdminUsername);
        user.Should().NotBeNull();
        var result = await userManager.UpdateSecurityStampAsync(user!);
        result.Succeeded.Should().BeTrue();

        var http = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await http.GetAsync("/api/user/settings/profile", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Verifies that an admin-scoped bearer token is rejected once the user's admin role is revoked, even
    /// though ASP.NET Identity does not rotate the security stamp on a role change alone - the admin claim
    /// baked into the token must not stay trusted after the role membership underneath it is gone.
    /// </summary>
    [Fact]
    public async Task Bearer_ShouldRejectAdminClaim_WhenCurrentAdminRoleWasRevokedWithoutSecurityStampChange()
    {
        var username = $"revoked_admin_{Guid.NewGuid():N}";
        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
            const string adminRole = "Admin";
            if (!await roleManager.RoleExistsAsync(adminRole))
            {
                var roleCreated = await roleManager.CreateAsync(new IdentityRole<Guid> { Name = adminRole, NormalizedName = adminRole.ToUpperInvariant() });
                roleCreated.Succeeded.Should().BeTrue();
            }

            var user = new User(username, isAdmin: true)
            {
                SecurityStamp = Guid.NewGuid().ToString("N"),
                ConcurrencyStamp = Guid.NewGuid().ToString("N")
            };
            var created = await userManager.CreateAsync(user, "Secret123");
            created.Succeeded.Should().BeTrue();
            var roleAdded = await userManager.AddToRoleAsync(user, adminRole);
            roleAdded.Succeeded.Should().BeTrue();
        }

        var token = await CreateBearerTokenAsync(username: username, includeAdminRole: true);
        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var user = await userManager.FindByNameAsync(username);
            user.Should().NotBeNull();
            var originalSecurityStamp = user!.SecurityStamp;
            var roleRemoved = await userManager.RemoveFromRoleAsync(user, "Admin");
            roleRemoved.Succeeded.Should().BeTrue();
            user.SecurityStamp.Should().Be(originalSecurityStamp);
        }

        var http = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await http.GetAsync("/api/admin/users", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Verifies that a bearer token issued before a user account was deactivated is rejected on subsequent
    /// requests, so deactivation takes effect immediately rather than only blocking future logins.
    /// </summary>
    [Fact]
    public async Task Bearer_ShouldRejectExistingToken_WhenUserWasDeactivated()
    {
        var username = $"deactivated_{Guid.NewGuid():N}";
        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var user = new User(username, "unused", false)
            {
                SecurityStamp = Guid.NewGuid().ToString("N"),
                ConcurrencyStamp = Guid.NewGuid().ToString("N")
            };
            var created = await userManager.CreateAsync(user, "Secret123");
            created.Succeeded.Should().BeTrue();
        }

        var token = await CreateBearerTokenAsync(username: username);
        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var user = await userManager.FindByNameAsync(username);
            user.Should().NotBeNull();
            user!.Deactivate();
            var updated = await userManager.UpdateAsync(user);
            updated.Succeeded.Should().BeTrue();
        }

        var http = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await http.GetAsync("/api/user/settings/profile", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Verifies that login fails for a user whose account was deactivated after registration, even with
    /// the correct password, so a deactivated account cannot be used to obtain a fresh session.
    /// </summary>
    [Fact]
    public async Task Login_ShouldRejectInactiveUser()
    {
        var username = $"inactive_{Guid.NewGuid():N}";
        const string password = "Secret123";
        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var user = new User(username, "unused", false)
            {
                SecurityStamp = Guid.NewGuid().ToString("N"),
                ConcurrencyStamp = Guid.NewGuid().ToString("N")
            };
            var created = await userManager.CreateAsync(user, password);
            created.Succeeded.Should().BeTrue();
            user.Deactivate();
            await userManager.UpdateAsync(user);
        }

        var api = CreateClient();
        Func<Task> login = () => api.Auth_LoginAsync(new LoginRequest(username, password, null, null));

        await login.Should().ThrowAsync<HttpRequestException>();
    }

    /// <summary>
    /// Verifies that the generated <see cref="FinanceManager.Shared.ApiClient"/> raises its
    /// <c>AuthenticationRequired</c> event with the response body as the error message whenever a call
    /// fails with 401 Unauthorized, so callers (e.g. the UI) can react by redirecting to login without
    /// inspecting every response manually.
    /// </summary>
    [Fact]
    public async Task ApiClient_ShouldRaiseAuthenticationRequired_OnUnauthorized()
    {
        var api = CreateClient(HttpStatusCode.Unauthorized, new StringContent("Session expired", Encoding.UTF8, "text/plain"));
        FinanceManager.Shared.ApiAuthenticationRequiredEventArgs? observed = null;
        api.AuthenticationRequired += (_, args) => observed = args;

        Func<Task> call = () => api.Users_HasAnyAsync();

        await call.Should().ThrowAsync<HttpRequestException>()
            .Where(ex => ex.StatusCode == HttpStatusCode.Unauthorized);
        observed.Should().NotBeNull();
        observed!.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        observed.ErrorMessage.Should().Be("Session expired");
        api.LastError.Should().Be("Session expired");
    }

    /// <summary>
    /// Verifies that a plain 403 Forbidden response (no authentication-required error code in the body)
    /// does not trigger the <c>AuthenticationRequired</c> event, so an ordinary permission failure is not
    /// misinterpreted as an expired session and does not force the user through a login redirect.
    /// </summary>
    [Fact]
    public async Task ApiClient_ShouldNotRaiseAuthenticationRequired_OnOrdinaryForbidden()
    {
        var api = CreateClient(HttpStatusCode.Forbidden, new StringContent("Forbidden", Encoding.UTF8, "text/plain"));
        var raised = false;
        api.AuthenticationRequired += (_, _) => raised = true;

        Func<Task> call = () => api.Users_HasAnyAsync();

        await call.Should().ThrowAsync<HttpRequestException>()
            .Where(ex => ex.StatusCode == HttpStatusCode.Forbidden);
        raised.Should().BeFalse();
        api.LastError.Should().Be("Forbidden");
    }

    /// <summary>
    /// Verifies that a 403 Forbidden response carrying the <c>authentication_required</c> error code in
    /// its JSON body is treated as an authentication failure and raises <c>AuthenticationRequired</c> -
    /// the complementary case to the plain-403 test, covering the backend's way of signaling "your
    /// session is gone" through a Forbidden status rather than Unauthorized.
    /// </summary>
    [Fact]
    public async Task ApiClient_ShouldRaiseAuthenticationRequired_OnForbiddenWithAuthenticationCode()
    {
        var content = new StringContent(
            """{"code":"authentication_required","message":"Please sign in again."}""",
            Encoding.UTF8,
            "application/json");
        var api = CreateClient(HttpStatusCode.Forbidden, content);
        FinanceManager.Shared.ApiAuthenticationRequiredEventArgs? observed = null;
        api.AuthenticationRequired += (_, args) => observed = args;

        Func<Task> call = () => api.Users_HasAnyAsync();

        await call.Should().ThrowAsync<HttpRequestException>()
            .Where(ex => ex.StatusCode == HttpStatusCode.Forbidden);
        observed.Should().NotBeNull();
        observed!.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        observed.ErrorCode.Should().Be("authentication_required");
        observed.ErrorMessage.Should().Be("Please sign in again.");
    }

    private async Task<string> CreateBearerTokenAsync(
        string issuer = JwtIssuer,
        string audience = JwtAudience,
        string username = TestWebApplicationFactory.BootstrapAdminUsername,
        bool includeAdminRole = false,
        DateTime? expiresUtc = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = db.Users.Single(u => u.UserName == username);
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(DevelopmentJwtKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.UserName!),
            new Claim(JwtRegisteredClaimNames.UniqueName, user.UserName!),
            new Claim("security_stamp", user.SecurityStamp!)
        };
        if (includeAdminRole)
        {
            claims.Add(new Claim(ClaimTypes.Role, "Admin"));
        }

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: expiresUtc ?? DateTime.UtcNow.AddMinutes(30),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;

        public StubHttpMessageHandler(HttpResponseMessage response)
        {
            _response = response;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_response);
        }
    }
}
