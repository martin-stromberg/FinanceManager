using System.Text.RegularExpressions;

namespace FinanceManager.Tests.E2E;

public sealed class SetupUpdateGateway
{
    private static readonly Regex UpdateSectionToggleText = new("Update|Aktualisierung", RegexOptions.IgnoreCase);

    private readonly IPage _page;

    public SetupUpdateGateway(IPage page)
    {
        _page = page;
    }

    public async Task OpenAsync()
    {
        await _page.GotoAsync("/card/setup");
        await _page.Locator(".setup-sections-accordion").WaitForAsync();

        var content = _page.Locator(".setup-update-tab");
        if (await content.CountAsync() == 0)
        {
            var toggles = _page.Locator(".setup-section-toggle");
            var count = await toggles.CountAsync();
            for (var i = 0; i < count; i++)
            {
                var toggle = toggles.Nth(i);
                var text = await toggle.InnerTextAsync();
                if (UpdateSectionToggleText.IsMatch(text))
                {
                    await toggle.ClickAsync();
                    break;
                }
            }
        }

        await content.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15000 });
        await WaitUntilLoadedAsync();
    }

    public async Task CheckNowAsync()
    {
        await CheckNowButton.ClickAsync();

        // No button-based "not busy" wait here (unlike SaveSettingsAsync below): SetupUpdateViewModel.Busy
        // is set true and then back to false within the same request, and both renders complete before the
        // first one reaches the client, so Blazor Server coalesces them into a single patch - the button's
        // disabled attribute is never observably toggled from the outside. Callers must instead assert on the
        // actual result via WaitForAvailableVersionAsync, whose auto-retrying assertion waits for the check's
        // outcome to actually be rendered, regardless of the button's (unreliable) transient disabled state.
    }

    public async Task SetEnabledAsync(bool enabled)
    {
        var isChecked = await EnabledCheckbox.IsCheckedAsync();
        if (isChecked != enabled)
        {
            await EnabledCheckbox.ClickAsync();
        }
    }

    public Task<bool> IsEnabledCheckedAsync() => EnabledCheckbox.IsCheckedAsync();

    public async Task AllowChecksAnyTimeAsync()
    {
        await SourceCheckStartTimeInput.FillAsync("00:00");
        await SourceCheckEndTimeInput.FillAsync("23:59");
    }

    public async Task SaveSettingsAsync()
    {
        await SaveSettingsButton.ClickAsync();
        await WaitUntilSaveCompletedAsync();
    }

    public Task<string> GetStatusValueAsync() => ReadTextAsync(StatusValue);

    public Task<string> GetAvailableVersionValueAsync() => ReadTextAsync(AvailableVersionValue);

    /// <summary>
    /// Waits until the available-version definition value equals <paramref name="expectedVersion"/>.
    /// <see cref="CheckNowAsync"/> does not itself wait for the check to complete (see its remarks), so this is
    /// the primary way callers observe completion: a plain one-shot read right after the click would very
    /// likely still see the pre-check value, so this uses Playwright's auto-retrying locator assertion instead.
    /// </summary>
    /// <param name="expectedVersion">The version expected to appear once the check result is fully rendered.</param>
    public Task WaitForAvailableVersionAsync(string expectedVersion)
        => Microsoft.Playwright.Assertions.Expect(AvailableVersionValue).ToHaveTextAsync(expectedVersion, new() { Timeout = 15000 });

    private ILocator StatusValue => _page.Locator("[data-testid='update-status-value']");
    private ILocator AvailableVersionValue => _page.Locator("[data-testid='update-available-value']");

    private ILocator EnabledCheckbox => _page.Locator(".setup-update-tab [data-testid='update-enabled-checkbox']");

    // "Jetzt prüfen" and "Speichern" are page-wide ribbon actions (see Ribbon.razor), not elements inside the
    // .setup-update-tab section: they are registered via SetupUpdateViewModel/SetupCardViewModel's
    // GetRibbonRegisterDefinition and rendered by the ribbon with a plain "id" attribute, so a CSS id selector
    // is used instead of a tab-scoped data-testid lookup.
    private ILocator CheckNowButton => _page.Locator("#UpdateCheckNow");

    private ILocator SaveSettingsButton => _page.Locator("#Save");

    private ILocator SourceCheckStartTimeInput => _page.Locator("#setup-update-source-check-start-time");
    private ILocator SourceCheckEndTimeInput => _page.Locator("#setup-update-source-check-end-time");

    private static async Task<string> ReadTextAsync(ILocator locator)
        => (await locator.InnerTextAsync()).Trim();

    private async Task WaitUntilLoadedAsync()
    {
        await _page.Locator(".setup-update-tab dl").WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15000 });
    }

    /// <summary>
    /// Waits until a triggered save of the update settings has completed.
    /// </summary>
    /// <remarks>
    /// The "Speichern" ribbon button is shared across all setup sections and is disabled whenever
    /// <c>Saving || !HasPendingChanges</c> at the card level. <see cref="SaveSettingsAsync"/> is only ever
    /// called after a change was made (so the button reads enabled just before the click); once the save
    /// completes, <c>HasPendingChanges</c> goes back to <c>false</c>, so the button becomes disabled again -
    /// a genuine before/after transition. This is unlike the "Jetzt prüfen" button (see
    /// <see cref="CheckNowAsync"/>), whose disabled state is identical before the click and after the
    /// operation completes (<c>Busy</c> flips back to <c>false</c> and <c>Status</c> stays non-null), so a
    /// transient busy render there - even if it reached the client - could never be told apart from the
    /// steady idle state.
    /// </remarks>
    private Task WaitUntilSaveCompletedAsync()
        => Microsoft.Playwright.Assertions.Expect(SaveSettingsButton).ToBeDisabledAsync(new() { Timeout = 15000 });
}
