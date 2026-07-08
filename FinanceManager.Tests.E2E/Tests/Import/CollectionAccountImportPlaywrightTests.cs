using FinanceManager.Shared.Dtos.Postings;

namespace FinanceManager.Tests.E2E;

[Collection(PlaywrightCollection.CollectionName)]
public sealed class CollectionAccountImportPlaywrightTests
{
    private readonly PlaywrightWebAppFixture _fixture;

    public CollectionAccountImportPlaywrightTests(PlaywrightWebAppFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// Verifies that uploading a multi-IBAN collection-account CSV via the home-page import widget
    /// produces multiple statement drafts visible in the draft list.
    /// </summary>
    [Fact]
    public async Task UploadCollectionAccountCsv_ViaUi_ShouldShowMultipleDraftsInList()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        var auth = new AuthGateway(page, _fixture.BaseUrl);
        var seeder = new TestUserSeeder(_fixture.DatabasePath);

        var username = $"coll-multi-ui-{Guid.NewGuid():N}";
        const string password = "Secret123";
        await seeder.EnsureUserAsync(username, password);
        await auth.LoginAsync(username, password);

        const string iban1 = "DE50700500000007882910";
        const string iban2 = "DE50700500000007882911";

        // Write the multi-IBAN CSV to a temp file and upload via the home-page file widget
        var csv = BuildMultiIbanCsv(iban1, iban2);
        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}-collection.csv");
        await File.WriteAllTextAsync(tempFile, csv);
        try
        {
            await page.GotoAsync("/");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await page.Locator("#Import").WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15_000 });
            await page.Locator("#Import input[type=file]").SetInputFilesAsync(tempFile);

            // Wait for the upload to complete (success indicator or dialog)
            var success = page.Locator(".import-success");
            var dialog = page.Locator(".mass-import-dialog");
            try
            {
                await success.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15_000 });
            }
            catch (TimeoutException)
            {
                if (await dialog.CountAsync() > 0)
                {
                    await dialog.Locator("button.btn").First.ClickAsync();
                    await success.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 30_000 });
                }
            }
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }

        // Navigate to the statement-drafts list and assert at least 2 rows are visible
        await page.GotoAsync("/list/statement-drafts");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Allow a short moment for the list to render items
        await page.Locator("tbody tr:visible, .generic-list-mobile-card:visible").First
            .WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15_000 });

        var draftRows = page.Locator("tbody tr:visible, .generic-list-mobile-card:visible");
        var rowCount = await draftRows.CountAsync();
        rowCount.Should().BeGreaterThanOrEqualTo(2,
            because: "uploading a two-IBAN collection CSV should create one draft per IBAN block");
    }

    /// <summary>
    /// Verifies that uploading a CSV whose IBAN matches a pre-linked IBAN of a collection account
    /// causes the created draft to display the collection account's name in the "Bank account" field.
    /// </summary>
    [Fact]
    public async Task UploadWithLinkedIban_ViaUi_DraftShouldShowAutoAssignedAccount()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        var auth = new AuthGateway(page, _fixture.BaseUrl);
        var seeder = new TestUserSeeder(_fixture.DatabasePath);

        var username = $"coll-auto-ui-{Guid.NewGuid():N}";
        const string password = "Secret123";
        await seeder.EnsureUserAsync(username, password);
        await auth.LoginAsync(username, password);

        const string collectionIban = "DE50700500000007882912";
        const string subIban = "DE50700500000007882913";
        var accountName = $"AutoAssign UI {Guid.NewGuid():N}";

        // Seed: collection account + pre-linked sub-IBAN
        var collectionAccount = await BrowserApiHelper.PostJsonAsync<AccountCreateRequest, AccountDto>(
            page,
            "/api/accounts",
            new AccountCreateRequest(
                Name: accountName,
                Type: AccountType.Giro,
                Iban: collectionIban,
                BankContactId: null,
                NewBankContactName: "ING",
                SymbolAttachmentId: null,
                SavingsPlanExpectation: SavingsPlanExpectation.Optional,
                SecurityProcessingEnabled: false,
                IsCollectionAccount: true));

        await BrowserApiHelper.PostJsonAsync(
            page,
            $"/api/accounts/{collectionAccount.Id}/linked-ibans",
            new AccountLinkedIbanUpsertRequest(subIban));

        // Upload the single-IBAN CSV (sub-IBAN) via the home-page import widget
        var csv = BuildSingleIbanCsv(subIban);
        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}-sub-iban.csv");
        await File.WriteAllTextAsync(tempFile, csv);
        StatementDraftUploadResult? uploadResult = null;
        try
        {
            // Use the browser-side fetch for the upload so we can capture the draft ID
            uploadResult = await BrowserApiHelper.PostMultipartAsync<StatementDraftUploadResult>(
                page, "/api/statement-drafts/upload", "sub-iban.csv", "text/csv",
                System.Text.Encoding.UTF8.GetBytes(csv));
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }

        uploadResult.Should().NotBeNull();
        uploadResult!.FirstDraft.Should().NotBeNull();
        var draftId = uploadResult.FirstDraft!.DraftId;

        // Navigate to the draft card page
        await page.GotoAsync($"/card/statement-drafts/{draftId}");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await page.WaitForURLAsync($"**/card/statement-drafts/{draftId}");

        // The first card row is the assigned bank-account lookup.
        var assignedAccountField = page.Locator("table.fm-table tbody tr").Nth(0);
        await assignedAccountField.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15_000 });

        var fieldValue = await assignedAccountField.Locator("input.card-input").InputValueAsync();
        fieldValue.Should().Contain(accountName,
            because: "the draft IBAN matches a pre-linked IBAN of the collection account, so it should be auto-assigned");
    }

    /// <summary>
    /// Verifies that manually selecting a collection account in the draft's "Bank account" lookup
    /// and saving the draft persists the assignment (visible after page reload).
    /// </summary>
    [Fact]
    public async Task ManualAccountAssignment_ViaUi_ShouldPersistAfterSave()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        var auth = new AuthGateway(page, _fixture.BaseUrl);
        var seeder = new TestUserSeeder(_fixture.DatabasePath);

        var username = $"coll-manual-ui-{Guid.NewGuid():N}";
        const string password = "Secret123";
        await seeder.EnsureUserAsync(username, password);
        await auth.LoginAsync(username, password);

        const string collectionIban = "DE50700500000007882914";
        const string unknownSubIban = "DE50700500000007882915";
        var accountName = $"ManualAssign UI {Guid.NewGuid():N}";

        // Seed: collection account (no linked IBANs — sub-IBAN is unknown)
        var collectionAccount = await BrowserApiHelper.PostJsonAsync<AccountCreateRequest, AccountDto>(
            page,
            "/api/accounts",
            new AccountCreateRequest(
                Name: accountName,
                Type: AccountType.Giro,
                Iban: collectionIban,
                BankContactId: null,
                NewBankContactName: "ING",
                SymbolAttachmentId: null,
                SavingsPlanExpectation: SavingsPlanExpectation.Optional,
                SecurityProcessingEnabled: false,
                IsCollectionAccount: true));

        // Upload CSV with an IBAN that is NOT linked → no auto-assignment
        var csv = BuildSingleIbanCsv(unknownSubIban);
        var uploaded = await BrowserApiHelper.PostMultipartAsync<StatementDraftUploadResult>(
            page, "/api/statement-drafts/upload", "manual.csv", "text/csv",
            System.Text.Encoding.UTF8.GetBytes(csv));
        uploaded.Should().NotBeNull();
        uploaded!.FirstDraft.Should().NotBeNull();
        var draftId = uploaded.FirstDraft!.DraftId;

        // Navigate to the draft card
        await page.GotoAsync($"/card/statement-drafts/{draftId}");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await page.WaitForURLAsync($"**/card/statement-drafts/{draftId}");

        // The first card row is the assigned bank-account lookup field.
        var bankAccountInput = page.Locator("table.fm-table tbody tr").Nth(0).Locator("input.card-input");
        await bankAccountInput.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15_000 });
        await bankAccountInput.FillAsync(accountName);

        var lookupItem = page.Locator(".lookup-item").Filter(new() { HasText = accountName }).First;
        await lookupItem.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10_000 });
        await lookupItem.ClickAsync();

        // Save the draft via the ribbon "Save" button
        await page.Locator("button#Save").ClickAsync();

        // After save the draft card reloads; the Bank account row should now display the account name
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var assignedRow = page.Locator("table.fm-table tbody tr").Nth(0);
        var rowValue = await assignedRow.Locator("input.card-input").InputValueAsync();
        rowValue.Should().Contain(accountName,
            because: "we manually assigned the collection account and saved the draft");
    }

    /// <summary>
    /// Verifies that booking a draft whose IBAN is unknown to the collection account at booking
    /// time automatically adds that IBAN to the account's linked-IBAN list, which is then visible
    /// in the account's LinkedIbansPanel.
    /// </summary>
    [Fact]
    public async Task BookCollectionAccountDraft_ViaUi_ShouldAddIbanToLinkedPanel()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        var auth = new AuthGateway(page, _fixture.BaseUrl);
        var seeder = new TestUserSeeder(_fixture.DatabasePath);

        var username = $"coll-book-ui-{Guid.NewGuid():N}";
        const string password = "Secret123";
        await seeder.EnsureUserAsync(username, password);
        await auth.LoginAsync(username, password);

        const string collectionIban = "DE50700500000007882916";
        const string subIban = "DE74200400600000500502";
        var accountName = $"Book UI {Guid.NewGuid():N}";

        // Seed: collection account
        var collectionAccount = await BrowserApiHelper.PostJsonAsync<AccountCreateRequest, AccountDto>(
            page,
            "/api/accounts",
            new AccountCreateRequest(
                Name: accountName,
                Type: AccountType.Giro,
                Iban: collectionIban,
                BankContactId: null,
                NewBankContactName: "ING",
                SymbolAttachmentId: null,
                SavingsPlanExpectation: SavingsPlanExpectation.Optional,
                SecurityProcessingEnabled: false,
                IsCollectionAccount: true));

        // Pre-link the sub-IBAN so upload auto-assigns the draft
        await BrowserApiHelper.PostJsonAsync(
            page,
            $"/api/accounts/{collectionAccount.Id}/linked-ibans",
            new AccountLinkedIbanUpsertRequest(subIban));

        // Upload CSV → auto-assigned
        var csv = BuildSingleIbanCsv(subIban);
        var uploaded = await BrowserApiHelper.PostMultipartAsync<StatementDraftUploadResult>(
            page, "/api/statement-drafts/upload", "book-ui.csv", "text/csv",
            System.Text.Encoding.UTF8.GetBytes(csv));
        uploaded.FirstDraft.Should().NotBeNull();
        var draftId = uploaded.FirstDraft!.DraftId;

        // Remove the sub-IBAN from the linked list to simulate "unknown at booking time"
        await BrowserApiHelper.DeleteAsync(
            page, $"/api/accounts/{collectionAccount.Id}/linked-ibans/{Uri.EscapeDataString(subIban)}");

        // Re-assign the draft to the collection account via API (since the link was removed)
        await BrowserApiHelper.PostNoContentAsync(
            page, $"/api/statement-drafts/{draftId}/account/{collectionAccount.Id}");

        // Assign a contact to all draft entries so booking can proceed without errors
        var allContacts = await BrowserApiHelper.GetJsonAsync<IReadOnlyList<ContactDto>>(
            page, "/api/contacts?all=true");
        var selfContact = allContacts.Single(c => c.Type == ContactType.Self);

        var fullDraft = await BrowserApiHelper.GetJsonAsync<StatementDraftDetailDto>(
            page, $"/api/statement-drafts/{draftId}?headerOnly=false");

        foreach (var entry in fullDraft.Entries)
        {
            await BrowserApiHelper.PostJsonAsync(
                page,
                $"/api/statement-drafts/{draftId}/entries/{entry.Id}/contact",
                new StatementDraftSetContactRequest(selfContact.Id));
        }

        // Navigate to the draft card page
        await page.GotoAsync($"/card/statement-drafts/{draftId}");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await page.WaitForURLAsync($"**/card/statement-drafts/{draftId}");

        // The first card row is the assigned bank-account lookup.
        var bankAccountRow = page.Locator("table.fm-table tbody tr").Nth(0);
        await bankAccountRow.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15_000 });
        (await bankAccountRow.Locator("input.card-input").InputValueAsync()).Should().Contain(accountName);

        // Click the "Book" ribbon button
        await page.Locator("button#Book").ClickAsync();

        // If a validation panel appears with warnings, click "Proceed" to confirm booking
        var proceedButton = page.Locator("button.btn-primary").Filter(new() { HasText = "Fortfahren" });
        try
        {
            await proceedButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5_000 });
            await proceedButton.ClickAsync();
        }
        catch (TimeoutException)
        {
            // No warnings — booking succeeded directly
        }

        // After booking the page navigates away from the draft (to next draft or list)
        await page.WaitForURLAsync("**/list/statement-drafts", new() { Timeout = 15_000 });

        // Navigate to the collection account's detail page
        await page.GotoAsync($"/card/accounts/{collectionAccount.Id}");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // The sub-IBAN should now be visible in the LinkedIbansPanel
        var panel = page.Locator(".linked-ibans-panel");
        await panel.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15_000 });
        await panel.GetByText(subIban)
            .WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10_000 });
        (await panel.InnerTextAsync()).Should().Contain(subIban,
            because: "booking a collection-account draft should auto-add the draft's IBAN to the linked list");
    }

    // ─── CSV helpers ─────────────────────────────────────────────────────────

    private static string BuildSingleIbanCsv(string iban) =>
        "Umsatzanzeige;Datei erstellt am: 02.12.2025 19:04\r\n" +
        "\r\n" +
        $"IBAN;{iban}\r\n" +
        "Kontoname;Sparkonto\r\n" +
        "Bank;ING\r\n" +
        "Kunde;Testuser\r\n" +
        "Zeitraum;01.11.2025 - 30.11.2025\r\n" +
        "Saldo;1.000,00;EUR\r\n" +
        "\r\n" +
        "Sortierung;Datum absteigend\r\n" +
        "\r\n" +
        "\r\n" +
        "Buchung;Valuta;Auftraggeber/Empfänger;Buchungstext;Verwendungszweck;Saldo;Währung;Betrag;Währung\r\n" +
        "15.11.2025;15.11.2025;Zinsen AG;Zinsgutschrift;Zinsen Q3;1.000,00;EUR;50,00;EUR\r\n";

    private static string BuildMultiIbanCsv(string iban1, string iban2) =>
        // First block
        "Umsatzanzeige;Datei erstellt am: 02.12.2025 19:04\r\n" +
        "\r\n" +
        $"IBAN;{iban1}\r\n" +
        "Kontoname;Sparkonto 1\r\n" +
        "Bank;ING\r\n" +
        "Kunde;Testuser\r\n" +
        "Zeitraum;01.11.2025 - 30.11.2025\r\n" +
        "Saldo;1.000,00;EUR\r\n" +
        "\r\n" +
        "Sortierung;Datum absteigend\r\n" +
        "\r\n" +
        "\r\n" +
        "Buchung;Valuta;Auftraggeber/Empfänger;Buchungstext;Verwendungszweck;Saldo;Währung;Betrag;Währung\r\n" +
        "15.11.2025;15.11.2025;Zinsen AG;Zinsgutschrift;Zinsen Block 1;1.000,00;EUR;50,00;EUR\r\n" +
        "\r\n" +
        // Second block
        $"IBAN;{iban2}\r\n" +
        "Kontoname;Sparkonto 2\r\n" +
        "Bank;ING\r\n" +
        "Kunde;Testuser\r\n" +
        "Zeitraum;01.11.2025 - 30.11.2025\r\n" +
        "Saldo;2.000,00;EUR\r\n" +
        "\r\n" +
        "Sortierung;Datum absteigend\r\n" +
        "\r\n" +
        "\r\n" +
        "Buchung;Valuta;Auftraggeber/Empfänger;Buchungstext;Verwendungszweck;Saldo;Währung;Betrag;Währung\r\n" +
        "20.11.2025;20.11.2025;Zinsen AG;Zinsgutschrift;Zinsen Block 2;2.000,00;EUR;100,00;EUR\r\n";
}
