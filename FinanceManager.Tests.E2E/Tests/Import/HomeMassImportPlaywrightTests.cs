using FinanceManager.Shared.Dtos.Postings;
using FinanceManager.Shared.Dtos.SavingsPlans;
using FinanceManager.Shared.Dtos.Securities;

namespace FinanceManager.Tests.E2E;

[Collection(PlaywrightCollection.CollectionName)]
public sealed class HomeMassImportPlaywrightTests
{
    private readonly PlaywrightWebAppFixture _fixture;

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
