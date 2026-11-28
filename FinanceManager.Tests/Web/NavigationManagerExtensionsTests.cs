using FinanceManager.Web.Extensions;
using FluentAssertions;
using Microsoft.AspNetCore.Components;

namespace FinanceManager.Tests.Web;

/// <summary>
/// Tests for <see cref="FinanceManager.Web.Extensions.NavigationManagerExtensions.IsSameOriginUri(NavigationManager, string)"/>, which validates
/// user-controlled navigation targets (e.g. the <c>?back=</c> query parameter used by
/// <c>ListPage</c> and <c>BudgetReport</c>) to prevent open redirects.
/// </summary>
public sealed class NavigationManagerExtensionsTests
{
    /// <summary>
    /// Verifies that relative paths (with and without a leading slash), paths with query and
    /// fragment, and absolute URIs pointing at the app's own origin resolve to same-origin targets.
    /// Also covers canonicalization edge cases: a backslash inside a path (<c>/\evil</c>) is
    /// normalized to a same-origin path, and scheme casing is irrelevant. A nested, percent-encoded
    /// <c>?back=</c> parameter (as produced when chaining card pages) is accepted as well.
    /// </summary>
    /// <param name="uri">The candidate navigation target.</param>
    [Theory]
    [InlineData("list/accounts")]
    [InlineData("/list/accounts")]
    [InlineData("card/accounts/00000000-0000-0000-0000-000000000001")]
    [InlineData("/list/accounts?foo=bar#frag")]
    [InlineData("https://app.test/list/accounts")]
    [InlineData("https://app.test:443/list/accounts")]
    [InlineData(".")]
    [InlineData("/\\evil")]
    [InlineData("HTTPS://app.test/list/accounts")]
    [InlineData("card/accounts/00000000-0000-0000-0000-000000000001?back=card%2Fcontacts%2F00000000-0000-0000-0000-000000000002")]
    public void IsSameOriginUri_ShouldAcceptRelativeAndSameOriginTargets(string uri)
    {
        var nav = new TestNavigationManager();
        nav.IsSameOriginUri(uri).Should().BeTrue();
    }

    /// <summary>
    /// Verifies that <c>null</c>, blank input, external origins, protocol-relative URLs, and
    /// non-http(s) schemes are rejected so they can never be used as open-redirect targets.
    /// Also covers documented bypass attempts that .NET resolves relative to the base URI but
    /// the browser's WHATWG URL parser resolves as absolute/scheme URLs: scheme prefixes without
    /// <c>//</c> (<c>https:host</c>, <c>https:/host</c>, <c>https:\host</c>), smuggled tab/newline
    /// characters (raw or percent-encoded, e.g. <c>java%09script:</c>), UNC-style backslashes
    /// (<c>\\evil</c> resolves to the <c>file:</c> scheme), a userinfo trick
    /// (<c>https://app.test@evil.test</c> — the real authority is <c>evil.test</c>), and an
    /// uppercase scheme on a foreign host.
    /// </summary>
    /// <param name="uri">The candidate navigation target to reject.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("https://evil.test/list/accounts")]
    [InlineData("http://app.test.evil.test/")]
    [InlineData("//evil.test/phishing")]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,<script>alert(1)</script>")]
    [InlineData("data:text/html;base64,x")]
    [InlineData("\\\\evil")]
    [InlineData("https://app.test@evil.test/list")]
    [InlineData("HTTPS://evil.test/")]
    [InlineData("https:evil.test")]
    [InlineData("https:app.test")]
    [InlineData("https:/evil.test")]
    [InlineData("https:\\evil.test")]
    [InlineData("java\tscript:alert(1)")]
    [InlineData("java%09script:alert(1)")]
    [InlineData("ht%09tps://evil.test")]
    [InlineData("https%3Aevil.test")]
    [InlineData("list/accounts\nhttps://evil.test")]
    [InlineData("list/accounts\tfoo")]
    [InlineData("https://app.test/foo bar")]
    public void IsSameOriginUri_ShouldRejectForeignOrUnsafeTargets(string? uri)
    {
        var nav = new TestNavigationManager();
        nav.IsSameOriginUri(uri).Should().BeFalse();
    }

    private sealed class TestNavigationManager : NavigationManager
    {
        public TestNavigationManager()
        {
            Initialize("https://app.test/", "https://app.test/list/accounts");
        }

        protected override void NavigateToCore(string uri, bool forceLoad)
        {
        }
    }
}
