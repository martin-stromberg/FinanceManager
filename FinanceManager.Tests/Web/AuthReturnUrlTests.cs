using FinanceManager.Web.Infrastructure.Auth;
using FluentAssertions;

namespace FinanceManager.Tests.Web;

/// <summary>
/// Tests for <see cref="AuthReturnUrl"/>, which validates and builds the <c>returnUrl</c> used to send a user
/// back to where they came from after login. This is a classic open-redirect surface, so the tests cover
/// accepting only safe internal routes (including once-encoded values), rejecting external/protocol-relative
/// targets and public pages that would create redirect loops or nonsensical destinations, and correctly
/// round-tripping the URL through encoding when deriving it from a Blazor navigation URI or building the
/// final login link.
/// </summary>
public sealed class AuthReturnUrlTests
{
    /// <summary>
    /// Verifies that plain internal paths, paths with query strings and fragments, and once URL-encoded
    /// internal paths are all accepted and normalized to their decoded, same-origin form.
    /// </summary>
    /// <param name="input">The raw or encoded candidate return URL.</param>
    /// <param name="expected">The expected normalized/decoded return URL.</param>
    [Theory]
    [InlineData("/reports", "/reports")]
    [InlineData("/reports/dashboard?favoriteId=123&edit=false#details", "/reports/dashboard?favoriteId=123&edit=false#details")]
    [InlineData("/reports/dashboard?filter=a%26b#section%201", "/reports/dashboard?filter=a%26b#section%201")]
    [InlineData("%2Freports%3Fq%3D1%23top", "/reports?q=1#top")]
    [InlineData("%2Freports%3Ffilter%3Da%2526b%23section%25201", "/reports?filter=a%26b#section%201")]
    public void Validate_ShouldAcceptInternalRoutes(string input, string expected)
    {
        AuthReturnUrl.Validate(input).Should().Be(expected);
    }

    /// <summary>
    /// Verifies that blank/whitespace input; absolute and protocol-relative external URLs (open-redirect
    /// vectors); double-encoded paths that could smuggle a protocol-relative URL past a naive single-decode
    /// check; a bare relative path without a leading slash; and public pages (login, register, help, error,
    /// API routes) are all rejected as invalid post-login redirect targets.
    /// </summary>
    /// <param name="input">The candidate return URL to reject.</param>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("https://evil.test/reports")]
    [InlineData("//evil.test/reports")]
    [InlineData("%252Freports")]
    [InlineData("/reports%252Fsummary")]
    [InlineData("%2F%252Fevil.test%2Freports")]
    [InlineData("reports")]
    [InlineData("/login")]
    [InlineData("/login?returnUrl=/reports")]
    [InlineData("/register")]
    [InlineData("/help")]
    [InlineData("/help/view/konten-und-buchungen")]
    [InlineData("/error")]
    [InlineData("/api/user/settings/profile")]
    public void Validate_ShouldRejectUnsafeOrPublicTargets(string? input)
    {
        AuthReturnUrl.Validate(input).Should().BeNull();
    }

    /// <summary>
    /// Verifies that deriving a return URL from a Blazor navigation URI on the same origin as the base URI
    /// preserves the path, query string, and fragment, dropping only the scheme/host.
    /// </summary>
    [Fact]
    public void FromNavigationUri_ShouldKeepPathQueryAndFragmentForSameOrigin()
    {
        var result = AuthReturnUrl.FromNavigationUri(
            "https://app.test/",
            "https://app.test/reports/dashboard?favoriteId=123&edit=false#details");

        result.Should().Be("/reports/dashboard?favoriteId=123&edit=false#details");
    }

    /// <summary>
    /// Verifies that percent-encoded characters within the query string and fragment (e.g. an encoded
    /// ampersand or space) survive unchanged when deriving the return URL, rather than being double-decoded
    /// or double-encoded.
    /// </summary>
    [Fact]
    public void FromNavigationUri_ShouldKeepEncodedQueryAndFragmentValuesForSameOrigin()
    {
        var result = AuthReturnUrl.FromNavigationUri(
            "https://app.test/",
            "https://app.test/reports/dashboard?filter=a%26b#section%201");

        result.Should().Be("/reports/dashboard?filter=a%26b#section%201");
    }

    /// <summary>
    /// Verifies that a navigation URI pointing to a different origin than the app's base URI yields no
    /// return URL, since a cross-origin navigation should never be echoed back as a redirect target.
    /// </summary>
    [Fact]
    public void FromNavigationUri_ShouldRejectExternalOrigin()
    {
        var result = AuthReturnUrl.FromNavigationUri(
            "https://app.test/",
            "https://evil.test/reports");

        result.Should().BeNull();
    }

    /// <summary>
    /// Verifies that a validated return URL is percent-encoded as the <c>returnUrl</c> query parameter value
    /// when building the login link, so its own query/fragment characters don't get parsed as part of the
    /// login URL's query string.
    /// </summary>
    [Fact]
    public void BuildLoginUrl_ShouldEncodeSafeReturnUrl()
    {
        AuthReturnUrl.BuildLoginUrl("/reports/dashboard?favoriteId=123#details")
            .Should().Be("/login?returnUrl=%2Freports%2Fdashboard%3FfavoriteId%3D123%23details");
    }

    /// <summary>
    /// Verifies that a return URL which already contains percent-encoded characters is encoded exactly once
    /// more when embedded as the login link's query parameter, avoiding double-encoding that would corrupt
    /// the value on the round trip back through the login page.
    /// </summary>
    [Fact]
    public void BuildLoginUrl_ShouldKeepEncodedReturnUrlDataBeforeEncodingParameter()
    {
        AuthReturnUrl.BuildLoginUrl("/reports/dashboard?filter=a%26b#section%201")
            .Should().Be("/login?returnUrl=%2Freports%2Fdashboard%3Ffilter%3Da%2526b%23section%25201");
    }
}
