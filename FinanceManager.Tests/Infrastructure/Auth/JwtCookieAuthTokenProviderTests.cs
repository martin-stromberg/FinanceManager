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

/// <summary>
/// Guards the cookie-to-access-token resolution logic in <see cref="JwtCookieAuthTokenProvider"/>: preferring the
/// live request cookie over any cached token so a stale token from a different browser tab or user session is
/// never served, falling back to an in-memory cache when no HTTP request context is available (for example inside
/// a running Blazor circuit), rejecting tokens whose issuer or audience does not match configuration, and
/// transparently refreshing tokens that are close to expiry via <see cref="IJwtRefreshService"/> so callers never
/// observe an about-to-expire access token.
/// </summary>
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

    /// <summary>
    /// Builds an <see cref="HttpContext"/> whose incoming request carries the given JWT in the application's
    /// "FinanceManager.Auth" authentication cookie, so tests can exercise cookie-based token resolution without
    /// standing up a real ASP.NET Core request pipeline.
    /// </summary>
    /// <param name="token">Serialized JWT to place in the request cookie.</param>
    /// <returns>An <see cref="HttpContext"/> carrying the token as the auth cookie.</returns>
    private static HttpContext CreateHttpContextWithCookie(string token)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Cookie = $"FinanceManager.Auth={token}";
        return context;
    }

    /// <summary>
    /// Verifies that when the cached access token is close enough to its expiry to fall inside the provider's
    /// renewal window, the provider silently exchanges it for a freshly issued token via
    /// <see cref="IJwtRefreshService"/> rather than handing the caller a token that is about to stop working.
    /// This is what allows long-lived browser sessions to stay authenticated without the user re-logging in.
    /// </summary>
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

    /// <summary>
    /// Verifies that when a near-expiry token cannot be refreshed (for example because the refresh service
    /// determines the underlying user or session is no longer valid), the provider surfaces this as a
    /// <c>null</c> access token instead of quietly returning the old, soon-to-expire cookie value. This prevents a
    /// revoked or invalidated session from continuing to authorize requests just because the cached token had not
    /// technically expired yet.
    /// </summary>
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

    /// <summary>
    /// Builds a <see cref="JwtCookieAuthTokenProvider"/> configured with the fixed signing key, issuer, and
    /// audience that <see cref="CreateToken"/> uses, so tokens created by that helper validate successfully
    /// against the provider under test. Accepts an optional refresh service mock so scenarios that depend on
    /// refresh outcomes (success, rejection) can be exercised without a real refresh implementation.
    /// </summary>
    /// <param name="accessor">HTTP context accessor supplying (or withholding) the current request.</param>
    /// <param name="refreshService">Optional refresh service; a permissive mock is used when omitted.</param>
    /// <returns>A provider instance ready to be exercised by the test.</returns>
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

    /// <summary>
    /// Issues a signed JWT equivalent to what <see cref="JwtTokenService"/> would produce, letting callers pick
    /// the subject, expiry, issuer, and audience — including intentionally wrong issuer/audience values — so
    /// tests can drive both the happy path and the validation-rejection paths of the token provider under test.
    /// </summary>
    /// <param name="subject">Value placed in the JWT "sub" claim.</param>
    /// <param name="expiresUtc">UTC expiry timestamp to embed in the token.</param>
    /// <param name="issuer">Issuer to embed in the token; defaults to the value the provider expects.</param>
    /// <param name="audience">Audience to embed in the token; defaults to the value the provider expects.</param>
    /// <returns>A serialized, signed JWT string.</returns>
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
