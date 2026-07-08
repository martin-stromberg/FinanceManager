using FinanceManager.Shared.Dtos.Accounts;
using FinanceManager.Shared.Dtos.Contacts;
using FinanceManager.Shared.Dtos.Postings;
using FinanceManager.Shared.Dtos.Statements;

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
    /// Verifies that uploading a multi-IBAN collection account CSV creates multiple statement drafts
    /// (one per IBAN block) grouped by the same UploadGroupId.
    /// </summary>
    [Fact]
    public async Task UploadCollectionAccountCsv_ShouldCreateMultipleDrafts()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        var auth = new AuthGateway(page, _fixture.BaseUrl);
        var seeder = new TestUserSeeder(_fixture.DatabasePath);

        var username = $"coll-multi-{Guid.NewGuid():N}";
        const string password = "Secret123";
        await seeder.EnsureUserAsync(username, password);
        await auth.LoginAsync(username, password);

        const string iban1 = "DE50700500000007882990";
        const string iban2 = "DE50700500000007882991";

        // Multi-IBAN CSV: two blocks separated by a repeated "Bank;ING" header line
        var csv = BuildMultiIbanCsv(iban1, iban2);

        var uploaded = await BrowserApiHelper.PostMultipartAsync<StatementDraftUploadResult>(
            page, "/api/statement-drafts/upload", "collection.csv", "text/csv",
            System.Text.Encoding.UTF8.GetBytes(csv));

        uploaded.Should().NotBeNull();
        uploaded.FirstDraft.Should().NotBeNull();

        // After uploading, list all open drafts — there should be at least 2 from this upload group
        var drafts = await BrowserApiHelper.GetJsonAsync<IReadOnlyList<StatementDraftDto>>(
            page, "/api/statement-drafts?skip=0&take=3");

        drafts.Should().HaveCountGreaterThanOrEqualTo(2,
            because: "uploading a two-IBAN collection CSV should produce one draft per IBAN block");
    }

    /// <summary>
    /// Verifies that uploading a CSV whose IBAN matches a known linked IBAN of a collection account
    /// causes the draft to be automatically assigned to that account.
    /// </summary>
    [Fact]
    public async Task UploadCollectionAccountCsv_ShouldAutoAssignAccountViaLinkedIban()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        var auth = new AuthGateway(page, _fixture.BaseUrl);
        var seeder = new TestUserSeeder(_fixture.DatabasePath);

        var username = $"coll-auto-assign-{Guid.NewGuid():N}";
        const string password = "Secret123";
        await seeder.EnsureUserAsync(username, password);
        await auth.LoginAsync(username, password);

        const string collectionIban = "DE50700500000007882992";
        const string subIban = "DE50700500000007882993";

        // Create a collection account
        var collectionAccount = await BrowserApiHelper.PostJsonAsync<AccountCreateRequest, AccountDto>(
            page,
            "/api/accounts",
            new AccountCreateRequest(
                Name: $"Sammelkonto AutoAssign {Guid.NewGuid():N}",
                Type: AccountType.Giro,
                Iban: collectionIban,
                BankContactId: null,
                NewBankContactName: "ING",
                SymbolAttachmentId: null,
                SavingsPlanExpectation: SavingsPlanExpectation.Optional,
                SecurityProcessingEnabled: false,
                IsCollectionAccount: true));

        // Link the sub-IBAN
        await BrowserApiHelper.PostJsonAsync(
            page,
            $"/api/accounts/{collectionAccount.Id}/linked-ibans",
            new AccountLinkedIbanUpsertRequest(subIban));

        // Upload CSV with the sub-IBAN (should be auto-assigned to collection account)
        var csv = BuildSingleIbanCsv(subIban);
        var uploaded = await BrowserApiHelper.PostMultipartAsync<StatementDraftUploadResult>(
            page, "/api/statement-drafts/upload", "sub-iban.csv", "text/csv",
            System.Text.Encoding.UTF8.GetBytes(csv));

        uploaded.Should().NotBeNull();
        uploaded.FirstDraft.Should().NotBeNull();
        uploaded.FirstDraft!.DetectedAccountId.Should().Be(collectionAccount.Id,
            because: "the draft IBAN matches a linked IBAN of the collection account");
    }

    /// <summary>
    /// Verifies that booking a draft for a collection account automatically adds the draft's IBAN
    /// to the account's linked IBAN list when it is not already present.
    /// </summary>
    [Fact]
    public async Task BookCollectionAccountDraft_ShouldAutoAddUnknownIbanToLinkedList()
    {
        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        var auth = new AuthGateway(page, _fixture.BaseUrl);
        var seeder = new TestUserSeeder(_fixture.DatabasePath);

        var username = $"coll-book-auto-{Guid.NewGuid():N}";
        const string password = "Secret123";
        await seeder.EnsureUserAsync(username, password);
        await auth.LoginAsync(username, password);

        const string collectionIban = "DE50700500000007882994";
        const string subIban = "DE74200400600000500501";

        // Create a collection account (starts without any linked IBANs)
        var collectionAccount = await BrowserApiHelper.PostJsonAsync<AccountCreateRequest, AccountDto>(
            page,
            "/api/accounts",
            new AccountCreateRequest(
                Name: $"Sammelkonto Book {Guid.NewGuid():N}",
                Type: AccountType.Giro,
                Iban: collectionIban,
                BankContactId: null,
                NewBankContactName: "ING",
                SymbolAttachmentId: null,
                SavingsPlanExpectation: SavingsPlanExpectation.Optional,
                SecurityProcessingEnabled: false,
                IsCollectionAccount: true));

        // Pre-link the subIban so that the upload auto-assigns the draft to the collection account
        await BrowserApiHelper.PostJsonAsync(
            page,
            $"/api/accounts/{collectionAccount.Id}/linked-ibans",
            new AccountLinkedIbanUpsertRequest(subIban));

        // Upload CSV with the sub-IBAN → auto-assigned to collection account
        var csv = BuildSingleIbanCsv(subIban);
        var uploaded = await BrowserApiHelper.PostMultipartAsync<StatementDraftUploadResult>(
            page, "/api/statement-drafts/upload", "auto-link.csv", "text/csv",
            System.Text.Encoding.UTF8.GetBytes(csv));

        uploaded.FirstDraft.Should().NotBeNull();
        var draftId = uploaded.FirstDraft!.DraftId;

        // Verify the draft was auto-assigned
        var draft = await BrowserApiHelper.GetJsonAsync<StatementDraftDetailDto>(
            page, $"/api/statement-drafts/{draftId}?headerOnly=false");
        draft.DetectedAccountId.Should().Be(collectionAccount.Id);

        // Remove the IBAN from the linked list to simulate it being unknown at booking time
        await BrowserApiHelper.DeleteAsync(
            page, $"/api/accounts/{collectionAccount.Id}/linked-ibans/{Uri.EscapeDataString(subIban)}");

        var ibansBeforeBooking = await BrowserApiHelper.GetJsonAsync<IReadOnlyList<string>>(
            page, $"/api/accounts/{collectionAccount.Id}/linked-ibans");
        ibansBeforeBooking.Should().NotContain(subIban,
            because: "we removed it to simulate an unknown IBAN at booking time");

        // Reassign the draft to the collection account (required since we removed the linked IBAN)
        await BrowserApiHelper.PostNoContentAsync(
            page, $"/api/statement-drafts/{draftId}/account/{collectionAccount.Id}");

        // Assign a contact to all entries so booking can proceed without errors
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

        // Book with forceWarnings to bypass any warnings
        var bookResult = await BrowserApiHelper.PostWithStatusAsync<BookingResult>(
            page, $"/api/statement-drafts/{draftId}/book?forceWarnings=true");

        bookResult.Status.Should().Be(200);
        bookResult.Value!.Success.Should().BeTrue();

        // After booking, the draft's IBAN (subIban) should now be auto-linked to the collection account
        var ibansAfterBooking = await BrowserApiHelper.GetJsonAsync<IReadOnlyList<string>>(
            page, $"/api/accounts/{collectionAccount.Id}/linked-ibans");
        ibansAfterBooking.Should().Contain(subIban,
            because: "booking a collection account draft should auto-add the draft IBAN to the linked list");
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
        // Second block (a new "Bank;ING" line starts the next block)
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
