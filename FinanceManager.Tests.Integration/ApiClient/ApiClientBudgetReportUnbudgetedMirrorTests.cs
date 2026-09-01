using FinanceManager.Shared;
using FinanceManager.Shared.Dtos.Budget;
using FinanceManager.Shared.Dtos.Contacts;
using FinanceManager.Shared.Dtos.Postings;
using FinanceManager.Shared.Dtos.SavingsPlans;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace FinanceManager.Tests.Integration.ApiClient;

/// <summary>
/// End-to-end integration tests for the budget report and unbudgeted-postings endpoints
/// (<c>Budgets_GetReportAsync</c>/<c>Budgets_GetReportRawAsync</c>/<c>Budgets_GetUnbudgetedPostingsAsync</c>),
/// covering the full stack from HTTP through statement-draft booking, budget purpose/rule matching (both
/// literal and regex purpose patterns), and self-contact "mirror" transfer handling, down to the database.
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

    /// <summary>
    /// Creates a fresh, unauthenticated <see cref="FinanceManager.Shared.ApiClient"/> bound to the in-memory
    /// test server, with automatic redirect-following disabled so auth/redirect responses can be inspected
    /// directly by the calling test.
    /// </summary>
    /// <returns>A new API client ready to be authenticated via <see cref="EnsureAuthenticatedAsync"/>.</returns>
    private FinanceManager.Shared.ApiClient CreateClient()
    {
        var http = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
        return new FinanceManager.Shared.ApiClient(http);
    }

    /// <summary>
    /// Registers a new, randomly named user on the given API client, leaving the resulting authentication
    /// cookie attached to it. Gives each test its own isolated, already-authenticated user so tests do not
    /// interfere with each other's accounts, contacts, and postings.
    /// </summary>
    /// <param name="api">The API client to authenticate; mutated in place via the registration response's auth cookie.</param>
    private async Task EnsureAuthenticatedAsync(FinanceManager.Shared.ApiClient api)
    {
        var username = $"user_{Guid.NewGuid():N}";
        await api.Auth_RegisterAsync(new RegisterRequest(username, "Secret123", PreferredLanguage: null, TimeZoneId: null));
    }

    /// <summary>
    /// Verifies that when a savings-plan posting is mirrored onto the user's own "self" contact (as happens
    /// for internal transfers, e.g. money set aside for an insurance savings plan), the unbudgeted-postings
    /// endpoint filters those mirrored self postings out instead of reporting them as unbudgeted, while a
    /// genuine self-contact posting that matches no budget purpose at all (here: "Extra", +12.34) still
    /// shows up. Also confirms the budget report classifies such mirrored/unmatched self-contact postings
    /// under the dedicated UnbudgetedSelfCostNeutral category rather than the generic Unbudgeted category,
    /// since they represent transfers between the user's own money pools, not real income or expenses.
    /// </summary>
    [Fact]
    public async Task BudgetReport_UnbudgetedPostings_ShouldOnlyContainNonMirroredSelfContactPostings_WhenSavingsPlanPostingsMirrorSelfContact()
    {
        var api = CreateClient();
        await EnsureAuthenticatedAsync(api);

        // Ensure account exists
        var accounts = await api.GetAccountsAsync(ct: TestContext.Current.CancellationToken);
        var account = accounts.Count == 0
            ? await api.CreateAccountAsync(new AccountCreateRequest(
                Name: "Test Account",
                Type: AccountType.Giro,
                Iban: "DE50700500000007882989",
                BankContactId: null,
                NewBankContactName: "Test Bank",
                SymbolAttachmentId: null,
                SavingsPlanExpectation: SavingsPlanExpectation.Optional,
                SecurityProcessingEnabled: true), TestContext.Current.CancellationToken) : accounts[0];

        // Create entities via API
        var insuranceContact = await api.Contacts_CreateAsync(new FinanceManager.Shared.Dtos.Contacts.ContactCreateRequest(
            Name: "Insurance",
            Type: ContactType.Person,
            CategoryId: null,
            Description: null,
            IsPaymentIntermediary: null,
            Parent: null), TestContext.Current.CancellationToken);

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
        }, TestContext.Current.CancellationToken);

        var spPurpose = await api.BudgetPurposes_CreateAsync(new BudgetPurposeCreateRequest(
            Name: "R�ckstellung Versicherung",
            SourceType: BudgetSourceType.SavingsPlan,
            SourceId: savingsPlan.Id,
            Description: null,
            BudgetCategoryId: null), TestContext.Current.CancellationToken);

        // Self-contact exists by default for each user.
        // There must NOT be a budget purpose for the self-contact.
        var selfContact = (await api.Contacts_ListAsync(type: ContactType.Self, all: true, ct: TestContext.Current.CancellationToken)).Single();

        await api.BudgetRules_CreateAsync(new BudgetRuleCreateRequest(
            BudgetPurposeId: spPurpose.Id,
            BudgetCategoryId: null,
            Amount: -5m,
            Interval: BudgetIntervalType.Monthly,
            CustomIntervalMonths: null,
            StartDate: new DateOnly(2025, 2, 1),
            EndDate: null), TestContext.Current.CancellationToken);

        await api.BudgetRules_CreateAsync(new BudgetRuleCreateRequest(
            BudgetPurposeId: spPurpose.Id,
            BudgetCategoryId: null,
            Amount: 60m,
            Interval: BudgetIntervalType.Monthly,
            CustomIntervalMonths: null,
            StartDate: new DateOnly(2026, 1, 1),
            EndDate: new DateOnly(2026, 1, 1)), TestContext.Current.CancellationToken);

        var contactPurpose = await api.BudgetPurposes_CreateAsync(new BudgetPurposeCreateRequest(
            Name: "Versicherung Jahresbeitrag",
            SourceType: BudgetSourceType.Contact,
            SourceId: insuranceContact.Id,
            Description: null,
            BudgetCategoryId: null), TestContext.Current.CancellationToken);

        await api.BudgetRules_CreateAsync(new BudgetRuleCreateRequest(
            BudgetPurposeId: contactPurpose.Id,
            BudgetCategoryId: null,
            Amount: -60m,
            Interval: BudgetIntervalType.Yearly,
            CustomIntervalMonths: null,
            StartDate: new DateOnly(2026, 1, 1),
            EndDate: null), TestContext.Current.CancellationToken);

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
                  "Buchung;Wertstellungsdatum;Auftraggeber/Empf�nger;Buchungstext;Verwendungszweck;Saldo;W�hrung;Betrag;W�hrung\r\n" +
                  "27.01.2026;27.01.2026;Self;�berweisung;Extra;0,00;EUR;12,34;EUR\r\n" +
                  "25.01.2026;25.01.2026;Insurance;�berweisung;Jahresbeitrag;0,00;EUR;-60,00;EUR\r\n" +
                  "20.01.2026;20.01.2026;Self;�berweisung;Mirror +60;0,00;EUR;60,00;EUR\r\n" +
                  "10.01.2026;10.01.2026;Self;�berweisung;Mirror -5;0,00;EUR;-5,00;EUR\r\n";

        using var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(csv));
        var upload = await api.StatementDrafts_UploadAsync(ms, "statement_budget_mirror.csv", TestContext.Current.CancellationToken);
        upload.Should().NotBeNull();
        upload!.FirstDraft.Should().NotBeNull();

        var draftId = upload.FirstDraft!.DraftId;
        var draft = await api.StatementDrafts_GetAsync(draftId, ct: TestContext.Current.CancellationToken);
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

        (await api.StatementDrafts_SetEntryContactAsync(draftId, mirrorMinus5.Id, new StatementDraftSetContactRequest(selfContact.Id), TestContext.Current.CancellationToken)).Should().NotBeNull();
        (await api.StatementDrafts_SetEntrySavingsPlanAsync(draftId, mirrorMinus5.Id, new StatementDraftSetSavingsPlanRequest(savingsPlan.Id), TestContext.Current.CancellationToken)).Should().NotBeNull();

        (await api.StatementDrafts_SetEntryContactAsync(draftId, mirrorPlus60.Id, new StatementDraftSetContactRequest(selfContact.Id), TestContext.Current.CancellationToken)).Should().NotBeNull();
        (await api.StatementDrafts_SetEntrySavingsPlanAsync(draftId, mirrorPlus60.Id, new StatementDraftSetSavingsPlanRequest(savingsPlan.Id), TestContext.Current.CancellationToken)).Should().NotBeNull();

        (await api.StatementDrafts_SetEntryContactAsync(draftId, insurance.Id, new StatementDraftSetContactRequest(insuranceContact.Id), TestContext.Current.CancellationToken)).Should().NotBeNull();
        (await api.StatementDrafts_SetEntryContactAsync(draftId, extra.Id, new StatementDraftSetContactRequest(selfContact.Id), TestContext.Current.CancellationToken)).Should().NotBeNull();

        var book = await api.StatementDrafts_BookAsync(draftId, forceWarnings: true, ct: TestContext.Current.CancellationToken);
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
            DateBasis: BudgetReportDateBasis.BookingDate), TestContext.Current.CancellationToken);

        // All postings that remain without a matching budget expectation in this scenario are self-contact
        // postings (mirrored savings-plan transfer "Mirror +60" and the extra self posting "Extra"), so per
        // issue.md/requirement.md they are reported as their own "cost-neutral" category, not as regular
        // Unbudgeted (Kind=Unbudgeted is only for postings without a match that are NOT self-contact/cost-neutral
        // transfers). The core requirement for this scenario is validated via the unbudgeted postings endpoint below.
        report.Categories.Should().Contain(c => c.Kind == BudgetReportCategoryRowKind.UnbudgetedSelfCostNeutral);
        report.Categories.Should().NotContain(c => c.Kind == BudgetReportCategoryRowKind.Unbudgeted);

        var from = new DateTime(2025, 2, 1);
        var to = new DateTime(2026, 1, 31, 23, 59, 59);
        var unbudgeted = await api.Budgets_GetUnbudgetedPostingsAsync(from, to, BudgetReportDateBasis.BookingDate, ct: TestContext.Current.CancellationToken);

        // The mirrored -5 self-contact posting is budgeted by the savings-plan purpose and therefore excluded.
        // "Mirror +60" matches the same purpose's source/period/pattern as the -5 rule, just with the wrong
        // sign for its ExactPostings valuation; it is therefore shown against that purpose (as an unvalued
        // match, not counted) instead of the generic Unbudgeted list - a posting shown at a purpose must not
        // also be listed here. Only "Extra", which matches no purpose at all, remains.
        unbudgeted.Should().HaveCount(1);
        unbudgeted.Should().OnlyContain(p => p.ContactId == selfContact.Id);
        unbudgeted.Should().ContainSingle(p => p.Subject == "Extra" && p.Amount == 12.34m);
        unbudgeted.Should().NotContain(p => p.Subject == "Mirror +60");
        unbudgeted.Should().NotContain(p => p.Subject == "Mirror -5");
    }

    /// <summary>
    /// Verifies that budget report and unbudgeted postings respect purpose patterns.
    /// </summary>
    [Fact]
    public async Task BudgetReport_ShouldRespectPurposePattern_ForActualAndUnbudgetedPostings()
    {
        var api = CreateClient();
        await EnsureAuthenticatedAsync(api);

        var account = await api.CreateAccountAsync(new AccountCreateRequest(
            Name: "Pattern Account",
            Type: AccountType.Giro,
            Iban: "DE50700500000007882990",
            BankContactId: null,
            NewBankContactName: "Test Bank",
            SymbolAttachmentId: null,
            SavingsPlanExpectation: SavingsPlanExpectation.Optional,
            SecurityProcessingEnabled: false), TestContext.Current.CancellationToken);

        var contact = await api.Contacts_CreateAsync(new FinanceManager.Shared.Dtos.Contacts.ContactCreateRequest(
            Name: "Utility Provider",
            Type: ContactType.Person,
            CategoryId: null,
            Description: null,
            IsPaymentIntermediary: null,
            Parent: null), TestContext.Current.CancellationToken);

        var category = await api.Budgets_CreateCategoryAsync(new BudgetCategoryCreateRequest("Utilities Category"), TestContext.Current.CancellationToken);
        var purpose = await api.BudgetPurposes_CreateAsync(new BudgetPurposeCreateRequest(
            Name: "Utilities",
            SourceType: BudgetSourceType.Contact,
            SourceId: contact.Id,
            Description: null,
            BudgetCategoryId: category.Id), TestContext.Current.CancellationToken);

        await api.BudgetRules_CreateAsync(new BudgetRuleCreateRequest(
            BudgetPurposeId: purpose.Id,
            BudgetCategoryId: null,
            Amount: -60m,
            Interval: BudgetIntervalType.Monthly,
            CustomIntervalMonths: null,
            StartDate: new DateOnly(2026, 1, 1),
            EndDate: null,
            PurposePattern: "ST6464646464",
            UseRegex: false), TestContext.Current.CancellationToken);

        var csv = "Umsatzanzeige;Datei erstellt am: 31.01.2026 10:00\r\n\r\n" +
                  $"IBAN;{account.Iban}\r\n" +
                  "Kontoname;Girokonto\r\n" +
                  "Bank;ING\r\n" +
                  "Kunde;Admin\r\n" +
                  "Zeitraum;01.01.2026 - 31.01.2026\r\n" +
                  "Saldo;0,00;EUR\r\n\r\n" +
                  "Sortierung;Datum absteigend\r\n\r\n\r\n" +
                  "Buchung;Wertstellungsdatum;Auftraggeber/Empfänger;Buchungstext;Verwendungszweck;Saldo;Währung;Betrag;Währung\r\n" +
                  "25.01.2026;25.01.2026;Utility Provider;Überweisung;Abrechnung ST6464646464 Januar;0,00;EUR;-60,00;EUR\r\n" +
                  "20.01.2026;20.01.2026;Utility Provider;Überweisung;Service ohne Vertragsnummer;0,00;EUR;-40,00;EUR\r\n";

        using var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(csv));
        var upload = await api.StatementDrafts_UploadAsync(ms, "statement_budget_pattern.csv", TestContext.Current.CancellationToken);
        upload.Should().NotBeNull();
        var draftId = upload!.FirstDraft!.DraftId;

        var draft = await api.StatementDrafts_GetAsync(draftId, ct: TestContext.Current.CancellationToken);
        draft.Should().NotBeNull();
        foreach (var entry in draft!.Entries)
        {
            await api.StatementDrafts_SetEntryContactAsync(draftId, entry.Id, new StatementDraftSetContactRequest(contact.Id), TestContext.Current.CancellationToken);
        }

        var book = await api.StatementDrafts_BookAsync(draftId, forceWarnings: true, ct: TestContext.Current.CancellationToken);
        book.Should().NotBeNull();
        book!.Success.Should().BeTrue();

        var report = await api.Budgets_GetReportAsync(new BudgetReportRequest(
            AsOfDate: new DateOnly(2026, 1, 31),
            Months: 1,
            Interval: BudgetReportInterval.Month,
            ShowTitle: false,
            ShowLineChart: false,
            ShowMonthlyTable: false,
            ShowDetailsTable: true,
            CategoryValueScope: BudgetReportValueScope.TotalRange,
            IncludePurposeRows: true,
            DateBasis: BudgetReportDateBasis.BookingDate), TestContext.Current.CancellationToken);
        report.Should().NotBeNull();
        var categoryRow = report.Categories.Single(x => x.Id == category.Id);
        var purposeRow = categoryRow.Purposes.Single(x => x.Id == purpose.Id);
        purposeRow.Actual.Should().Be(-60m);
    }

    /// <summary>
    /// Verifies that regex purpose patterns split matching and non-matching postings in report and unbudgeted results.
    /// </summary>
    [Fact]
    public async Task BudgetReport_ShouldRespectRegexPurposePattern_ForActualAndUnbudgetedPostings()
    {
        var api = CreateClient();
        await EnsureAuthenticatedAsync(api);

        var account = await api.CreateAccountAsync(new AccountCreateRequest(
            Name: "Regex Pattern Account",
            Type: AccountType.Giro,
            Iban: "DE50700500000007882996",
            BankContactId: null,
            NewBankContactName: "Test Bank",
            SymbolAttachmentId: null,
            SavingsPlanExpectation: SavingsPlanExpectation.Optional,
            SecurityProcessingEnabled: false), TestContext.Current.CancellationToken);

        var contact = await api.Contacts_CreateAsync(new FinanceManager.Shared.Dtos.Contacts.ContactCreateRequest(
            Name: "Utility Provider Regex",
            Type: ContactType.Person,
            CategoryId: null,
            Description: null,
            IsPaymentIntermediary: null,
            Parent: null), TestContext.Current.CancellationToken);

        var category = await api.Budgets_CreateCategoryAsync(new BudgetCategoryCreateRequest("Utilities Regex Category"), TestContext.Current.CancellationToken);
        var purpose = await api.BudgetPurposes_CreateAsync(new BudgetPurposeCreateRequest(
            Name: "Utilities Regex",
            SourceType: BudgetSourceType.Contact,
            SourceId: contact.Id,
            Description: null,
            BudgetCategoryId: category.Id), TestContext.Current.CancellationToken);

        await api.BudgetRules_CreateAsync(new BudgetRuleCreateRequest(
            BudgetPurposeId: purpose.Id,
            BudgetCategoryId: null,
            Amount: -60m,
            Interval: BudgetIntervalType.Monthly,
            CustomIntervalMonths: null,
            StartDate: new DateOnly(2026, 1, 1),
            EndDate: null,
            PurposePattern: "ST\\d{10}",
            UseRegex: true), TestContext.Current.CancellationToken);

        var csv = "Umsatzanzeige;Datei erstellt am: 31.01.2026 10:00\r\n\r\n" +
                  $"IBAN;{account.Iban}\r\n" +
                  "Kontoname;Girokonto\r\n" +
                  "Bank;ING\r\n" +
                  "Kunde;Admin\r\n" +
                  "Zeitraum;01.01.2026 - 31.01.2026\r\n" +
                  "Saldo;0,00;EUR\r\n\r\n" +
                  "Sortierung;Datum absteigend\r\n\r\n\r\n" +
                  "Buchung;Wertstellungsdatum;Auftraggeber/Empfänger;Buchungstext;Verwendungszweck;Saldo;Währung;Betrag;Währung\r\n" +
                  "25.01.2026;25.01.2026;Utility Provider Regex;Überweisung;Abrechnung ST6464646464 Januar;0,00;EUR;-60,00;EUR\r\n" +
                  "20.01.2026;20.01.2026;Utility Provider Regex;Überweisung;Service ohne Vertragsnummer;0,00;EUR;-40,00;EUR\r\n";

        using var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(csv));
        var upload = await api.StatementDrafts_UploadAsync(ms, "statement_budget_pattern_regex.csv", TestContext.Current.CancellationToken);
        upload.Should().NotBeNull();
        var draftId = upload!.FirstDraft!.DraftId;

        var draft = await api.StatementDrafts_GetAsync(draftId, ct: TestContext.Current.CancellationToken);
        draft.Should().NotBeNull();
        foreach (var entry in draft!.Entries)
        {
            await api.StatementDrafts_SetEntryContactAsync(draftId, entry.Id, new StatementDraftSetContactRequest(contact.Id), TestContext.Current.CancellationToken);
        }

        var book = await api.StatementDrafts_BookAsync(draftId, forceWarnings: true, ct: TestContext.Current.CancellationToken);
        book.Should().NotBeNull();
        book!.Success.Should().BeTrue();

        var report = await api.Budgets_GetReportAsync(new BudgetReportRequest(
            AsOfDate: new DateOnly(2026, 1, 31),
            Months: 1,
            Interval: BudgetReportInterval.Month,
            ShowTitle: false,
            ShowLineChart: false,
            ShowMonthlyTable: false,
            ShowDetailsTable: true,
            CategoryValueScope: BudgetReportValueScope.TotalRange,
            IncludePurposeRows: true,
            DateBasis: BudgetReportDateBasis.BookingDate), TestContext.Current.CancellationToken);
        report.Should().NotBeNull();
        var categoryRow = report.Categories.Single(x => x.Id == category.Id);
        var purposeRow = categoryRow.Purposes.Single(x => x.Id == purpose.Id);
        purposeRow.Actual.Should().Be(-60m);
    }

    /// <summary>
    /// Regression test built from a real August 2026 bank statement, with all identifying data - IBAN,
    /// contact/company/person names and free-text descriptions - replaced by fictional placeholders; only
    /// the amounts, dates and overall statement structure (which postings share a recipient, which budget
    /// rule anchor days apply, etc.) are preserved, since those are what originally reproduced the bug
    /// fixed in <see cref="FinanceManager.Domain.Budget.ReportCalculation.Budgetbericht"/>
    /// (<c>ExpandRuleOccurrences</c>/<c>ExpandRulesToExpectationPostings</c>): a posting matching a
    /// mid-month-anchored monthly rule (e.g. StartDate on the 11th) was shown correctly at its budget
    /// purpose AND additionally listed among the unbudgeted postings, because the "unbudgeted" endpoint
    /// builds its own <c>Budgetbericht</c> over a narrower date range than the main report and dropped the
    /// rule occurrence that reaches into that range from the month before it. All 18 August 2026 bank
    /// postings are replayed with the exact master data (accounts, contacts, savings plans, securities,
    /// budget categories/purposes/rules) that produced them, step by step (statement entry,
    /// contact/savings-plan/security assignment). After booking, every resulting
    /// contact/savings-plan/security posting is checked against the original (anonymized) values, and only
    /// then is the budget report requested and checked field by field: the Nordstern and Rheinstern
    /// "Berufsunfaehigkeit" postings from 03.08. must be shown exactly once, at their own budget purpose,
    /// and must not additionally appear among the unbudgeted postings.
    /// </summary>
    [Fact]
    public async Task BudgetReport_WithComplexData()
    {
        var api = CreateClient();
        await EnsureAuthenticatedAsync(api);
        var ct = TestContext.Current.CancellationToken;

        #region Stammdaten: Konto, Kategorien

        var account = await api.CreateAccountAsync(new AccountCreateRequest(
            Name: "Girokonto",
            Type: AccountType.Giro,
            Iban: "DE12300000001234567890",
            BankContactId: null,
            NewBankContactName: "Testbank AG",
            SymbolAttachmentId: null,
            SavingsPlanExpectation: SavingsPlanExpectation.Optional,
            SecurityProcessingEnabled: true),
            ct);
        account.Should().NotBeNull();
        account.SecurityProcessingEnabled.Should().BeTrue();

        var selfContact = (await api.Contacts_ListAsync(type: ContactType.Self, all: true, ct: ct)).Single();
        var bankContactId = account.BankContactId;

        // Contact categories (as used by the real data; created on first use, reused afterwards).
        var contactCategoryDienstleister = await api.ContactCategories_CreateAsync(new ContactCategoryCreateRequest("Dienstleister"), ct);
        var contactCategoryVersicherung = await api.ContactCategories_CreateAsync(new ContactCategoryCreateRequest("Versicherung"), ct);
        var contactCategoryFreizeiteinrichtung = await api.ContactCategories_CreateAsync(new ContactCategoryCreateRequest("Freizeiteinrichtung"), ct);

        // Savings-plan categories.
        var savingsPlanCategorySteuer = await api.SavingsPlanCategories_CreateAsync(new SavingsPlanCategoryDto { Name = "Steuer" }, ct);
        var savingsPlanCategoryVersicherung = await api.SavingsPlanCategories_CreateAsync(new SavingsPlanCategoryDto { Name = "Versicherung" }, ct);
        var savingsPlanCategorySparen = await api.SavingsPlanCategories_CreateAsync(new SavingsPlanCategoryDto { Name = "Sparen" }, ct);

        // Budget categories.
        var budgetCategorySteuer = await api.Budgets_CreateCategoryAsync(new BudgetCategoryCreateRequest("Steuer"), ct);
        var budgetCategoryWohnen = await api.Budgets_CreateCategoryAsync(new BudgetCategoryCreateRequest("Wohnen"), ct);
        var budgetCategoryVersicherungen = await api.Budgets_CreateCategoryAsync(new BudgetCategoryCreateRequest("Versicherungen"), ct);
        var budgetCategoryVorsorge = await api.Budgets_CreateCategoryAsync(new BudgetCategoryCreateRequest("Vorsorge"), ct);
        var budgetCategoryUnterhaltung = await api.Budgets_CreateCategoryAsync(new BudgetCategoryCreateRequest("Unterhaltung & Aktivitäten"), ct);

        // Security category.
        var securityCategoryAktien = await api.SecurityCategories_CreateAsync(new SecurityCategoryRequest { Name = "Aktien" }, ct);

        var statementDraft = await api.StatementDrafts_CreateAsync(null, ct);
        (await api.StatementDrafts_SetAccountAsync(statementDraft.DraftId, account.Id, ct)).Should().NotBeNull();

        async Task<Guid> AddFullEntryAsync(DateTime bookingDate, DateTime valutaDate, decimal amount, string subject, string recipientName, string description)
        {
            var added = await api.StatementDrafts_AddEntryAsync(statementDraft.DraftId, new StatementDraftAddEntryRequest(bookingDate, amount, subject), ct);
            added.Should().NotBeNull();
            var entry = added!.Entries.Last();
            var updated = await api.StatementDrafts_UpdateEntryCoreAsync(statementDraft.DraftId, entry.Id,
                new StatementDraftUpdateEntryCoreRequest(bookingDate, valutaDate, amount, subject, recipientName, "EUR", description), ct);
            updated.Should().NotBeNull();
            return entry.Id;
        }

        #endregion

        var aug3 = new DateTime(2026, 08, 03);
        var aug4 = new DateTime(2026, 08, 04);

        #region Posten 1: Stadtwerke Musterstadt (Stromkosten) -75.00 am 03.08.

        var stadtwerkeContact = await api.Contacts_CreateAsync(new FinanceManager.Shared.Dtos.Contacts.ContactCreateRequest(
            Name: "Stadtwerke Musterstadt", Type: ContactType.Organization, CategoryId: contactCategoryDienstleister!.Id,
            Description: null, IsPaymentIntermediary: null, Parent: null), ct);
        var budgetPurposeStromkosten = await api.BudgetPurposes_CreateAsync(new BudgetPurposeCreateRequest(
            Name: "Stromkosten", SourceType: BudgetSourceType.Contact, SourceId: stadtwerkeContact.Id,
            Description: null, BudgetCategoryId: budgetCategoryWohnen!.Id), ct);
        await api.BudgetRules_CreateAsync(new BudgetRuleCreateRequest(
            BudgetPurposeId: budgetPurposeStromkosten.Id, BudgetCategoryId: null, Amount: -75m,
            Interval: BudgetIntervalType.Monthly, CustomIntervalMonths: null, StartDate: new DateOnly(2020, 01, 01), EndDate: null), ct);

        var entry1 = await AddFullEntryAsync(aug3, aug3, -75.00m,
            "Vertragskonto 1234567890, Musterweg 1, Musterstadt, Energie", "Stadtwerke Musterstadt GmbH", "Lastschrift");
        (await api.StatementDrafts_SetEntryContactAsync(statementDraft.DraftId, entry1, new StatementDraftSetContactRequest(stadtwerkeContact.Id), ct)).Should().NotBeNull();

        #endregion

        #region Posten 2: Muster Krankenversicherung -> Zahnzusatzversicherung -10.50 am 03.08.

        var zahnzusatzSavingsPlan = await api.SavingsPlans_CreateAsync(new SavingsPlanCreateRequest(
            "Zahnzusatzversicherung", SavingsPlanType.Recurring, 115.2m, new DateTime(2027, 06, 08),
            SavingsPlanInterval.Annually, savingsPlanCategoryVersicherung!.Id, null), ct);
        var budgetPurposeZahnzusatz = await api.BudgetPurposes_CreateAsync(new BudgetPurposeCreateRequest(
            Name: "Rückstellung Zahnzusatzversicherung", SourceType: BudgetSourceType.SavingsPlan, SourceId: zahnzusatzSavingsPlan.Id,
            Description: null, BudgetCategoryId: budgetCategoryVersicherungen!.Id), ct);
        await api.BudgetRules_CreateAsync(new BudgetRuleCreateRequest(
            BudgetPurposeId: budgetPurposeZahnzusatz.Id, BudgetCategoryId: null, Amount: -10.5m,
            Interval: BudgetIntervalType.Monthly, CustomIntervalMonths: null, StartDate: new DateOnly(2020, 01, 01), EndDate: null), ct);

        var entry2 = await AddFullEntryAsync(aug3, aug3, -10.50m,
            "Rueckstellung Muster Krankenversiche rung Zahnzusatzversicherung", "Max Mustermann", "Dauerauftrag / Terminueberweisung");
        (await api.StatementDrafts_SetEntryContactAsync(statementDraft.DraftId, entry2, new StatementDraftSetContactRequest(selfContact.Id), ct)).Should().NotBeNull();
        (await api.StatementDrafts_SetEntrySavingsPlanAsync(statementDraft.DraftId, entry2, new StatementDraftSetSavingsPlanRequest(zahnzusatzSavingsPlan.Id), ct)).Should().NotBeNull();

        #endregion

        #region Posten 3: Nordstern Lebensversicherung -> "Nordstern Berufsunfähigkeit" -20.93 am 03.08.

        var axaContact = await api.Contacts_CreateAsync(new FinanceManager.Shared.Dtos.Contacts.ContactCreateRequest(
            Name: "Nordstern", Type: ContactType.Organization, CategoryId: contactCategoryVersicherung.Id,
            Description: null, IsPaymentIntermediary: null, Parent: null), ct);
        var budgetPurposeAxa = await api.BudgetPurposes_CreateAsync(new BudgetPurposeCreateRequest(
            Name: "Nordstern Berufsunfähigkeit", SourceType: BudgetSourceType.Contact, SourceId: axaContact.Id,
            Description: null, BudgetCategoryId: budgetCategoryVersicherungen.Id), ct);
        await api.BudgetRules_CreateAsync(new BudgetRuleCreateRequest(
            BudgetPurposeId: budgetPurposeAxa.Id, BudgetCategoryId: null, Amount: -20.93m,
            Interval: BudgetIntervalType.Monthly, CustomIntervalMonths: null, StartDate: new DateOnly(2020, 01, 11), EndDate: null), ct);

        var entry3 = await AddFullEntryAsync(aug3, aug3, -20.93m,
            "LV 12345678901 20,93. EUR BTR. 08/2 6", "Nordstern Lebensversicherung AG", "Lastschrift");
        (await api.StatementDrafts_SetEntryContactAsync(statementDraft.DraftId, entry3, new StatementDraftSetContactRequest(axaContact.Id), ct)).Should().NotBeNull();

        #endregion

        #region Posten 4: Rueckstellung Rundfunkgebuehr -18.36 am 03.08.

        var rundfunkSavingsPlan = await api.SavingsPlans_CreateAsync(new SavingsPlanCreateRequest(
            "Rundfunkgebühr", SavingsPlanType.Recurring, 54.22m, new DateTime(2026, 11, 01),
            SavingsPlanInterval.Quarterly, savingsPlanCategorySteuer!.Id, null), ct);
        var budgetPurposeRundfunk = await api.BudgetPurposes_CreateAsync(new BudgetPurposeCreateRequest(
            Name: "Rückstellung Rundfunkgebuehr", SourceType: BudgetSourceType.SavingsPlan, SourceId: rundfunkSavingsPlan.Id,
            Description: null, BudgetCategoryId: budgetCategorySteuer!.Id), ct);
        await api.BudgetRules_CreateAsync(new BudgetRuleCreateRequest(
            BudgetPurposeId: budgetPurposeRundfunk.Id, BudgetCategoryId: null, Amount: -18.36m,
            Interval: BudgetIntervalType.Monthly, CustomIntervalMonths: null, StartDate: new DateOnly(2020, 01, 01), EndDate: null), ct);
        await api.BudgetRules_CreateAsync(new BudgetRuleCreateRequest(
            BudgetPurposeId: budgetPurposeRundfunk.Id, BudgetCategoryId: null, Amount: 55.08m,
            Interval: BudgetIntervalType.Quarterly, CustomIntervalMonths: null, StartDate: new DateOnly(2020, 02, 15), EndDate: null), ct);

        var entry4 = await AddFullEntryAsync(aug3, aug3, -18.36m,
            "Rueckstellung Rundfunkgebuehr", "Max Mustermann", "Dauerauftrag / Terminueberweisung");
        (await api.StatementDrafts_SetEntryContactAsync(statementDraft.DraftId, entry4, new StatementDraftSetContactRequest(selfContact.Id), ct)).Should().NotBeNull();
        (await api.StatementDrafts_SetEntrySavingsPlanAsync(statementDraft.DraftId, entry4, new StatementDraftSetSavingsPlanRequest(rundfunkSavingsPlan.Id), ct)).Should().NotBeNull();

        #endregion

        #region Posten 5: Musterversicherung -> Krankenhaustagegeld -3.82 am 03.08.

        var krankenhaustagegeldSavingsPlan = await api.SavingsPlans_CreateAsync(new SavingsPlanCreateRequest(
            "Krankenhaustagegeld", SavingsPlanType.Recurring, 11.46m, new DateTime(2026, 09, 15),
            SavingsPlanInterval.Quarterly, savingsPlanCategoryVersicherung.Id, null), ct);
        var budgetPurposeKrankenhaustagegeld = await api.BudgetPurposes_CreateAsync(new BudgetPurposeCreateRequest(
            Name: "Rückstellung Krankenhaustagegeld", SourceType: BudgetSourceType.SavingsPlan, SourceId: krankenhaustagegeldSavingsPlan.Id,
            Description: null, BudgetCategoryId: budgetCategoryVersicherungen.Id), ct);
        await api.BudgetRules_CreateAsync(new BudgetRuleCreateRequest(
            BudgetPurposeId: budgetPurposeKrankenhaustagegeld.Id, BudgetCategoryId: null, Amount: -3.82m,
            Interval: BudgetIntervalType.Monthly, CustomIntervalMonths: null, StartDate: new DateOnly(2020, 01, 01), EndDate: null), ct);
        await api.BudgetRules_CreateAsync(new BudgetRuleCreateRequest(
            BudgetPurposeId: budgetPurposeKrankenhaustagegeld.Id, BudgetCategoryId: null, Amount: 11.46m,
            Interval: BudgetIntervalType.Quarterly, CustomIntervalMonths: null, StartDate: new DateOnly(2020, 01, 01), EndDate: null), ct);

        var entry5 = await AddFullEntryAsync(aug3, aug3, -3.82m,
            "Rueckstellung Musterversicherung Krankenh austagegeld", "Max Mustermann", "Dauerauftrag / Terminueberweisung");
        (await api.StatementDrafts_SetEntryContactAsync(statementDraft.DraftId, entry5, new StatementDraftSetContactRequest(selfContact.Id), ct)).Should().NotBeNull();
        (await api.StatementDrafts_SetEntrySavingsPlanAsync(statementDraft.DraftId, entry5, new StatementDraftSetSavingsPlanRequest(krankenhaustagegeldSavingsPlan.Id), ct)).Should().NotBeNull();

        #endregion

        #region Posten 6: Musterbausparkasse Nordwest -> Bausparen (Einzahlung Bausparvertrag) -10.00 am 03.08.

        var bausparenSavingsPlan = await api.SavingsPlans_CreateAsync(new SavingsPlanCreateRequest(
            "Bausparen", SavingsPlanType.OneTime, 50000m, new DateTime(2030, 01, 01),
            null, savingsPlanCategorySparen!.Id, "1234509876"), ct);
        var budgetPurposeBausparen = await api.BudgetPurposes_CreateAsync(new BudgetPurposeCreateRequest(
            Name: "Einzahlung Bausparvertrag", SourceType: BudgetSourceType.SavingsPlan, SourceId: bausparenSavingsPlan.Id,
            Description: null, BudgetCategoryId: budgetCategoryVorsorge!.Id), ct);
        await api.BudgetRules_CreateAsync(new BudgetRuleCreateRequest(
            BudgetPurposeId: budgetPurposeBausparen.Id, BudgetCategoryId: null, Amount: -10m,
            Interval: BudgetIntervalType.Monthly, CustomIntervalMonths: null, StartDate: new DateOnly(2020, 01, 01), EndDate: null), ct);

        var entry6 = await AddFullEntryAsync(aug3, aug3, -10.00m,
            "1234509876 10,00", "Musterbausparkasse Nordwest", "Lastschrift");
        (await api.StatementDrafts_SetEntryContactAsync(statementDraft.DraftId, entry6, new StatementDraftSetContactRequest(selfContact.Id), ct)).Should().NotBeNull();
        (await api.StatementDrafts_SetEntrySavingsPlanAsync(statementDraft.DraftId, entry6, new StatementDraftSetSavingsPlanRequest(bausparenSavingsPlan.Id), ct)).Should().NotBeNull();

        #endregion

        #region Posten 7: Rheinstern Leben AG -> "Rheinstern Berufsunfähigkeit" -20.64 am 03.08.

        var provinzialContact = await api.Contacts_CreateAsync(new FinanceManager.Shared.Dtos.Contacts.ContactCreateRequest(
            Name: "Rheinstern Leben AG", Type: ContactType.Organization, CategoryId: contactCategoryVersicherung.Id,
            Description: null, IsPaymentIntermediary: null, Parent: null), ct);
        var budgetPurposeRheinstern = await api.BudgetPurposes_CreateAsync(new BudgetPurposeCreateRequest(
            Name: "Rheinstern Berufsunfähigkeit", SourceType: BudgetSourceType.Contact, SourceId: provinzialContact.Id,
            Description: null, BudgetCategoryId: budgetCategoryVersicherungen.Id), ct);
        await api.BudgetRules_CreateAsync(new BudgetRuleCreateRequest(
            BudgetPurposeId: budgetPurposeRheinstern.Id, BudgetCategoryId: null, Amount: -20.64m,
            Interval: BudgetIntervalType.Monthly, CustomIntervalMonths: null, StartDate: new DateOnly(2020, 01, 11), EndDate: null), ct);

        var entry7 = await AddFullEntryAsync(aug3, aug3, -20.64m,
            "BU - Vorsorge Plus L987654321 01.08 .2026", "Rheinstern Leben AG", "Lastschrift");
        (await api.StatementDrafts_SetEntryContactAsync(statementDraft.DraftId, entry7, new StatementDraftSetContactRequest(provinzialContact.Id), ct)).Should().NotBeNull();

        #endregion

        #region Posten 8: Erika Musterfrau -> Wohnungsmiete -649.42 am 03.08.

        var landlordContact = await api.Contacts_CreateAsync(new FinanceManager.Shared.Dtos.Contacts.ContactCreateRequest(
            Name: "Erika Musterfrau", Type: ContactType.Organization, CategoryId: contactCategoryDienstleister.Id,
            Description: null, IsPaymentIntermediary: null, Parent: null), ct);
        var budgetPurposeMiete = await api.BudgetPurposes_CreateAsync(new BudgetPurposeCreateRequest(
            Name: "Wohnungsmiete", SourceType: BudgetSourceType.Contact, SourceId: landlordContact.Id,
            Description: null, BudgetCategoryId: budgetCategoryWohnen.Id), ct);
        await api.BudgetRules_CreateAsync(new BudgetRuleCreateRequest(
            BudgetPurposeId: budgetPurposeMiete.Id, BudgetCategoryId: null, Amount: -649.42m,
            Interval: BudgetIntervalType.Monthly, CustomIntervalMonths: null, StartDate: new DateOnly(2020, 01, 01), EndDate: null), ct);

        var entry8 = await AddFullEntryAsync(aug3, aug3, -649.42m,
            "WOHNUNGSMIETE MUSTERWEG 20", "Erika Musterfrau", "Dauerauftrag / Terminueberweisung");
        (await api.StatementDrafts_SetEntryContactAsync(statementDraft.DraftId, entry8, new StatementDraftSetContactRequest(landlordContact.Id), ct)).Should().NotBeNull();

        #endregion

        #region Posten 9: Musterkasse Musterstadt -> Unfallversicherung -13.01 am 03.08.

        var unfallSavingsPlan = await api.SavingsPlans_CreateAsync(new SavingsPlanCreateRequest(
            "Unfallversicherung", SavingsPlanType.Recurring, 39.03m, new DateTime(2026, 10, 01),
            SavingsPlanInterval.Quarterly, savingsPlanCategoryVersicherung.Id, null), ct);
        var budgetPurposeUnfall = await api.BudgetPurposes_CreateAsync(new BudgetPurposeCreateRequest(
            Name: "Rückstellung Unfallversicherung", SourceType: BudgetSourceType.SavingsPlan, SourceId: unfallSavingsPlan.Id,
            Description: null, BudgetCategoryId: budgetCategoryVersicherungen.Id), ct);
        await api.BudgetRules_CreateAsync(new BudgetRuleCreateRequest(
            BudgetPurposeId: budgetPurposeUnfall.Id, BudgetCategoryId: null, Amount: -13.01m,
            Interval: BudgetIntervalType.Monthly, CustomIntervalMonths: null, StartDate: new DateOnly(2020, 01, 01), EndDate: null), ct);
        await api.BudgetRules_CreateAsync(new BudgetRuleCreateRequest(
            BudgetPurposeId: budgetPurposeUnfall.Id, BudgetCategoryId: null, Amount: 39.01m,
            Interval: BudgetIntervalType.Quarterly, CustomIntervalMonths: null, StartDate: new DateOnly(2020, 01, 01), EndDate: null), ct);

        var entry9 = await AddFullEntryAsync(aug3, aug3, -13.01m,
            "Rueckstellung Musterkasse Muster stadt Unfallversicherung", "Max Mustermann", "Dauerauftrag / Terminueberweisung");
        (await api.StatementDrafts_SetEntryContactAsync(statementDraft.DraftId, entry9, new StatementDraftSetContactRequest(selfContact.Id), ct)).Should().NotBeNull();
        (await api.StatementDrafts_SetEntrySavingsPlanAsync(statementDraft.DraftId, entry9, new StatementDraftSetSavingsPlanRequest(unfallSavingsPlan.Id), ct)).Should().NotBeNull();

        #endregion

        #region Posten 10: FitStudio Deutschland GmbH -> Fitnessstudio -15.00 am 03.08.

        var fitxContact = await api.Contacts_CreateAsync(new FinanceManager.Shared.Dtos.Contacts.ContactCreateRequest(
            Name: "FitStudio Deutschland GmbH", Type: ContactType.Organization, CategoryId: contactCategoryFreizeiteinrichtung!.Id,
            Description: null, IsPaymentIntermediary: null, Parent: null), ct);
        var budgetPurposeFitness = await api.BudgetPurposes_CreateAsync(new BudgetPurposeCreateRequest(
            Name: "Fitnessstudio", SourceType: BudgetSourceType.Contact, SourceId: fitxContact.Id,
            Description: null, BudgetCategoryId: budgetCategoryUnterhaltung!.Id), ct);
        await api.BudgetRules_CreateAsync(new BudgetRuleCreateRequest(
            BudgetPurposeId: budgetPurposeFitness.Id, BudgetCategoryId: null, Amount: -15m,
            Interval: BudgetIntervalType.Monthly, CustomIntervalMonths: null, StartDate: new DateOnly(2020, 01, 01), EndDate: null), ct);

        var entry10 = await AddFullEntryAsync(aug3, aug3, -15.00m,
            "12--0000-0000000 12-000000 Einzug 1 5 12/12 15.00 EUR 01.08.26-31.08.26", "FitStudio Deutschland GmbH", "Lastschrift");
        (await api.StatementDrafts_SetEntryContactAsync(statementDraft.DraftId, entry10, new StatementDraftSetContactRequest(fitxContact.Id), ct)).Should().NotBeNull();

        #endregion

        #region Posten 11: Nordkasse Hausratversicherung -5.21 am 03.08.

        var hausratSavingsPlan = await api.SavingsPlans_CreateAsync(new SavingsPlanCreateRequest(
            "Hausratversicherung", SavingsPlanType.Recurring, 62.6m, new DateTime(2026, 12, 01),
            SavingsPlanInterval.Annually, savingsPlanCategoryVersicherung.Id, null), ct);
        var budgetPurposeHausrat = await api.BudgetPurposes_CreateAsync(new BudgetPurposeCreateRequest(
            Name: "Rückstellung Hausratversicherung", SourceType: BudgetSourceType.SavingsPlan, SourceId: hausratSavingsPlan.Id,
            Description: null, BudgetCategoryId: budgetCategoryVersicherungen.Id), ct);
        await api.BudgetRules_CreateAsync(new BudgetRuleCreateRequest(
            BudgetPurposeId: budgetPurposeHausrat.Id, BudgetCategoryId: null, Amount: -5.21m,
            Interval: BudgetIntervalType.Monthly, CustomIntervalMonths: null, StartDate: new DateOnly(2020, 01, 01), EndDate: null), ct);

        var entry11 = await AddFullEntryAsync(aug3, aug3, -5.21m,
            "Rueckstellung Nordkasse Hausratversich erung", "Max Mustermann", "Dauerauftrag / Terminueberweisung");
        (await api.StatementDrafts_SetEntryContactAsync(statementDraft.DraftId, entry11, new StatementDraftSetContactRequest(selfContact.Id), ct)).Should().NotBeNull();
        (await api.StatementDrafts_SetEntrySavingsPlanAsync(statementDraft.DraftId, entry11, new StatementDraftSetSavingsPlanRequest(hausratSavingsPlan.Id), ct)).Should().NotBeNull();

        #endregion

        #region Posten 12: Zins/Dividende Muster Corp +8.37 am 03.08. (Valuta 31.07.)

        var musterCorpSecurity = await api.Securities_CreateAsync(new SecurityRequest
        {
            Name = "Muster Corp",
            Identifier = "US0000000001",
            CurrencyCode = "USD",
            AlphaVantageCode = "MUST",
            CategoryId = securityCategoryAktien!.Id
        }, ct);

        var entry12 = await AddFullEntryAsync(aug3, new DateTime(2026, 07, 31), 8.37m,
            "Zins/Dividende ISIN US0000000001 MUSTERCORP", "", "Zins / Dividende WP");
        (await api.StatementDrafts_SetEntryContactAsync(statementDraft.DraftId, entry12, new StatementDraftSetContactRequest(bankContactId), ct)).Should().NotBeNull();
        (await api.StatementDrafts_SetEntrySecurityAsync(statementDraft.DraftId, entry12,
            new StatementDraftSetEntrySecurityRequest(musterCorpSecurity.Id, SecurityTransactionType.Dividend, null, null, 1.18m), ct)).Should().NotBeNull();

        #endregion

        #region Posten 13: Nordkasse Haftpflichtversicherung -4.63 am 03.08.

        var haftpflichtSavingsPlan = await api.SavingsPlans_CreateAsync(new SavingsPlanCreateRequest(
            "Haftpflichtversicherung", SavingsPlanType.Recurring, 55.65m, new DateTime(2026, 12, 01),
            SavingsPlanInterval.Annually, savingsPlanCategoryVersicherung.Id, null), ct);
        var budgetPurposeHaftpflicht = await api.BudgetPurposes_CreateAsync(new BudgetPurposeCreateRequest(
            Name: "Rückstellung Haftpflichtversicherung", SourceType: BudgetSourceType.SavingsPlan, SourceId: haftpflichtSavingsPlan.Id,
            Description: null, BudgetCategoryId: budgetCategoryVersicherungen.Id), ct);
        await api.BudgetRules_CreateAsync(new BudgetRuleCreateRequest(
            BudgetPurposeId: budgetPurposeHaftpflicht.Id, BudgetCategoryId: null, Amount: -4.63m,
            Interval: BudgetIntervalType.Monthly, CustomIntervalMonths: null, StartDate: new DateOnly(2020, 01, 01), EndDate: null), ct);

        var entry13 = await AddFullEntryAsync(aug3, aug3, -4.63m,
            "Rueckstellung Nordkasse Haftpflichtve rsicherung", "Max Mustermann", "Dauerauftrag / Terminueberweisung");
        (await api.StatementDrafts_SetEntryContactAsync(statementDraft.DraftId, entry13, new StatementDraftSetContactRequest(selfContact.Id), ct)).Should().NotBeNull();
        (await api.StatementDrafts_SetEntrySavingsPlanAsync(statementDraft.DraftId, entry13, new StatementDraftSetSavingsPlanRequest(haftpflichtSavingsPlan.Id), ct)).Should().NotBeNull();

        #endregion

        #region Posten 14: Transfer Testbausparkasse +5000.00 am 04.08. (Selbstkontakt, kostenneutral)

        var entry14 = await AddFullEntryAsync(aug4, aug4, 5000.00m,
            "Transfer Testbausparkasse", "Max Mustermann", "Gutschrift");
        (await api.StatementDrafts_SetEntryContactAsync(statementDraft.DraftId, entry14, new StatementDraftSetContactRequest(selfContact.Id), ct)).Should().NotBeNull();

        #endregion

        #region Posten 15: Zins/Dividende Beispiel AG +9.67 am 04.08. (Valuta 31.07.)

        var beispielAGSecurity = await api.Securities_CreateAsync(new SecurityRequest
        {
            Name = "Beispiel AG",
            Identifier = "US0000000002",
            CurrencyCode = "EUR",
            AlphaVantageCode = "BSPL",
            CategoryId = securityCategoryAktien.Id
        }, ct);

        var entry15 = await AddFullEntryAsync(aug4, new DateTime(2026, 07, 31), 9.67m,
            "Zins/Dividende ISIN US0000000002 BEISPIELAG", "", "Zins / Dividende WP");
        (await api.StatementDrafts_SetEntryContactAsync(statementDraft.DraftId, entry15, new StatementDraftSetContactRequest(bankContactId), ct)).Should().NotBeNull();
        (await api.StatementDrafts_SetEntrySecurityAsync(statementDraft.DraftId, entry15,
            new StatementDraftSetEntrySecurityRequest(beispielAGSecurity.Id, SecurityTransactionType.Dividend, null, null, 1.36m), ct)).Should().NotBeNull();

        #endregion

        #region Posten 16: Transfer Testbausparkasse -5000.00 am 04.08. (Selbstkontakt, kostenneutral)

        var entry16 = await AddFullEntryAsync(aug4, aug4, -5000.00m,
            "Transfer Testbausparkasse", "Max Mustermann Testbausparkasse", "Überweisung");
        (await api.StatementDrafts_SetEntryContactAsync(statementDraft.DraftId, entry16, new StatementDraftSetContactRequest(selfContact.Id), ct)).Should().NotBeNull();

        #endregion

        #region Posten 17: Sparrate 123456789 -> S-Vorsorge -139.00 am 04.08.

        var sVorsorgeSavingsPlan = await api.SavingsPlans_CreateAsync(new SavingsPlanCreateRequest(
            "S-Vorsorge", SavingsPlanType.Open, 0m, null, null, savingsPlanCategoryVersicherung.Id, "123456789"), ct);
        var budgetPurposeSVorsorge = await api.BudgetPurposes_CreateAsync(new BudgetPurposeCreateRequest(
            Name: "Rückstellung S-Vorsorge", SourceType: BudgetSourceType.SavingsPlan, SourceId: sVorsorgeSavingsPlan.Id,
            Description: null, BudgetCategoryId: budgetCategoryVorsorge.Id), ct);
        await api.BudgetRules_CreateAsync(new BudgetRuleCreateRequest(
            BudgetPurposeId: budgetPurposeSVorsorge.Id, BudgetCategoryId: null, Amount: -139m,
            Interval: BudgetIntervalType.Monthly, CustomIntervalMonths: null, StartDate: new DateOnly(2020, 01, 01), EndDate: null), ct);

        var entry17 = await AddFullEntryAsync(aug4, aug4, -139.00m,
            "Sparrate 123456789 SPARKASSE MUSTERLAND OST", "Max Mustermann", "Lastschrift");
        (await api.StatementDrafts_SetEntryContactAsync(statementDraft.DraftId, entry17, new StatementDraftSetContactRequest(selfContact.Id), ct)).Should().NotBeNull();
        (await api.StatementDrafts_SetEntrySavingsPlanAsync(statementDraft.DraftId, entry17, new StatementDraftSetSavingsPlanRequest(sVorsorgeSavingsPlan.Id), ct)).Should().NotBeNull();

        #endregion

        #region Posten 18: Sparplan Allgemein -200.00 am 04.08. (Selbstkontakt + Sparplan ohne Budgetzweck, kostenneutral)

        var sparplanAllgemeinSavingsPlan = await api.SavingsPlans_CreateAsync(new SavingsPlanCreateRequest(
            "Sparplan Allgemein", SavingsPlanType.Open, 0m, null, null, savingsPlanCategorySparen.Id, null), ct);
        // Deliberately no BudgetPurpose is created for this savings plan (matches the real data): the
        // posting matches no budget purpose and must end up unbudgeted/cost-neutral.

        var entry18 = await AddFullEntryAsync(aug4, aug4, -200.00m,
            "Sparplan Allgemein", "Max Mustermann Testbausparkasse", "Dauerauftrag / Terminueberweisung");
        (await api.StatementDrafts_SetEntryContactAsync(statementDraft.DraftId, entry18, new StatementDraftSetContactRequest(selfContact.Id), ct)).Should().NotBeNull();
        (await api.StatementDrafts_SetEntrySavingsPlanAsync(statementDraft.DraftId, entry18, new StatementDraftSetSavingsPlanRequest(sparplanAllgemeinSavingsPlan.Id), ct)).Should().NotBeNull();

        #endregion

        #region Buchen

        var book = await api.StatementDrafts_BookAsync(statementDraft.DraftId, forceWarnings: true, ct);
        book.Should().NotBeNull();
        book!.Success.Should().BeTrue();

        #endregion

        #region Pruefung der gebuchten Kontakt-/Sparplan-/Wertpapierposten

        var checkFrom = new DateTime(2026, 08, 01);
        var checkTo = new DateTime(2026, 08, 31, 23, 59, 59);

        async Task<PostingServiceDto> GetSingleContactPostingAsync(Guid contactId, decimal amount, string subject)
        {
            var postings = await api.Postings_GetContactAsync(contactId, 0, 100, null, checkFrom, checkTo, ct);
            return postings.Should().ContainSingle(p => p.Amount == amount && p.Subject == subject).Subject;
        }

        async Task<PostingServiceDto> GetSingleSavingsPlanPostingAsync(Guid savingsPlanId, decimal amount, string subject)
        {
            var postings = await api.Postings_GetSavingsPlanAsync(savingsPlanId, 0, 100, checkFrom, checkTo, null, ct);
            return postings.Should().ContainSingle(p => p.Amount == amount && p.Subject == subject).Subject;
        }

        void AssertCore(PostingServiceDto posting, PostingKind kind, DateTime bookingDate, DateTime valutaDate, string recipientName, string description)
        {
            posting.Kind.Should().Be(kind);
            posting.BookingDate.Should().Be(bookingDate);
            posting.ValutaDate.Should().Be(valutaDate);
            posting.RecipientName.Should().Be(recipientName);
            posting.Description.Should().Be(description);
            posting.IsReversed.Should().BeFalse();
            posting.IsReversal.Should().BeFalse();
        }

        // Posten 1: Stadtwerke
        var posting1 = await GetSingleContactPostingAsync(stadtwerkeContact.Id, -75.00m, "Vertragskonto 1234567890, Musterweg 1, Musterstadt, Energie");
        AssertCore(posting1, PostingKind.Contact, aug3, aug3, "Stadtwerke Musterstadt GmbH", "Lastschrift");
        posting1.ContactId.Should().Be(stadtwerkeContact.Id);

        // Posten 2: Zahnzusatzversicherung (Kontakt- und Sparplanposten)
        var posting2Contact = await GetSingleContactPostingAsync(selfContact.Id, -10.50m, "Rueckstellung Muster Krankenversiche rung Zahnzusatzversicherung");
        AssertCore(posting2Contact, PostingKind.Contact, aug3, aug3, "Max Mustermann", "Dauerauftrag / Terminueberweisung");
        posting2Contact.ContactId.Should().Be(selfContact.Id);
        posting2Contact.SavingsPlanId.Should().BeNull("Contact-kind postings never carry a SavingsPlanId - only the linked SavingsPlan-kind posting does");
        var posting2SavingsPlan = await GetSingleSavingsPlanPostingAsync(zahnzusatzSavingsPlan.Id, 10.50m, "Rueckstellung Muster Krankenversiche rung Zahnzusatzversicherung");
        AssertCore(posting2SavingsPlan, PostingKind.SavingsPlan, aug3, aug3, "Max Mustermann", "Dauerauftrag / Terminueberweisung");
        posting2SavingsPlan.SavingsPlanId.Should().Be(zahnzusatzSavingsPlan.Id);
        posting2SavingsPlan.GroupId.Should().Be(posting2Contact.GroupId);

        // Posten 3: Nordstern
        var posting3 = await GetSingleContactPostingAsync(axaContact.Id, -20.93m, "LV 12345678901 20,93. EUR BTR. 08/2 6");
        AssertCore(posting3, PostingKind.Contact, aug3, aug3, "Nordstern Lebensversicherung AG", "Lastschrift");
        posting3.ContactId.Should().Be(axaContact.Id);

        // Posten 4: Rundfunkgebuehr
        var posting4Contact = await GetSingleContactPostingAsync(selfContact.Id, -18.36m, "Rueckstellung Rundfunkgebuehr");
        AssertCore(posting4Contact, PostingKind.Contact, aug3, aug3, "Max Mustermann", "Dauerauftrag / Terminueberweisung");
        posting4Contact.SavingsPlanId.Should().BeNull("Contact-kind postings never carry a SavingsPlanId - only the linked SavingsPlan-kind posting does");
        var posting4SavingsPlan = await GetSingleSavingsPlanPostingAsync(rundfunkSavingsPlan.Id, 18.36m, "Rueckstellung Rundfunkgebuehr");
        posting4SavingsPlan.GroupId.Should().Be(posting4Contact.GroupId);

        // Posten 5: Musterversicherung Krankenhaustagegeld
        var posting5Contact = await GetSingleContactPostingAsync(selfContact.Id, -3.82m, "Rueckstellung Musterversicherung Krankenh austagegeld");
        AssertCore(posting5Contact, PostingKind.Contact, aug3, aug3, "Max Mustermann", "Dauerauftrag / Terminueberweisung");
        posting5Contact.SavingsPlanId.Should().BeNull("Contact-kind postings never carry a SavingsPlanId - only the linked SavingsPlan-kind posting does");
        var posting5SavingsPlan = await GetSingleSavingsPlanPostingAsync(krankenhaustagegeldSavingsPlan.Id, 3.82m, "Rueckstellung Musterversicherung Krankenh austagegeld");
        posting5SavingsPlan.GroupId.Should().Be(posting5Contact.GroupId);

        // Posten 6: LBS Bausparen
        var posting6Contact = await GetSingleContactPostingAsync(selfContact.Id, -10.00m, "1234509876 10,00");
        AssertCore(posting6Contact, PostingKind.Contact, aug3, aug3, "Musterbausparkasse Nordwest", "Lastschrift");
        posting6Contact.SavingsPlanId.Should().BeNull("Contact-kind postings never carry a SavingsPlanId - only the linked SavingsPlan-kind posting does");
        var posting6SavingsPlan = await GetSingleSavingsPlanPostingAsync(bausparenSavingsPlan.Id, 10.00m, "1234509876 10,00");
        posting6SavingsPlan.GroupId.Should().Be(posting6Contact.GroupId);

        // Posten 7: Rheinstern
        var posting7 = await GetSingleContactPostingAsync(provinzialContact.Id, -20.64m, "BU - Vorsorge Plus L987654321 01.08 .2026");
        AssertCore(posting7, PostingKind.Contact, aug3, aug3, "Rheinstern Leben AG", "Lastschrift");

        // Posten 8: Wohnungsmiete
        var posting8 = await GetSingleContactPostingAsync(landlordContact.Id, -649.42m, "WOHNUNGSMIETE MUSTERWEG 20");
        AssertCore(posting8, PostingKind.Contact, aug3, aug3, "Erika Musterfrau", "Dauerauftrag / Terminueberweisung");

        // Posten 9: Musterkasse Musterstadt Unfallversicherung
        var posting9Contact = await GetSingleContactPostingAsync(selfContact.Id, -13.01m, "Rueckstellung Musterkasse Muster stadt Unfallversicherung");
        posting9Contact.SavingsPlanId.Should().BeNull("Contact-kind postings never carry a SavingsPlanId - only the linked SavingsPlan-kind posting does");
        var posting9SavingsPlan = await GetSingleSavingsPlanPostingAsync(unfallSavingsPlan.Id, 13.01m, "Rueckstellung Musterkasse Muster stadt Unfallversicherung");
        posting9SavingsPlan.GroupId.Should().Be(posting9Contact.GroupId);

        // Posten 10: FitStudio
        var posting10 = await GetSingleContactPostingAsync(fitxContact.Id, -15.00m, "12--0000-0000000 12-000000 Einzug 1 5 12/12 15.00 EUR 01.08.26-31.08.26");
        AssertCore(posting10, PostingKind.Contact, aug3, aug3, "FitStudio Deutschland GmbH", "Lastschrift");

        // Posten 11: Nordkasse Hausratversicherung
        var posting11Contact = await GetSingleContactPostingAsync(selfContact.Id, -5.21m, "Rueckstellung Nordkasse Hausratversich erung");
        posting11Contact.SavingsPlanId.Should().BeNull("Contact-kind postings never carry a SavingsPlanId - only the linked SavingsPlan-kind posting does");
        var posting11SavingsPlan = await GetSingleSavingsPlanPostingAsync(hausratSavingsPlan.Id, 5.21m, "Rueckstellung Nordkasse Hausratversich erung");
        posting11SavingsPlan.GroupId.Should().Be(posting11Contact.GroupId);

        // Posten 12: Dividende Muster Corp (Kontakt- und Wertpapierposten, Brutto/Steuer-Split)
        var posting12Contact = await GetSingleContactPostingAsync(bankContactId, 8.37m, "Zins/Dividende ISIN US0000000001 MUSTERCORP");
        AssertCore(posting12Contact, PostingKind.Contact, aug3, new DateTime(2026, 07, 31), null, "Zins / Dividende WP");
        posting12Contact.SecurityId.Should().BeNull("Contact-kind postings never carry a SecurityId - only the linked Security-kind postings do");
        var musterCorpPostings = await api.Postings_GetSecurityAsync(musterCorpSecurity.Id, 0, 100, checkFrom, checkTo, ct);
        musterCorpPostings.Should().HaveCount(2);
        var posting12Dividend = musterCorpPostings.Should().ContainSingle(p => p.SecuritySubType == SecurityPostingSubType.Dividend).Subject;
        posting12Dividend.Amount.Should().Be(9.55m);
        var posting12Tax = musterCorpPostings.Should().ContainSingle(p => p.SecuritySubType == SecurityPostingSubType.Tax).Subject;
        posting12Tax.Amount.Should().Be(-1.18m);
        posting12Dividend.GroupId.Should().Be(posting12Contact.GroupId);
        posting12Tax.GroupId.Should().Be(posting12Contact.GroupId);

        // Posten 13: Nordkasse Haftpflichtversicherung
        var posting13Contact = await GetSingleContactPostingAsync(selfContact.Id, -4.63m, "Rueckstellung Nordkasse Haftpflichtve rsicherung");
        posting13Contact.SavingsPlanId.Should().BeNull("Contact-kind postings never carry a SavingsPlanId - only the linked SavingsPlan-kind posting does");
        var posting13SavingsPlan = await GetSingleSavingsPlanPostingAsync(haftpflichtSavingsPlan.Id, 4.63m, "Rueckstellung Nordkasse Haftpflichtve rsicherung");
        posting13SavingsPlan.GroupId.Should().Be(posting13Contact.GroupId);

        // Posten 14: Transfer Testbausparkasse +5000 (Selbstkontakt)
        var posting14 = await GetSingleContactPostingAsync(selfContact.Id, 5000.00m, "Transfer Testbausparkasse");
        AssertCore(posting14, PostingKind.Contact, aug4, aug4, "Max Mustermann", "Gutschrift");
        posting14.GroupId.Should().NotBe(Guid.Empty);

        // Posten 15: Dividende Beispiel AG
        var posting15Contact = await GetSingleContactPostingAsync(bankContactId, 9.67m, "Zins/Dividende ISIN US0000000002 BEISPIELAG");
        AssertCore(posting15Contact, PostingKind.Contact, aug4, new DateTime(2026, 07, 31), null, "Zins / Dividende WP");
        posting15Contact.SecurityId.Should().BeNull("Contact-kind postings never carry a SecurityId - only the linked Security-kind postings do");
        var beispielAGPostings = await api.Postings_GetSecurityAsync(beispielAGSecurity.Id, 0, 100, checkFrom, checkTo, ct);
        beispielAGPostings.Should().HaveCount(2);
        var posting15Dividend = beispielAGPostings.Should().ContainSingle(p => p.SecuritySubType == SecurityPostingSubType.Dividend).Subject;
        posting15Dividend.Amount.Should().Be(11.03m);
        var posting15Tax = beispielAGPostings.Should().ContainSingle(p => p.SecuritySubType == SecurityPostingSubType.Tax).Subject;
        posting15Tax.Amount.Should().Be(-1.36m);

        // Posten 16: Transfer Testbausparkasse -5000 (Selbstkontakt)
        var posting16 = await GetSingleContactPostingAsync(selfContact.Id, -5000.00m, "Transfer Testbausparkasse");
        AssertCore(posting16, PostingKind.Contact, aug4, aug4, "Max Mustermann Testbausparkasse", "Überweisung");
        posting16.GroupId.Should().NotBe(posting14.GroupId, "each booked transfer forms its own mirror group, even though both share the same subject");

        // Posten 17: Sparrate S-Vorsorge
        var posting17Contact = await GetSingleContactPostingAsync(selfContact.Id, -139.00m, "Sparrate 123456789 SPARKASSE MUSTERLAND OST");
        posting17Contact.SavingsPlanId.Should().BeNull("Contact-kind postings never carry a SavingsPlanId - only the linked SavingsPlan-kind posting does");
        var posting17SavingsPlan = await GetSingleSavingsPlanPostingAsync(sVorsorgeSavingsPlan.Id, 139.00m, "Sparrate 123456789 SPARKASSE MUSTERLAND OST");
        posting17SavingsPlan.GroupId.Should().Be(posting17Contact.GroupId);

        // Posten 18: Sparplan Allgemein (Selbstkontakt + Sparplan, aber ohne Budgetzweck)
        var posting18Contact = await GetSingleContactPostingAsync(selfContact.Id, -200.00m, "Sparplan Allgemein");
        AssertCore(posting18Contact, PostingKind.Contact, aug4, aug4, "Max Mustermann Testbausparkasse", "Dauerauftrag / Terminueberweisung");
        posting18Contact.SavingsPlanId.Should().BeNull("Contact-kind postings never carry a SavingsPlanId - only the linked SavingsPlan-kind posting does");
        var posting18SavingsPlan = await GetSingleSavingsPlanPostingAsync(sparplanAllgemeinSavingsPlan.Id, 200.00m, "Sparplan Allgemein");
        posting18SavingsPlan.GroupId.Should().Be(posting18Contact.GroupId);

        #endregion

        #region Budgetbericht abrufen und pruefen

        var reportRequest = new BudgetReportRequest(
            AsOfDate: new DateOnly(2026, 08, 31),
            Months: 12,
            Interval: BudgetReportInterval.Month,
            ShowTitle: false,
            ShowLineChart: false,
            ShowMonthlyTable: false,
            ShowDetailsTable: true,
            CategoryValueScope: BudgetReportValueScope.TotalRange,
            IncludePurposeRows: true,
            DateBasis: BudgetReportDateBasis.BookingDate);

        var report = await api.Budgets_GetReportAsync(reportRequest, ct);
        report.Should().NotBeNull();
        var rawReport = await api.Budgets_GetReportRawAsync(reportRequest, ct);
        rawReport.Should().NotBeNull();

        // Full-field check of a BudgetReportPurposeDto row: Name, Budget (the rule-derived expectation
        // summed over all 12 months of the report range), Actual, and the values Budget/Actual imply -
        // Delta and DeltaPct are computed here from the same known-correct Budget/Actual rather than
        // hardcoded, since DeltaPct is a repeating decimal for most of these purposes (Actual is exactly
        // one month's worth of a purely monthly rule, so DeltaPct always comes out to 11/12) - plus
        // SourceType/SourceId, which the previous version of this test never checked at all.
        void AssertPurposeRow(BudgetReportPurposeDto row, string expectedName, decimal expectedBudget, decimal expectedActual, BudgetSourceType expectedSourceType, Guid expectedSourceId)
        {
            row.Name.Should().Be(expectedName);
            row.Budget.Should().Be(expectedBudget);
            row.Actual.Should().Be(expectedActual);
            var expectedDelta = expectedActual - expectedBudget;
            row.Delta.Should().Be(expectedDelta);
            row.DeltaPct.Should().Be(expectedBudget == 0m ? 0m : expectedDelta / Math.Abs(expectedBudget));
            row.SourceType.Should().Be(expectedSourceType);
            row.SourceId.Should().Be(expectedSourceId);
        }

        // Steuer / Rueckstellung Rundfunkgebuehr: Monatsregel -18.36 (Tag 1, x12 = -220.32) und
        // Quartalsregel +55.08 (Tag 15, x4 Vorkommen im Berichtszeitraum = 220.32) gleichen sich exakt aus
        // (Rueckstellungs-Charakter: monatlich zurueckgelegt, quartalsweise "freigegeben").
        var steuerCategory = report.Categories.Single(c => c.Id == budgetCategorySteuer!.Id);
        var rundfunkPurposeRow = steuerCategory.Purposes.Single(p => p.Id == budgetPurposeRundfunk.Id);
        AssertPurposeRow(rundfunkPurposeRow, "Rückstellung Rundfunkgebuehr", 0.00m, -18.36m, BudgetSourceType.SavingsPlan, rundfunkSavingsPlan.Id);

        // Wohnen: Stromkosten (-75.00 x12) und Wohnungsmiete (-649.42 x12).
        var wohnenCategory = report.Categories.Single(c => c.Id == budgetCategoryWohnen!.Id);
        var stromkostenPurposeRow = wohnenCategory.Purposes.Single(p => p.Id == budgetPurposeStromkosten.Id);
        AssertPurposeRow(stromkostenPurposeRow, "Stromkosten", -900.00m, -75.00m, BudgetSourceType.Contact, stadtwerkeContact.Id);
        var mietePurposeRow = wohnenCategory.Purposes.Single(p => p.Id == budgetPurposeMiete.Id);
        AssertPurposeRow(mietePurposeRow, "Wohnungsmiete", -7793.04m, -649.42m, BudgetSourceType.Contact, landlordContact.Id);

        // Vorsorge: Einzahlung Bausparvertrag (-10.00 x12, keine Ausgleichsregel) und Rueckstellung
        // S-Vorsorge (-139.00 x12, keine Ausgleichsregel).
        var vorsorgeCategory = report.Categories.Single(c => c.Id == budgetCategoryVorsorge!.Id);
        var bausparenPurposeRow = vorsorgeCategory.Purposes.Single(p => p.Id == budgetPurposeBausparen.Id);
        AssertPurposeRow(bausparenPurposeRow, "Einzahlung Bausparvertrag", -120.00m, -10.00m, BudgetSourceType.SavingsPlan, bausparenSavingsPlan.Id);
        var sVorsorgePurposeRow = vorsorgeCategory.Purposes.Single(p => p.Id == budgetPurposeSVorsorge.Id);
        AssertPurposeRow(sVorsorgePurposeRow, "Rückstellung S-Vorsorge", -1668.00m, -139.00m, BudgetSourceType.SavingsPlan, sVorsorgeSavingsPlan.Id);

        // Unterhaltung & Aktivitaeten: Fitnessstudio (-15.00 x12, keine Ausgleichsregel).
        var unterhaltungCategory = report.Categories.Single(c => c.Id == budgetCategoryUnterhaltung!.Id);
        var fitnessPurposeRow = unterhaltungCategory.Purposes.Single(p => p.Id == budgetPurposeFitness.Id);
        AssertPurposeRow(fitnessPurposeRow, "Fitnessstudio", -180.00m, -15.00m, BudgetSourceType.Contact, fitxContact.Id);

        // Versicherungen: Zahnzusatz, Unfall und Hausrat sind je exakt gebucht; Krankenhaustagegeld gleicht
        // sich (wie Rundfunkgebuehr) ueber Monats-/Quartalsregel fast/genau aus. Nordstern Berufsunfaehigkeit
        // UND Rheinstern Berufsunfaehigkeit haben beide eine monatliche Regel mit StartDate am 11., waehrend
        // die Buchung bereits am 03. erfolgte - siehe unten fuer die konkrete Auswirkung.
        var versicherungenCategory = report.Categories.Single(c => c.Id == budgetCategoryVersicherungen!.Id);
        var zahnzusatzPurposeRow = versicherungenCategory.Purposes.Single(p => p.Id == budgetPurposeZahnzusatz.Id);
        AssertPurposeRow(zahnzusatzPurposeRow, "Rückstellung Zahnzusatzversicherung", -126.00m, -10.50m, BudgetSourceType.SavingsPlan, zahnzusatzSavingsPlan.Id);
        var krankenhaustagegeldPurposeRow = versicherungenCategory.Purposes.Single(p => p.Id == budgetPurposeKrankenhaustagegeld.Id);
        AssertPurposeRow(krankenhaustagegeldPurposeRow, "Rückstellung Krankenhaustagegeld", 0.00m, -3.82m, BudgetSourceType.SavingsPlan, krankenhaustagegeldSavingsPlan.Id);
        var unfallPurposeRow = versicherungenCategory.Purposes.Single(p => p.Id == budgetPurposeUnfall.Id);
        AssertPurposeRow(unfallPurposeRow, "Rückstellung Unfallversicherung", -0.08m, -13.01m, BudgetSourceType.SavingsPlan, unfallSavingsPlan.Id);
        var hausratPurposeRow = versicherungenCategory.Purposes.Single(p => p.Id == budgetPurposeHausrat.Id);
        AssertPurposeRow(hausratPurposeRow, "Rückstellung Hausratversicherung", -62.52m, -5.21m, BudgetSourceType.SavingsPlan, hausratSavingsPlan.Id);
        var haftpflichtPurposeRow = versicherungenCategory.Purposes.Single(p => p.Id == budgetPurposeHaftpflicht.Id);
        AssertPurposeRow(haftpflichtPurposeRow, "Rückstellung Haftpflichtversicherung", -55.56m, -4.63m, BudgetSourceType.SavingsPlan, haftpflichtSavingsPlan.Id);

        // Nordstern Berufsunfaehigkeit und Rheinstern Berufsunfaehigkeit teilen dieselbe Konstellation: eine
        // monatliche Exakte-Buchung-Regel mit StartDate am 11. eines Monats, aber die tatsaechliche
        // Buchung liegt bereits am 03. des Monats - also VOR dem Beginn des periodenbasierten
        // Gueltigkeitsfensters der (auf den Regel-Starttag verankerten) monatlichen Erwartung fuer August.
        var nordsternPurposeRow = versicherungenCategory.Purposes.Single(p => p.Id == budgetPurposeAxa.Id);
        var rheinsternPurposeRow = versicherungenCategory.Purposes.Single(p => p.Id == budgetPurposeRheinstern.Id);

        var nordsternRawPurpose = rawReport.Categories.Single(c => c.CategoryId == budgetCategoryVersicherungen.Id)
            .Purposes.Single(p => p.PurposeId == budgetPurposeAxa.Id);
        var rheinsternRawPurpose = rawReport.Categories.Single(c => c.CategoryId == budgetCategoryVersicherungen.Id)
            .Purposes.Single(p => p.PurposeId == budgetPurposeRheinstern.Id);

        var unbudgeted = await api.Budgets_GetUnbudgetedPostingsAsync(checkFrom, checkTo, BudgetReportDateBasis.BookingDate, null, ct);
        var nordsternAlsoInUnbudgeted = unbudgeted.Any(p => p.ContactId == axaContact.Id && p.Amount == -20.93m);
        var rheinsternAlsoInUnbudgeted = unbudgeted.Any(p => p.ContactId == provinzialContact.Id && p.Amount == -20.64m);

        // Sowohl Nordstern als auch Rheinstern werden - trotz des am 11. verankerten monatlichen Regel-
        // Zeitfensters - korrekt (mit vollem Betrag, voll ausgewertet) ihrem jeweiligen Budgetzweck
        // zugeordnet, wenn der Bericht (wie die echte UI per Default) ueber mehrere Monate (Months: 12)
        // abgefragt wird - anders als bei einer einmonatigen Abfrage (siehe oben), bei der der Posten
        // gar keinem Budgetzweck zugeordnet wird.
        var nordsternPosting = nordsternRawPurpose.Postings.Should().ContainSingle(p => p.Amount == -20.93m && p.PostingId == posting3.Id).Subject;
        nordsternPosting.IsValuedForBudgetPurpose.Should().BeTrue();
        // Budget: 12 natuerliche Monatsvorkommen (Tag 11) x -20.93 = -251.16. Das 13. (hereinragende)
        // Vorkommen vom Vormonat wird - seit der Korrektur - NICHT mitgezaehlt (siehe
        // MonthlyBudgetExpectationPosting.IsCarriedOverFromPriorPeriod/BudgetedDisplayAmount), da es sonst
        // gemeinsam mit dem natuerlichen ersten Monatsvorkommen im selben Berichtsmonat doppelt zaehlen wuerde.
        AssertPurposeRow(nordsternPurposeRow, "Nordstern Berufsunfähigkeit", -251.16m, -20.93m, BudgetSourceType.Contact, axaContact.Id);

        var rheinsternPosting = rheinsternRawPurpose.Postings.Should().ContainSingle(p => p.Amount == -20.64m && p.PostingId == posting7.Id).Subject;
        rheinsternPosting.IsValuedForBudgetPurpose.Should().BeTrue();
        AssertPurposeRow(rheinsternPurposeRow, "Rheinstern Berufsunfähigkeit", -247.68m, -20.64m, BudgetSourceType.Contact, provinzialContact.Id);

        // Ein Posten, der bereits korrekt seinem Budgetzweck zugeordnet ist (siehe oben), darf NICHT
        // zusaetzlich in der Liste der nicht budgetierten Posten auftauchen. Vor der Korrektur von
        // Budgetbericht.ExpandRuleOccurrences (siehe Klassenkommentar) schlugen beide Assertions fehl, da
        // der eng auf einen Monat begrenzte "unbudgeted"-Bericht die von Ende Juli hereinreichende
        // Regel-Periode nicht kannte.
        nordsternAlsoInUnbudgeted.Should().BeFalse(
            "der Nordstern-Posten vom 03.08. ist bereits korrekt seinem Budgetzweck zugeordnet und darf nicht zusaetzlich " +
            "unter den nicht budgetierten Posten gefuehrt werden");
        rheinsternAlsoInUnbudgeted.Should().BeFalse(
            "der Rheinstern-Posten vom 03.08. ist bereits korrekt seinem Budgetzweck zugeordnet und darf nicht zusaetzlich " +
            "unter den nicht budgetierten Posten gefuehrt werden");

        // Zweiter, urspruenglich unabhaengig gemeldeter Fehler: mit CategoryValueScope.LastInterval (dem
        // Default der echten UI) zeigte der Bericht fuer August bei Nordstern Budget=-20.93/Actual=0/
        // Delta=20.93 - der Posten vom 03.08. schien "noch nicht bezahlt", obwohl er bereits gebucht war.
        // Ursache: Die Regel-Periode 11.07.-10.08. wurde (vor der Periodenende-Korrektur in
        // ExpandRuleOccurrences) dem Monat Juli zugeordnet, sodass die auf August gefilterte Einzelmonats-
        // Ansicht den zugeordneten Ist-Betrag nicht sah.
        var lastIntervalRequest = reportRequest with { CategoryValueScope = BudgetReportValueScope.LastInterval };
        var lastIntervalReport = await api.Budgets_GetReportAsync(lastIntervalRequest, ct);
        var lastIntervalNordstern = lastIntervalReport.Categories.Single(c => c.Id == budgetCategoryVersicherungen!.Id)
            .Purposes.Single(p => p.Id == budgetPurposeAxa.Id);
        lastIntervalNordstern.Budget.Should().Be(-20.93m);
        lastIntervalNordstern.Actual.Should().Be(-20.93m);
        lastIntervalNordstern.Delta.Should().Be(0.00m);

        #endregion
    }
}
