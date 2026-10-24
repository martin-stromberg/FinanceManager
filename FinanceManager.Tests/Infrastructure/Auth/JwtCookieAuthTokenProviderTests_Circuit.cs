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

/// <summary>Checks long-lived circuit state, identity boundaries and late refresh responses.</summary>
public sealed class JwtCookieAuthTokenProviderTests_Circuit
{
    private static readonly IOptions<JwtOptions> Options = Microsoft.Extensions.Options.Options.Create(new JwtOptions
    {
        Key = "test-signing-key-with-sufficient-length-1234567890", Issuer = "financemanager", Audience = "financemanager", LifetimeMinutes = 30
    });

    /// <summary>A valid circuit token remains usable inside the renewal window.</summary>
    [Fact]
    public async Task GetAccessTokenAsync_CachedTokenInsideRenewalWindow_RemainsUsable()
    {
        var sut = CreateProvider();
        var token = CreateToken("user-a", DateTime.UtcNow.AddMinutes(10));
        sut.PersistedToken = token;

        var actual = await sut.GetAccessTokenAsync(CancellationToken.None);

        Assert.Equal(token, actual);
    }

    /// <summary>A response from another user cannot overwrite circuit authentication.</summary>
    [Fact]
    public async Task AcceptBrowserToken_DifferentUser_KeepsCurrentToken()
    {
        var sut = CreateProvider();
        var token = CreateToken("user-a", DateTime.UtcNow.AddMinutes(10));
        sut.PersistedToken = token;

        sut.AcceptBrowserToken(CreateToken("user-b", DateTime.UtcNow.AddMinutes(30)));

        Assert.Equal(token, await sut.GetAccessTokenAsync(CancellationToken.None));
    }

    /// <summary>Out-of-order responses cannot roll authentication back to an older expiry.</summary>
    [Fact]
    public async Task AcceptBrowserToken_OlderResponse_KeepsNewerToken()
    {
        var sut = CreateProvider();
        var token = CreateToken("user-a", DateTime.UtcNow.AddMinutes(30));
        sut.PersistedToken = token;

        sut.AcceptBrowserToken(CreateToken("user-a", DateTime.UtcNow.AddMinutes(10)));

        Assert.Equal(token, await sut.GetAccessTokenAsync(CancellationToken.None));
    }

    /// <summary>An invalidated circuit cannot be resurrected by a delayed refresh response.</summary>
    [Fact]
    public async Task AcceptBrowserToken_AfterInvalidation_DoesNotRestoreToken()
    {
        var sut = CreateProvider();
        sut.PersistedToken = CreateToken("user-a", DateTime.UtcNow.AddMinutes(10));
        sut.InvalidateCache();

        sut.AcceptBrowserToken(CreateToken("user-a", DateTime.UtcNow.AddMinutes(30)));

        Assert.Null(await sut.GetAccessTokenAsync(CancellationToken.None));
    }

    /// <summary>A valid renewed token replaces the circuit token.</summary>
    [Fact]
    public async Task AcceptBrowserToken_SameUserAndLaterExpiry_UpdatesCircuitToken()
    {
        var sut = CreateProvider();
        sut.PersistedToken = CreateToken("user-a", DateTime.UtcNow.AddMinutes(10));
        var renewed = CreateToken("user-a", DateTime.UtcNow.AddMinutes(30));

        sut.AcceptBrowserToken(renewed);

        Assert.Equal(renewed, await sut.GetAccessTokenAsync(CancellationToken.None));
    }

    /// <summary>Persisted state still uses ordinary JWT lifetime validation.</summary>
    [Fact]
    public async Task PersistedToken_ExpiredToken_IsRejected()
    {
        var sut = CreateProvider();

        sut.PersistedToken = CreateToken("user-a", DateTime.UtcNow.AddMinutes(-1));

        Assert.Null(await sut.GetAccessTokenAsync(CancellationToken.None));
    }

    private static JwtCookieAuthTokenProvider CreateProvider() => new(new HttpContextAccessor(), Options,
        new JwtTokenValidationParametersFactory(Options), Mock.Of<IJwtRefreshService>());

    private static string CreateToken(string subject, DateTime expires) => new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
        Options.Value.Issuer, Options.Value.Audience, [new Claim(JwtRegisteredClaimNames.Sub, subject)],
        notBefore: DateTime.UtcNow.AddMinutes(-2), expires: expires,
        signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Options.Value.Key)), SecurityAlgorithms.HmacSha256)));
}
