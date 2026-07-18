using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FinanceManager.Infrastructure.Auth;
using FinanceManager.Web.Infrastructure.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Moq;

namespace FinanceManager.Tests.Infrastructure.Auth;

public sealed class JwtCookieAuthTokenProviderTests
{
    private const string JwtKey = "test-signing-key-with-sufficient-length-1234567890";
    private const string JwtIssuer = "financemanager";
    private const string JwtAudience = "financemanager";

    /// <summary>
    /// Ensures that an available request cookie takes precedence over a still-valid cached token.
    /// </summary>
    [Fact]
    public async Task GetAccessTokenAsync_ShouldPreferRequestCookie_WhenCacheContainsDifferentToken()
    {
        // Arrange
        var accessor = new HttpContextAccessor();
        var sut = CreateProvider(accessor);

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
        var sut = CreateProvider(accessor);

        var token = CreateToken("user-a", DateTime.UtcNow.AddMinutes(120));
        accessor.HttpContext = CreateHttpContextWithCookie(token);
        _ = await sut.GetAccessTokenAsync(CancellationToken.None);

        accessor.HttpContext = null;

        // Act
        var actual = await sut.GetAccessTokenAsync(CancellationToken.None);

        // Assert
        Assert.Equal(token, actual);
    }

    /// <summary>
    /// Ensures that cookie JWTs with an unexpected issuer are rejected.
    /// </summary>
    [Fact]
    public async Task GetAccessTokenAsync_ShouldReturnNull_WhenIssuerIsInvalid()
    {
        // Arrange
        var accessor = new HttpContextAccessor();
        var sut = CreateProvider(accessor);

        var token = CreateToken("user-a", DateTime.UtcNow.AddMinutes(120), issuer: "wrong-issuer");
        accessor.HttpContext = CreateHttpContextWithCookie(token);

        // Act
        var actual = await sut.GetAccessTokenAsync(CancellationToken.None);

        // Assert
        Assert.Null(actual);
    }

    /// <summary>
    /// Ensures that cookie JWTs with an unexpected audience are rejected.
    /// </summary>
    [Fact]
    public async Task GetAccessTokenAsync_ShouldReturnNull_WhenAudienceIsInvalid()
    {
        // Arrange
        var accessor = new HttpContextAccessor();
        var sut = CreateProvider(accessor);

        var token = CreateToken("user-a", DateTime.UtcNow.AddMinutes(120), audience: "wrong-audience");
        accessor.HttpContext = CreateHttpContextWithCookie(token);

        // Act
        var actual = await sut.GetAccessTokenAsync(CancellationToken.None);

        // Assert
        Assert.Null(actual);
    }

    private static HttpContext CreateHttpContextWithCookie(string token)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Cookie = $"FinanceManager.Auth={token}";
        return context;
    }

    [Fact]
    public async Task GetAccessTokenAsync_ShouldUseRefreshService_WhenTokenNearExpiry()
    {
        var accessor = new HttpContextAccessor();
        var refresh = new Mock<IJwtRefreshService>();
        var refreshedExpiry = DateTime.UtcNow.AddMinutes(30);
        refresh.Setup(r => r.RefreshAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(JwtRefreshResult.Success("new-token", refreshedExpiry));
        var sut = CreateProvider(accessor, refresh.Object);

        var token = CreateToken("user-a", DateTime.UtcNow.AddMinutes(1));
        accessor.HttpContext = CreateHttpContextWithCookie(token);

        var actual = await sut.GetAccessTokenAsync(CancellationToken.None);

        Assert.Equal("new-token", actual);
        refresh.Verify(r => r.RefreshAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAccessTokenAsync_ShouldReturnNull_WhenRefreshIsRejected()
    {
        var accessor = new HttpContextAccessor();
        var refresh = new Mock<IJwtRefreshService>();
        refresh.Setup(r => r.RefreshAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(JwtRefreshResult.Fail("inactive"));
        var sut = CreateProvider(accessor, refresh.Object);

        var token = CreateToken("user-a", DateTime.UtcNow.AddMinutes(1));
        accessor.HttpContext = CreateHttpContextWithCookie(token);

        var actual = await sut.GetAccessTokenAsync(CancellationToken.None);

        Assert.Null(actual);
    }

    private static JwtCookieAuthTokenProvider CreateProvider(HttpContextAccessor accessor, IJwtRefreshService? refreshService = null)
    {
        var options = Options.Create(new JwtOptions
        {
            Key = JwtKey,
            Issuer = JwtIssuer,
            Audience = JwtAudience,
            LifetimeMinutes = 30
        });
        var validationParametersFactory = new JwtTokenValidationParametersFactory(options);
        refreshService ??= Mock.Of<IJwtRefreshService>();
        return new JwtCookieAuthTokenProvider(accessor, options, validationParametersFactory, refreshService);
    }

    private static string CreateToken(
        string subject,
        DateTime expiresUtc,
        string issuer = JwtIssuer,
        string audience = JwtAudience)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: new[] { new Claim(JwtRegisteredClaimNames.Sub, subject) },
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: expiresUtc,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
