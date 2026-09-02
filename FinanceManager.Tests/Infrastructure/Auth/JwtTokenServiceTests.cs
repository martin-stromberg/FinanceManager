using System.IdentityModel.Tokens.Jwt;
using FinanceManager.Infrastructure.Auth;
using Microsoft.Extensions.Options;

namespace FinanceManager.Tests.Infrastructure.Auth;

/// <summary>
/// Verifies that <see cref="JwtTokenService.CreateToken"/> stamps issued tokens with the values from
/// <see cref="JwtOptions"/> and embeds the security stamp claim that <see cref="FinanceManager.Web.Infrastructure.Auth.JwtRefreshService"/> later relies on
/// to detect stale tokens after a security-relevant change to the account.
/// </summary>
public sealed class JwtTokenServiceTests
{
    /// <summary>
    /// Verifies that the issuer and audience configured via <see cref="JwtOptions"/> end up on the emitted JWT -
    /// a misconfiguration here would let tokens be accepted by, or rejected from, the wrong audience.
    /// </summary>
    [Fact]
    public void CreateToken_ShouldUseConfiguredIssuerAndAudience()
    {
        var options = Options.Create(new JwtOptions
        {
            Key = "test-signing-key-with-sufficient-length-1234567890",
            Issuer = "configured-issuer",
            Audience = "configured-audience",
            LifetimeMinutes = 30
        });
        var sut = new JwtTokenService(options);

        var token = sut.CreateToken(Guid.NewGuid(), "test-user", false, "stamp-123", out _);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.Equal("configured-issuer", jwt.Issuer);
        Assert.Contains("configured-audience", jwt.Audiences);
    }

    /// <summary>
    /// Verifies that the token carries the caller's security stamp as a "security_stamp" claim - the claim that
    /// <see cref="FinanceManager.Web.Infrastructure.Auth.JwtRefreshService"/> compares against the user's current stamp on refresh to invalidate tokens
    /// issued before a password change or role revocation.
    /// </summary>
    [Fact]
    public void CreateToken_ShouldIncludeSecurityStamp()
    {
        var options = Options.Create(new JwtOptions
        {
            Key = "test-signing-key-with-sufficient-length-1234567890",
            Issuer = "configured-issuer",
            Audience = "configured-audience",
            LifetimeMinutes = 30
        });
        var sut = new JwtTokenService(options);

        var token = sut.CreateToken(Guid.NewGuid(), "test-user", false, "stamp-123", out _);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.Contains(jwt.Claims, c => c.Type == "security_stamp" && c.Value == "stamp-123");
    }
}
