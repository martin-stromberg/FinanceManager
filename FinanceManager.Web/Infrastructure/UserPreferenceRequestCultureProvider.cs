using FinanceManager.Infrastructure;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace FinanceManager.Web.Infrastructure;

/// <summary>
/// Resolves request culture from user preferences.
/// </summary>
/// <remarks>
/// The provider resolves culture in the following order:
/// 1) JWT claim "pref_lang" (set at login/registration)
/// 2) Database fallback (User.PreferredLanguage)
/// 3) null → delegate to the next configured provider (Accept-Language header)
///
/// Returning null for unauthenticated requests and for users with no explicit
/// preference ("Automatisch") lets the browser's Accept-Language header determine
/// the display language. An explicit preference ("de" or "en") is always honoured.
/// Database access is performed using a scoped <see cref="AppDbContext"/> resolved
/// from the request services.
/// </remarks>
public sealed class UserPreferenceRequestCultureProvider : RequestCultureProvider
{
    /// <summary>
    /// Determines the culture for the current request by consulting user-specific preferences.
    /// </summary>
    /// <param name="httpContext">The HTTP context for the current request. Must not be <c>null</c>.</param>
    /// <returns>
    /// A <see cref="ProviderCultureResult"/> when the user has an explicit language preference;
    /// <c>null</c> to let subsequent providers (Accept-Language header) participate when no
    /// explicit preference is stored or when the request is unauthenticated.
    /// </returns>
    /// <exception cref="OperationCanceledException">Thrown when the request's cancellation token is signalled.</exception>
    public override async Task<ProviderCultureResult?> DetermineProviderCultureResult(HttpContext httpContext)
    {
        if (httpContext.User?.Identity?.IsAuthenticated != true)
        {
            // Unauthenticated: let the Accept-Language header decide.
            return null;
        }

        // 1) Try JWT claim first (no DB access)
        var prefLangClaim = httpContext.User.FindFirst("pref_lang")?.Value;
        if (!string.IsNullOrWhiteSpace(prefLangClaim))
        {
            try
            {
                var culture = new CultureInfo(prefLangClaim);
                return new ProviderCultureResult(culture.Name, culture.Name);
            }
            catch (CultureNotFoundException)
            {
                // Invalid claim — fall through to DB lookup
            }
        }

        // 2) DB fallback
        var userIdClaim = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return null;
        }

        var db = httpContext.RequestServices.GetService<AppDbContext>();
        if (db == null)
        {
            return null;
        }

        var lang = await db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId && u.PreferredLanguage != null)
            .Select(u => u.PreferredLanguage)
            .FirstOrDefaultAsync(httpContext.RequestAborted);

        if (string.IsNullOrWhiteSpace(lang))
        {
            // "Automatisch" / no explicit preference: let the Accept-Language header decide.
            return null;
        }

        try
        {
            var culture = new CultureInfo(lang);
            return new ProviderCultureResult(culture.Name, culture.Name);
        }
        catch (CultureNotFoundException)
        {
            return null;
        }
    }
}