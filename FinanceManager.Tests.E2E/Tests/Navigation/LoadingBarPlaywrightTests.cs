namespace FinanceManager.Tests.E2E;

[Collection(PlaywrightCollection.CollectionName)]
public sealed class LoadingBarPlaywrightTests
{
    private readonly PlaywrightWebAppFixture _fixture;

    public LoadingBarPlaywrightTests(PlaywrightWebAppFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task LoadingBar_ShouldUseSingleDomNodeAndRestartSameInstance()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        await EnsureAuthenticatedAsync(page, "loading-bar-single-user");

        var bar = page.Locator("#fm-loading-bar");
        await bar.WaitForAsync();
        (await bar.CountAsync()).Should().Be(1);

        await page.EvaluateAsync("window.financeManager.loadingBar.start()");
        await page.WaitForFunctionAsync("() => document.querySelector('#fm-loading-bar')?.classList.contains('is-visible')");
        var firstSequence = await bar.GetAttributeAsync("data-sequence");

        await page.EvaluateAsync("window.financeManager.loadingBar.restart()");
        (await bar.CountAsync()).Should().Be(1);
        (await bar.GetAttributeAsync("class")).Should().Contain("is-visible");
        var secondSequence = await bar.GetAttributeAsync("data-sequence");

        int.Parse(secondSequence!).Should().BeGreaterThan(int.Parse(firstSequence!));
    }

    [Fact]
    public async Task LoadingBar_ShouldBeFixedToViewportTop_OnDesktop()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        await EnsureAuthenticatedAsync(page, "loading-bar-desktop-user");

        await page.EvaluateAsync("window.financeManager.loadingBar.start()");

        var metrics = await page.Locator("#fm-loading-bar").EvaluateAsync<LoadingBarMetrics>(
            @"el => {
                const rect = el.getBoundingClientRect();
                const style = getComputedStyle(el);
                return { top: rect.top, left: rect.left, width: rect.width, position: style.position, pointerEvents: style.pointerEvents };
            }");

