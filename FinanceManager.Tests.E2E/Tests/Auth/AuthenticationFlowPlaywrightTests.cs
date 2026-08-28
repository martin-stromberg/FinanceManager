namespace FinanceManager.Tests.E2E;

[Collection(PlaywrightCollection.CollectionName)]
public sealed class AuthenticationFlowPlaywrightTests
{
    private readonly PlaywrightWebAppFixture _fixture;

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

    [Fact]
    public async Task Register_Login_Logout_Flow_ShouldWork_OnMobileViewport()
    {
        await RegisterLoginLogoutFlowShouldWorkAsync(
            () => _fixture.CreateMobileSessionAsync(),
            "ui-mobile-user");
    }

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

        var keepaliveResponse = WaitForKeepaliveResponseAsync(page);
        await page.GotoAsync("/list/accounts");
        await page.Locator("body").ClickAsync();
        var response = await keepaliveResponse;

        response.Status.Should().Be(204);
        page.Url.ToLowerInvariant().Should().NotContain("/login");

        var profile = await BrowserApiHelper.GetWithStatusAsync<object>(page, "/api/user/settings/profile");
        profile.Status.Should().Be(200);
    }

    [Fact]
    public async Task InvalidatedSession_KeepaliveFailure_ShouldNotRedirectUntilProtectedActionRedirectsOnce()
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
        await WaitForForcedKeepaliveThrottleAsync(page);

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

        var keepaliveResponseTask = WaitForKeepaliveResponseAsync(page);
        await page.EvaluateAsync("""
            () => {
                window.financeManager.keepalive.ping({ force: true, replace: true });
                window.financeManager.keepalive.ping({ force: true, replace: true });
            }
            """);

        var keepaliveResponse = await keepaliveResponseTask;
        keepaliveResponse.Status.Should().Be(401);
        CurrentRelativeUrl(page.Url).Should().Be(protectedRoute);
        loginDocumentNavigations.Should().Be(0);

        await page.EvaluateAsync("""
            () => {
                document.querySelector('#Reload')?.click();
                document.querySelector('#Reload')?.click();
            }
            """);

        await WaitForLoginUrlWithReturnUrlAsync(page, protectedRoute);
        GetQueryParameter(new Uri(page.Url), "returnUrl").Should().Be(protectedRoute);
        loginDocumentNavigations.Should().Be(1);
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

    private static Task<IResponse> WaitForKeepaliveResponseAsync(IPage page)
        => page.WaitForResponseAsync(response =>
            Uri.TryCreate(response.Url, UriKind.Absolute, out var uri)
            && uri.AbsolutePath.Equals("/api/auth/keepalive", StringComparison.OrdinalIgnoreCase));

    private static Task WaitForForcedKeepaliveThrottleAsync(IPage page)
        => page.WaitForTimeoutAsync(5200);

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
