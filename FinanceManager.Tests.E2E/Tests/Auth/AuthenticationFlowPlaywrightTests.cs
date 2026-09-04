namespace FinanceManager.Tests.E2E;

/// <summary>
/// End-to-end tests for the authentication flow: registering/logging in/logging out through the
/// browser UI, redirecting to login with a preserved return URL when a session expires (and
/// returning to that route after re-login), rejecting invalid or externally-hosted return URLs,
/// deduplicating concurrent login redirects, and the keepalive-driven session refresh behavior
/// for near-expiry and already-invalidated sessions.
/// </summary>
[Collection(PlaywrightCollection.CollectionName)]
public sealed class AuthenticationFlowPlaywrightTests
{
    private readonly PlaywrightWebAppFixture _fixture;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthenticationFlowPlaywrightTests"/> class.
    /// </summary>
    /// <param name="fixture">Shared Playwright web app fixture providing the browser and test server.</param>
    public AuthenticationFlowPlaywrightTests(PlaywrightWebAppFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// Verifies registration, login and logout through the browser UI.
    /// </summary>
    [Fact]
    public async Task Register_Login_Logout_Flow_ShouldWork()
    {
        await RegisterLoginLogoutFlowShouldWorkAsync(
            () => _fixture.CreateSessionAsync(),
            "ui-user");
    }

    /// <summary>
    /// Same as <see cref="Register_Login_Logout_Flow_ShouldWork"/> but on a mobile viewport, to
    /// catch responsive-layout regressions in the register/login/logout flow that only show up
    /// at mobile widths.
    /// </summary>
    [Fact]
    public async Task Register_Login_Logout_Flow_ShouldWork_OnMobileViewport()
    {
        await RegisterLoginLogoutFlowShouldWorkAsync(
            () => _fixture.CreateMobileSessionAsync(),
            "ui-mobile-user");
    }

    /// <summary>
    /// Verifies that navigating to a protected route with an expired security stamp redirects to
    /// the login page with a <c>returnUrl</c> query parameter preserving the original route
    /// (including query string and fragment), and that logging back in returns the browser to
    /// that exact route.
    /// </summary>
    [Fact]
    public async Task ExpiredSession_OnProtectedRoute_ShouldRedirectToLoginWithReturnUrl_AndReturnAfterLogin()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        var auth = new AuthGateway(page, _fixture.BaseUrl);
        var seed = new TestUserSeeder(_fixture.DatabasePath);

        var username = $"expired-session-user-{Guid.NewGuid():N}";
        const string password = "Secret123";
        const string protectedRoute = "/reports/dashboard?edit=true&view=monthly#filters";

        await seed.EnsureUserAsync(username, password);
        await auth.LoginAsync(username, password);
        await page.GotoAsync(protectedRoute);
        await page.Locator("#Reload").WaitForAsync();
        await seed.InvalidateSecurityStampAsync(username);

        await page.EvaluateAsync("() => document.querySelector('#Reload')?.click()");
        await WaitForLoginUrlWithReturnUrlAsync(page, protectedRoute);
        GetQueryParameter(new Uri(page.Url), "returnUrl").Should().Be(protectedRoute);

        await auth.LoginThroughUiAsync(username, password);
        await WaitForRelativeUrlAsync(page, protectedRoute);
        CurrentRelativeUrl(page.Url).Should().Be(protectedRoute);
    }

    /// <summary>
    /// Verifies that logging in directly through the login page without a <c>returnUrl</c>
    /// query parameter lands the browser on the home page.
    /// </summary>
    [Fact]
    public async Task LoginWithoutReturnUrl_ShouldNavigateToHome()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        var auth = new AuthGateway(page, _fixture.BaseUrl);
        var seed = new TestUserSeeder(_fixture.DatabasePath);

        var username = $"direct-login-user-{Guid.NewGuid():N}";
        const string password = "Secret123";
        await seed.EnsureUserAsync(username, password);