        metrics.Position.Should().Be("fixed");
        metrics.PointerEvents.Should().Be("none");
        metrics.Top.Should().BeApproximately(0, 0.5);
        metrics.Left.Should().BeApproximately(0, 0.5);
        metrics.Width.Should().BeGreaterThan(100);
    }

    [Fact]
    public async Task LoadingBar_ShouldSitBelowMobileTopbar_OnMobile()
    {
        await using var session = await _fixture.CreateMobileSessionAsync();
        var page = session.Page;
        await EnsureAuthenticatedAsync(page, "loading-bar-mobile-user");

        await page.EvaluateAsync("window.financeManager.loadingBar.start()");

        var metrics = await page.EvaluateAsync<MobileLoadingBarMetrics>(
            @"() => {
                const bar = document.querySelector('#fm-loading-bar');
                const topbar = document.querySelector('.mobile-topbar');
                const barRect = bar.getBoundingClientRect();
                const topbarRect = topbar.getBoundingClientRect();
                return { barTop: barRect.top, topbarBottom: topbarRect.bottom, barHeight: barRect.height };
            }");

        metrics.BarTop.Should().BeApproximately(metrics.TopbarBottom, 2);
        metrics.BarHeight.Should().BeApproximately(3, 0.5);
    }

    [Fact]
    public async Task FormSubmit_WithValidationMessage_ShouldNotLeaveLoadingBarVisible()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        var userSeed = new TestUserSeeder(_fixture.DatabasePath);
        await userSeed.EnsureUserAsync($"loading-bar-login-form-{Guid.NewGuid():N}", "Secret123");

        await page.GotoAsync("/login");
        await page.Locator("form").WaitForAsync();
        await page.EvaluateAsync(
            @"() => {
                const form = document.querySelector('form');
                form.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
                window.setTimeout(() => {
                    const message = document.createElement('div');
                    message.className = 'validation-message';
                    document.body.appendChild(message);
                }, 50);
            }");

        await page.WaitForFunctionAsync("() => document.querySelector('#fm-loading-bar')?.classList.contains('is-visible')");
        await page.WaitForFunctionAsync(
            "() => !document.querySelector('#fm-loading-bar')?.classList.contains('is-visible')",
            null,
            new() { Timeout = 1500 });

        (await page.Locator("#fm-loading-bar").GetAttributeAsync("class")).Should().NotContain("is-visible");
    }

    [Fact]
    public async Task InternalLinkClick_ShouldStartLoadingBarAutomatically()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        await EnsureAuthenticatedAsync(page, "loading-bar-link-user");

        await page.Locator("#fm-loading-bar").WaitForAsync();
        await AssertLoadingBarApiAvailableAsync(page);
        var firstSequence = await GetLoadingBarSequenceAsync(page);

        await InstallLoadingBarVisibilityObserverAsync(page);
        await page.Locator("nav.sidebar a[href='/list/accounts']").ClickAsync();
        await page.WaitForURLAsync("**/list/accounts");

        await page.WaitForFunctionAsync("() => window.__fmLoadingBarObservedVisible === true");
        (await GetLoadingBarSequenceAsync(page)).Should().BeGreaterThan(firstSequence);
    }

    [Fact]
    public async Task SetupStatementSubmit_ShouldStopLoadingBarAfterNonNavigatingSave()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        await EnsureAuthenticatedAsync(page, "loading-bar-setup-submit-user");

        await page.GotoAsync("/card/setup");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await AssertLoadingBarApiAvailableAsync(page);

        await page.Locator("button.setup-section-toggle").Nth(2).ClickAsync();
        await page.Locator(".setup-statement-tab form").WaitForAsync();
        await page.Locator(".setup-statement-tab input[type='number']").First.FillAsync("21");
        await page.Locator(".setup-statement-tab input[type='number']").First.PressAsync("Tab");

        await InstallLoadingBarVisibilityObserverAsync(page);
        await page.Locator(".setup-statement-tab form").EvaluateAsync("form => form.requestSubmit()");

        await page.WaitForFunctionAsync("() => window.__fmLoadingBarObservedVisible === true");
        await page.WaitForFunctionAsync(
            "() => !document.querySelector('#fm-loading-bar')?.classList.contains('is-visible')",
            null,
            new() { Timeout = 5000 });

        (await page.Locator("#fm-loading-bar").GetAttributeAsync("class")).Should().NotContain("is-visible");
    }

    private async Task EnsureAuthenticatedAsync(IPage page, string userPrefix)
    {
        var auth = new AuthGateway(page, _fixture.BaseUrl);
        var userSeed = new TestUserSeeder(_fixture.DatabasePath);
        var username = $"{userPrefix}-{Guid.NewGuid():N}";
        const string password = "Secret123";
        await userSeed.EnsureUserAsync(username, password);
        await auth.LoginAsync(username, password);
    }

    private static async Task AssertLoadingBarApiAvailableAsync(IPage page)
    {
        var isAvailable = await page.EvaluateAsync<bool>(
            "() => typeof window.financeManager?.loadingBar?.start === 'function' && typeof window.financeManager?.loadingBar?.stop === 'function'");

        isAvailable.Should().BeTrue();
    }

    private static Task<int> GetLoadingBarSequenceAsync(IPage page)
    {
        return page.EvaluateAsync<int>(
            "() => Number(document.querySelector('#fm-loading-bar')?.dataset.sequence || '0')");
    }

    private static Task InstallLoadingBarVisibilityObserverAsync(IPage page)
    {
        return page.EvaluateAsync(
            @"() => {
                window.__fmLoadingBarObservedVisible = false;
                window.__fmLoadingBarObserver?.disconnect();
                const bar = document.querySelector('#fm-loading-bar');
                const markIfVisible = () => {
                    if (bar?.classList.contains('is-visible')) {
                        window.__fmLoadingBarObservedVisible = true;
                    }
                };
                markIfVisible();
                window.__fmLoadingBarObserver = new MutationObserver(markIfVisible);
                if (bar) {
                    window.__fmLoadingBarObserver.observe(bar, { attributes: true, attributeFilter: ['class'] });
                }
            }");
    }

    private sealed class LoadingBarMetrics
    {
        public double Top { get; set; }
        public double Left { get; set; }
        public double Width { get; set; }
        public string Position { get; set; } = string.Empty;
        public string PointerEvents { get; set; } = string.Empty;
    }

    private sealed class MobileLoadingBarMetrics
    {
        public double BarTop { get; set; }
        public double TopbarBottom { get; set; }
        public double BarHeight { get; set; }
    }
}
