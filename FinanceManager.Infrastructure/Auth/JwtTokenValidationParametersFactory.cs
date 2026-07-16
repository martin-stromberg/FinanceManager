using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace FinanceManager.Infrastructure.Auth;

/// <summary>
/// Creates consistent token validation parameters for JWT bearer and cookie token validation.
/// </summary>
public sealed class JwtTokenValidationParametersFactory
{
    private readonly IOptions<JwtOptions> _options;

    /// <summary>
    /// Initializes a new instance of <see cref="JwtTokenValidationParametersFactory"/>.
    /// </summary>
    /// <param name="options">Validated JWT options.</param>
    public JwtTokenValidationParametersFactory(IOptions<JwtOptions> options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Creates validation parameters using the current configured JWT issuer, audience and signing key.
    /// </summary>
    /// <returns>Token validation parameters for HMAC signed JWTs.</returns>
    public TokenValidationParameters Create()
    {
        var jwt = _options.Value;
        if (string.IsNullOrWhiteSpace(jwt.Key))
        {
            throw new InvalidOperationException("Jwt:Key missing");
        }

        return new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ClockSkew = TimeSpan.FromSeconds(10)
        };
    }
}
