using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace FinanceManager.Web.Infrastructure.Auth
{
    /// <summary>
    /// Provides access tokens based on the authentication cookie. The provider reads the JWT stored in the
    /// configured authentication cookie, validates it and returns the token string. When the token is close to
    /// expiry the provider will issue a refreshed token and set it as a response cookie.
    /// </summary>
    public sealed class JwtCookieAuthTokenProvider : IAuthTokenProvider
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IConfiguration _configuration;

        private readonly object _sync = new();
        private string? _cachedToken;
        private DateTimeOffset _cachedExpiry;

        private static readonly TimeSpan MinRenewalWindow = TimeSpan.FromMinutes(5);
        private const string AuthCookieName = "FinanceManager.Auth"; // <- zentraler Name

        /// <summary>
        /// Initializes a new instance of the <see cref="JwtCookieAuthTokenProvider"/> class.
        /// </summary>
        /// <param name="httpContextAccessor">Accessor to obtain the current HTTP context (required).</param>
        /// <param name="configuration">Application configuration used to read JWT settings (required).</param>
        /// <exception cref="ArgumentNullException">Thrown when either argument is <c>null</c>.</exception>
        public JwtCookieAuthTokenProvider(IHttpContextAccessor httpContextAccessor, IConfiguration configuration)
        {
            _httpContextAccessor = httpContextAccessor;
            _configuration = configuration;
        }

        /// <summary>
        /// Asynchronously obtains an access token string suitable for authorizing outgoing HTTP requests.
        /// The provider reads the JWT from the authentication cookie, validates it and returns the token.
        /// When the token is near expiry a refreshed token is issued and written to the response cookie;
        /// the refreshed token is returned and cached for subsequent calls.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token used to cancel the operation.</param>
        /// <returns>
        /// A task that resolves to the access token string when available; or <c>null</c> when no valid token could be obtained.
        /// </returns>
        /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled via the provided <paramref name="cancellationToken"/>.</exception>
        /// <exception cref="InvalidOperationException">May be thrown when the provider is not correctly configured (for example missing signing key) - implementations may also return <c>null</c> instead of throwing.</exception>
        public Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken)
        {
            var ctx = _httpContextAccessor.HttpContext;
            if (ctx == null)
            {
                return Task.FromResult<string?>(null);
            }

            var now = DateTimeOffset.UtcNow;

            // Determine renewal window from configured lifetime (half of it)
            var lifetimeMinutes = int.TryParse(_configuration["Jwt:LifetimeMinutes"], out var lm) ? lm : 30;
            var renewalWindow = TimeSpan.FromMinutes(Math.Max(MinRenewalWindow.TotalMinutes, lm / 2.0));

            // Cache noch gültig?
            if (_cachedToken != null && _cachedExpiry - renewalWindow > now)
            {
                return Task.FromResult<string?>(_cachedToken);
            }

            // read cookie by new name
            var cookie = ctx.Request.Cookies[AuthCookieName];
            if (string.IsNullOrEmpty(cookie))
            {
                // Kein Token vorhanden
                InvalidateCache();
                return Task.FromResult<string?>(null);
            }

            var handler = new JwtSecurityTokenHandler();
            var key = _configuration["Jwt:Key"];
            if (string.IsNullOrWhiteSpace(key))
            {
                InvalidateCache();
                return Task.FromResult<string?>(null);
            }

            try
            {
                var parameters = new TokenValidationParameters
                {
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
                    ClockSkew = TimeSpan.FromSeconds(10)
                };

                var principal = handler.ValidateToken(cookie, parameters, out var validatedToken);
                var jwt = (JwtSecurityToken)validatedToken;
                var exp = DateTimeOffset.FromUnixTimeSeconds(long.Parse(jwt.Claims.First(c => c.Type == JwtRegisteredClaimNames.Exp).Value));

                // Erneuern wenn bald ablaufend
                if (exp - renewalWindow <= now)
                {
                    var refreshed = IssueToken(principal.Claims, lifetimeMinutes);
                    SetCookie(ctx, refreshed.token, refreshed.expiry);
                    Cache(refreshed.token, refreshed.expiry);
                    return Task.FromResult<string?>(refreshed.token);
                }

                Cache(cookie, exp);
                return Task.FromResult<string?>(cookie);
            }
            catch
            {
                InvalidateCache();
                return Task.FromResult<string?>(null);
            }
        }

        /// <summary>
        /// Clears any internally cached token. Call this when the user logs out to ensure subsequent calls don't return a stale token.
        /// </summary>
        public void Clear()
        {
            InvalidateCache();
        }

        /// <summary>
        /// Issues a new JWT for the supplied claims with the configured lifetime.
        /// The returned tuple contains the serialized token and the expiry timestamp.
        /// </summary>
        /// <param name="claims">Claims to include in the token.</param>
        /// <param name="lifetimeMinutes">Lifetime in minutes for the issued token.</param>
        /// <returns>A tuple with the token string and its expiry <see cref="DateTimeOffset"/>.</returns>
        private (string token, DateTimeOffset expiry) IssueToken(IEnumerable<Claim> claims, int lifetimeMinutes)
        {
            var key = _configuration["Jwt:Key"]!;
            var expiry = DateTimeOffset.UtcNow.AddMinutes(lifetimeMinutes);

            var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
            var creds = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

            // Claims filtern: Keine doppelten exp/nbf/iat erneut hinzufügen
            var filtered = claims.Where(c =>
                c.Type != JwtRegisteredClaimNames.Exp &&
                c.Type != JwtRegisteredClaimNames.Nbf &&
                c.Type != JwtRegisteredClaimNames.Iat).ToList();

            // Ensure Admin role claim is present when principal indicates admin
            var hasRoleClaim = filtered.Any(c => c.Type == ClaimTypes.Role || string.Equals(c.Type, "role", StringComparison.OrdinalIgnoreCase));

            var jwt = new JwtSecurityToken(
                claims: filtered,
                notBefore: DateTime.UtcNow,
                expires: expiry.UtcDateTime,
                signingCredentials: creds);

            var token = new JwtSecurityTokenHandler().WriteToken(jwt);
            return (token, expiry);
        }

        /// <summary>
        /// Writes the authentication cookie to the response.
        /// </summary>
        /// <param name="ctx">Current HTTP context.</param>
        /// <param name="token">Token string to set in the cookie.</param>
        /// <param name="expiry">Expiry timestamp for the cookie.</param>
        private void SetCookie(HttpContext ctx, string token, DateTimeOffset expiry)
        {
            ctx.Response.Cookies.Append(AuthCookieName, token, new CookieOptions
            {
                HttpOnly = true,
                Secure = ctx.Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                Expires = expiry,
                Path = "/"
            });
        }

        /// <summary>
        /// Caches the token and its expiry in-memory for quick subsequent access.
        /// </summary>
        private void Cache(string token, DateTimeOffset expiry)
        {
            lock (_sync)
            {
                _cachedToken = token;
                _cachedExpiry = expiry;
            }
        }

        /// <summary>
        /// Invalidates any cached token.
        /// </summary>
        private void InvalidateCache()
        {
            lock (_sync)
            {
                _cachedToken = null;
                _cachedExpiry = DateTimeOffset.MinValue;
            }
        }
    }
}