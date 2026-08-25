using FinanceManager.Web.Infrastructure.Auth;
using FluentAssertions;

namespace FinanceManager.Tests.Web;

public sealed class AuthReturnUrlTests
{
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

    [Fact]
    public void FromNavigationUri_ShouldKeepPathQueryAndFragmentForSameOrigin()
    {
        var result = AuthReturnUrl.FromNavigationUri(
            "https://app.test/",
            "https://app.test/reports/dashboard?favoriteId=123&edit=false#details");

        result.Should().Be("/reports/dashboard?favoriteId=123&edit=false#details");
    }

    [Fact]
    public void FromNavigationUri_ShouldKeepEncodedQueryAndFragmentValuesForSameOrigin()
    {
        var result = AuthReturnUrl.FromNavigationUri(
            "https://app.test/",
            "https://app.test/reports/dashboard?filter=a%26b#section%201");

        result.Should().Be("/reports/dashboard?filter=a%26b#section%201");
    }

    [Fact]
    public void FromNavigationUri_ShouldRejectExternalOrigin()
    {
        var result = AuthReturnUrl.FromNavigationUri(
            "https://app.test/",
            "https://evil.test/reports");

        result.Should().BeNull();
    }

    [Fact]
    public void BuildLoginUrl_ShouldEncodeSafeReturnUrl()
    {
        AuthReturnUrl.BuildLoginUrl("/reports/dashboard?favoriteId=123#details")
            .Should().Be("/login?returnUrl=%2Freports%2Fdashboard%3FfavoriteId%3D123%23details");
    }

    [Fact]
    public void BuildLoginUrl_ShouldKeepEncodedReturnUrlDataBeforeEncodingParameter()
    {
        AuthReturnUrl.BuildLoginUrl("/reports/dashboard?filter=a%26b#section%201")
            .Should().Be("/login?returnUrl=%2Freports%2Fdashboard%3Ffilter%3Da%2526b%23section%25201");
    }
}
