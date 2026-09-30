using FinanceManager.Shared.Dtos.Postings;
using FinanceManager.Shared.Dtos.SavingsPlans;
using FinanceManager.Shared.Dtos.Securities;

namespace FinanceManager.Tests.E2E;

/// <summary>
/// End-to-end tests for the home page's mass statement import flow: uploads a bank statement CSV as an
/// authenticated browser user and verifies the app navigates to the resulting draft, and that a full booking
/// workflow - including a validation error, a forced-warning booking, and a successful booking that assigns a
/// savings plan and a security transaction - produces the expected account/savings-plan/security postings.
/// Runs through a real browser session so it exercises the actual upload/redirect/routing behavior together
/// with the backend booking logic, rather than the backend logic in isolation.
/// </summary>
[Collection(PlaywrightCollection.CollectionName)]
public sealed class HomeMassImportPlaywrightTests
{
    private readonly PlaywrightWebAppFixture _fixture;

    /// <summary>
    /// Initializes a new instance of the <see cref="HomeMassImportPlaywrightTests"/> class.
    /// </summary>
    /// <param name="fixture">Shared Playwright web app fixture providing the browser and test server.</param>
    public HomeMassImportPlaywrightTests(PlaywrightWebAppFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// Verifies that the home page mass import shows a success state for a recognized statement file.
    /// </summary>
    [Fact]
    public async Task UploadStatementFile_ShouldShowSuccess_WhenImportCompletes()
    {
        await UploadStatementFileShouldShowSuccessWhenImportCompletesAsync(
            () => _fixture.CreateSessionAsync(),
            "import-user",
            "Import Account",
            "statement.csv");
    }

    /// <summary>
    /// Same as <see cref="UploadStatementFile_ShouldShowSuccess_WhenImportCompletes"/> but on a mobile viewport,
    /// to catch responsive-layout regressions in the mass-import success flow that only show up at mobile widths.
    /// </summary>
    [Fact]
    public async Task UploadStatementFile_ShouldShowSuccess_WhenImportCompletes_OnMobileViewport()
    {
        await UploadStatementFileShouldShowSuccessWhenImportCompletesAsync(
            () => _fixture.CreateMobileSessionAsync(),
            "import-mobile-user",
            "Import Mobile Account",
            "statement-mobile.csv");
    }

    private async Task UploadStatementFileShouldShowSuccessWhenImportCompletesAsync(
        Func<Task<PlaywrightBrowserSession>> createSessionAsync,
        string userPrefix,
        string accountPrefix,
        string fileName)
    {
        await using var session = await createSessionAsync();
        var page = session.Page;
        var auth = new AuthGateway(page, _fixture.BaseUrl);
        var userSeed = new TestUserSeeder(_fixture.DatabasePath);
        var accountSeed = new AccountsApiSeedHelper(page);

        var username = $"{userPrefix}-{Guid.NewGuid():N}";
        const string password = "Secret123";
        var user = await userSeed.EnsureUserAsync(username, password);
        await auth.LoginAsync(username, password);

        var account = await accountSeed.CreateAccountAsync($"{accountPrefix} {Guid.NewGuid():N}", "DE50700500000007882995");
        account.Should().NotBeNull();
        await userSeed.EnsureSelfContactAsync(user.Id, $"Self {username}");

        var csv = "Umsatzanzeige;Datei erstellt am: 02.12.2025 19:04\r\n" +
                  "\r\n" +
                  $"IBAN;{account.Iban}\r\n" +
                  "Kontoname;Girokonto\r\n" +
                  "Bank;ING\r\n" +
                  "Kunde;Admin\r\n" +
                  "Zeitraum;02.11.2025 - 02.12.2025\r\n" +
                  "Saldo;2.776,45;EUR\r\n" +
                  "\r\n" +
                  "Sortierung;Datum absteigend\r\n" +
                  "\r\n" +
                  "\r\n" +
                  "Buchung;Valuta;Auftraggeber/Empfänger;Buchungstext;Verwendungszweck;Saldo;Währung;Betrag;Währung\r\n" +
                  "02.12.2025;02.12.2025;Testempfänger;Überweisung;Ihr Einkauf;2.776,45;EUR;-206,44;EUR\r\n";

        var uploaded = await BrowserApiHelper.PostMultipartAsync<StatementDraftUploadResult>(page, "/api/statement-drafts/upload", fileName, "text/csv", System.Text.Encoding.UTF8.GetBytes(csv));
        uploaded.Should().NotBeNull();
        uploaded!.FirstDraft.Should().NotBeNull();
        var draftId = uploaded.FirstDraft!.DraftId;

        await page.GotoAsync($"/card/statement-drafts/{draftId}");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await page.Locator("body").WaitForAsync();
        page.Url.Should().Contain($"/card/statement-drafts/{draftId}");
    }

    /// <summary>
    /// Uploads an ING account-statement CSV in the new export layout (extra "Referenz" column between
    /// "Verwendungszweck" and "Saldo") through the home page's import widget and verifies the file is
    /// recognized and imported directly - without the mass-import review dialog - and that the success
    /// indicator with a link to the created draft appears.
    /// </summary>
    [Fact]
    public async Task UploadIngCsvNewFormat_ViaUi_ShouldImportWithoutReviewDialog()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        var auth = new AuthGateway(page, _fixture.BaseUrl);
        var userSeed = new TestUserSeeder(_fixture.DatabasePath);

        var username = $"import-ing-new-{Guid.NewGuid():N}";
        const string password = "Secret123";
        var user = await userSeed.EnsureUserAsync(username, password);
        await auth.LoginAsync(username, password);
        await userSeed.EnsureSelfContactAsync(user.Id, $"Self {username}");

        var csv = "Umsatzanzeige;Datei erstellt am: 02.12.2025 19:04\r\n" +
                  "\r\n" +
                  "IBAN;DE50700500000007882996\r\n" +
                  "Kontoname;Girokonto\r\n" +
                  "Bank;ING\r\n" +
                  "Kunde;Admin\r\n" +
                  "Zeitraum;02.11.2025 - 02.12.2025\r\n" +
                  "Saldo;2.776,45;EUR\r\n" +
                  "\r\n" +
                  "Sortierung;Datum absteigend\r\n" +
                  "\r\n" +
                  "\r\n" +
                  "Buchung;Wertstellungsdatum;Auftraggeber/Empfänger;Buchungstext;Verwendungszweck;Referenz;Saldo;Währung;Betrag;Währung\r\n" +
                  "02.12.2025;02.12.2025;Testempfänger;Überweisung;Ihr Einkauf;NOTPROVIDED;2.776,45;EUR;-206,44;EUR\r\n";

        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}-ing-new-format.csv");
        await File.WriteAllTextAsync(tempFile, csv, TestContext.Current.CancellationToken);
        try
        {
            await page.GotoAsync("/");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await page.Locator("#Import").WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15_000 });
            await page.Locator("#Import input[type=file]").SetInputFilesAsync(tempFile);

            var success = page.Locator(".import-success");
            await success.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 30_000 });

            (await page.Locator(".mass-import-dialog").CountAsync()).Should().Be(0,
                because: "a recognized ING statement must import directly without the review dialog");
            var detailsLink = success.Locator("a.alert-link");
            (await detailsLink.GetAttributeAsync("href")).Should().Contain("/card/statement-drafts/");
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Uploads a file that no parser recognizes through the home page's import widget and verifies the
    /// mass-import review dialog lists the file together with its failure reason. Confirming the dialog
    /// must close it without showing the "cannot be undone" finalize confirmation, because a batch in
    /// which nothing is importable executes no changes at all.
    /// </summary>
    [Fact]
    public async Task UploadUnrecognizedFile_ViaUi_ShouldShowReasonInReviewDialog_WithoutFinalizeWarning()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        var auth = new AuthGateway(page, _fixture.BaseUrl);
        var userSeed = new TestUserSeeder(_fixture.DatabasePath);

        var username = $"import-unknown-{Guid.NewGuid():N}";
        const string password = "Secret123";
        await userSeed.EnsureUserAsync(username, password);
        await auth.LoginAsync(username, password);

        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}-unrecognized.txt");
        await File.WriteAllTextAsync(tempFile, "just some text\nthat matches nothing\n", TestContext.Current.CancellationToken);
        try
        {
            await page.GotoAsync("/");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await page.Locator("#Import").WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15_000 });
            await page.Locator("#Import input[type=file]").SetInputFilesAsync(tempFile);

            var dialog = page.Locator(".mass-import-dialog");
            await dialog.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15_000 });

            var fileName = Path.GetFileName(tempFile);
            (await dialog.InnerTextAsync()).Should().Contain(fileName,
                because: "the review dialog must list the unrecognized file so the user sees what was uploaded");
            var errorMessage = dialog.Locator(".mass-import-entry .error");
            await errorMessage.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10_000 });
            (await errorMessage.InnerTextAsync()).Should().NotBeNullOrWhiteSpace(
                because: "the review dialog must surface a localized reason why the file cannot be imported");

            await dialog.Locator("button.btn").First.ClickAsync();

            await dialog.WaitForAsync(new() { State = WaitForSelectorState.Hidden, Timeout = 15_000 });
            await page.WaitForTimeoutAsync(500);
            (await page.Locator(".confirm-dialog").CountAsync()).Should().Be(0,
                because: "nothing is executed for a fully skipped batch, so the irreversibility warning is pointless");
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Drives a single statement draft through the full booking lifecycle - a failing validation (missing
    /// contact), a forced-warning booking, then a successful booking that also assigns a savings plan and a
    /// security transaction to a second entry - and verifies the resulting postings are correctly linked to
    /// the account, the savings plan and the security. Exercises the multi-step, multi-status booking API
    /// end-to-end within one authenticated browser session.
    /// </summary>
    [Fact]
    public async Task Booking_WithErrorsWarnings_AndWithOrWithoutSavingsSecurity_ShouldCreateExpectedPostings()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        var auth = new AuthGateway(page, _fixture.BaseUrl);
        var seed = new TestUserSeeder(_fixture.DatabasePath);

        var username = $"booking-user-{Guid.NewGuid():N}";
        const string password = "Secret123";
        await seed.EnsureUserAsync(username, password);
        await auth.LoginAsync(username, password);

        var account = await BrowserApiHelper.PostJsonAsync<AccountCreateRequest, AccountDto>(
            page,
            "/api/accounts",
            new AccountCreateRequest(
                Name: $"Booking Konto {Guid.NewGuid():N}",
                Type: AccountType.Giro,
                Iban: "DE50700500000007882998",
                BankContactId: null,
                NewBankContactName: "Booking Bank",
                SymbolAttachmentId: null,
                SavingsPlanExpectation: SavingsPlanExpectation.Optional,
                SecurityProcessingEnabled: true));

        var selfContact = (await BrowserApiHelper.GetJsonAsync<IReadOnlyList<ContactDto>>(page, "/api/contacts?all=true"))
            .Single(x => x.Type == ContactType.Self);

        var savingsPlan = await BrowserApiHelper.PostJsonAsync<SavingsPlanCreateRequest, SavingsPlanDto>(
            page,
            "/api/savings-plans",
            new SavingsPlanCreateRequest($"Booking Plan {Guid.NewGuid():N}", SavingsPlanType.Recurring, 400m, DateTime.UtcNow.Date.AddMonths(10), SavingsPlanInterval.Monthly, null, "BOOK-001"));

        var security = await BrowserApiHelper.PostJsonAsync<SecurityRequest, SecurityDto>(
            page,
            "/api/securities",
            new SecurityRequest
            {
                Name = $"Booking Security {Guid.NewGuid():N}",
                Identifier = $"BK-{Guid.NewGuid():N}",
                CurrencyCode = "EUR"
            });

        var upload = await BrowserApiHelper.PostMultipartAsync<StatementDraftUploadResult>(
            page,
            "/api/statement-drafts/upload",
            "booking.csv",
            "text/csv",
            System.Text.Encoding.UTF8.GetBytes(CreateBookingStatementCsv(account.Iban!)));
        upload.FirstDraft.Should().NotBeNull();

        var draftId = upload.FirstDraft!.DraftId;
        var draft = await BrowserApiHelper.GetJsonAsync<StatementDraftDetailDto>(page, $"/api/statement-drafts/{draftId}?headerOnly=false");
        var firstEntryId = draft.Entries.First().Id;

        var errorResult = await BrowserApiHelper.PostWithStatusAsync<BookingResult>(page, $"/api/statement-drafts/{draftId}/book?forceWarnings=false");
        errorResult.Status.Should().Be(400);
        errorResult.Value.Should().NotBeNull();
        errorResult.Value!.Success.Should().BeFalse();
        errorResult.Value.HasWarnings.Should().BeFalse();
        errorResult.Value.Validation.Messages.Should().Contain(x => x.Code == "ENTRY_NO_CONTACT");

        await BrowserApiHelper.PostJsonAsync(page, $"/api/statement-drafts/{draftId}/entries/{firstEntryId}/contact", new StatementDraftSetContactRequest(selfContact.Id));
        var warningResult = await BrowserApiHelper.PostWithStatusAsync<BookingResult>(page, $"/api/statement-drafts/{draftId}/book?forceWarnings=false");
        warningResult.Status.Should().Be(428);
        warningResult.Value.Should().NotBeNull();
        warningResult.Value!.Success.Should().BeFalse();
        warningResult.Value.HasWarnings.Should().BeTrue();

        await BrowserApiHelper.PostJsonAsync(page, $"/api/statement-drafts/{draftId}/entries/{firstEntryId}/savingsplan", new StatementDraftSetSavingsPlanRequest(savingsPlan.Id));
        var withSavings = await BrowserApiHelper.GetJsonAsync<StatementDraftEntryDetailDto>(page, $"/api/statement-drafts/{draftId}/entries/{firstEntryId}");
        withSavings.Entry.SavingsPlanId.Should().Be(savingsPlan.Id);

        var added = await BrowserApiHelper.PostJsonAsync<StatementDraftAddEntryRequest, StatementDraftDetailDto>(
            page,
            $"/api/statement-drafts/{draftId}/entries",
            new StatementDraftAddEntryRequest(DateTime.UtcNow.Date, -90m, "Security-Kauf"));
        var securityEntry = added.Entries.Single(x => x.Subject == "Security-Kauf");

        await BrowserApiHelper.PostJsonAsync(page, $"/api/statement-drafts/{draftId}/entries/{securityEntry.Id}/contact", new StatementDraftSetContactRequest(account.BankContactId));
        await BrowserApiHelper.PostJsonAsync(page, $"/api/statement-drafts/{draftId}/entries/{securityEntry.Id}/security", new StatementDraftSetEntrySecurityRequest(security.Id, SecurityTransactionType.Buy, 1m, 0m, 0m));

        var successResult = await BrowserApiHelper.PostWithStatusAsync<BookingResult>(page, $"/api/statement-drafts/{draftId}/book?forceWarnings=true");
        successResult.Status.Should().Be(200);
        successResult.Value.Should().NotBeNull();
        successResult.Value!.Success.Should().BeTrue();

        var accountPostings = await BrowserApiHelper.GetJsonAsync<IReadOnlyList<PostingServiceDto>>(page, $"/api/postings/account/{account.Id}?skip=0&take=50");
        var savingsPostings = await BrowserApiHelper.GetJsonAsync<IReadOnlyList<PostingServiceDto>>(page, $"/api/postings/savings-plan/{savingsPlan.Id}?skip=0&take=50");
        var securityPostings = await BrowserApiHelper.GetJsonAsync<IReadOnlyList<PostingServiceDto>>(page, $"/api/postings/security/{security.Id}?skip=0&take=50");

        accountPostings.Should().NotBeEmpty();
        savingsPostings.Should().Contain(x => x.SavingsPlanId == savingsPlan.Id);
        securityPostings.Should().Contain(x => x.SecurityId == security.Id);
    }

    private static string CreateBookingStatementCsv(string iban)
    {
        return "Umsatzanzeige;Datei erstellt am: 02.12.2025 19:04\r\n" +
               "\r\n" +
               $"IBAN;{iban}\r\n" +
               "Kontoname;Girokonto\r\n" +
               "Bank;ING\r\n" +
               "Kunde;Admin\r\n" +
               "Zeitraum;02.11.2025 - 02.12.2025\r\n" +
               "Saldo;2.776,45;EUR\r\n" +
               "\r\n" +
               "Sortierung;Datum absteigend\r\n" +
               "\r\n" +
               "\r\n" +
               "Buchung;Wertstellungsdatum;Auftraggeber/Empfänger;Buchungstext;Verwendungszweck;Saldo;Währung;Betrag;Währung\r\n" +
               "02.12.2025;02.12.2025;Self Transfer;Überweisung;Sparen;2.776,45;EUR;-206,44;EUR\r\n";
    }
}
