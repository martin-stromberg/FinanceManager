using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FinanceManager.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Playwright;

namespace FinanceManager.Tests.E2E;

public sealed class TestAuthCookieHelper
{
    private const string DevelopmentJwtKey = "PLEASE_REPLACE_WITH_LONG_RANDOM_256BIT_SECRET_BASE64";
    private const string JwtIssuer = "financemanager";
    private const string JwtAudience = "financemanager";
    private const string AuthCookieName = "FinanceManager.Auth";

    private readonly string _databasePath;
    private readonly string _baseUrl;

    public TestAuthCookieHelper(string databasePath, string baseUrl)
    {
        _databasePath = databasePath;
        _baseUrl = baseUrl;
    }

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
