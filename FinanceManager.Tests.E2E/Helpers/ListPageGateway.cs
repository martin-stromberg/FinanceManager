namespace FinanceManager.Tests.E2E;

public sealed class ListPageGateway
{
    private readonly IPage _page;

    public ListPageGateway(IPage page)
    {
        _page = page;
    }

    public async Task OpenAccountsAsync()
    {
        await _page.GotoAsync("/list/accounts");
        await _page.Locator(".generic-list-mobile-card:visible, tbody tr:visible").First.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 30000 });
    }

    public async Task WaitForAccountVisibleAsync(string text)
    {
        await GetAccountRowLocator(text).WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 30000 });
    }

    public async Task OpenRowAsync(string text)
    {
        var row = GetAccountRowLocator(text);
        await row.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 30000 });
        await row.ClickAsync();
    }

    private ILocator GetAccountRowLocator(string text)
        => _page.Locator(".generic-list-mobile-card:visible, tbody tr:visible").Filter(new() { HasText = text }).First;
}