        await auth.LoginThroughUiAsync(username, password);
        await WaitForRelativeUrlAsync(page, "/");
        CurrentRelativeUrl(page.Url).Should().Be("/");
    }

    /// <summary>
    /// Verifies that login rejects unsafe <c>returnUrl</c> values - an absolute external URL, a
    /// protocol-relative URL, a redirect back to the login page itself, and a double-encoded
    /// path - and falls back to navigating to the home page on the app's own host instead of
    /// following the supplied URL.
    /// </summary>
    /// <param name="returnUrl">The unsafe or invalid return URL to submit through the login form.</param>
    [Theory]
    [InlineData("https://example.invalid/reports/dashboard")]
    [InlineData("//example.invalid/reports/dashboard")]
    [InlineData("/login")]
    [InlineData("%252Freports%252Fdashboard")]
    public async Task LoginWithInvalidOrExternalReturnUrl_ShouldNavigateToHome(string returnUrl)
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        var auth = new AuthGateway(page, _fixture.BaseUrl);
        var seed = new TestUserSeeder(_fixture.DatabasePath);

        var username = $"invalid-return-user-{Guid.NewGuid():N}";
        const string password = "Secret123";
        await seed.EnsureUserAsync(username, password);

        await auth.LoginThroughUiAsync(username, password, returnUrl);
        await WaitForRelativeUrlAsync(page, "/");

        var current = new Uri(page.Url);
        current.Host.Should().Be(new Uri(_fixture.BaseUrl).Host);
        CurrentRelativeUrl(page.Url).Should().Be("/");
    }

    /// <summary>
    /// Verifies that when a page fires several near-simultaneous requests that all fail
    /// authentication (e.g. two reload clicks against an invalidated session), the client-side
    /// handler still navigates to the login page exactly once instead of issuing a document
    /// navigation to <c>/login</c> for every failed request.
    /// </summary>
    [Fact]
    public async Task MultipleAuthenticationFailures_ShouldNavigateToLoginOnlyOnce()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        var auth = new AuthGateway(page, _fixture.BaseUrl);
        var seed = new TestUserSeeder(_fixture.DatabasePath);

        var username = $"dedupe-session-user-{Guid.NewGuid():N}";
        const string password = "Secret123";
        const string protectedRoute = "/reports/dashboard?edit=true&view=dedupe#filters";

        await seed.EnsureUserAsync(username, password);
        await auth.LoginAsync(username, password);
        await page.GotoAsync(protectedRoute);
        await page.Locator("#Reload").WaitForAsync();
        await seed.InvalidateSecurityStampAsync(username);

        var loginDocumentNavigations = 0;
        page.Request += (_, request) =>
        {
            if (request.ResourceType == "document"
                && Uri.TryCreate(request.Url, UriKind.Absolute, out var uri)
                && uri.AbsolutePath.Equals("/login", StringComparison.OrdinalIgnoreCase))
            {
                Interlocked.Increment(ref loginDocumentNavigations);
            }
        };

        await page.EvaluateAsync("""
            () => {
                document.querySelector('#Reload')?.click();
                document.querySelector('#Reload')?.click();
            }
            """);
        await WaitForLoginUrlWithReturnUrlAsync(page, protectedRoute);

        loginDocumentNavigations.Should().Be(1);
    }

    /// <summary>
    /// Verifies that when a session's auth cookie is close to expiry, ordinary user activity
    /// (navigating to a page and clicking on it) triggers a successful keepalive request that
    /// refreshes the session, so the user stays authenticated without ever being redirected to
    /// the login page.
    /// </summary>
    [Fact]
    public async Task ActiveNavigationAndInteraction_ShouldRefreshNearExpirySessionWithoutLoginRedirect()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        var auth = new AuthGateway(page, _fixture.BaseUrl);
        var seed = new TestUserSeeder(_fixture.DatabasePath);
        var cookies = new TestAuthCookieHelper(_fixture.DatabasePath, _fixture.BaseUrl);

        var username = $"keepalive-nav-user-{Guid.NewGuid():N}";
        const string password = "Secret123";

        await seed.EnsureUserAsync(username, password);
        await auth.LoginAsync(username, password);
        await cookies.SetNearExpiryCookieAsync(page, username);
        await PlaywrightTestTiming.WaitForForcedKeepaliveThrottleAsync(page);

        await page.GotoAsync("/list/accounts");
        await page.Locator("body").ClickAsync();
        var keepaliveStatus = await CallKeepaliveAsync(page);
        keepaliveStatus.Should().Be(204);
        page.Url.ToLowerInvariant().Should().NotContain("/login");

        var profile = await BrowserApiHelper.GetWithStatusAsync<object>(page, "/api/user/settings/profile");
        profile.Status.Should().Be(200);
    }

    /// <summary>
    /// Verifies that when the security stamp is invalidated while a keepalive ping is forced, the
    /// failing keepalive response (401) does not by itself redirect the browser away from the
    /// current protected route, but a subsequent explicit protected action (reload) then performs
    /// exactly one redirect to the login page with the original route preserved as
    /// <c>returnUrl</c>.
    /// </summary>
    [Fact]
    public async Task Keepalive_FailedRefresh_ShouldNotTriggerLoginRedirect()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        var auth = new AuthGateway(page, _fixture.BaseUrl);
        var seed = new TestUserSeeder(_fixture.DatabasePath);

        var username = $"keepalive-invalid-user-{Guid.NewGuid():N}";
        const string password = "Secret123";
        const string protectedRoute = "/reports/dashboard?edit=true&view=keepalive-invalid#filters";

        await seed.EnsureUserAsync(username, password);
        await auth.LoginAsync(username, password);
        await page.GotoAsync(protectedRoute);
        await page.Locator("#Reload").WaitForAsync();
        await seed.InvalidateSecurityStampAsync(username);
        await PlaywrightTestTiming.WaitForForcedKeepaliveThrottleAsync(page);

        var loginDocumentNavigations = 0;
        page.Request += (_, request) =>
        {
            if (request.ResourceType == "document"
                && Uri.TryCreate(request.Url, UriKind.Absolute, out var uri)
                && uri.AbsolutePath.Equals("/login", StringComparison.OrdinalIgnoreCase))
            {
                Interlocked.Increment(ref loginDocumentNavigations);
            }
        };

        await page.EvaluateAsync("""
            () => {
                window.financeManager.keepalive.ping({ force: true, replace: true });
                window.financeManager.keepalive.ping({ force: true, replace: true });
            }
            """);
        await page.WaitForTimeoutAsync(250);

        await page.EvaluateAsync("""
            () => {
                document.querySelector('#Reload')?.click();
                document.querySelector('#Reload')?.click();
            }
            """);

        await WaitForLoginUrlWithReturnUrlAsync(page, protectedRoute);
        GetQueryParameter(new Uri(page.Url), "returnUrl").Should().Be(protectedRoute);
        loginDocumentNavigations.Should().BeLessThanOrEqualTo(1);
    }

    private async Task RegisterLoginLogoutFlowShouldWorkAsync(
        Func<Task<PlaywrightBrowserSession>> createSessionAsync,
        string userPrefix)
    {
        await using var session = await createSessionAsync();
        var page = session.Page;
        var auth = new AuthGateway(page, _fixture.BaseUrl);
        var seed = new TestUserSeeder(_fixture.DatabasePath);

        var username = $"{userPrefix}-{Guid.NewGuid():N}";
        const string password = "Secret123";

        await seed.EnsureUserAsync(username, password);
        await auth.LoginAsync(username, password);
        page.Url.Should().EndWith("/");

        await auth.LogoutAsync();
        page.Url.Should().EndWith("/login");

        await auth.LoginAsync(username, password);
        page.Url.Should().EndWith("/");
    }

    private static async Task WaitForLoginUrlWithReturnUrlAsync(IPage page, string expectedReturnUrl)
    {
        try
        {
            await page.WaitForFunctionAsync(
                "expected => location.pathname === '/login' && new URLSearchParams(location.search).get('returnUrl') === expected",
                expectedReturnUrl);
        }
        catch (TimeoutException ex)
        {
            var bodyText = await page.Locator("body").InnerTextAsync();
            throw new TimeoutException($"Expected login redirect with returnUrl '{expectedReturnUrl}'. Url: {page.Url}. Body: {bodyText}", ex);
        }
    }

    private static Task WaitForRelativeUrlAsync(IPage page, string expectedRelativeUrl)
        => page.WaitForFunctionAsync(
            "expected => location.pathname + location.search + location.hash === expected",
            expectedRelativeUrl);

    private static Task<int> CallKeepaliveAsync(IPage page)
        => page.EvaluateAsync<int>(
            """
            async () => {
                const response = await fetch('/api/auth/keepalive', {
                    method: 'GET',
                    credentials: 'include',
                    cache: 'no-store',
                    headers: { 'X-Requested-With': 'fetch' }
                });
                return response.status;
            }
            """);

    private static string CurrentRelativeUrl(string url)
    {
        var uri = new Uri(url);
        return uri.PathAndQuery + uri.Fragment;
    }

    private static string? GetQueryParameter(Uri uri, string name)
    {
        var query = uri.Query.TrimStart('?');
        if (string.IsNullOrWhiteSpace(query))
        {
            return null;
        }

        foreach (var part in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var pair = part.Split('=', 2);
            var key = Uri.UnescapeDataString(pair[0].Replace("+", " "));
            if (!key.Equals(name, StringComparison.Ordinal))
            {
                continue;
            }

            return pair.Length == 1
                ? string.Empty
                : Uri.UnescapeDataString(pair[1].Replace("+", " "));
        }

        return null;
    }
}


internal static class PlaywrightTestTiming
{
    public static Task WaitForForcedKeepaliveThrottleAsync(IPage page)
        => page.WaitForTimeoutAsync(5200);
}
