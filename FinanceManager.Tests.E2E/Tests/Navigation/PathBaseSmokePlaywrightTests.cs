using System.Collections.Concurrent;

namespace FinanceManager.Tests.E2E;

/// <summary>
/// Smoke tests for reverse-proxy subfolder hosting (<c>UsePathBase</c>, issue #612): against an
/// isolated server running with <c>PathBase=/finance</c> they verify that the dynamic
/// <c>&lt;base href&gt;</c> carries the prefix, that all icon-rail and login-status links resolve
/// under it, that rail navigation and the unauthenticated login redirect stay inside the prefix,
/// and that no request issued by the prefixed document escapes it.
/// </summary>
[Collection(PathBaseCollection.Name)]
public sealed class PathBaseSmokePlaywrightTests
{
    private readonly PathBaseWebAppFixture _fixture;

    /// <summary>
    /// Initializes a new instance of the <see cref="PathBaseSmokePlaywrightTests"/> class.
    /// </summary>
    /// <param name="fixture">Isolated web app fixture hosting the application under <c>PathBase=/finance</c>.</param>
    public PathBaseSmokePlaywrightTests(PathBaseWebAppFixture fixture)
    {
        _fixture = fixture;
    }

    private string PrefixedRoot => $"{_fixture.BaseUrl}{PathBaseWebAppFixture.PathBasePrefix}/";

    /// <summary>
    /// Verifies that the home page under the prefix renders <c>&lt;base href="/finance/"&gt;</c> and
    /// that every icon-rail link is relative (no leading slash) and resolves to a URL inside the
    /// prefix - including the admin-only <c>list/users</c> link, which is why the user is seeded as
    /// admin and at least twelve rail links are expected.
    /// </summary>
    [Fact]
    public async Task BaseElement_AndNavRailLinks_ResolveUnderPathBase()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        await LoginAdminAsync(page, "pathbase-rail");

        await page.GotoAsync($"{PathBaseWebAppFixture.PathBasePrefix}/");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        (await page.Locator("base").GetAttributeAsync("href"))
            .Should().Be($"{PathBaseWebAppFixture.PathBasePrefix}/");

        var links = page.Locator("nav.sidebar .nav-links a[href]");
        await links.First.WaitForAsync();
        var count = await links.CountAsync();
        count.Should().BeGreaterThanOrEqualTo(12, "all rail links including the admin-only list/users entry must render");

