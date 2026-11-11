using FinanceManager.Shared.Dtos.Postings;
using FinanceManager.Shared.Dtos.SavingsPlans;

namespace FinanceManager.Tests.E2E;

/// <summary>
/// End-to-end verification that the five ribbon actions of the "linked information" group on the
/// posting card (<c>/card/postings/{id}</c>) are actually wired: the four navigation actions open
/// the card of the respective linked entity through the generic <c>OpenPostings</c> URL handler
/// (including the <c>back</c> parameter), and <c>OpenAttachments</c> opens the attachments overlay.
/// Buttons stay disabled when their link - or the posting itself - is missing.
/// </summary>
[Collection(PlaywrightCollection.CollectionName)]
public sealed class PostingsCardLinkedActionsPlaywrightTests
{
    private readonly PlaywrightWebAppFixture _fixture;

    /// <summary>
    /// Initializes a new instance of the <see cref="PostingsCardLinkedActionsPlaywrightTests"/> class.
    /// </summary>
    /// <param name="fixture">Shared Playwright web app fixture providing the browser and test server.</param>
    public PostingsCardLinkedActionsPlaywrightTests(PlaywrightWebAppFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// Clicking "#OpenAccount" on a bank posting navigates to the linked account's card including a
    /// <c>back</c> parameter, and the account card's Back action returns to the posting card.
    /// </summary>
    [Fact]
    public async Task PostingsCard_OpenAccount_NavigatesToAccountCard_WithBack()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        var user = await LoginNewUserAsync(page, "linked-open-account");

        var seed = new AccountsApiSeedHelper(page, _fixture.DatabasePath, user.Id);
        var account = await seed.CreateAccountWithBalanceAsync(
            $"Linked Konto {Guid.NewGuid():N}",
            "DE50700500000007882990",
            AccountType.Giro,
            120m,
            "Linked Bank",
            [new AccountsApiSeedHelper.SeededPosting(DateTime.UtcNow.Date, -20m)]);

        var posting = await GetSinglePostingAsync(page, account.AccountId);

        await page.GotoAsync($"/card/postings/{posting.Id}");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await page.Locator("#OpenAccount").ClickAsync();
        await page.WaitForURLAsync(url => url.Contains($"/card/accounts/{account.AccountId}"));

        page.Url.Should().Contain($"/card/accounts/{account.AccountId}");
        page.Url.Should().Contain("back=", "the linked card must be able to navigate back to the posting card");

        await page.Locator("#Back").ClickAsync();
        await page.WaitForURLAsync(url => url.Contains($"/card/postings/{posting.Id}"));
        page.Url.Should().Contain($"/card/postings/{posting.Id}");
    }

    /// <summary>
    /// Clicking "#OpenAttachments" on a loaded posting opens the attachments overlay as a modal dialog.
    /// </summary>
    [Fact]
    public async Task PostingsCard_OpenAttachments_OpensOverlay()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        var user = await LoginNewUserAsync(page, "linked-attachments");

        var seed = new AccountsApiSeedHelper(page, _fixture.DatabasePath, user.Id);
        var account = await seed.CreateAccountWithBalanceAsync(
            $"Attachments Konto {Guid.NewGuid():N}",
            "DE50700500000007882991",
            AccountType.Giro,
            80m,
            "Attachments Bank",
            [new AccountsApiSeedHelper.SeededPosting(DateTime.UtcNow.Date, -15m)]);

        var posting = await GetSinglePostingAsync(page, account.AccountId);

        await page.GotoAsync($"/card/postings/{posting.Id}");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await page.Locator("#OpenAttachments").ClickAsync();

