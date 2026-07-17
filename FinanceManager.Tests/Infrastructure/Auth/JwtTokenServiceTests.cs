using System.IdentityModel.Tokens.Jwt;
using FinanceManager.Infrastructure.Auth;
using Microsoft.Extensions.Options;

namespace FinanceManager.Tests.Infrastructure.Auth;

public sealed class JwtTokenServiceTests
{
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
