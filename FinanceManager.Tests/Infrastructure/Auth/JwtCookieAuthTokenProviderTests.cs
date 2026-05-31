using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FinanceManager.Web.Infrastructure.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace FinanceManager.Tests.Infrastructure.Auth;

public sealed class JwtCookieAuthTokenProviderTests
{
    private const string JwtKey = "test-signing-key-with-sufficient-length-1234567890";

    /// <summary>
    /// Ensures that an available request cookie takes precedence over a still-valid cached token.
    /// </summary>
    [Fact]
    public async Task GetAccessTokenAsync_ShouldPreferRequestCookie_WhenCacheContainsDifferentToken()
    {
        // Arrange
        var accessor = new HttpContextAccessor();
        var config = CreateConfiguration();
        var sut = new JwtCookieAuthTokenProvider(accessor, config);

        var firstToken = CreateToken("user-a", DateTime.UtcNow.AddMinutes(120));
        accessor.HttpContext = CreateHttpContextWithCookie(firstToken);
        _ = await sut.GetAccessTokenAsync(CancellationToken.None);

        var secondToken = CreateToken("user-b", DateTime.UtcNow.AddMinutes(120));
        accessor.HttpContext = CreateHttpContextWithCookie(secondToken);

        // Act
        var actual = await sut.GetAccessTokenAsync(CancellationToken.None);

        // Assert
        Assert.Equal(secondToken, actual);
    }

    /// <summary>
    /// Ensures that the provider can continue using a valid cached token when no HTTP context is available.
    /// </summary>
    [Fact]
    public async Task GetAccessTokenAsync_ShouldReturnCachedToken_WhenHttpContextIsUnavailable()
    {
        // Arrange
        var accessor = new HttpContextAccessor();
        var config = CreateConfiguration();
        var sut = new JwtCookieAuthTokenProvider(accessor, config);

        var token = CreateToken("user-a", DateTime.UtcNow.AddMinutes(120));
        accessor.HttpContext = CreateHttpContextWithCookie(token);
        _ = await sut.GetAccessTokenAsync(CancellationToken.None);

        accessor.HttpContext = null;

        // Act
        var actual = await sut.GetAccessTokenAsync(CancellationToken.None);

        // Assert
        Assert.Equal(token, actual);
    }

    private static IConfiguration CreateConfiguration()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = JwtKey,
                ["Jwt:LifetimeMinutes"] = "30"
            })
            .Build();
    }

    private static HttpContext CreateHttpContextWithCookie(string token)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Cookie = $"FinanceManager.Auth={token}";
        return context;
    }

    private static string CreateToken(string subject, DateTime expiresUtc)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            claims: new[] { new Claim(JwtRegisteredClaimNames.Sub, subject) },
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: expiresUtc,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
