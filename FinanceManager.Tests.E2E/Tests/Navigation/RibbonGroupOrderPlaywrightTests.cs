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

    /// <summary>
    /// On the contacts list page the ribbon renders the groups "Verwalten" then "Ansicht" in this
    /// order — the target split after the ribbon rework.
    /// </summary>
    [Fact]
    public async Task ContactsList_RendersManageThenViewGroup()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        await LoginNewUserAsync(page, "ribbon-order-contacts");

        await page.GotoAsync("/list/contacts");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var footers = await GetGroupFooterTextsAsync(page);
        footers.Count.Should().Be(2);
        AssertFooter(footers[0], "Verwalten", "Manage");
        AssertFooter(footers[1], "Ansicht", "View");
    }

    /// <summary>
    /// On the securities list page the ribbon renders "Verwalten", "Ansicht" and "Berichte"
    /// in this order — including the promoted large portfolio-analysis action.
    /// </summary>
    [Fact]
    public async Task SecuritiesList_RendersManageViewReportsGroups()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        await LoginNewUserAsync(page, "ribbon-order-securities");

        await page.GotoAsync("/list/securities");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var footers = await GetGroupFooterTextsAsync(page);
        footers.Count.Should().Be(3);
        AssertFooter(footers[0], "Verwalten", "Manage");
        AssertFooter(footers[1], "Ansicht", "View");
        AssertFooter(footers[2], "Berichte", "Reports");
    }

    /// <summary>
    /// On the users list page (admin) the ribbon renders exactly the "Verwalten" group — the
    /// missing Reload/ClearSearch actions must not leave behind an empty "Ansicht" group.
    /// </summary>
    [Fact]
    public async Task UsersList_RendersNoEmptyViewGroup()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        await LoginNewUserAsync(page, "ribbon-order-users", isAdmin: true);

        await page.GotoAsync("/list/users");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var footers = await GetGroupFooterTextsAsync(page);
        footers.Count.Should().Be(1);
        AssertFooter(footers[0], "Verwalten", "Manage");
        footers.Should().NotContain(t => t == "Ansicht" || t == "View", "no empty view group may render while its actions are missing");
    }

    /// <summary>
    /// On the setup card (admin) the ribbon groups follow the sort convention:
    /// Verwalten → Daten → Profil → KI → Programmversion → Sicherungen.
    /// </summary>
    [Fact]
    public async Task SetupCard_RendersGroupsInConventionOrder()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        await LoginNewUserAsync(page, "ribbon-order-setup", isAdmin: true);

        await page.GotoAsync("/card/setup");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var footers = await GetGroupFooterTextsAsync(page);
        footers.Count.Should().Be(6);
        AssertFooter(footers[0], "Verwalten", "Manage");
        AssertFooter(footers[1], "Daten", "Data");
        AssertFooter(footers[2], "Profil", "Profile");
        AssertFooter(footers[3], "KI", "AI");
        AssertFooter(footers[4], "Programmversion", "Program version");
        AssertFooter(footers[5], "Sicherungen", "Backups");
    }

    /// <summary>
    /// The RebuildAggregates action on the setup card exposes a localized tooltip instead of the
    /// raw resource key — the Pages-localizer key was added alongside the new "Daten" group.
    /// </summary>
    [Fact]
    public async Task SetupCard_RebuildAggregatesTooltip_IsLocalized()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        await LoginNewUserAsync(page, "ribbon-tooltip-setup", isAdmin: true);

        await page.GotoAsync("/card/setup");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var action = page.Locator("button#RebuildAggregates").First;
        await action.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15000 });
        var tooltip = await action.GetAttributeAsync("title");
        tooltip.Should().NotBeNullOrWhiteSpace("the action must carry a tooltip");
        tooltip.Should().NotBe("Hint_RebuildAggregates", "the tooltip must be the translated text, not the raw resource key");
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

    private static async Task<List<string>> GetGroupFooterTextsAsync(IPage page)
    {
        var footers = page.Locator(".fm-ribbon-group-footer");
        await footers.First.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15000 });
        var texts = new List<string>();
        var count = await footers.CountAsync();
        for (var i = 0; i < count; i++)
        {
            // TextContentAsync statt InnerTextAsync: der Gruppen-Footer wird per CSS
            // text-transform: uppercase gerendert; die Assertions pruefen die Ressourcentitel.
            texts.Add((await footers.Nth(i).TextContentAsync() ?? string.Empty).Trim());
        }

        return texts;
    }

    private static void AssertFooter(string actual, params string[] expectedAlternates)
        => expectedAlternates.Should().Contain(actual, $"unexpected ribbon group title '{actual}'");

    private async Task<FinanceManager.Domain.Users.User> LoginNewUserAsync(IPage page, string prefix, bool isAdmin = false)
    {
        var username = $"{prefix}-{Guid.NewGuid():N}";
        const string password = "Secret123";
        var auth = new AuthGateway(page, _fixture.BaseUrl);
        var seeder = new TestUserSeeder(_fixture.DatabasePath);
        var user = await seeder.EnsureUserAsync(username, password, timeZoneId: "UTC", isAdmin: isAdmin);
        await auth.LoginAsync(username, password);
        return user;
    }
}
