namespace FinanceManager.Tests.E2E;

/// <summary>
/// Drives the generic list page (currently only exercised for <c>/list/accounts</c>), which renders either
/// as a table (desktop) or as a stack of cards (mobile) depending on viewport. Tests use this gateway so
/// they can interact with "the account row" without caring which of the two markup shapes is currently
/// rendered - every method here matches both <c>.generic-list-mobile-card</c> and <c>tbody tr</c>.
/// </summary>
public sealed class ListPageGateway
{
    private readonly IPage _page;

    /// <summary>Creates the gateway for the given page.</summary>
    /// <param name="page">The Playwright page to drive.</param>
    public ListPageGateway(IPage page)
    {
        _page = page;
    }

    /// <summary>
    /// Navigates to the accounts list page and waits (up to 30s) for at least one row/card to be visible,
    /// so callers can rely on the list being populated before interacting with it.
    /// </summary>
    public async Task OpenAccountsAsync()
    {
        await _page.GotoAsync("/list/accounts");
        await _page.Locator(".generic-list-mobile-card:visible, tbody tr:visible").First.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 30000 });
    }

    /// <summary>
    /// Waits (up to 30s) for a row/card whose text contains <paramref name="text"/> to become visible,
    /// without clicking it. Useful for asserting that a newly created or renamed account has appeared in
    /// the list after a Blazor Server round-trip.
    /// </summary>
    /// <param name="text">Substring to match against the row's/card's text content, typically an account name.</param>
    public async Task WaitForAccountVisibleAsync(string text)
    {
        await GetAccountRowLocator(text).WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 30000 });
    }

    /// <summary>
    /// Waits (up to 30s) for the row/card matching <paramref name="text"/> to become visible and clicks it,
    /// which is expected to navigate to that account's detail page.
    /// </summary>
    /// <param name="text">Substring to match against the row's/card's text content, typically an account name.</param>
    public async Task OpenRowAsync(string text)
    {
        var row = GetAccountRowLocator(text);
        await row.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 30000 });
        await row.ClickAsync();
    }

    private ILocator GetAccountRowLocator(string text)
        => _page.Locator(".generic-list-mobile-card:visible, tbody tr:visible").Filter(new() { HasText = text }).First;
}
