using FinanceManager.Infrastructure.Auth;
using Microsoft.Extensions.Options;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Components;

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
        private readonly IOptions<JwtOptions> _options;
        private readonly JwtTokenValidationParametersFactory _validationParametersFactory;
        private readonly IJwtRefreshService _refreshService;
        private readonly CircuitAuthTokenStore _tokenStore;

        private readonly object _sync = new();
        private string? _cachedToken;
        private DateTimeOffset _cachedExpiry;

        private const string AuthCookieName = "FinanceManager.Auth"; // <- zentraler Name

        /// <summary>
        /// Opaque single-use key transferred through the persistent component state between the
        /// prerendered request and the interactive circuit. The key resolves the cached token
        /// server-side via <see cref="CircuitAuthTokenStore"/> so no token is embedded in the page.
        /// </summary>
        [PersistentState]
        public string? PersistedTokenKey
        {
            get => _cachedToken is null ? null : _tokenStore.StoreForHandoff(_cachedToken, _cachedExpiry);
            set
            {
                if (string.IsNullOrWhiteSpace(value)) { InvalidateCache(); return; }
                if (!_tokenStore.TryTakeHandoff(value, out var token, out _) || string.IsNullOrWhiteSpace(token))
                {
                    InvalidateCache();
                    return;
                }

                try
                {
                    var principal = new JwtSecurityTokenHandler().ValidateToken(token, _validationParametersFactory.Create(), out var validated);
                    var expiry = new DateTimeOffset(validated.ValidTo, TimeSpan.Zero);
                    Cache(token, expiry);
                    var subject = principal.FindFirstValue(ClaimTypes.NameIdentifier)
                        ?? principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
                    if (Guid.TryParse(subject, out var userId))
                    {
                        _tokenStore.SetCurrent(userId, token, expiry);
                    }
                }
                catch (Microsoft.IdentityModel.Tokens.SecurityTokenException) { InvalidateCache(); }
                catch (ArgumentException) { InvalidateCache(); }
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="JwtCookieAuthTokenProvider"/> class.
        /// </summary>
        /// <param name="httpContextAccessor">Accessor to obtain the current HTTP context (required).</param>
        /// <param name="options">Validated JWT settings.</param>
        /// <param name="validationParametersFactory">Factory for shared JWT validation parameters.</param>
        /// <param name="refreshService">Service that validates and renews JWTs against current user state.</param>
        /// <param name="tokenStore">Process-wide store used for the prerender handoff and the per-user latest-token registry.</param>
        /// <exception cref="ArgumentNullException">Thrown when either argument is <c>null</c>.</exception>
        public JwtCookieAuthTokenProvider(
            IHttpContextAccessor httpContextAccessor,
            IOptions<JwtOptions> options,
            JwtTokenValidationParametersFactory validationParametersFactory,
            IJwtRefreshService refreshService,
            CircuitAuthTokenStore tokenStore)
        {
            _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _validationParametersFactory = validationParametersFactory ?? throw new ArgumentNullException(nameof(validationParametersFactory));
            _refreshService = refreshService ?? throw new ArgumentNullException(nameof(refreshService));
            _tokenStore = tokenStore ?? throw new ArgumentNullException(nameof(tokenStore));
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
        public async Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken)
        {
            var ctx = _httpContextAccessor.HttpContext;
            var now = DateTimeOffset.UtcNow;

            // Determine renewal window from configured lifetime (half of it)
            var renewalWindow = _options.Value.RenewalWindow;

            // Prefer request cookie over cache when a request context is available.
            // This prevents serving stale tokens from a different browser tab/user session.
            if (ctx != null && !ctx.Response.HasStarted)
            {
                var cookie = ctx.Request.Cookies[AuthCookieName];
                if (string.IsNullOrEmpty(cookie))
                {
                    InvalidateCache();
                    return null;
                }

                if (_cachedToken != null
                    && string.Equals(_cachedToken, cookie, StringComparison.Ordinal)
                    && _cachedExpiry - renewalWindow > now)
                {
                    return _cachedToken;
                }

                return await ValidateAndRefreshTokenAsync(ctx, cookie, renewalWindow, now, cancellationToken);
            }

            // When no request context is available (for example within a running Blazor circuit),
            // fall back to a still-valid cached token.
            if (_cachedToken != null && _cachedExpiry > now)
            {
                if (_cachedExpiry - renewalWindow <= now)
                {
                    var currentSubject = TryGetSubject(_cachedToken);
                    if (currentSubject != null
                        && _tokenStore.TryGetCurrent(currentSubject.Value, out var currentToken, out var currentExpiry)
                        && currentExpiry > _cachedExpiry)
                    {
                        Cache(currentToken!, currentExpiry);
                        return currentToken;
                    }
                }

                return _cachedToken;
            }

            // Cached token missing or expired: adopt the latest registry token issued for the
            // same subject (for example a browser-side renewal published via PrimeCache).
            var subject = TryGetSubject(_cachedToken);
            if (subject != null
                && _tokenStore.TryGetCurrent(subject.Value, out var registryToken, out var registryExpiry)
                && registryExpiry > now)
            {
                Cache(registryToken!, registryExpiry);
                return registryToken;
            }

            return null;
        }

        private async Task<string?> ValidateAndRefreshTokenAsync(
            HttpContext ctx,
            string cookie,
            TimeSpan renewalWindow,
            DateTimeOffset now,
            CancellationToken cancellationToken)
        {
            var handler = new JwtSecurityTokenHandler();

            try
            {
                var parameters = _validationParametersFactory.Create();

                var principal = handler.ValidateToken(cookie, parameters, out var validatedToken);
                var jwt = (JwtSecurityToken)validatedToken;
                var exp = DateTimeOffset.FromUnixTimeSeconds(long.Parse(jwt.Claims.First(c => c.Type == JwtRegisteredClaimNames.Exp).Value));

                // Erneuern wenn bald ablaufend
                if (exp - renewalWindow <= now)
                {
                    var refreshed = await _refreshService.RefreshAsync(principal, cancellationToken);
                    if (!refreshed.Succeeded || refreshed.Token is null || refreshed.ExpiresUtc is null)
                    {
                        InvalidateCache();
                        DeleteCookie(ctx);
                        return null;
                    }

                    var expiry = new DateTimeOffset(refreshed.ExpiresUtc.Value);
                    SetCookie(ctx, refreshed.Token, expiry);
                    Cache(refreshed.Token, expiry);
                    return refreshed.Token;
                }

                Cache(cookie, exp);
                return cookie;
            }
            catch
            {
                InvalidateCache();
                return null;
            }
        }

        /// <summary>
        /// Clears any internally cached token. Call this when the user logs out to ensure subsequent calls don't return a stale token.
        /// </summary>
        public void Clear()
        {
            InvalidateCache();
        }

        /// <inheritdoc/>
        public void InvalidateCache()
        {
            lock (_sync)
            {
                _cachedToken = null;
                _cachedExpiry = DateTimeOffset.MinValue;
            }
        }

        /// <summary>
        /// Stores a known-valid token for later use when no request context is available, for example in a Blazor circuit.
        /// Also publishes it as the latest token for the token's subject in the shared registry.
        /// </summary>
        /// <param name="token">Serialized JWT token.</param>
        /// <param name="expiry">Token expiry timestamp.</param>
        public void PrimeCache(string token, DateTimeOffset expiry)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                InvalidateCache();
                return;
            }

            Cache(token, expiry);
            var subject = TryGetSubject(token);
            if (subject != null)
            {
                _tokenStore.SetCurrent(subject.Value, token, expiry);
            }
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

        private static void DeleteCookie(HttpContext ctx)
        {
            ctx.Response.Cookies.Delete(AuthCookieName, new CookieOptions
            {
                Secure = ctx.Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                Path = "/"
            });
        }

        /// <summary>
        /// Caches the token and its expiry in-memory for quick subsequent access.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <param name="expiry">The expiry.</param>
        private void Cache(string token, DateTimeOffset expiry)
        {
            lock (_sync)
            {
                _cachedToken = token;
                _cachedExpiry = expiry;
            }
        }

        private static Guid? TryGetSubject(string? token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return null;
            }

            try
            {
                var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
                var subject = jwt.Subject;
                if (string.IsNullOrEmpty(subject))
                {
                    subject = jwt.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
                }

                return Guid.TryParse(subject, out var userId) ? userId : null;
            }
            catch (ArgumentException)
            {
                return null;
            }
        }
    }
}
