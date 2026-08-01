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
    /// the provider falls back to the database lookup path (or default culture if DB unavailable).
    /// This test verifies the failure path when DB context is not available.
    /// </summary>
    [Fact]
    public async Task DetermineProviderCultureResult_JwtClaimInvalid_FallsBackToDefault()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateAuthenticatedUser(prefLangClaim: "invalid-culture", userId);
        var context = CreateHttpContextWithUser(user);

        // Act
        var result = await _provider.DetermineProviderCultureResult(context);

        // Assert - Falls back to default culture when invalid and no DB
        result.Should().NotBeNull();
        result!.Cultures.Should().HaveCount(1);
        result.Cultures.First().ToString().Should().Be("de");
        result.UICultures.Should().HaveCount(1);
        result.UICultures.First().ToString().Should().Be("de");
    }

    /// <summary>
    /// Critical test: Verifies the bug fix - when no JWT claim and no DB preference,
    /// the provider returns the default culture ("de") instead of null.
    /// This prevents the browser's Accept-Language header from overriding user preferences.
    /// </summary>
    [Fact]
    public async Task DetermineProviderCultureResult_NoClaimNoDatabaseValue_ReturnsDefaultCulture()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateAuthenticatedUser(prefLangClaim: null, userId);
        var context = CreateHttpContextWithUser(user);

        // Act
        var result = await _provider.DetermineProviderCultureResult(context);

        // Assert - Must NOT be null to prevent browser language override
        result.Should().NotBeNull("Provider must return a ProviderCultureResult to prevent browser Accept-Language override");
        result!.Cultures.Should().HaveCount(1);
        result.Cultures.First().ToString().Should().Be("de", "Default culture must be 'de'");
        result.UICultures.Should().HaveCount(1);
        result.UICultures.First().ToString().Should().Be("de", "Default UI culture must be 'de'");
    }

    /// <summary>
    /// Verifies that unauthenticated requests get the default culture instead of null.
    /// </summary>
    [Fact]
    public async Task DetermineProviderCultureResult_UnauthenticatedRequest_ReturnsDefaultCulture()
    {
        // Arrange
        var user = CreateUnauthenticatedUser();
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

    /// <summary>
    /// Verifies that an invalid culture code in the JWT claim falls back to default.
    /// This tests exception handling during CultureInfo creation.
    /// </summary>
    [Fact]
    public async Task DetermineProviderCultureResult_InvalidCultureExceptionFallsBack_ReturnsDefaultCulture()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateAuthenticatedUser(prefLangClaim: "xx-INVALID", userId);
        var context = CreateHttpContextWithUser(user);

        // Act
        var result = await _provider.DetermineProviderCultureResult(context);

        // Assert - Must not crash and must return default culture
        result.Should().NotBeNull();
        result!.Cultures.Should().HaveCount(1);
        result.Cultures.First().ToString().Should().Be("de");
        result.UICultures.Should().HaveCount(1);
        result.UICultures.First().ToString().Should().Be("de");
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
