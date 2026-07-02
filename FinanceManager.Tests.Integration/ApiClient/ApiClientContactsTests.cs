using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace FinanceManager.Tests.Integration.ApiClient;

public class ApiClientContactsTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public ApiClientContactsTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private FinanceManager.Shared.ApiClient CreateClient()
    {
        var http = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
        return new FinanceManager.Shared.ApiClient(http);
    }

    private async Task EnsureAuthenticatedAsync(FinanceManager.Shared.ApiClient api)
    {
        var username = $"user_{Guid.NewGuid():N}";
        await api.Auth_RegisterAsync(new FinanceManager.Shared.Dtos.Users.RegisterRequest(username, "Secret123", PreferredLanguage: null, TimeZoneId: null));
    }

    [Fact]
    public async Task Contacts_List_Create_Get_Update_Delete_Flow()
    {
        var api = CreateClient();
        await EnsureAuthenticatedAsync(api);

        // list initially contains auto-created Self contact
        var list = await api.Contacts_ListAsync(skip: 0, take: 10);
        list.Should().NotBeNull();
        list.Should().NotBeEmpty();
        list.Should().ContainSingle(c => c.Type == ContactType.Self);
        var initialCount = list.Count;

        // create
        var created = await api.Contacts_CreateAsync(new ContactCreateRequest("Test", ContactType.Bank, null, null, false));
        created.Should().NotBeNull();
        created.Name.Should().Be("Test");

        // get by id
        var got = await api.Contacts_GetAsync(created.Id);
        got.Should().NotBeNull();
        got!.Id.Should().Be(created.Id);

        // update
        var updated = await api.Contacts_UpdateAsync(created.Id, new ContactUpdateRequest("Test2", ContactType.Bank, null, null, false));
        updated.Should().NotBeNull();
        updated!.Name.Should().Be("Test2");

        // aliases
        var addOk = await api.Contacts_AddAliasAsync(created.Id, new AliasCreateRequest("PATTERN"));
        addOk.Should().BeTrue();
        var aliases = await api.Contacts_GetAliasesAsync(created.Id);
        aliases.Should().NotBeNull();
        aliases.Should().ContainSingle(a => a.Pattern == "PATTERN");

        // count should be at least initialCount
        var count = await api.Contacts_CountAsync();
        count.Should().BeGreaterThanOrEqualTo(initialCount);
        // delete
        var delOk = await api.Contacts_DeleteAsync(created.Id);
        delOk.Should().BeTrue();
        var gone = await api.Contacts_GetAsync(created.Id);
        gone.Should().BeNull();
    }

    [Fact]
    public async Task Contacts_Create_WithStatementEntryParent_ShouldAssignCreatedContactToEntry()
    {
        var api = CreateClient();
        await EnsureAuthenticatedAsync(api);

        var (draftId, entryId) = await CreateDraftWithSingleEntryAsync(api, "contact_auto_assign.csv");
        var request = new ContactCreateRequest(
            Name: $"Inline Contact {Guid.NewGuid():N}",
            Type: ContactType.Other,
            CategoryId: null,
            Description: "Created from statement entry context",
            IsPaymentIntermediary: false,
            Parent: new FinanceManager.Shared.Dtos.Common.ParentLinkRequest("statement-drafts/entries", entryId, "ContactId"));

        var created = await api.Contacts_CreateAsync(request);
        created.Should().NotBeNull();

        var draft = await api.StatementDrafts_GetAsync(draftId);
        draft.Should().NotBeNull();
        draft!.Entries.Should().ContainSingle(e => e.Id == entryId && e.ContactId == created.Id);
    }

    [Fact]
    public async Task Contacts_Create_WithInvalidParent_ShouldReturnConflictAndRollbackContactCreate()
    {
        var api = CreateClient();
        await EnsureAuthenticatedAsync(api);
        var countBefore = (await api.Contacts_ListAsync(all: true)).Count;

        Func<Task> act = async () => await api.Contacts_CreateAsync(new ContactCreateRequest(
            Name: $"Inline Contact Invalid Parent {Guid.NewGuid():N}",
            Type: ContactType.Other,
            CategoryId: null,
            Description: "Should fail",
            IsPaymentIntermediary: false,
            Parent: new FinanceManager.Shared.Dtos.Common.ParentLinkRequest("statement-drafts/entries", Guid.NewGuid(), "ContactId")));

        await act.Should().ThrowAsync<HttpRequestException>();
        api.LastErrorCode.Should().Be("Err_Conflict_ParentAssignment");
        api.LastError.Should().NotBeNullOrWhiteSpace();
        var lastError = api.LastError!;
        (lastError.Contains("assignment to the selected statement entry failed", StringComparison.OrdinalIgnoreCase) ||
         lastError.Contains("assignment to the requested entry failed", StringComparison.OrdinalIgnoreCase) ||
         lastError.Contains("Zuordnung zum ausgewählten Kontoauszugseintrag fehlgeschlagen", StringComparison.OrdinalIgnoreCase))
            .Should().BeTrue();

        var countAfter = (await api.Contacts_ListAsync(all: true)).Count;
        countAfter.Should().Be(countBefore);
    }

    /// <summary>
    /// Creates one account, uploads a statement draft and returns the draft/entry identifiers.
    /// </summary>
    private static async Task<(Guid DraftId, Guid EntryId)> CreateDraftWithSingleEntryAsync(FinanceManager.Shared.ApiClient api, string fileName)
    {
        var account = await api.CreateAccountAsync(new AccountCreateRequest(
            Name: $"Statement Account {Guid.NewGuid():N}",
            Type: AccountType.Giro,
            Iban: "DE50700500000007882995",
            BankContactId: null,
            NewBankContactName: "Test Bank",
            SymbolAttachmentId: null,
            SavingsPlanExpectation: SavingsPlanExpectation.Optional,
            SecurityProcessingEnabled: false));

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
                  "Buchung;Wertstellungsdatum;Auftraggeber/Empf�nger;Buchungstext;Verwendungszweck;Saldo;W�hrung;Betrag;W�hrung\r\n" +
                  "02.12.2025;02.12.2025;Inline Contact;�berweisung;Ihr Einkauf;2.776,45;EUR;-206,44;EUR\r\n";

        using var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(csv));
        var upload = await api.StatementDrafts_UploadAsync(ms, fileName);
        upload.Should().NotBeNull();
        var draftId = upload!.FirstDraft!.DraftId;

        var detail = await api.StatementDrafts_GetAsync(draftId);
        detail.Should().NotBeNull();
        var entryId = detail!.Entries.First().Id;

        return (draftId, entryId);
    }
}
