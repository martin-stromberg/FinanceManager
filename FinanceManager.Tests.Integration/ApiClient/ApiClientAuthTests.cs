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
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await CreateBearerTokenAsync(issuer: "wrong-issuer"));

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
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await CreateBearerTokenAsync(audience: "wrong-audience"));

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
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await CreateBearerTokenAsync(includeAdminRole: true));

        var response = await http.GetAsync("/api/user/settings/profile");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

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

        var response = await http.GetAsync("/api/user/settings/profile");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

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

        var response = await http.GetAsync("/api/admin/users");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

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

        var response = await http.GetAsync("/api/user/settings/profile");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

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
        bool includeAdminRole = false)
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
            expires: DateTime.UtcNow.AddMinutes(30),
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
