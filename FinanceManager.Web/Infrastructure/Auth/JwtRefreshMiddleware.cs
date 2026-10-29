using FinanceManager.Infrastructure.Auth;
using Microsoft.Extensions.Options;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace FinanceManager.Web.Infrastructure.Auth
{
    /// <summary>
    /// Middleware that automatically renews short-lived JWTs for authenticated requests when they approach expiry.
    /// When a token is eligible for renewal the middleware generates a fresh token via <see cref="IJwtRefreshService"/>,
    /// appends it as a secure cookie and also sets response headers with the refreshed token and expiry.
    /// </summary>
    public sealed class JwtRefreshMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IOptions<JwtOptions> _options;

        private const string RefreshHeaderName = "X-Auth-Token";
        private const string RefreshExpiresHeaderName = "X-Auth-Token-Expires";
        private const string AuthCookieName = "FinanceManager.Auth";

        /// <summary>
        /// Initializes a new instance of <see cref="JwtRefreshMiddleware"/>.
        /// </summary>
        /// <param name="next">The next middleware delegate in the pipeline.</param>
        /// <param name="options">Validated JWT settings.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="next"/> or <paramref name="options"/> is <c>null</c>.</exception>
        public JwtRefreshMiddleware(RequestDelegate next, IOptions<JwtOptions> options)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        /// <summary>
        /// Invokes the middleware to inspect the incoming JWT and refresh it when appropriate.
        /// If the current user is not authenticated or no token is present the middleware simply continues the pipeline.
        /// When a refreshed token is issued it is set as a secure HTTP cookie and also exposed via response headers.
        /// </summary>
        /// <param name="context">The current <see cref="HttpContext"/> for the request.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        /// <remarks>
        /// The middleware swallows token parsing errors and will not block the request if the token is invalid.
        /// The actual token creation is delegated to <see cref="IJwtTokenService"/> resolved from the request services.
        /// </remarks>
        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                if (context.User?.Identity?.IsAuthenticated != true)
                {
                    return;
                }

                var token = GetIncomingToken(context);
                if (string.IsNullOrWhiteSpace(token))
                {
                    return;
                }

                var handler = new JwtSecurityTokenHandler();
                JwtSecurityToken? jwt;
                ClaimsPrincipal principal;
                try
                {
                    var parameters = context.RequestServices.GetRequiredService<JwtTokenValidationParametersFactory>().Create();
                    principal = handler.ValidateToken(token, parameters, out var validatedToken);
                    jwt = (JwtSecurityToken)validatedToken;
                    context.RequestServices.GetRequiredService<JwtCookieAuthTokenProvider>()
                        .PrimeCache(token, new DateTimeOffset(jwt.ValidTo, TimeSpan.Zero));
                }
                catch
                {
                    return;
                }

                var expClaim = jwt.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Exp)?.Value;
                if (string.IsNullOrEmpty(expClaim) || !long.TryParse(expClaim, out var expSec))
                {
                    return;
                }

                var exp = DateTimeOffset.FromUnixTimeSeconds(expSec);
                var now = DateTimeOffset.UtcNow;

                // Determine renewal window dynamically based on configured lifetime
                var renewalWindow = _options.Value.RenewalWindow;

                if (exp - renewalWindow > now)
                {
                    if (HttpMethods.IsGet(context.Request.Method) && context.Request.Path == "/api/auth/keepalive")
                    {
                        SetTokenResponseHeaders(context, token, exp);
                    }
                    return;
                }

                var refreshService = context.RequestServices.GetRequiredService<IJwtRefreshService>();
                var refresh = await refreshService.RefreshAsync(principal, context.RequestAborted);
                if (!refresh.Succeeded || refresh.Token is null || refresh.ExpiresUtc is null)
                {
                    if (!context.Response.HasStarted)
                    {
                        context.Response.Cookies.Delete(AuthCookieName, new CookieOptions
                        {
                            Secure = true,
                            SameSite = SameSiteMode.Lax,
                            Path = "/"
                        });
                    }
                    return;
                }

                if (!context.Response.HasStarted)
                {
                    context.RequestServices.GetRequiredService<JwtCookieAuthTokenProvider>()
                        .PrimeCache(refresh.Token, new DateTimeOffset(refresh.ExpiresUtc.Value));
                    context.Response.Cookies.Append(AuthCookieName, refresh.Token, new CookieOptions
                    {
                        HttpOnly = true,
                        Secure = true, // vorher: true
                        SameSite = SameSiteMode.Lax,
                        Expires = new DateTimeOffset(refresh.ExpiresUtc.Value),
                        Path = "/"
                    });
                }

                SetTokenResponseHeaders(context, refresh.Token, new DateTimeOffset(refresh.ExpiresUtc.Value));
            }
            finally
            {
                await _next(context);
            }
        }

        private static void SetTokenResponseHeaders(HttpContext context, string token, DateTimeOffset expires)
        {
            if (context.Response.HasStarted) return;
            context.Response.Headers[RefreshHeaderName] = token;
            context.Response.Headers[RefreshExpiresHeaderName] = expires.UtcDateTime.ToString("o");
            context.Response.Headers.CacheControl = "no-store";
        }

        /// <summary>
        /// Attempts to obtain the incoming JWT from the Authorization header (Bearer) or the authentication cookie.
        /// </summary>
        /// <param name="context">HTTP context to inspect.</param>
        /// <returns>The token string when found; otherwise <c>null</c>.</returns>
        private static string? GetIncomingToken(HttpContext context)
        {
            if (context.Request.Headers.TryGetValue("Authorization", out var authVals))
            {
                var auth = authVals.FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(auth) && auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    return auth.Substring("Bearer ".Length).Trim();
                }
            }

            if (context.Request.Cookies.TryGetValue(AuthCookieName, out var cookie) && !string.IsNullOrWhiteSpace(cookie))
            {
                return cookie;
            }
            return null;
        }
    }
}
