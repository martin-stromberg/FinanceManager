using FinanceManager.Domain.WellKnown;
using FluentAssertions;

namespace FinanceManager.Tests.WellKnownDomain;

/// <summary>
/// Covers the URL invariant enforced by <see cref="WellKnownSettings.Update"/>: the change-password
/// redirect target must be a local root path or an absolute http/https URL — anything else would produce
/// a broken or scriptable redirect on a publicly reachable endpoint.
/// </summary>
public sealed class WellKnownSettingsTests_Initialization
{
    /// <summary>Verifies the default settings carry the documented fallback change-password URL.</summary>
    [Fact]
    public void CreateDefault_HasChangePasswordUrl()
    {
        var settings = WellKnownSettings.CreateDefault();

        settings.ChangePasswordUrl.Should().Be("/change-password");
        settings.ChangePasswordUrl.Should().Be(WellKnownSettings.DefaultChangePasswordUrl);
    }

    /// <summary>Verifies a local root path is accepted and stored.</summary>
    [Fact]
    public void Update_AcceptsLocalPath()
    {
        var settings = WellKnownSettings.CreateDefault();

        settings.Update("/account/password");

        settings.ChangePasswordUrl.Should().Be("/account/password");
        settings.ModifiedUtc.Should().NotBeNull();
    }

    /// <summary>Verifies an absolute http/https URL is accepted and stored.</summary>
    /// <param name="url">An absolute http or https URL.</param>
    [Theory]
    [InlineData("https://idp.example.com/account/password")]
    [InlineData("http://localhost:5000/change-password")]
    public void Update_AcceptsAbsoluteHttpUrl(string url)
    {
        var settings = WellKnownSettings.CreateDefault();

        settings.Update(url);

        settings.ChangePasswordUrl.Should().Be(url);
    }

    /// <summary>
    /// Verifies that empty, relative, protocol-relative and non-http(s) values are rejected — a
    /// <c>javascript:</c> or <c>//host</c> value would turn the endpoint into an unsafe redirector.
    /// </summary>
    /// <param name="url">A value that violates the URL rule.</param>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("javascript:alert(1)")]
    [InlineData("foo")]
    [InlineData("//host/path")]
    [InlineData("ftp://host/path")]
    public void Update_RejectsInvalid(string url)
    {
        var settings = WellKnownSettings.CreateDefault();

        var act = () => settings.Update(url);

        act.Should().Throw<ArgumentException>();
        settings.ChangePasswordUrl.Should().Be(WellKnownSettings.DefaultChangePasswordUrl);
    }
}
