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
}
