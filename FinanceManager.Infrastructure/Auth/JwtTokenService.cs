using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace FinanceManager.Infrastructure.Auth;

/// <summary>
/// Service capable of creating signed JSON Web Tokens (JWT) for authenticated users.
/// </summary>
public interface IJwtTokenService
{
    /// <summary>
    /// Creates a signed JWT for the specified user.
    /// </summary>
    /// <param name="userId">The unique identifier of the user for whom the token is created.</param>
    /// <param name="username">The username to include in the token claims.</param>
    /// <param name="isAdmin">Flag indicating whether the user has administrator privileges; when <c>true</c> a role claim is added.</param>
    /// <param name="expiresUtc">Output parameter receiving the token expiration time in UTC.</param>
    /// <param name="preferredLanguage">Optional preferred language code to include as a custom claim.</param>
    /// <param name="timeZoneId">Optional time zone identifier to include as a custom claim.</param>
    /// <returns>The serialized JWT as string.</returns>
    string CreateToken(Guid userId, string username, bool isAdmin, out DateTime expiresUtc, string? preferredLanguage = null, string? timeZoneId = null);
}

/// <summary>
/// Default implementation of <see cref="IJwtTokenService"/> that reads signing configuration from <see cref="IConfiguration"/> and
/// produces HMAC-SHA256 signed tokens.
/// </summary>
public sealed class JwtTokenService : IJwtTokenService
{
    private readonly IConfiguration _config;

    /// <summary>
    /// Initializes a new instance of the <see cref="JwtTokenService"/> class.
    /// </summary>
    /// <param name="config">Application configuration used to read JWT signing settings (e.g. Jwt:Key, Jwt:Issuer).</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="config"/> is <c>null</c>.</exception>
    public JwtTokenService(IConfiguration config) => _config = config ?? throw new ArgumentNullException(nameof(config));

    /// <summary>
    /// Creates a signed JWT for the specified user and returns the token string.
    /// </summary>
    /// <param name="userId">The user's unique identifier to include in the token subject claim.</param>
    /// <param name="username">The user's username to include in the token claims.</param>
    /// <param name="isAdmin">When <c>true</c>, an "Admin" role claim is included in the token.</param>
    /// <param name="expiresUtc">Output parameter that will contain the token expiration time in UTC.</param>
    /// <param name="preferredLanguage">Optional user preferred language to include as the custom claim "pref_lang".</param>
    /// <param name="timeZoneId">Optional user time zone id to include as the custom claim "tz".</param>
    /// <returns>The serialized JWT string.</returns>
    /// <exception cref="InvalidOperationException">Thrown when required configuration value "Jwt:Key" is missing.</exception>
    public string CreateToken(Guid userId, string username, bool isAdmin, out DateTime expiresUtc, string? preferredLanguage = null, string? timeZoneId = null)
    {
        var key = _config["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key missing");
        var issuer = _config["Jwt:Issuer"] ?? "financemanager";
        var audience = _config["Jwt:Audience"] ?? issuer;
        var lifetimeMinutes = int.TryParse(_config["Jwt:LifetimeMinutes"], out var lm) ? lm : 30;
        expiresUtc = DateTime.UtcNow.AddMinutes(lifetimeMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(ClaimTypes.NameIdentifier, userId.ToString()), // ensure claim recognized by CurrentUserService
            new(ClaimTypes.Name, username),
            new(JwtRegisteredClaimNames.UniqueName, username)
        };
        if (isAdmin)
        {
            // use standard role claim instead of custom is_admin flag
            claims.Add(new Claim(ClaimTypes.Role, "Admin"));
        }
        if (!string.IsNullOrWhiteSpace(preferredLanguage))
        {
            claims.Add(new Claim("pref_lang", preferredLanguage));
        }
        if (!string.IsNullOrWhiteSpace(timeZoneId))
        {
            claims.Add(new Claim("tz", timeZoneId));
        }
        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(issuer, audience, claims, notBefore: DateTime.UtcNow, expires: expiresUtc, signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
