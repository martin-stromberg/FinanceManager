using Microsoft.Playwright;

namespace FinanceManager.Tests.E2E;

public sealed class ReportDashboardGateway
{
    private readonly IPage _page;

    public ReportDashboardGateway(IPage page)
    {
        _page = page;
    }

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
