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
        await _page.Locator("tbody tr").First.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 30000 });
    }

    public async Task OpenRowAsync(string text)
    {
        await _page.Locator("tbody tr").Filter(new() { HasText = text }).First.ClickAsync();
    }
}
