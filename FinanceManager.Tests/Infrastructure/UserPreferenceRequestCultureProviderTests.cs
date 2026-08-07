using FluentAssertions;
using FinanceManager.Web.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;
using Xunit;

namespace FinanceManager.Tests.Infrastructure;

/// <summary>
/// Unit tests for <see cref="UserPreferenceRequestCultureProvider"/> to verify that
/// culture resolution returns an explicit default culture ("de") instead of null,
/// preventing the browser's Accept-Language header from overriding user preferences.
/// 
/// Note: These are lightweight tests that verify the main behavior without full integration.
/// Full integration tests are provided in the E2E test suite (ProfileSettingsLanguageTests).
/// </summary>
public class UserPreferenceRequestCultureProviderTests
{
    private readonly UserPreferenceRequestCultureProvider _provider = new();

    private static HttpContext CreateHttpContextWithUser(ClaimsPrincipal user)
    {
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();
        
        var context = new DefaultHttpContext
        {
            User = user,
            RequestServices = serviceProvider
        };
        return context;
    }

    private static ClaimsPrincipal CreateAuthenticatedUser(string? prefLangClaim = null, Guid? userId = null)
    {
        var id = userId ?? Guid.NewGuid();
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, id.ToString()),
            new(ClaimTypes.Name, "testuser")
        };
        
        if (!string.IsNullOrWhiteSpace(prefLangClaim))
        {
            claims.Add(new Claim("pref_lang", prefLangClaim));
        }
        
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    private static ClaimsPrincipal CreateUnauthenticatedUser()
    {
        return new ClaimsPrincipal();
    }

    /// <summary>
    /// Verifies that a valid JWT claim "pref_lang" is correctly returned as the culture.
    /// </summary>
    [Fact]
    public async Task DetermineProviderCultureResult_JwtClaimPresent_ReturnsCorrectCulture()
    {
        // Arrange
        var user = CreateAuthenticatedUser(prefLangClaim: "en");
        var context = CreateHttpContextWithUser(user);

        // Act
        var result = await _provider.DetermineProviderCultureResult(context);

        // Assert
        result.Should().NotBeNull();
        result!.Cultures.Should().HaveCount(1);
        result.Cultures.First().ToString().Should().Be("en");
        result.UICultures.Should().HaveCount(1);
        result.UICultures.First().ToString().Should().Be("en");
    }

    /// <summary>
    /// Verifies that when an invalid JWT claim is provided (cannot be parsed to CultureInfo),
    /// the provider falls back to the database lookup path and returns null when DB is unavailable.
    /// Returning null lets the next provider (Accept-Language header) determine the culture.
    /// </summary>
    [Fact]
    public async Task DetermineProviderCultureResult_JwtClaimInvalid_FallsBackToNull()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateAuthenticatedUser(prefLangClaim: "invalid-culture", userId);
        var context = CreateHttpContextWithUser(user);

        // Act
        var result = await _provider.DetermineProviderCultureResult(context);

        // Assert - Falls back to null when claim invalid and no DB (Automatic mode)
        result.Should().BeNull("Provider must return null to allow Accept-Language header to decide when no explicit preference");
    }

    /// <summary>
    /// Verifies that when no JWT claim and no DB preference ("Automatisch" / Automatic mode),
    /// the provider returns null so that the browser's Accept-Language header determines the culture.
    /// </summary>
    [Fact]
    public async Task DetermineProviderCultureResult_NoClaimNoDatabaseValue_ReturnsNull()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateAuthenticatedUser(prefLangClaim: null, userId);
        var context = CreateHttpContextWithUser(user);

        // Act
        var result = await _provider.DetermineProviderCultureResult(context);

        // Assert - Must be null: "Automatisch" should let the Accept-Language header decide
        result.Should().BeNull("Provider must return null for Automatic mode to allow browser language to take effect");
    }

    /// <summary>
    /// Verifies that unauthenticated requests return null so that the next provider
    /// (Accept-Language header or default) determines the culture.
    /// </summary>
    [Fact]
    public async Task DetermineProviderCultureResult_UnauthenticatedRequest_ReturnsNull()
    {
        // Arrange
        var user = CreateUnauthenticatedUser();
        var context = CreateHttpContextWithUser(user);

        // Act
        var result = await _provider.DetermineProviderCultureResult(context);

        // Assert
        result.Should().BeNull("Unauthenticated requests should fall through to the Accept-Language header provider");
    }

    /// <summary>
    /// Verifies that an invalid culture code in the JWT claim returns null (falls through to next provider).
    /// </summary>
    [Fact]
    public async Task DetermineProviderCultureResult_InvalidCultureExceptionFallsBack_ReturnsNull()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateAuthenticatedUser(prefLangClaim: "xx-INVALID", userId);
        var context = CreateHttpContextWithUser(user);

        // Act
        var result = await _provider.DetermineProviderCultureResult(context);

        // Assert - Must not crash, must return null
        result.Should().BeNull("Invalid culture codes must return null, not crash");
    }

    /// <summary>
    /// Verifies that German (de) is correctly resolved from a valid JWT claim.
    /// </summary>
    [Fact]
    public async Task DetermineProviderCultureResult_JwtClaimGerman_ReturnsCorrectCulture()
    {
        // Arrange
        var user = CreateAuthenticatedUser(prefLangClaim: "de");
        var context = CreateHttpContextWithUser(user);

        // Act
        var result = await _provider.DetermineProviderCultureResult(context);

        // Assert
        result.Should().NotBeNull();
        result!.Cultures.Should().HaveCount(1);
        result.Cultures.First().ToString().Should().Be("de");
        result.UICultures.Should().HaveCount(1);
        result.UICultures.First().ToString().Should().Be("de");
    }
}
