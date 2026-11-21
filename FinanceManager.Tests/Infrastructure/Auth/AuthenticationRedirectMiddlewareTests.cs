using System.Net;
using FinanceManager.Shared;
using FinanceManager.Web.Infrastructure.Auth;
using Microsoft.AspNetCore.Http;

namespace FinanceManager.Tests.Infrastructure.Auth;

/// <summary>
/// Covers <see cref="AuthenticationRedirectMiddleware"/>: an <see cref="ApiAuthenticationRequiredException"/>
/// escaping server-side prerendering must turn into a 302 redirect to the login page carrying the current
/// route as returnUrl, while requests where a redirect is meaningless (API paths, public pages, non-GET
/// requests) rethrow the exception untouched.
/// </summary>
public sealed class AuthenticationRedirectMiddlewareTests
{
    /// <summary>
    /// Verifies the core scenario from the bug report: a prerendered report page whose API call fails with
    /// an expired token redirects to /login and preserves the originally requested route as returnUrl.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_AuthenticationRequired_RedirectsToLoginWithReturnUrl()
    {
        var middleware = new AuthenticationRedirectMiddleware(_ => Task.FromException(
            new ApiAuthenticationRequiredException(HttpStatusCode.Unauthorized, null, "Unauthorized")));
        var context = CreateContext("/reports/dashboard", "?favoriteId=2b9cca67-cd62-4af2-bff7-b92385c913a0&edit=false");

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status302Found, context.Response.StatusCode);
        var expectedReturnUrl = Uri.EscapeDataString("/reports/dashboard?favoriteId=2b9cca67-cd62-4af2-bff7-b92385c913a0&edit=false");
        Assert.Equal($"/login?returnUrl={expectedReturnUrl}", context.Response.Headers.Location.ToString());
    }

    /// <summary>
    /// Verifies that the login redirect stays inside the application's base path when the app is hosted
    /// under a reverse-proxy PathBase prefix.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_AuthenticationRequired_PrependsPathBase()
    {
        var middleware = new AuthenticationRedirectMiddleware(_ => Task.FromException(
            new ApiAuthenticationRequiredException(HttpStatusCode.Unauthorized, null, null)));
        var context = CreateContext("/reports");
        context.Request.PathBase = "/finance";

        await middleware.InvokeAsync(context);

        var expectedReturnUrl = Uri.EscapeDataString("/reports");
        Assert.Equal($"/finance/login?returnUrl={expectedReturnUrl}", context.Response.Headers.Location.ToString());
    }

    /// <summary>
    /// Verifies that authentication failures on API endpoints rethrow: machine clients must receive the
    /// original error instead of an HTML login redirect they cannot follow.
    /// </summary>
    /// <param name="path">API-style request path.</param>
    [Theory]
    [InlineData("/api/accounts")]
    [InlineData("/_blazor")]
    [InlineData("/_framework/blazor.web.js")]
    public async Task InvokeAsync_AuthenticationRequired_OnMachinePath_Rethrows(string path)
    {
        var middleware = new AuthenticationRedirectMiddleware(_ => Task.FromException(
            new ApiAuthenticationRequiredException(HttpStatusCode.Unauthorized, null, null)));
        var context = CreateContext(path);

        await Assert.ThrowsAsync<ApiAuthenticationRequiredException>(() => middleware.InvokeAsync(context));
    }

    /// <summary>
    /// Verifies that an authentication failure raised on a public page rethrows instead of redirecting to
    /// /login, which would otherwise loop.
    /// </summary>
    /// <param name="path">Public request path.</param>
    [Theory]
    [InlineData("/login")]
    [InlineData("/register")]
    [InlineData("/error")]
    public async Task InvokeAsync_AuthenticationRequired_OnPublicPath_Rethrows(string path)
    {
        var middleware = new AuthenticationRedirectMiddleware(_ => Task.FromException(
            new ApiAuthenticationRequiredException(HttpStatusCode.Unauthorized, null, null)));
        var context = CreateContext(path);

        await Assert.ThrowsAsync<ApiAuthenticationRequiredException>(() => middleware.InvokeAsync(context));
    }

    /// <summary>
    /// Verifies that non-GET requests rethrow: a 302 cannot replay the original POST, so the exception must
    /// reach the regular error handling.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_AuthenticationRequired_OnPost_Rethrows()
    {
        var middleware = new AuthenticationRedirectMiddleware(_ => Task.FromException(
            new ApiAuthenticationRequiredException(HttpStatusCode.Unauthorized, null, null)));
        var context = CreateContext("/reports/dashboard");
        context.Request.Method = HttpMethods.Post;

        await Assert.ThrowsAsync<ApiAuthenticationRequiredException>(() => middleware.InvokeAsync(context));
    }

    /// <summary>
    /// Verifies that unrelated exceptions are not affected by the middleware.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_OtherException_Rethrows()
    {
        var middleware = new AuthenticationRedirectMiddleware(_ => Task.FromException(new InvalidOperationException("boom")));
        var context = CreateContext("/reports/dashboard");

        await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.InvokeAsync(context));
    }

    /// <summary>
    /// Verifies that requests without authentication failures pass through untouched.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_Success_PassesThrough()
    {
        var middleware = new AuthenticationRedirectMiddleware(context =>
        {
            context.Response.StatusCode = StatusCodes.Status200OK;
            return Task.CompletedTask;
        });
        var context = CreateContext("/reports/dashboard");

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.False(context.Response.Headers.Location.Count > 0);
    }

    private static DefaultHttpContext CreateContext(string path, string? queryString = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = path;
        context.Request.QueryString = string.IsNullOrEmpty(queryString) ? QueryString.Empty : new QueryString(queryString);
        return context;
    }
}