        for (var i = 0; i < count; i++)
        {
            var link = links.Nth(i);
            var href = await link.GetAttributeAsync("href");
            href.Should().NotBeNull("every rail link must carry an href attribute");
            href.Should().NotStartWith("/", "rail link hrefs must be relative to resolve under the PathBase prefix");
            (await link.EvaluateAsync<string>("a => a.href"))
                .Should().StartWith(PrefixedRoot, "the browser must resolve every rail link inside the PathBase prefix");
        }
    }

    /// <summary>
    /// Reproduces the reported failure: clicking "Konten" in the icon rail from the prefixed home
    /// page must navigate to <c>/finance/list/accounts</c> instead of escaping to the domain root,
    /// and no request issued while the prefixed document is active may leave the prefix.
    /// </summary>
    [Fact]
    public async Task RailNavigation_ToAccounts_StaysWithinPathBase()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        await LoginAdminAsync(page, "pathbase-nav");

        // Attribute each request to the document that issued it: requests fired by the previous,
        // unprefixed login document (e.g. a trailing keepalive) are outside the scope of this check -
        // only the prefixed document's own traffic must stay inside the prefix.
        var requests = new ConcurrentQueue<string>();
        page.Request += (_, request) =>
        {
            string? frameUrl = null;
            try
            {
                frameUrl = request.Frame?.Url;
            }
            catch (PlaywrightException)
            {
                // Frame already detached; the navigation-URL check below still applies.
            }

            var issuedByPrefixedDocument = frameUrl?.StartsWith(PrefixedRoot, StringComparison.Ordinal) == true;
            var isPrefixedDocumentNavigation = request.IsNavigationRequest
                && request.Url.StartsWith(PrefixedRoot, StringComparison.Ordinal);
            if (issuedByPrefixedDocument || isPrefixedDocumentNavigation)
            {
                requests.Enqueue(request.Url);
            }
        };

        await page.GotoAsync($"{PathBaseWebAppFixture.PathBasePrefix}/");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await page.Locator("nav.sidebar .nav-links a[href='list/accounts']").ClickAsync();
        await page.WaitForURLAsync(url => new Uri(url).AbsolutePath == $"{PathBaseWebAppFixture.PathBasePrefix}/list/accounts");

        new Uri(page.Url).AbsolutePath.Should().Be($"{PathBaseWebAppFixture.PathBasePrefix}/list/accounts");
        await Assertions.Expect(page.Locator(".list-page-title")).ToBeVisibleAsync();
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var outsidePrefix = requests
            .Select(url => new Uri(url))
            .Where(uri => uri.Scheme is "http" or "https")
            // The ASP.NET Core dev-mode browser tooling probe (hot-reload settings) is fetched
            // root-absolute by blazor.web.js itself - it is not rendered app markup and does not
            // exist under production hosting, so it is out of scope for this contract check.
            .Where(uri => !uri.AbsolutePath.StartsWith("/_framework/dotnet-browser-tools/", StringComparison.Ordinal))
            .Where(uri => !uri.AbsolutePath.StartsWith($"{PathBaseWebAppFixture.PathBasePrefix}/", StringComparison.Ordinal))
            .Select(uri => uri.PathAndQuery)
            .ToList();
        outsidePrefix.Should().BeEmpty("no request issued by the prefixed document may leave the PathBase prefix");
    }

    /// <summary>
    /// Regression test for the explicit <c>UseRouting</c> after <c>UsePathBase</c> in
    /// <c>ProgramExtensions.ConfigureMiddleware</c> (#612): endpoint matching must observe the
    /// prefix-stripped path, so <c>POST /finance/_blazor/negotiate</c> must answer with a SignalR
    /// negotiate payload instead of the 405 that left the Blazor circuit dead. The DOM assertions
    /// in the other tests cannot catch this - prerendered markup renders fine while the circuit
    /// is broken.
    /// </summary>
    [Fact]
    public async Task BlazorNegotiate_UnderPathBase_ReturnsNegotiatePayload()
    {
        using var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        };
        using var client = new HttpClient(handler);

        using var response = await client.PostAsync(
            $"{_fixture.BaseUrl}{PathBaseWebAppFixture.PathBasePrefix}/_blazor/negotiate?negotiateVersion=1",
            new StringContent(string.Empty),
            TestContext.Current.CancellationToken);

        response.IsSuccessStatusCode.Should().BeTrue(
            "the SignalR negotiate endpoint must match under the PathBase prefix (a 405 means routing ran before UsePathBase stripped it, killing the interactive circuit)");
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain("\"connectionToken\"", "the negotiate response must establish a circuit connection");
    }

    /// <summary>
    /// Verifies that an unauthenticated request to a protected route under the prefix is redirected
    /// by <c>AuthenticationRedirectMiddleware</c> to <c>/finance/login?returnUrl=…</c> instead of
    /// <c>/login</c> on the domain root.
    /// </summary>
    [Fact]
    public async Task ProtectedPage_WithoutAuth_RedirectsToLoginUnderPathBase()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;

        // A user must exist or the unauthenticated redirect targets /register instead of /login.
        await new TestUserSeeder(_fixture.DatabasePath)
            .EnsureUserAsync($"pathbase-unauth-{Guid.NewGuid():N}", "Secret123");

        // DOMContentLoaded instead of Load, then wait on the login form: the redirect can come from
        // the AuthenticationRedirectMiddleware (302 during prerender) or from the AuthRedirect
        // component's client-side force-load navigation - waiting on a full Load event of a
        // superseded navigation would time out.
        await page.GotoAsync(
            $"{PathBaseWebAppFixture.PathBasePrefix}/list/accounts",
            new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
        await Assertions.Expect(page.Locator("#login-user")).ToBeVisibleAsync(new() { Timeout = 30000 });

        var url = new Uri(page.Url);
        url.AbsolutePath.Should().Be($"{PathBaseWebAppFixture.PathBasePrefix}/login");
        Uri.UnescapeDataString(url.Query).Should().Contain(
            $"returnUrl={PathBaseWebAppFixture.PathBasePrefix}/list/accounts",
            "the returnUrl must carry the PathBase prefix - the post-login force-load NavigateTo resolves it against the domain root, so an unprefixed value would escape the reverse-proxy subfolder");
    }

    /// <summary>
    /// Verifies that the <c>LoginStatus</c> links resolve inside the prefix: the guest login link on
    /// the login page and the authenticated change-password link must both stay relative so the
    /// browser resolves them under <c>/finance/</c>.
    /// </summary>
    [Fact]
    public async Task LoginStatusLinks_ResolveUnderPathBase()
    {
        await using var guestSession = await _fixture.CreateSessionAsync();
        var guestPage = guestSession.Page;
        await guestPage.GotoAsync($"{PathBaseWebAppFixture.PathBasePrefix}/login");

        var loginLink = guestPage.Locator(".login-status a[href]");
        await loginLink.WaitForAsync(new() { State = WaitForSelectorState.Attached });
        (await loginLink.GetAttributeAsync("href")).Should().Be("login");
        (await loginLink.EvaluateAsync<string>("a => a.href"))
            .Should().Be($"{PrefixedRoot}login");

        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        await LoginAdminAsync(page, "pathbase-status");
        await page.GotoAsync($"{PathBaseWebAppFixture.PathBasePrefix}/");

        var changePasswordLink = page.Locator("#change-password-link");
        await changePasswordLink.WaitForAsync(new() { State = WaitForSelectorState.Attached });
        (await changePasswordLink.GetAttributeAsync("href")).Should().Be("change-password");
        (await changePasswordLink.EvaluateAsync<string>("a => a.href"))
            .Should().Be($"{PrefixedRoot}change-password");
    }

    private async Task LoginAdminAsync(IPage page, string userPrefix)
    {
        var username = $"{userPrefix}-{Guid.NewGuid():N}";
        const string password = "Secret123";
        await new TestUserSeeder(_fixture.DatabasePath).EnsureUserAsync(username, password, isAdmin: true);
        await new AuthGateway(page, _fixture.BaseUrl).LoginAsync(username, password);
    }
}