        var dialog = page.Locator(".split-dialog");
        await dialog.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        (await dialog.CountAsync()).Should().Be(1, "the attachments overlay must render as a modal dialog");
    }

    /// <summary>
    /// On a bank posting without contact, savings plan or security links the corresponding ribbon
    /// buttons render disabled while "#OpenAccount" and "#OpenAttachments" stay enabled.
    /// </summary>
    [Fact]
    public async Task PostingsCard_LinkedActions_Disabled_WhenNoLink()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        var user = await LoginNewUserAsync(page, "linked-disabled");

        var seed = new AccountsApiSeedHelper(page, _fixture.DatabasePath, user.Id);
        var account = await seed.CreateAccountWithBalanceAsync(
            $"Disabled Konto {Guid.NewGuid():N}",
            "DE50700500000007882992",
            AccountType.Giro,
            60m,
            "Disabled Bank",
            [new AccountsApiSeedHelper.SeededPosting(DateTime.UtcNow.Date, -10m)]);

        var posting = await GetSinglePostingAsync(page, account.AccountId);

        await page.GotoAsync($"/card/postings/{posting.Id}");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        (await page.Locator("#OpenContact").IsDisabledAsync()).Should().BeTrue("the posting has no contact link");
        (await page.Locator("#OpenSavingsPlan").IsDisabledAsync()).Should().BeTrue("the posting has no savings plan link");
        (await page.Locator("#OpenSecurity").IsDisabledAsync()).Should().BeTrue("the posting has no security link");
        (await page.Locator("#OpenAccount").IsDisabledAsync()).Should().BeFalse("the posting has an account link");
        (await page.Locator("#OpenAttachments").IsDisabledAsync()).Should().BeFalse("the posting is loaded");
    }

    /// <summary>
    /// Opening the posting card with a non-existent id renders the card in its error state while all
    /// five linked-information ribbon buttons - including "#OpenAttachments" - render disabled.
    /// </summary>
    [Fact]
    public async Task PostingsCard_LinkedActions_AllDisabled_WhenPostingNotFound()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        await LoginNewUserAsync(page, "linked-not-found");

        await page.GotoAsync($"/card/postings/{Guid.NewGuid()}");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await page.Locator("#OpenAttachments").WaitForAsync(new() { State = WaitForSelectorState.Attached });

        (await page.Locator("#OpenAccount").IsDisabledAsync()).Should().BeTrue();
        (await page.Locator("#OpenContact").IsDisabledAsync()).Should().BeTrue();
        (await page.Locator("#OpenSavingsPlan").IsDisabledAsync()).Should().BeTrue();
        (await page.Locator("#OpenSecurity").IsDisabledAsync()).Should().BeTrue();
        (await page.Locator("#OpenAttachments").IsDisabledAsync()).Should().BeTrue(
            "no attachments overlay may be opened without a loaded posting");
    }

    /// <summary>
    /// On a posting with seeded contact, savings plan and security links the corresponding ribbon
    /// buttons are enabled and navigate to the respective linked card (with <c>back</c> parameter).
    /// </summary>
    [Fact]
    public async Task PostingsCard_OpenLinkedEntities_NavigateToCards()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        var user = await LoginNewUserAsync(page, "linked-entities");

        var seed = new AccountsApiSeedHelper(page, _fixture.DatabasePath, user.Id);
        var contactId = await seed.CreateBankContactAsync($"Linked Kontakt {Guid.NewGuid():N}");
        var savingsPlan = await BrowserApiHelper.PostJsonAsync<SavingsPlanCreateRequest, SavingsPlanDto>(
            page,
            "/api/savings-plans",
            new SavingsPlanCreateRequest($"Linked Plan {Guid.NewGuid():N}", SavingsPlanType.OneTime, 250m, DateTime.UtcNow.Date.AddMonths(6), null, null, null));
        var security = await BrowserApiHelper.PostJsonAsync<SecurityRequest, SecurityDto>(
            page,
            "/api/securities",
            new SecurityRequest { Name = $"Linked Security {Guid.NewGuid():N}", Identifier = $"LNK-{Guid.NewGuid():N}", CurrencyCode = "EUR" });

        var account = await seed.CreateAccountWithBalanceAsync(
            $"Entities Konto {Guid.NewGuid():N}",
            "DE50700500000007882993",
            AccountType.Giro,
            90m,
            "Entities Bank",
            [new AccountsApiSeedHelper.SeededPosting(
                DateTime.UtcNow.Date,
                -25m,
                ContactId: contactId,
                SavingsPlanId: savingsPlan.Id,
                SecurityId: security.Id)]);

        var posting = await GetSinglePostingAsync(page, account.AccountId);

        await page.GotoAsync($"/card/postings/{posting.Id}");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await AssertLinkedNavigationAsync(page, "#OpenContact", "contacts", contactId, posting.Id);
        await AssertLinkedNavigationAsync(page, "#OpenSavingsPlan", "savings-plans", savingsPlan.Id, posting.Id);
        await AssertLinkedNavigationAsync(page, "#OpenSecurity", "securities", security.Id, posting.Id);
    }

    private static async Task AssertLinkedNavigationAsync(IPage page, string buttonSelector, string route, Guid targetId, Guid postingId)
    {
        var button = page.Locator(buttonSelector);
        (await button.IsDisabledAsync()).Should().BeFalse($"{buttonSelector} must be enabled when its link is set");

        await button.ClickAsync();
        await page.WaitForURLAsync(url => url.Contains($"/card/{route}/{targetId}"));
        page.Url.Should().Contain($"/card/{route}/{targetId}");
        page.Url.Should().Contain("back=", "the linked card must be able to navigate back to the posting card");

        await page.GotoAsync($"/card/postings/{postingId}");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await page.Locator(buttonSelector).WaitForAsync(new() { State = WaitForSelectorState.Visible });
    }

    private static async Task<PostingServiceDto> GetSinglePostingAsync(IPage page, Guid accountId)
    {
        var postings = await BrowserApiHelper.GetJsonAsync<List<PostingServiceDto>>(page, $"/api/postings/account/{accountId}");
        return postings.Should().ContainSingle().Which;
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
