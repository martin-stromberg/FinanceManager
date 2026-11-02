using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FinanceManager.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace FinanceManager.Tests.Integration;

/// <summary>
/// Shared JWT builder for integration tests: mints a signed bearer token for a user looked up in the
/// test database, mirroring the claims (<c>sub</c>, name identifier, name, unique name, security
/// stamp, optional Admin role) that the application's token provider emits.
/// </summary>
public static class TestJwtTokens
{
    /// <summary>Issuer matching the <c>Jwt:Issuer</c> configuration used by the application.</summary>
    public const string DefaultIssuer = "financemanager";

    /// <summary>Audience matching the <c>Jwt:Audience</c> configuration used by the application.</summary>
    public const string DefaultAudience = "financemanager";

    /// <summary>
    /// Creates a signed JWT for the named user. The user is resolved through a scoped
    /// <see cref="AppDbContext"/> from <paramref name="services"/>, so subject, name and
    /// security-stamp claims always reflect the current database state.
    /// </summary>
    /// <param name="services">Service provider of the running <see cref="TestWebApplicationFactory"/>.</param>
    /// <param name="signingKey">Symmetric signing key the token is signed with (UTF-8 bytes).</param>
    /// <param name="username">User the token is issued for; defaults to the seeded bootstrap admin.</param>
    /// <param name="issuer">Token issuer claim value.</param>
    /// <param name="audience">Token audience claim value.</param>
    /// <param name="includeAdminRole">Whether to add an <c>Admin</c> role claim.</param>
    /// <param name="expiresUtc">Token expiry; defaults to 30 minutes from now.</param>
    /// <returns>The serialized JWT, ready for a <c>Bearer</c> authorization header or auth cookie.</returns>
    public static string Create(
        IServiceProvider services,
        string signingKey,
        string username = TestWebApplicationFactory.BootstrapAdminUsername,
        string issuer = DefaultIssuer,
        string audience = DefaultAudience,
        bool includeAdminRole = false,
        DateTime? expiresUtc = null)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = db.Users.Single(u => u.UserName == username);
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
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
}
