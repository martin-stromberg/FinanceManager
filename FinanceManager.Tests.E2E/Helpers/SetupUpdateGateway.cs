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
        await WaitUntilNotBusyAsync();
    }

    public async Task SetEnabledAsync(bool enabled)
    {
        var isChecked = await EnabledCheckbox.IsCheckedAsync();
        if (isChecked != enabled)
        {
            await EnabledCheckbox.ClickAsync();
        }
    }

    public Task<bool> IsEnabledAsync() => EnabledCheckbox.IsCheckedAsync();

    public async Task SaveSettingsAsync()
    {
        await SaveSettingsButton.ClickAsync();
        await Microsoft.Playwright.Assertions.Expect(SaveSettingsButton).ToBeDisabledAsync();
    }

    public async Task AllowChecksAnyTimeAsync()
    {
        await SourceCheckStartTimeInput.FillAsync("00:00");
        await SourceCheckEndTimeInput.FillAsync("00:00");
    }

    public Task<string> GetStatusValueAsync() => ReadTextAsync(StatusValue);

    public Task<string> GetAvailableVersionValueAsync() => ReadTextAsync(AvailableVersionValue);

    /// <summary>
    /// Waits until the available-version definition value equals <paramref name="expectedVersion"/>. Blazor
    /// Server applies a render as several sequential DOM patches, so the "not busy" signal (button re-enabled)
    /// can be observed slightly before the status values in the same render have been patched; a plain one-shot
    /// read right after <see cref="CheckNowAsync"/> can therefore race. This uses Playwright's auto-retrying
    /// locator assertion instead of a single read.
    /// </summary>
    /// <param name="expectedVersion">The version expected to appear once the check result is fully rendered.</param>
    public Task WaitForAvailableVersionAsync(string expectedVersion)
        => Microsoft.Playwright.Assertions.Expect(AvailableVersionValue).ToHaveTextAsync(
            expectedVersion,
            new() { Timeout = 15000 });

    private ILocator StatusValue => _page.Locator("#setup-update-status-value");
    private ILocator AvailableVersionValue => _page.Locator("#setup-update-available-version-value");

    private ILocator CheckNowButton => _page.Locator("#UpdateCheckNow");

    private ILocator SaveSettingsButton => _page.Locator("#Save");

    private ILocator EnabledCheckbox => _page.Locator("#setup-update-enabled");
    private ILocator SourceCheckStartTimeInput => _page.Locator("#setup-update-source-check-start-time");
    private ILocator SourceCheckEndTimeInput => _page.Locator("#setup-update-source-check-end-time");

    private static async Task<string> ReadTextAsync(ILocator locator)
        => (await locator.InnerTextAsync()).Trim();

    private async Task WaitUntilLoadedAsync()
    {
        await _page.Locator(".setup-update-tab dl").WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15000 });
    }

    private async Task WaitUntilNotBusyAsync()
    {
        await CheckNowButton.WaitForAsync(new() { State = WaitForSelectorState.Attached, Timeout = 15000 });
        await Microsoft.Playwright.Assertions.Expect(CheckNowButton).ToBeEnabledAsync();
    }
}
