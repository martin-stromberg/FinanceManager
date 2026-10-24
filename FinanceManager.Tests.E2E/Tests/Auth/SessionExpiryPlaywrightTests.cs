namespace FinanceManager.Tests.E2E;

/// <summary>Exercises the real login, circuit requests and session activity end to end.</summary>
/// <param name="fixture">The isolated regular application used for session scenarios.</param>
/// <returns>A test instance bound to the supplied application fixture.</returns>
[Collection(SessionExpiryCollection.Name)]
public sealed class SessionExpiryPlaywrightTests(SessionExpiryWebAppFixture fixture)
{
    /// <summary>Checks the first Home API and subsequent interactive navigation after form login.</summary>
    [Fact]
    public async Task UiLogin_FirstHomeApiAndInteractiveNavigationRemainAuthenticated()
    {
        await using var session = await fixture.CreateSessionAsync();
        var page = session.Page;
        var probe = new SessionExpiryProbe(fixture, page);
        var sequence = fixture.ReadObservations().LastOrDefault()?.Sequence ?? 0;
        var login = await probe.LoginAsync("first-home");

        var home = await probe.ExpectApiAsync("/api/notifications", sequence, login.UserId);
        var interactiveHome = await probe.ExpectApiAsync("/api/notifications", home.Sequence, login.UserId);
        await page.Locator("a[href='/list/accounts']").First.ClickAsync();
        var accounts = await probe.ExpectApiAsync("/api/accounts/statistics", interactiveHome.Sequence, login.UserId);
        await page.Locator("a[href='/']").First.ClickAsync();
        await probe.ExpectApiAsync("/api/notifications", accounts.Sequence, login.UserId);
        new Uri(page.Url).AbsolutePath.Should().Be("/");
    }

    /// <summary>Uses actual navigation and verifies a renewed browser and circuit token after original expiry.</summary>
    [Fact]
    public async Task ActiveNavigation_BeyondOriginalExpiry_UsesRenewedBrowserAndCircuitToken()
    {
        await using var session = await fixture.CreateSessionAsync();
        var page = session.Page;
        var probe = new SessionExpiryProbe(fixture, page);
        var login = await probe.LoginAsync("navigation");
        var original = await probe.ReadBrowserTokenAsync();
        var sequence = fixture.ReadObservations().Max(e => e.Sequence);
        var deadline = DateTimeOffset.UtcNow.AddMinutes(fixture.LifetimeMinutes + 3);
        var accounts = true;
        ApiObservation? last = null;
        while (DateTimeOffset.UtcNow <= login.Expires.AddSeconds(12) || accounts)
        {
            DateTimeOffset.UtcNow.Should().BeBefore(deadline);
            var route = accounts ? "/list/accounts" : "/";
            await page.Locator($"a[href='{route}']").First.ClickAsync();
            last = await probe.ExpectApiAsync(accounts ? "/api/accounts/statistics" : "/api/notifications", sequence, login.UserId);
            sequence = last.Sequence;
            _ = await probe.ReadBrowserTokenAsync();
            new Uri(page.Url).AbsolutePath.Should().Be(route);
            accounts = !accounts;
            await Task.Delay(TimeSpan.FromSeconds(10));
        }
        var renewed = await probe.ReadBrowserTokenAsync();
        renewed.Expires.Should().BeAfter(login.Expires);
        renewed.Fingerprint.Should().NotBe(original.Fingerprint);


    }

    /// <summary>Passively waits through expiry; only a later protected action may redirect to login.</summary>
    [Fact]
    public async Task Idle_NoKeepalive_ThenAccountsActionRedirectsWithReturnUrl()
    {
        await using var session = await fixture.CreateSessionAsync();
        var page = session.Page;
        var probe = new SessionExpiryProbe(fixture, page);
        var login = await probe.LoginAsync("idle");
        var first = await probe.ExpectApiAsync("/api/notifications", 0, login.UserId);
        await probe.ExpectApiAsync("/api/notifications", first.Sequence, login.UserId);
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        var lastToken = await probe.ReadBrowserTokenAsync();
        var pings = 0;
        page.Request += (_, request) => { if (IsKeepalive(request.Url)) Interlocked.Increment(ref pings); };
        while (DateTimeOffset.UtcNow < lastToken.Expires.AddSeconds(12))
        {
            await Task.Delay(1000);
            new Uri(page.Url).AbsolutePath.Should().Be("/");
            pings.Should().Be(0, "idle time must never trigger a keepalive");
        }
        var polls = fixture.ReadObservations().Where(e => e.Path == "/api/background-tasks/active").ToArray();
        polls.Should().Contain(e => e.Status == 401);
        var pollCount = polls.Length;
        await Task.Delay(5000);
        fixture.ReadObservations().Count(e => e.Path == "/api/background-tasks/active").Should().Be(pollCount);
        var sequence = fixture.ReadObservations().Max(e => e.Sequence);
        await page.Locator("a[href='/list/accounts']").First.ClickAsync();
        await probe.ExpectApiAsync("/api/accounts/statistics", sequence, login.UserId, 401);
        await page.WaitForURLAsync(url => new Uri(url).AbsolutePath == "/login");
        Uri.UnescapeDataString(new Uri(page.Url).Query).Should().Contain("returnUrl=/list/accounts");
    }

    /// <summary>Rejected activity refresh does not navigate until a protected operation is requested.</summary>
    [Fact]
    public async Task RejectedKeepalive_StaysUntilProtectedAccountsAction()
    {
        await using var session = await fixture.CreateSessionAsync();
        var page = session.Page;
        var probe = new SessionExpiryProbe(fixture, page);
        var login = await probe.LoginAsync("revoked");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await new TestUserSeeder(fixture.DatabasePath).InvalidateSecurityStampAsync(login.Username);
        await Task.Delay(61_000);
        var ping = page.WaitForResponseAsync(response => IsKeepalive(response.Url));
        await page.Locator("body").PressAsync("x");
        (await ping).Status.Should().Be(401);
        await Task.Delay(500);
        new Uri(page.Url).AbsolutePath.Should().Be("/");
        var sequence = fixture.ReadObservations().Max(e => e.Sequence);
        await page.Locator("a[href='/list/accounts']").First.ClickAsync();
        await probe.ExpectApiAsync("/api/accounts/statistics", sequence, login.UserId, 401);
        await page.WaitForURLAsync(url => new Uri(url).AbsolutePath == "/login");
    }

    /// <summary>Observes visible SSR content through deliberately slowed hydration.</summary>
    [Fact]
    public async Task Initialization_KeepsLoginAndApplicationContentVisible()
    {
        await using var session = await fixture.CreateSessionAsync();
        var page = session.Page;
        await InitializationVisibilityProbe.InstallAsync(page.Context);
        await page.RouteAsync("**/_framework/blazor.web.js", async route =>
        {
            await Task.Delay(750);
            await route.ContinueAsync();
        });
        var probe = new SessionExpiryProbe(fixture, page);
        var login = await probe.LoginAsync("visible");
        await probe.ExpectApiAsync("/api/notifications", 0, login.UserId);
        await Task.Delay(1000);
        await InitializationVisibilityProbe.AssertVisibleAsync(page);
    }

    private static bool IsKeepalive(string url) => new Uri(url).AbsolutePath == "/api/auth/keepalive";
}
