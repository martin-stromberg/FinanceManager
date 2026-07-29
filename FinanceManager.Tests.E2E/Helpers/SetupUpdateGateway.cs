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
        var checkbox = _page.Locator(".setup-update-tab input[type=checkbox]");
        var isChecked = await checkbox.IsCheckedAsync();
        if (isChecked != enabled)
        {
            await checkbox.ClickAsync();
        }
    }

    public async Task SaveSettingsAsync()
    {
        await SaveSettingsButton.ClickAsync();
        await WaitUntilNotBusyAsync();
    }

    public Task<string> GetStatusValueAsync() => GetDefinitionValueAsync("update-status-value");

    public Task<string> GetAvailableVersionValueAsync() => GetDefinitionValueAsync("update-available-value");

    /// <summary>
    /// Waits until the available-version definition value equals <paramref name="expectedVersion"/>. Blazor
    /// Server applies a render as several sequential DOM patches, so the "not busy" signal (button re-enabled)
    /// can be observed slightly before the status values in the same render have been patched; a plain one-shot
    /// read right after <see cref="CheckNowAsync"/> can therefore race. This uses Playwright's auto-retrying
    /// locator assertion instead of a single read.
    /// </summary>
    /// <param name="expectedVersion">The version expected to appear once the check result is fully rendered.</param>
    public Task WaitForAvailableVersionAsync(string expectedVersion)
        => Microsoft.Playwright.Assertions.Expect(AvailableVersionValue).ToHaveTextAsync(expectedVersion);

    private ILocator AvailableVersionValue => _page.Locator(".setup-update-tab [data-testid='update-available-value']");

    private ILocator CheckNowButton => _page.Locator(".setup-update-tab [data-testid='update-check-now']");

    private ILocator SaveSettingsButton => _page.Locator(".setup-update-tab [data-testid='update-save-settings']");

    private async Task<string> GetDefinitionValueAsync(string testId)
    {
        var value = _page.Locator($".setup-update-tab [data-testid='{testId}']");
        return (await value.InnerTextAsync()).Trim();
    }

    private async Task WaitUntilLoadedAsync()
    {
        await _page.Locator(".setup-update-tab dl").WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15000 });
    }

    private async Task WaitUntilNotBusyAsync()
    {
        // Only the save/check-now buttons are disabled exclusively by the "Busy" flag; the install and
        // reset-lock buttons are also disabled by unrelated status conditions (not Ready / not locked) and
        // would otherwise make this locator match more than one element even when idle.
        await _page.Locator(".setup-update-tab [data-testid='update-save-settings'][disabled]").WaitForAsync(new() { State = WaitForSelectorState.Detached, Timeout = 15000 });
    }
}
