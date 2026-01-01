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
/// 3) null -> delegate to the next configured provider (cookie/query/header)
///
/// When a culture string cannot be parsed to a valid <see cref="CultureInfo"/>, the provider falls back to the next source.
/// Database access is performed using a scoped <see cref="AppDbContext"/> resolved from the request services. If the DB
/// context is not available the provider returns <c>null</c> and allows other providers to participate.
/// </remarks>
public sealed class UserPreferenceRequestCultureProvider : RequestCultureProvider
{
    /// <summary>
    /// Determines the culture for the current request by consulting user-specific preferences.
    /// </summary>
    /// <param name="httpContext">The HTTP context for the current request. Must not be <c>null</c>.</param>
    /// <returns>
    /// A <see cref="ProviderCultureResult"/> when a culture could be resolved from the user's preferences; otherwise <c>null</c>
    /// to let subsequent <see cref="RequestCultureProvider"/> instances attempt resolution.
    /// </returns>
    /// <exception cref="OperationCanceledException">Thrown when the request's cancellation token (<see cref="HttpContext.RequestAborted"/>) is signalled while awaiting database operations.</exception>
    /// <remarks>
    /// The method first attempts to read a "pref_lang" claim from the authenticated user's claims (no DB access).
    /// If the claim exists and represents a valid culture name it will be returned. If the claim is absent or invalid
    /// the provider looks up the user's preferred language in the database using <see cref="AppDbContext"/>.
    /// If no preference is configured or if parsing fails the method returns <c>null</c>.
    /// </remarks>
    public override async Task<ProviderCultureResult?> DetermineProviderCultureResult(HttpContext httpContext)
    {
        if (httpContext.User?.Identity?.IsAuthenticated != true)
        {
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
                // ignore and fallback to DB or next providers
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