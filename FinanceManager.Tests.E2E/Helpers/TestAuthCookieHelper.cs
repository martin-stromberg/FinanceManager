using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FinanceManager.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Playwright;

namespace FinanceManager.Tests.E2E;

/// <summary>
/// Mints and injects a real, signed auth JWT directly into the Playwright browser context - bypassing the
/// login flow entirely - so E2E tests can exercise near-expiry/renewal and session-boundary behavior
/// without waiting out the actual token lifetime.
/// </summary>
public sealed class TestAuthCookieHelper
{
    private const string DevelopmentJwtKey = "PLEASE_REPLACE_WITH_LONG_RANDOM_256BIT_SECRET_BASE64";
    private const string JwtIssuer = "financemanager";
    private const string JwtAudience = "financemanager";
    private const string AuthCookieName = "FinanceManager.Auth";

    private readonly string _databasePath;
    private readonly string _baseUrl;

    /// <summary>
    /// Initializes a new instance of the <see cref="TestAuthCookieHelper"/> class.
    /// </summary>
    /// <param name="databasePath">Path to the SQLite database backing the test server, used to look up the target user.</param>
    /// <param name="baseUrl">Base URL of the application under test, used as the cookie's target URL.</param>
    public TestAuthCookieHelper(string databasePath, string baseUrl)
    {
        _databasePath = databasePath;
        _baseUrl = baseUrl;
    }

    /// <summary>
    /// Injects an auth cookie for <paramref name="username"/> that expires in one minute - used to test
    /// behavior around session expiry without an actual long wait.
    /// </summary>
    /// <param name="page">Browser context to inject the cookie into.</param>
    /// <param name="username">Username the minted token is issued for.</param>
    public async Task SetNearExpiryCookieAsync(IPage page, string username)
    {
        var expiresUtc = DateTimeOffset.UtcNow.AddMinutes(1);
        var token = await CreateTokenAsync(username, expiresUtc.UtcDateTime);
        await page.Context.AddCookiesAsync(new[]
        {
            new Cookie
            {
                Name = AuthCookieName,
                Value = token,
                Url = _baseUrl,
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteAttribute.Lax,
                Expires = expiresUtc.ToUnixTimeSeconds()
            }
        });
    }

    private async Task<string> CreateTokenAsync(string username, DateTime expiresUtc)
    {
        await using var db = CreateContext();
        var user = await db.Users.SingleAsync(u => u.UserName == username);
        var isAdmin = await db.Roles
            .Join(db.UserRoles, role => role.Id, userRole => userRole.RoleId, (role, userRole) => new { role, userRole })
            .AnyAsync(x => x.userRole.UserId == user.Id && x.role.Name == "Admin");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(DevelopmentJwtKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.UserName!),
            new(JwtRegisteredClaimNames.UniqueName, user.UserName!),
            new("security_stamp", user.SecurityStamp!)
        };

        if (isAdmin)
        {
            claims.Add(new Claim(ClaimTypes.Role, "Admin"));
        }

        var token = new JwtSecurityToken(
            issuer: JwtIssuer,
            audience: JwtAudience,
            claims: claims,
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: expiresUtc,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={_databasePath}")
            .Options;

        return new AppDbContext(options);
    }
}
