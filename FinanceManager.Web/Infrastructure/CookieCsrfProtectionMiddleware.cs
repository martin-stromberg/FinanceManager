using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;

namespace FinanceManager.Web.Infrastructure;

/// <summary>
/// Enforces antiforgery validation on state-changing requests that are authenticated via
/// cookies. Bearer-authenticated requests (API clients, Blazor circuit HttpClient) and
/// anonymous endpoints are not subject to the check.
/// </summary>
public sealed class CookieCsrfProtectionMiddleware
{
    private readonly RequestDelegate _next;

    /// <summary>
    /// Initializes a new instance of <see cref="CookieCsrfProtectionMiddleware"/>.
    /// </summary>
    /// <param name="next">The next middleware delegate in the pipeline.</param>
    public CookieCsrfProtectionMiddleware(RequestDelegate next)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
    }

    /// <summary>
    /// Validates the antiforgery token for unsafe, cookie-authenticated requests targeting
    /// authorized endpoints and rejects them with 403 when validation fails.
    /// </summary>
    /// <param name="context">The current <see cref="HttpContext"/>.</param>
    /// <param name="antiforgery">The antiforgery service used to validate the request.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task InvokeAsync(HttpContext context, IAntiforgery antiforgery)
    {
        if (RequiresValidation(context))
        {
            try
            {
                await antiforgery.ValidateRequestAsync(context);
            }
            catch (AntiforgeryValidationException)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }
        }

        await _next(context);
    }

    private static bool RequiresValidation(HttpContext context)
    {
        var method = context.Request.Method;
        if (!HttpMethods.IsPost(method)
            && !HttpMethods.IsPut(method)
            && !HttpMethods.IsDelete(method)
            && !HttpMethods.IsPatch(method))
        {
            return false;
        }

        var authorization = context.Request.Headers.Authorization.ToString();
        if (authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var endpoint = context.GetEndpoint();
        // Endpoints opting out of authorization (e.g. [AllowAnonymous] under an [Authorize]
        // controller or a fallback policy) must not require an antiforgery token either.
        if (endpoint?.Metadata.GetMetadata<IAllowAnonymous>() is not null)
        {
            return false;
        }

        return endpoint?.Metadata.GetMetadata<IAuthorizeData>() is not null;
    }
}
