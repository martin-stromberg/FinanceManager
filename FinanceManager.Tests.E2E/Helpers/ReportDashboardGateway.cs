using Microsoft.Playwright;

namespace FinanceManager.Tests.E2E;

/// <summary>
/// Drives the report dashboard's editing surface, specifically the "save current dashboard configuration
/// as a named favorite" flow. Tests use this so they don't have to re-implement navigating into edit mode,
/// opening the save dialog, and waiting for the resulting favorite-scoped URL each time they need a
/// favorite to exist.
/// </summary>
public sealed class ReportDashboardGateway
{
    private readonly IPage _page;

    /// <summary>Creates the gateway for the given page.</summary>
    /// <param name="page">The Playwright page to drive.</param>
    public ReportDashboardGateway(IPage page)
    {
        _page = page;
    }

    /// <summary>
    /// Opens the dashboard in edit mode, triggers "save as", fills the favorite's name into the resulting
    /// dialog, confirms it, and waits for the page to redirect to <c>/reports/dashboard?favoriteId=*</c> -
    /// the signal that the favorite was actually persisted server-side rather than the dialog merely closing.
    /// </summary>
    /// <param name="favoriteName">Name to give the new favorite dashboard configuration.</param>
    public async Task SaveFavoriteAsAsync(string favoriteName)
    {
        await _page.GotoAsync("/reports/dashboard?edit=true");
        await _page.Locator("#SaveAs").WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 30000 });
        await _page.Locator("#SaveAs").ClickAsync();
        var dialog = _page.Locator(".modal");
        await dialog.Locator("input[type=text]").FillAsync(favoriteName);
        await dialog.Locator(".dialog-actions .btn").First.ClickAsync();
        await _page.WaitForURLAsync("**/reports/dashboard?favoriteId=*");
    }

    /// <summary>
    /// Opens the dashboard filter dialog via the "FiltersOpen" ribbon action and waits for the
    /// filter modal to be visible (the dialog's loading state may briefly disable the actions,
    /// so callers should wait for an enabled Apply button before confirming). When the page is
    /// already on the dashboard the navigation is skipped: a full navigation would restart the
    /// Blazor circuit and discard in-memory filter state (and drop a favoriteId query parameter),
    /// so applied filters would no longer be visible in the reopened dialog.
    /// </summary>
    /// <param name="edit">Whether to land on the dashboard in edit mode first.</param>
    /// <returns>Locator for the open filter modal.</returns>
    public async Task<ILocator> OpenFilterDialogAsync(bool edit = true)
    {
        if (!_page.Url.Contains("/reports/dashboard", StringComparison.OrdinalIgnoreCase))
        {
            await _page.GotoAsync(edit ? "/reports/dashboard?edit=true" : "/reports/dashboard");
        }
        await _page.Locator("#FiltersOpen").WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 30000 });
        await _page.Locator("#FiltersOpen").ClickAsync();
        var dialog = _page.Locator(".filter-modal");
        await dialog.WaitForAsync();
        return dialog;
    }

    /// <summary>
    /// Types a value into the free-text filter input of the open filter dialog and confirms the
    /// Add button, then waits until the entry is listed. The input is the dialog's text field
    /// carrying the 500-character limit.
    /// </summary>
    /// <param name="dialog">Locator for the open filter modal.</param>
    /// <param name="text">Filter text to add.</param>
    public async Task AddTextFilterAsync(ILocator dialog, string text)
    {
        var input = dialog.Locator("input[type=text][maxlength]");
        await input.FillAsync(text);
        var addButton = dialog.Locator("input[type=text][maxlength] + .btn, input[type=text][maxlength] ~ .btn");
        await addButton.ClickAsync();
        await dialog.Locator(".mk-item.line span").Filter(new LocatorFilterOptions { HasTextString = text }).WaitForAsync();
    }

    /// <summary>
    /// Confirms the open filter dialog with the enabled Apply button and waits for the dialog
    /// to close (filters are copied from the temp buffer into the active state on apply).
    /// </summary>
    /// <param name="dialog">Locator for the open filter modal.</param>
    public async Task ApplyFilterDialogAsync(ILocator dialog)
    {
        var apply = dialog.Locator(".dialog-actions .btn").First;
        await apply.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        await ExpectEnabledAsync(apply);
        await apply.ClickAsync();
        await dialog.WaitForAsync(new() { State = WaitForSelectorState.Hidden });
    }

    /// <summary>
    /// Removes a listed free-text filter entry from the open filter dialog via its remove button
    /// and waits for the entry to disappear.
    /// </summary>
    /// <param name="dialog">Locator for the open filter modal.</param>
    /// <param name="text">Filter text of the entry to remove.</param>
    public async Task RemoveTextFilterAsync(ILocator dialog, string text)
    {
        var entry = dialog.Locator(".mk-item.line").Filter(new LocatorFilterOptions { HasTextString = text });
        await entry.Locator("button").ClickAsync();
        await dialog.Locator(".mk-item.line span").Filter(new LocatorFilterOptions { HasTextString = text }).WaitForAsync(new() { State = WaitForSelectorState.Hidden });
    }

    /// <summary>
    /// Saves the current dashboard configuration (including the active filters) as a new
    /// favorite via the "SaveAs" ribbon action, assuming the dashboard is already open.
    /// </summary>
    /// <param name="favoriteName">Name to give the new favorite.</param>
    public async Task SaveAsAsync(string favoriteName)
    {
        await _page.Locator("#SaveAs").ClickAsync();
        var dialog = _page.Locator(".modal").Last;
        await dialog.Locator("input[type=text]").FillAsync(favoriteName);
        await dialog.Locator(".dialog-actions .btn").First.ClickAsync();
        await _page.WaitForURLAsync("**/reports/dashboard?favoriteId=*");
    }

    private static async Task ExpectEnabledAsync(ILocator button)
    {
        for (var i = 0; i < 100; i++)
        {
            if (await button.IsEnabledAsync())
            {
                return;
            }
            await Task.Delay(200);
        }
    }
}
