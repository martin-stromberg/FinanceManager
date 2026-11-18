using FinanceManager.Domain.Postings;
using FinanceManager.Domain.Statements;
using FinanceManager.Infrastructure;
using FinanceManager.Shared.Dtos.Postings;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Tests.E2E;

/// <summary>
/// End-to-end tests for posting types ("Postenart", Issue #541): managing account posting rules
/// through the account card's <c>PostingRulesPanel</c>, rule-based classification of statement
/// draft entries, manual override with re-classification protection, transfer of the posting
/// type to booked postings, the postings-list column and the ribbon-triggered background
/// re-evaluation of an account's postings.
/// </summary>
[Collection(PlaywrightCollection.CollectionName)]
public sealed class AccountPostingRulesPlaywrightTests
{
    private readonly PlaywrightWebAppFixture _fixture;

    /// <summary>
    /// Initializes a new instance of the <see cref="AccountPostingRulesPlaywrightTests"/> class.
    /// </summary>
    /// <param name="fixture">Shared Playwright web app fixture providing the browser and test server.</param>
    public AccountPostingRulesPlaywrightTests(PlaywrightWebAppFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// Verifies that a posting rule can be managed through the posting-rules overlay on the account
    /// card: opened via the "OpenPostingRules" ribbon action, added by pressing Enter in the pattern
    /// field, edited through the row's icon buttons and removed again via the trash icon button.
    /// </summary>
    [Fact]
    public async Task ManagePostingRules_ViaUi()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        var auth = new AuthGateway(page, _fixture.BaseUrl);
        var seeder = new TestUserSeeder(_fixture.DatabasePath);

        var username = $"postingrules-ui-{Guid.NewGuid():N}";
        const string password = "Secret123";
        await seeder.EnsureUserAsync(username, password);
        await auth.LoginAsync(username, password);

        var account = await new AccountsApiSeedHelper(page).CreateAccountAsync("E2E Posting Rules", "DE50700500000007882910");

        await page.GotoAsync($"/card/accounts/{account.Id}");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // The panel is no longer embedded; it opens as an overlay via the ribbon action
        var openButton = page.Locator("button#OpenPostingRules");
        await openButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15_000 });
        await openButton.ClickAsync();

        var panel = page.Locator(".split-dialog .posting-rules-panel");
        await panel.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15_000 });

        // Fill the add row: pattern "Zins", posting type Interest (value = (int)BankPostingType.Interest),
        // then submit by pressing Enter in the pattern field (OnKeyDownAsync -> SaveRuleAsync)
        var patternInput = panel.Locator("input[type='text'].card-input");
        await patternInput.FillAsync("Zins");
        await panel.Locator("select.card-input").Nth(1).SelectOptionAsync(((int)BankPostingType.Interest).ToString());
        await panel.Locator("input[type='number'].card-input").FillAsync("0");
        await patternInput.PressAsync("Enter");

        // The rule appears as a row showing the pattern
        await panel.GetByText("Zins", new() { Exact = true })
            .WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10_000 });
        (await panel.InnerTextAsync()).Should().Contain("Zins");

        // The persisted rule is visible via the API as well
        var rules = await BrowserApiHelper.GetJsonAsync<List<AccountPostingRuleDto>>(page, $"/api/accounts/{account.Id}/posting-rules");
        rules.Should().ContainSingle(r => r.Pattern == "Zins" && r.PostingType == BankPostingType.Interest);

        // Filter on a <td> with the exact pattern text so the add-row (whose select contains the
        // option "Zinsen") is excluded. The cell locator must be page-scoped: "Has" is evaluated
        // relative to the candidate <tr>, and a panel-scoped locator (.posting-rules-panel td)
        // can never resolve inside it.
        var patternCell = page.Locator("td")
            .Filter(new() { HasTextRegex = new System.Text.RegularExpressions.Regex("^\\s*Zins\\s*$") });
        var ruleRow = panel.Locator("tr").Filter(new() { Has = patternCell });

        // Edit the rule through its #edit icon button: the input row then shows the #undo
        // (cancel) and #check (save) icon buttons instead of #plus (add)
        await ruleRow.Locator("button.icon-btn:has(use[href*='#edit'])").ClickAsync();
        var saveEditButton = panel.Locator("button.icon-btn:has(use[href*='#check'])");
        await saveEditButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10_000 });
        (await panel.Locator("button.icon-btn:has(use[href*='#undo'])").CountAsync()).Should().Be(1);
        await saveEditButton.ClickAsync();

        // The unchanged rule is still present after saving the edit
        rules = await BrowserApiHelper.GetJsonAsync<List<AccountPostingRuleDto>>(page, $"/api/accounts/{account.Id}/posting-rules");
        rules.Should().ContainSingle(r => r.Pattern == "Zins");

        // Remove the rule through the row's #trash icon button
        await ruleRow.Locator("button.icon-btn.danger:has(use[href*='#trash'])").ClickAsync();
        await patternCell
            .WaitForAsync(new() { State = WaitForSelectorState.Hidden, Timeout = 10_000 });

        rules = await BrowserApiHelper.GetJsonAsync<List<AccountPostingRuleDto>>(page, $"/api/accounts/{account.Id}/posting-rules");
        rules.Should().BeEmpty();
    }

    /// <summary>
    /// Verifies that clicking Add with an empty pattern shows a validation hint inside the panel
    /// and creates no rule, and that the posting-type dropdown does not offer "Unspecified".
    /// </summary>
    [Fact]
    public async Task ManagePostingRules_ValidationError_ViaUi()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        var auth = new AuthGateway(page, _fixture.BaseUrl);
        var seeder = new TestUserSeeder(_fixture.DatabasePath);

        var username = $"postingrules-val-{Guid.NewGuid():N}";
        const string password = "Secret123";
        await seeder.EnsureUserAsync(username, password);
        await auth.LoginAsync(username, password);

        var account = await new AccountsApiSeedHelper(page).CreateAccountAsync("E2E Posting Rules Validation", "DE50700500000007882911");

        await page.GotoAsync($"/card/accounts/{account.Id}");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Open the posting-rules overlay via the ribbon action
        var openButton = page.Locator("button#OpenPostingRules");
        await openButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15_000 });
        await openButton.ClickAsync();

        var panel = page.Locator(".split-dialog .posting-rules-panel");
        await panel.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15_000 });

        // The posting-type select (second select in the add row) must not offer "Unspecified" (value 0)
        var typeSelect = panel.Locator("select.card-input").Nth(1);
        (await typeSelect.Locator("option[value='0']").CountAsync()).Should().Be(0);

        // Click the add icon button (#plus) without entering a pattern -> validation hint inside the panel
        await panel.Locator("button.icon-btn:has(use[href*='#plus'])").ClickAsync();
        var errorCell = panel.Locator("td")
            .Filter(new() { HasTextRegex = new System.Text.RegularExpressions.Regex("Suchtext|pattern", System.Text.RegularExpressions.RegexOptions.IgnoreCase) });
        await errorCell.First.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10_000 });

        var rules = await BrowserApiHelper.GetJsonAsync<List<AccountPostingRuleDto>>(page, $"/api/accounts/{account.Id}/posting-rules");
        rules.Should().BeEmpty();
    }

    /// <summary>
    /// Verifies that the posting-rules panel is not embedded on the account card anymore: it only
    /// appears inside a modal overlay after clicking the "OpenPostingRules" ribbon action, shows the
    /// localized title and disappears again via the overlay's close icon button while the card
    /// underneath stays intact.
    /// </summary>
    [Fact]
    public async Task PostingRulesOverlay_OpensAndCloses_ViaRibbon()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        var auth = new AuthGateway(page, _fixture.BaseUrl);
        var seeder = new TestUserSeeder(_fixture.DatabasePath);

        var username = $"postingrules-overlay-{Guid.NewGuid():N}";
        const string password = "Secret123";
        await seeder.EnsureUserAsync(username, password);
        await auth.LoginAsync(username, password);

        var account = await new AccountsApiSeedHelper(page).CreateAccountAsync("E2E Posting Rules Overlay", "DE50700500000007882917");

        await page.GotoAsync($"/card/accounts/{account.Id}");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // The card is rendered but the posting-rules panel is not embedded anymore
        await page.Locator("button#Save").WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15_000 });
        (await page.Locator(".posting-rules-panel").CountAsync()).Should().Be(0);

        // Ribbon action opens the modal overlay containing the panel and the localized title
        var openButton = page.Locator("button#OpenPostingRules");
        await openButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15_000 });
        await openButton.ClickAsync();

        var dialog = page.Locator(".split-dialog");
        await dialog.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15_000 });
        (await dialog.Locator(".posting-rules-panel").CountAsync()).Should().Be(1);
        (await dialog.Locator("h2").InnerTextAsync()).Should().MatchRegex("Kontierungsregeln|Posting rules");

        // The close icon button removes the overlay; the card content stays intact
        await dialog.Locator("button.icon-btn:has(use[href*='#clear'])").ClickAsync();
        await dialog.WaitForAsync(new() { State = WaitForSelectorState.Hidden, Timeout = 10_000 });
        (await page.Locator(".posting-rules-panel").CountAsync()).Should().Be(0);
        await page.Locator("button#Save").WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10_000 });
    }

    /// <summary>
    /// Verifies that the "OpenPostingRules" ribbon action is rendered disabled on a new, unsaved
    /// account card and carries a localized hint as its tooltip so the overlay cannot be opened.
    /// </summary>
    [Fact]
    public async Task PostingRulesRibbon_Disabled_OnNewAccount()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        var auth = new AuthGateway(page, _fixture.BaseUrl);
        var seeder = new TestUserSeeder(_fixture.DatabasePath);

        var username = $"postingrules-disabled-{Guid.NewGuid():N}";
        const string password = "Secret123";
        await seeder.EnsureUserAsync(username, password);
        await auth.LoginAsync(username, password);

        await page.GotoAsync("/card/accounts/new");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var openButton = page.Locator("button#OpenPostingRules");
        await openButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15_000 });
        (await openButton.IsDisabledAsync()).Should().BeTrue();
        var title = await openButton.GetAttributeAsync("title");
        title.Should().NotBeNullOrEmpty();
        title!.Should().MatchRegex("gespeicherte Konten|saved accounts");

        // No overlay may be present while the button is disabled
        (await page.Locator(".split-dialog").CountAsync()).Should().Be(0);
    }

    /// <summary>
    /// Verifies that a draft entry whose subject matches an account posting rule receives the
    /// rule's posting type during classification and that the type is shown on the entry card
    /// as well as in the draft's entry table.
    /// </summary>
    [Fact]
    public async Task ClassifiedEntry_CardShowsRuleBasedPostingType()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        var auth = new AuthGateway(page, _fixture.BaseUrl);
        var seeder = new TestUserSeeder(_fixture.DatabasePath);

        var username = $"postingtype-classify-{Guid.NewGuid():N}";
        const string password = "Secret123";
        var user = await seeder.EnsureUserAsync(username, password);
        await auth.LoginAsync(username, password);

        var account = await new AccountsApiSeedHelper(page).CreateAccountAsync("E2E PostingType Classify", "DE50700500000007882912");

        await BrowserApiHelper.PostJsonAsync(page, $"/api/accounts/{account.Id}/posting-rules",
            new AccountPostingRuleUpsertRequest(null, PostingRuleField.Subject, "Zins", BankPostingType.Interest, 0));

        var (draftId, entryId) = await SeedDraftWithEntryAsync(user.Id, account.Id, "Zinsgutschrift", 100m);

        await BrowserApiHelper.PostNoContentAsync(page, $"/api/statement-drafts/{draftId}/classify");

        var detail = await BrowserApiHelper.GetJsonAsync<StatementDraftDetailDto>(page, $"/api/statement-drafts/{draftId}");
        var entry = detail.Entries.Should().ContainSingle(e => e.Id == entryId).Which;
        entry.PostingType.Should().Be(BankPostingType.Interest);
        entry.IsPostingTypeManual.Should().BeFalse();

        // The draft card shows the posting type in the entry table (localized cell text)
        await page.GotoAsync($"/card/statement-drafts/{draftId}");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        var typeCell = page.Locator("td:has-text('Zinsen'), td:has-text('Interest')").First;
        await typeCell.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15_000 });

        // The entry card exposes the posting type as an editable enum field
        await page.GotoAsync($"/card/statement-drafts/entries/{entryId}?draftId={draftId}");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        var postingTypeRow = page.Locator("tr")
            .Filter(new() { Has = page.Locator("th").Filter(new() { HasTextRegex = new System.Text.RegularExpressions.Regex("Postenart|Posting type") }) });
        await postingTypeRow.Locator("select.card-input")
            .WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15_000 });
        // "Unspecified" must not be a selectable option
        (await postingTypeRow.Locator("option[value='Unspecified']").CountAsync()).Should().Be(0);
    }

    /// <summary>
    /// Verifies that changing the posting type on the entry card to a manual value persists the
    /// change and that a later re-classification keeps the manually chosen value (overwrite
    /// protection via <c>IsPostingTypeManual</c>).
    /// </summary>
    [Fact]
    public async Task ManualPostingTypeChange_PersistsAfterReclassify()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        var auth = new AuthGateway(page, _fixture.BaseUrl);
        var seeder = new TestUserSeeder(_fixture.DatabasePath);

        var username = $"postingtype-manual-{Guid.NewGuid():N}";
        const string password = "Secret123";
        var user = await seeder.EnsureUserAsync(username, password);
        await auth.LoginAsync(username, password);

        var account = await new AccountsApiSeedHelper(page).CreateAccountAsync("E2E PostingType Manual", "DE50700500000007882913");

        await BrowserApiHelper.PostJsonAsync(page, $"/api/accounts/{account.Id}/posting-rules",
            new AccountPostingRuleUpsertRequest(null, PostingRuleField.Subject, "Zins", BankPostingType.Interest, 0));

        var (draftId, entryId) = await SeedDraftWithEntryAsync(user.Id, account.Id, "Zinsgutschrift", 100m);
        await BrowserApiHelper.PostNoContentAsync(page, $"/api/statement-drafts/{draftId}/classify");

        // Open the entry card, change the posting type to Tax and save through the ribbon
        await page.GotoAsync($"/card/statement-drafts/entries/{entryId}?draftId={draftId}");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        var postingTypeRow = page.Locator("tr")
            .Filter(new() { Has = page.Locator("th").Filter(new() { HasTextRegex = new System.Text.RegularExpressions.Regex("Postenart|Posting type") }) });
        var select = postingTypeRow.Locator("select.card-input");
        await select.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15_000 });
        // The dropdown renders localized labels (e.g. "Steuer" in German), so resolve the
        // option value for the Tax entry instead of relying on the English enum name.
        var taxOptionValue = await select.Locator("option").EvaluateAllAsync<string?>(
            @"opts => {
                const match = Array.from(opts).find(o => /^\s*(steuer|tax)\s*$/i.test(o.textContent));
                return match ? match.getAttribute('value') : null;
            }");
        taxOptionValue.Should().NotBeNull("the posting-type dropdown must offer a 'Tax'/'Steuer' option");
        await select.SelectOptionAsync(taxOptionValue!);
        await page.Locator("button#Save").ClickAsync();

        // Wait for the manual value to be persisted
        await WaitForEntryPostingTypeAsync(page, draftId, entryId, BankPostingType.Tax, expectManual: true);

        // Re-classify the entry - the manual value must survive
        await BrowserApiHelper.PostNoContentAsync(page, $"/api/statement-drafts/{draftId}/entries/{entryId}/classify-entry");
        var detail = await BrowserApiHelper.GetJsonAsync<StatementDraftDetailDto>(page, $"/api/statement-drafts/{draftId}");
        var entry = detail.Entries.Should().ContainSingle(e => e.Id == entryId).Which;
        entry.PostingType.Should().Be(BankPostingType.Tax);
        entry.IsPostingTypeManual.Should().BeTrue();
    }

    /// <summary>
    /// Verifies that booking a classified draft entry transfers its posting type to the created
    /// bank posting and that the postings list of the account shows the type column.
    /// </summary>
    [Fact]
    public async Task BookDraft_TransfersPostingTypeToPosting()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        var auth = new AuthGateway(page, _fixture.BaseUrl);
        var seeder = new TestUserSeeder(_fixture.DatabasePath);

        var username = $"postingtype-book-{Guid.NewGuid():N}";
        const string password = "Secret123";
        var user = await seeder.EnsureUserAsync(username, password);
        await auth.LoginAsync(username, password);

        var account = await new AccountsApiSeedHelper(page).CreateAccountAsync("E2E PostingType Book", "DE50700500000007882914");
        var contact = await BrowserApiHelper.PostJsonAsync<ContactCreateRequest, ContactDto>(
            page, "/api/contacts", new ContactCreateRequest("E2E Bank", ContactType.Organization, null, null, false));

        await BrowserApiHelper.PostJsonAsync(page, $"/api/accounts/{account.Id}/posting-rules",
            new AccountPostingRuleUpsertRequest(null, PostingRuleField.Subject, "Zins", BankPostingType.Interest, 0));

        var (draftId, entryId) = await SeedDraftWithEntryAsync(user.Id, account.Id, "Zinsgutschrift", 100m);
        await BrowserApiHelper.PostNoContentAsync(page, $"/api/statement-drafts/{draftId}/classify");
        await BrowserApiHelper.PostJsonAsync(page, $"/api/statement-drafts/{draftId}/entries/{entryId}/contact", new StatementDraftSetContactRequest(contact.Id));

        var bookResponse = await BrowserApiHelper.PostWithStatusAsync<BookingResult>(page, $"/api/statement-drafts/{draftId}/book?forceWarnings=true");
        bookResponse.Status.Should().Be(200);
        bookResponse.Value.Should().NotBeNull();
        bookResponse.Value!.Success.Should().BeTrue();

        var postings = await BrowserApiHelper.GetJsonAsync<List<PostingServiceDto>>(page, $"/api/postings/account/{account.Id}");
        var bankPosting = postings.Should().ContainSingle(p => p.Kind == PostingKind.Bank).Which;
        bankPosting.PostingType.Should().Be(BankPostingType.Interest);

        await page.GotoAsync($"/list/postings/account/{account.Id}");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        var typeCell = page.Locator("td:has-text('Zinsen'), td:has-text('Interest')").First;
        await typeCell.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15_000 });
    }

    /// <summary>
    /// Verifies that the "re-evaluate posting types" ribbon action on the account card enqueues a
    /// background task whose completion re-evaluates the account's bank postings, so a rule added
    /// after booking is applied and the postings list shows the new posting type.
    /// </summary>
    [Fact]
    public async Task ReevaluatePostingTypes_ViaRibbon()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        var auth = new AuthGateway(page, _fixture.BaseUrl);
        var seeder = new TestUserSeeder(_fixture.DatabasePath);

        var username = $"postingtype-reeval-{Guid.NewGuid():N}";
        const string password = "Secret123";
        var user = await seeder.EnsureUserAsync(username, password);
        await auth.LoginAsync(username, password);

        var account = await new AccountsApiSeedHelper(page).CreateAccountAsync("E2E PostingType Reeval", "DE50700500000007882915");

        // Seed a bank posting with a sign-based posting type directly in the database
        await using (var db = CreateDbContext())
        {
            var posting = new Posting(
                Guid.NewGuid(),
                PostingKind.Bank,
                account.Id,
                contactId: null,
                savingsPlanId: null,
                securityId: null,
                DateTime.Today,
                DateTime.Today,
                100m,
                "Zinsgutschrift",
                "Test Bank",
                "Zinsen Q3",
                null,
                null);
            posting.SetPostingType(BankPostingType.Inflow);
            db.Postings.Add(posting);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // The rule is only added after the posting already exists
        await BrowserApiHelper.PostJsonAsync(page, $"/api/accounts/{account.Id}/posting-rules",
            new AccountPostingRuleUpsertRequest(null, PostingRuleField.Subject, "Zins", BankPostingType.Interest, 0));

        await page.GotoAsync($"/card/accounts/{account.Id}");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var button = page.Locator("button#ReevaluatePostingTypes");
        await button.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15_000 });
        await button.ClickAsync();

        // Wait for the background task to re-evaluate the posting
        await WaitForPostingTypeAsync(page, account.Id, BankPostingType.Interest);

        // The postings list shows the re-evaluated type in the posting-type column
        await page.GotoAsync($"/list/postings/account/{account.Id}");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        var typeCell = page.Locator("td:has-text('Zinsen'), td:has-text('Interest')").First;
        await typeCell.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15_000 });
    }

    /// <summary>
    /// Verifies the sign-based fallback: without any posting rules a negative amount is classified
    /// as "Outflow" and shown accordingly on the draft card.
    /// </summary>
    [Fact]
    public async Task EntryWithoutRules_ShowsSignBasedPostingType()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        var auth = new AuthGateway(page, _fixture.BaseUrl);
        var seeder = new TestUserSeeder(_fixture.DatabasePath);

        var username = $"postingtype-sign-{Guid.NewGuid():N}";
        const string password = "Secret123";
        var user = await seeder.EnsureUserAsync(username, password);
        await auth.LoginAsync(username, password);

        var account = await new AccountsApiSeedHelper(page).CreateAccountAsync("E2E PostingType Sign", "DE50700500000007882916");
        var (draftId, _) = await SeedDraftWithEntryAsync(user.Id, account.Id, "Lastschrift", -50m);

        await BrowserApiHelper.PostNoContentAsync(page, $"/api/statement-drafts/{draftId}/classify");

        var detail = await BrowserApiHelper.GetJsonAsync<StatementDraftDetailDto>(page, $"/api/statement-drafts/{draftId}");
        detail.Entries.Should().ContainSingle(e => e.PostingType == BankPostingType.Outflow);

        await page.GotoAsync($"/card/statement-drafts/{draftId}");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        var typeCell = page.Locator("td:has-text('Ausgang'), td:has-text('Outflow')").First;
        await typeCell.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15_000 });
    }

    private async Task<(Guid DraftId, Guid EntryId)> SeedDraftWithEntryAsync(Guid userId, Guid accountId, string subject, decimal amount)
    {
        await using var db = CreateDbContext();
        var draft = new StatementDraft(userId, "e2e-postingtype.csv", string.Empty, "E2E posting type");
        draft.SetDetectedAccount(accountId);
        var entry = draft.AddEntry(DateTime.Today, amount, subject, "Test Bank", DateTime.Today, null, subject, false, false);
        db.StatementDrafts.Add(draft);
        db.StatementDraftEntries.Add(entry);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return (draft.Id, entry.Id);
    }

    private static async Task WaitForEntryPostingTypeAsync(IPage page, Guid draftId, Guid entryId, BankPostingType expected, bool expectManual)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            var detail = await BrowserApiHelper.GetJsonAsync<StatementDraftDetailDto>(page, $"/api/statement-drafts/{draftId}");
            var entry = detail.Entries.FirstOrDefault(e => e.Id == entryId);
            if (entry != null && entry.PostingType == expected && entry.IsPostingTypeManual == expectManual)
            {
                return;
            }
            await Task.Delay(500, TestContext.Current.CancellationToken);
        }

        var final = await BrowserApiHelper.GetJsonAsync<StatementDraftDetailDto>(page, $"/api/statement-drafts/{draftId}");
        var finalEntry = final.Entries.First(e => e.Id == entryId);
        finalEntry.PostingType.Should().Be(expected);
        finalEntry.IsPostingTypeManual.Should().Be(expectManual);
    }

    private static async Task WaitForPostingTypeAsync(IPage page, Guid accountId, BankPostingType expected)
    {
        var deadline = DateTime.UtcNow.AddSeconds(60);
        while (DateTime.UtcNow < deadline)
        {
            var postings = await BrowserApiHelper.GetJsonAsync<List<PostingServiceDto>>(page, $"/api/postings/account/{accountId}");
            if (postings.Any(p => p.Kind == PostingKind.Bank && p.PostingType == expected))
            {
                return;
            }
            await Task.Delay(500, TestContext.Current.CancellationToken);
        }

        var final = await BrowserApiHelper.GetJsonAsync<List<PostingServiceDto>>(page, $"/api/postings/account/{accountId}");
        final.Should().Contain(p => p.Kind == PostingKind.Bank && p.PostingType == expected);
    }

    private AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={_fixture.DatabasePath}")
            .Options;
        return new AppDbContext(options);
    }
}
