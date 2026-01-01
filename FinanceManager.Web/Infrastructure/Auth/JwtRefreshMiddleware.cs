using FinanceManager.Infrastructure.Auth;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace FinanceManager.Web.Infrastructure.Auth
{
    /// <summary>
    /// Middleware that automatically renews short-lived JWTs for authenticated requests when they approach expiry.
    /// When a token is eligible for renewal the middleware generates a fresh token via <see cref="IJwtTokenService"/>,
    /// appends it as a secure cookie and also sets response headers with the refreshed token and expiry.
    /// </summary>
    public sealed class JwtRefreshMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IConfiguration _configuration;

        private const string RefreshHeaderName = "X-Auth-Token";
        private const string RefreshExpiresHeaderName = "X-Auth-Token-Expires";
        private const string AuthCookieName = "FinanceManager.Auth";

        /// <summary>
        /// Initializes a new instance of <see cref="JwtRefreshMiddleware"/>.
        /// </summary>
        /// <param name="next">The next middleware delegate in the pipeline.</param>
        /// <param name="configuration">Application configuration used to read JWT lifetime settings (e.g. "Jwt:LifetimeMinutes").</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="next"/> or <paramref name="configuration"/> is <c>null</c>.</exception>
        public JwtRefreshMiddleware(RequestDelegate next, IConfiguration configuration)
        {
            _next = next;
            _configuration = configuration;
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
                try
                {
                    jwt = handler.ReadJwtToken(token);
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
                var lifetimeMinutes = int.TryParse(_configuration["Jwt:LifetimeMinutes"], out var lm) ? lm : 30;
                var renewalWindow = TimeSpan.FromMinutes(Math.Max(5, lm / 2)); // renew when half of lifetime has passed

                if (exp - renewalWindow > now)
                {
                    return;
                }

                var userIdStr = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                                  ?? context.User.FindFirstValue(JwtRegisteredClaimNames.Sub);
                var username = context.User.Identity?.Name
                               ?? context.User.FindFirstValue(JwtRegisteredClaimNames.UniqueName)
                               ?? context.User.FindFirstValue(ClaimTypes.Name)
                               ?? string.Empty;
                var isAdmin = context.User.IsInRole("Admin");

                if (!Guid.TryParse(userIdStr, out var userId))
                {
                    return;
                }

                var jts = context.RequestServices.GetRequiredService<IJwtTokenService>();
                var newToken = jts.CreateToken(userId, username, isAdmin, out var newExpiry);

                if (!context.Response.HasStarted)
                {
                    context.Response.Cookies.Append(AuthCookieName, newToken, new CookieOptions
                    {
                        HttpOnly = true,
                        Secure = context.Request.IsHttps, // vorher: true
                        SameSite = SameSiteMode.Lax,
                        Expires = new DateTimeOffset(newExpiry),
                        Path = "/"
                    });
                }

                context.Response.Headers[RefreshHeaderName] = newToken;
                context.Response.Headers[RefreshExpiresHeaderName] = newExpiry.ToString("o");
            }
            finally
            {
                await _next(context);
            }
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
