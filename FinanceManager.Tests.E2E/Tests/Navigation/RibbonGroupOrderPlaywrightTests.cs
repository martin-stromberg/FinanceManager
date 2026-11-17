using FinanceManager.Shared.Dtos.Postings;
using FinanceManager.Shared.Dtos.SavingsPlans;

namespace FinanceManager.Tests.E2E;

/// <summary>
/// End-to-end verification of the ribbon group order on the card detail pages covered by the
/// project grouping convention: on the savings plan card, the bank account card, and the posting
/// card, the "linked information" group must render as the last ribbon group while the navigation
/// group (containing the Back action) renders first.
/// </summary>
[Collection(PlaywrightCollection.CollectionName)]
public sealed class RibbonGroupOrderPlaywrightTests
{
    private readonly PlaywrightWebAppFixture _fixture;

    /// <summary>
    /// Initializes a new instance of the <see cref="RibbonGroupOrderPlaywrightTests"/> class.
    /// </summary>
    /// <param name="fixture">Shared Playwright web app fixture providing the browser and test server.</param>
    public RibbonGroupOrderPlaywrightTests(PlaywrightWebAppFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// On a savings plan's card page the ribbon renders its five groups in the order
    /// navigation → manage → analysis → bookings → linked, with the linked-information
    /// group last.
    /// </summary>
    [Fact]
    public async Task SavingsPlanCard_LinkedGroup_IsLastRibbonGroup()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        await LoginNewUserAsync(page, "ribbon-order-plan");

        var plan = await BrowserApiHelper.PostJsonAsync<SavingsPlanCreateRequest, SavingsPlanDto>(
            page,
            "/api/savings-plans",
            new SavingsPlanCreateRequest($"Plan {Guid.NewGuid():N}", SavingsPlanType.OneTime, 100m, DateTime.UtcNow.Date.AddMonths(6), null, null, null));

        await page.GotoAsync($"/card/savings-plans/{plan.Id}");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await AssertNavigationGroupFirstAndLinkedGroupLastAsync(page);
    }

    /// <summary>
    /// On a bank account's card page the ribbon renders its four groups in the order
    /// navigation → manage → bookings → linked, with the linked-information group last.
    /// </summary>
    [Fact]
    public async Task BankAccountCard_LinkedGroup_IsLastRibbonGroup()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        await LoginNewUserAsync(page, "ribbon-order-account");

        var account = await new AccountsApiSeedHelper(page)
            .CreateAccountAsync($"Ribbon Konto {Guid.NewGuid():N}", "DE50700500000007882997");

        await page.GotoAsync($"/card/accounts/{account.Id}");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await AssertNavigationGroupFirstAndLinkedGroupLastAsync(page);
    }

    /// <summary>
    /// On a posting's card page the ribbon renders its three groups in the order
    /// navigation → actions → linked, with the linked-information group last.
    /// </summary>
    [Fact]
    public async Task PostingsCard_LinkedGroup_IsLastRibbonGroup()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        var user = await LoginNewUserAsync(page, "ribbon-order-posting");

        var seed = new AccountsApiSeedHelper(page, _fixture.DatabasePath, user.Id);
        var account = await seed.CreateAccountWithBalanceAsync(
            $"Ribbon Posting Konto {Guid.NewGuid():N}",
            "DE50700500000007882998",
            AccountType.Giro,
            50m,
            "Ribbon Bank",
            [new AccountsApiSeedHelper.SeededPosting(DateTime.UtcNow.Date, -10m)]);

        var postings = await BrowserApiHelper.GetJsonAsync<List<PostingServiceDto>>(page, $"/api/postings/account/{account.AccountId}");
        var posting = postings.Should().ContainSingle().Which;

        await page.GotoAsync($"/card/postings/{posting.Id}");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await AssertNavigationGroupFirstAndLinkedGroupLastAsync(page);
    }

    private static async Task AssertNavigationGroupFirstAndLinkedGroupLastAsync(IPage page)
    {
        var groups = page.Locator(".fm-ribbon-group");
        await groups.First.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15000 });

        (await groups.First.Locator("#Back").CountAsync())
            .Should().Be(1, "the first ribbon group must contain the Back navigation action");
        (await groups.Last.Locator("#OpenAttachments").CountAsync())
            .Should().Be(1, "the last ribbon group must contain a linked-information action");
    }

    private async Task<FinanceManager.Domain.Users.User> LoginNewUserAsync(IPage page, string prefix)
    {
        var username = $"{prefix}-{Guid.NewGuid():N}";
        const string password = "Secret123";
        var auth = new AuthGateway(page, _fixture.BaseUrl);
        var seeder = new TestUserSeeder(_fixture.DatabasePath);
        var user = await seeder.EnsureUserAsync(username, password, timeZoneId: "UTC");
        await auth.LoginAsync(username, password);
        return user;
    }
}
