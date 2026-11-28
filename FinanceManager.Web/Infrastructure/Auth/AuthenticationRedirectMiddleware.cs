using FinanceManager.Shared;

namespace FinanceManager.Web.Infrastructure.Auth
{
    /// <summary>
    /// Middleware that converts <see cref="ApiAuthenticationRequiredException"/> failures raised during
    /// server-side prerendering into an HTTP redirect to the login page.
    /// Interactive circuits handle authentication failures via the <c>AuthRedirect</c> component instead;
    /// this middleware covers the non-interactive prerender path where an unauthenticated API call would
    /// otherwise surface the generic error page. It is registered inside the exception-handler scope so it
    /// wins over the <c>/Error</c> page.
    /// </summary>
    public sealed class AuthenticationRedirectMiddleware
    {
        private readonly RequestDelegate _next;

        /// <summary>
        /// Initializes a new instance of <see cref="AuthenticationRedirectMiddleware"/>.
        /// </summary>
        /// <param name="next">The next middleware delegate in the pipeline.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="next"/> is <c>null</c>.</exception>
        public AuthenticationRedirectMiddleware(RequestDelegate next)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
        }

        /// <summary>
        /// Invokes the middleware. Passes the request through and, when the downstream pipeline throws an
        /// <see cref="ApiAuthenticationRequiredException"/>, issues a <c>302</c> redirect to
        /// <c>{PathBase}/login?returnUrl={PathBase}{current route}</c> so the user returns to the
        /// originally requested page after signing in. The return URL carries the
        /// <see cref="HttpRequest.PathBase"/> prefix (like <c>CookieAuthenticationHandler.BuildRedirectUri</c>
        /// and <c>AuthReturnUrl.FromNavigationUri</c>) because the post-login navigation resolves it
        /// against the domain root, not the app base.
        /// </summary>
        /// <param name="context">The current <see cref="HttpContext"/>.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        /// <remarks>
        /// The exception is rethrown untouched when a redirect is not meaningful: once the response has
        /// started, for non-GET requests (a redirect cannot replay them), for API and Blazor hub paths
        /// (machine clients), and for public paths such as <c>/login</c> itself (loop prevention).
        /// </remarks>
        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (ApiAuthenticationRequiredException)
            {
                if (!CanRedirect(context))
                {
                    throw;
                }

                var returnUrl = AuthReturnUrl.Validate(
                    context.Request.PathBase.Value + context.Request.Path.Value + context.Request.QueryString.Value);
                var loginUrl = context.Request.PathBase + "/" + AuthReturnUrl.BuildLoginUrl(returnUrl);
                context.Response.Redirect(loginUrl);
            }
        }

        private static bool CanRedirect(HttpContext context)
        {
            if (context.Response.HasStarted || !HttpMethods.IsGet(context.Request.Method))
            {
                return false;
            }

            var path = context.Request.Path;
            if (AuthReturnUrl.IsPublicPath(path.Value ?? "/"))
            {
                return false;
            }

            return !path.StartsWithSegments("/api")
                && !path.StartsWithSegments("/_blazor")
                && !path.StartsWithSegments("/_framework");
        }
    }
}
