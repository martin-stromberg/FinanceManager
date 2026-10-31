namespace FinanceManager.Tests.E2E;

/// <summary>Passively checks that regular content remains visible across SSR and hydration.</summary>
public static class InitializationVisibilityProbe
{
    /// <summary>Installs before the first document; records only visibility metadata.</summary>
    public static Task InstallAsync(IBrowserContext context) => context.AddInitScriptAsync("""
        (() => {
            let path = location.pathname;
            const seen = new Set();
            const errors = JSON.parse(sessionStorage.getItem('sessionVisibilityErrors') || '[]');
            const inspect = () => {
                if (path !== location.pathname) { path = location.pathname; seen.clear(); }
                const selectors = path === '/login'
                    ? ['#login-user', '#login-pass', 'button[type=submit]']
                    : ['.app-shell', '.content h1', "a[href='/list/accounts']", ...Array.from(document.querySelectorAll('[id^=qe_subject_]')).map(e => '#' + e.id)];
                for (const selector of selectors) {
                    const el = document.querySelector(selector);
                    const style = el && getComputedStyle(el);
                    const rect = el && el.getBoundingClientRect();
                    const visible = el && !el.hidden && style.display !== 'none' && style.visibility !== 'hidden' && rect.width > 0 && rect.height > 0;
                    if (visible) seen.add(selector);
                    else if (seen.has(selector) && !errors.some(e => e.path === path && e.selector === selector)) {
                        errors.push({ path, selector });
                        sessionStorage.setItem('sessionVisibilityErrors', JSON.stringify(errors));
                    }
                }
                requestAnimationFrame(inspect);
            };
            requestAnimationFrame(inspect);
        })();
        """);

    /// <summary>Checks the accumulated observation after the page has hydrated.</summary>
    /// <param name="page">The hydrated page whose visibility observations are checked.</param>
    public static async Task AssertVisibleAsync(IPage page)
    {
        var errors = await page.EvaluateAsync<string>("() => sessionStorage.getItem('sessionVisibilityErrors') || '[]'");
        errors.Should().Be("[]", "content that was already visible must not be hidden during initialization");
        await Assertions.Expect(page.Locator(".app-shell")).ToBeVisibleAsync();
    }
}
