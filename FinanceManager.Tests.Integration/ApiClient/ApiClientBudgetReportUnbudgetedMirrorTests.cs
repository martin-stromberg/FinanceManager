using FinanceManager.Shared;
using FinanceManager.Shared.Dtos.Budget;
using FinanceManager.Shared.Dtos.Contacts;
using FinanceManager.Shared.Dtos.SavingsPlans;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace FinanceManager.Tests.Integration.ApiClient;

/// <summary>
/// Integration tests for the Budget Report Unbudgeted Mirror functionality.
/// </summary>
public sealed class ApiClientBudgetReportUnbudgetedMirrorTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    /// <summary>
    /// Initializes a new instance of the ApiClientBudgetReportUnbudgetedMirrorTests class using the specified test web
    /// application factory. This constructor sets up the integration test environment for budget report functionality.
    /// </summary>
    /// <param name="factory">The TestWebApplicationFactory used to create a test server for the API client. Must not be null.</param>
    public ApiClientBudgetReportUnbudgetedMirrorTests(TestWebApplicationFactory factory)
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
        await api.Auth_RegisterAsync(new RegisterRequest(username, "Secret123", PreferredLanguage: null, TimeZoneId: null));
    }
    /// <summary>
    /// Integrationstest: Wenn Sparplan-Buchungen auf das Self-Konto gespiegelt werden,
    /// müssen die Unbudgeted-Endpunkte die gespiegelten Self-Buchungen herausfiltern
    /// und nur tatsächlich ungeplante Self-Postings zurückgeben (hier: +12,34 €).
    /// </summary>
    [Fact]
    public async Task BudgetReport_UnbudgetedPostings_ShouldOnlyContainNonMirroredSelfContactPostings_WhenSavingsPlanPostingsMirrorSelfContact()
    {
        var api = CreateClient();
        await EnsureAuthenticatedAsync(api);

        // Ensure account exists
        var accounts = await api.GetAccountsAsync();
        var account = accounts.Count == 0
            ? await api.CreateAccountAsync(new AccountCreateRequest(
                Name: "Test Account",
                Type: AccountType.Giro,
                Iban: "DE50700500000007882989",
                BankContactId: null,
                NewBankContactName: "Test Bank",
                SymbolAttachmentId: null,
                SavingsPlanExpectation: SavingsPlanExpectation.Optional,
                SecurityProcessingEnabled: true))
            : accounts[0];

        // Create entities via API
        var insuranceContact = await api.Contacts_CreateAsync(new FinanceManager.Shared.Dtos.Contacts.ContactCreateRequest(
            Name: "Insurance",
            Type: ContactType.Person,
            CategoryId: null,
            Description: null,
            IsPaymentIntermediary: null,
            Parent: null));

        var savingsPlan = await api.SavingsPlans_CreateAsync(new SavingsPlanCreateRequest
        {
            Name = "Versicherung",
            Type = SavingsPlanType.Recurring,
            TargetAmount = null,
            TargetDate = null,
            Interval = null,
            CategoryId = null,
            ContractNumber = null,
            Parent = null
        });

        var spPurpose = await api.BudgetPurposes_CreateAsync(new BudgetPurposeCreateRequest(
            Name: "Rückstellung Versicherung",
            SourceType: BudgetSourceType.SavingsPlan,
            SourceId: savingsPlan.Id,
            Description: null,
            BudgetCategoryId: null));

        // Self-contact exists by default for each user.
        // There must NOT be a budget purpose for the self-contact.
        var selfContact = (await api.Contacts_ListAsync(type: ContactType.Self, all: true)).Single();

        await api.BudgetRules_CreateAsync(new BudgetRuleCreateRequest(
            BudgetPurposeId: spPurpose.Id,
            BudgetCategoryId: null,
            Amount: -5m,
            Interval: BudgetIntervalType.Monthly,
            CustomIntervalMonths: null,
            StartDate: new DateOnly(2025, 2, 1),
            EndDate: null));

        await api.BudgetRules_CreateAsync(new BudgetRuleCreateRequest(
            BudgetPurposeId: spPurpose.Id,
            BudgetCategoryId: null,
            Amount: 60m,
            Interval: BudgetIntervalType.Monthly,
            CustomIntervalMonths: null,
            StartDate: new DateOnly(2026, 1, 1),
            EndDate: new DateOnly(2026, 1, 1)));

        var contactPurpose = await api.BudgetPurposes_CreateAsync(new BudgetPurposeCreateRequest(
            Name: "Versicherung Jahresbeitrag",
            SourceType: BudgetSourceType.Contact,
            SourceId: insuranceContact.Id,
            Description: null,
            BudgetCategoryId: null));

        await api.BudgetRules_CreateAsync(new BudgetRuleCreateRequest(
            BudgetPurposeId: contactPurpose.Id,
            BudgetCategoryId: null,
            Amount: -60m,
            Interval: BudgetIntervalType.Yearly,
            CustomIntervalMonths: null,
            StartDate: new DateOnly(2026, 1, 1),
            EndDate: null));

        // Create postings via statement draft upload + booking.
        // We create 4 movements:
        // 1) -5 mirrored self-contact + savings plan
        // 2) +60 mirrored self-contact + savings plan
        // 3) -60 insurance contact (contact budget)
        // 4) +12.34 extra self-contact (unbudgeted)
        var csv = "Umsatzanzeige;Datei erstellt am: 31.01.2026 10:00\r\n\r\n" +
                  $"IBAN;{account.Iban}\r\n" +
                  "Kontoname;Girokonto\r\n" +
                  "Bank;ING\r\n" +
                  "Kunde;Admin\r\n" +
                  "Zeitraum;01.01.2026 - 31.01.2026\r\n" +
                  "Saldo;0,00;EUR\r\n\r\n" +
                  "Sortierung;Datum absteigend\r\n\r\n\r\n" +
                  "Buchung;Wertstellungsdatum;Auftraggeber/Empfänger;Buchungstext;Verwendungszweck;Saldo;Währung;Betrag;Währung\r\n" +
                  "27.01.2026;27.01.2026;Self;Überweisung;Extra;0,00;EUR;12,34;EUR\r\n" +
                  "25.01.2026;25.01.2026;Insurance;Überweisung;Jahresbeitrag;0,00;EUR;-60,00;EUR\r\n" +
                  "20.01.2026;20.01.2026;Self;Überweisung;Mirror +60;0,00;EUR;60,00;EUR\r\n" +
                  "10.01.2026;10.01.2026;Self;Überweisung;Mirror -5;0,00;EUR;-5,00;EUR\r\n";

        using var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(csv));
        var upload = await api.StatementDrafts_UploadAsync(ms, "statement_budget_mirror.csv");
        upload.Should().NotBeNull();
        upload!.FirstDraft.Should().NotBeNull();

        var draftId = upload.FirstDraft!.DraftId;
        var draft = await api.StatementDrafts_GetAsync(draftId);
        draft.Should().NotBeNull();
        draft!.Entries.Should().HaveCount(4);

        var byPurpose = draft.Entries.ToDictionary(e => e.Subject ?? string.Empty, e => e);
        byPurpose.Should().ContainKey("Mirror -5");
        byPurpose.Should().ContainKey("Mirror +60");
        byPurpose.Should().ContainKey("Jahresbeitrag");
        byPurpose.Should().ContainKey("Extra");

        // Assign contacts and savings plan
        var mirrorMinus5 = byPurpose["Mirror -5"];
        var mirrorPlus60 = byPurpose["Mirror +60"];
        var insurance = byPurpose["Jahresbeitrag"];
        var extra = byPurpose["Extra"];

        (await api.StatementDrafts_SetEntryContactAsync(draftId, mirrorMinus5.Id, new StatementDraftSetContactRequest(selfContact.Id))).Should().NotBeNull();
        (await api.StatementDrafts_SetEntrySavingsPlanAsync(draftId, mirrorMinus5.Id, new StatementDraftSetSavingsPlanRequest(savingsPlan.Id))).Should().NotBeNull();

        (await api.StatementDrafts_SetEntryContactAsync(draftId, mirrorPlus60.Id, new StatementDraftSetContactRequest(selfContact.Id))).Should().NotBeNull();
        (await api.StatementDrafts_SetEntrySavingsPlanAsync(draftId, mirrorPlus60.Id, new StatementDraftSetSavingsPlanRequest(savingsPlan.Id))).Should().NotBeNull();

        (await api.StatementDrafts_SetEntryContactAsync(draftId, insurance.Id, new StatementDraftSetContactRequest(insuranceContact.Id))).Should().NotBeNull();
        (await api.StatementDrafts_SetEntryContactAsync(draftId, extra.Id, new StatementDraftSetContactRequest(selfContact.Id))).Should().NotBeNull();

        var book = await api.StatementDrafts_BookAsync(draftId, forceWarnings: true);
        book.Should().NotBeNull();
        book!.Success.Should().BeTrue();

        var asOf = new DateOnly(2026, 1, 31);

        var report = await api.Budgets_GetReportAsync(new BudgetReportRequest(
            AsOfDate: asOf,
            Months: 12,
            Interval: BudgetReportInterval.Month,
            ShowTitle: false,
            ShowLineChart: false,
            ShowMonthlyTable: false,
            ShowDetailsTable: true,
            CategoryValueScope: BudgetReportValueScope.TotalRange,
            IncludePurposeRows: true,
            DateBasis: BudgetReportDateBasis.BookingDate));

        // The report may include additional unbudgeted effects depending on booking/grouping logic.
        // The core requirement for this scenario is validated via the unbudgeted postings endpoint below.
        report.Categories.Should().Contain(c => c.Kind == BudgetReportCategoryRowKind.Unbudgeted);

        var from = new DateTime(2025, 2, 1);
        var to = new DateTime(2026, 1, 31, 23, 59, 59);
        var unbudgeted = await api.Budgets_GetUnbudgetedPostingsAsync(from, to, BudgetReportDateBasis.BookingDate);

        // Mirrored self-contact postings must be filtered out (covered via budgeted savings plan postings in same group).
        // Only the extra self-contact posting without a savings plan mirror should remain.
        unbudgeted.Should().ContainSingle();
        unbudgeted[0].ContactId.Should().Be(selfContact.Id);
        unbudgeted[0].Amount.Should().Be(12.34m);
    }
}
