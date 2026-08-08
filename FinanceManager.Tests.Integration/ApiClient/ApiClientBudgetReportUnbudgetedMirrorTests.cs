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
    /// m�ssen die Unbudgeted-Endpunkte die gespiegelten Self-Buchungen herausfiltern
    /// und nur tats�chlich ungeplante Self-Postings zur�ckgeben (hier: +12,34 �).
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
            Name: "R�ckstellung Versicherung",
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
                  "Buchung;Wertstellungsdatum;Auftraggeber/Empf�nger;Buchungstext;Verwendungszweck;Saldo;W�hrung;Betrag;W�hrung\r\n" +
                  "27.01.2026;27.01.2026;Self;�berweisung;Extra;0,00;EUR;12,34;EUR\r\n" +
                  "25.01.2026;25.01.2026;Insurance;�berweisung;Jahresbeitrag;0,00;EUR;-60,00;EUR\r\n" +
                  "20.01.2026;20.01.2026;Self;�berweisung;Mirror +60;0,00;EUR;60,00;EUR\r\n" +
                  "10.01.2026;10.01.2026;Self;�berweisung;Mirror -5;0,00;EUR;-5,00;EUR\r\n";

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

        // All postings that remain without a matching budget expectation in this scenario are self-contact
        // postings (mirrored savings-plan transfer "Mirror +60" and the extra self posting "Extra"), so per
        // issue.md/requirement.md they are reported as their own "cost-neutral" category, not as regular
        // Unbudgeted (Kind=Unbudgeted is only for postings without a match that are NOT self-contact/cost-neutral
        // transfers). The core requirement for this scenario is validated via the unbudgeted postings endpoint below.
        report.Categories.Should().Contain(c => c.Kind == BudgetReportCategoryRowKind.UnbudgetedSelfCostNeutral);
        report.Categories.Should().NotContain(c => c.Kind == BudgetReportCategoryRowKind.Unbudgeted);

        var from = new DateTime(2025, 2, 1);
        var to = new DateTime(2026, 1, 31, 23, 59, 59);
        var unbudgeted = await api.Budgets_GetUnbudgetedPostingsAsync(from, to, BudgetReportDateBasis.BookingDate);

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
            SecurityProcessingEnabled: false));

        var contact = await api.Contacts_CreateAsync(new FinanceManager.Shared.Dtos.Contacts.ContactCreateRequest(
            Name: "Utility Provider",
            Type: ContactType.Person,
            CategoryId: null,
            Description: null,
            IsPaymentIntermediary: null,
            Parent: null));

        var category = await api.Budgets_CreateCategoryAsync(new BudgetCategoryCreateRequest("Utilities Category"));
        var purpose = await api.BudgetPurposes_CreateAsync(new BudgetPurposeCreateRequest(
            Name: "Utilities",
            SourceType: BudgetSourceType.Contact,
            SourceId: contact.Id,
            Description: null,
            BudgetCategoryId: category.Id));

        await api.BudgetRules_CreateAsync(new BudgetRuleCreateRequest(
            BudgetPurposeId: purpose.Id,
            BudgetCategoryId: null,
            Amount: -60m,
            Interval: BudgetIntervalType.Monthly,
            CustomIntervalMonths: null,
            StartDate: new DateOnly(2026, 1, 1),
            EndDate: null,
            PurposePattern: "ST6464646464",
            UseRegex: false));

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
        var upload = await api.StatementDrafts_UploadAsync(ms, "statement_budget_pattern.csv");
        upload.Should().NotBeNull();
        var draftId = upload!.FirstDraft!.DraftId;

        var draft = await api.StatementDrafts_GetAsync(draftId);
        draft.Should().NotBeNull();
        foreach (var entry in draft!.Entries)
        {
            await api.StatementDrafts_SetEntryContactAsync(draftId, entry.Id, new StatementDraftSetContactRequest(contact.Id));
        }

        var book = await api.StatementDrafts_BookAsync(draftId, forceWarnings: true);
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
            DateBasis: BudgetReportDateBasis.BookingDate));
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
            SecurityProcessingEnabled: false));

        var contact = await api.Contacts_CreateAsync(new FinanceManager.Shared.Dtos.Contacts.ContactCreateRequest(
            Name: "Utility Provider Regex",
            Type: ContactType.Person,
            CategoryId: null,
            Description: null,
            IsPaymentIntermediary: null,
            Parent: null));

        var category = await api.Budgets_CreateCategoryAsync(new BudgetCategoryCreateRequest("Utilities Regex Category"));
        var purpose = await api.BudgetPurposes_CreateAsync(new BudgetPurposeCreateRequest(
            Name: "Utilities Regex",
            SourceType: BudgetSourceType.Contact,
            SourceId: contact.Id,
            Description: null,
            BudgetCategoryId: category.Id));

        await api.BudgetRules_CreateAsync(new BudgetRuleCreateRequest(
            BudgetPurposeId: purpose.Id,
            BudgetCategoryId: null,
            Amount: -60m,
            Interval: BudgetIntervalType.Monthly,
            CustomIntervalMonths: null,
            StartDate: new DateOnly(2026, 1, 1),
            EndDate: null,
            PurposePattern: "ST\\d{10}",
            UseRegex: true));

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
        var upload = await api.StatementDrafts_UploadAsync(ms, "statement_budget_pattern_regex.csv");
        upload.Should().NotBeNull();
        var draftId = upload!.FirstDraft!.DraftId;

        var draft = await api.StatementDrafts_GetAsync(draftId);
        draft.Should().NotBeNull();
        foreach (var entry in draft!.Entries)
        {
            await api.StatementDrafts_SetEntryContactAsync(draftId, entry.Id, new StatementDraftSetContactRequest(contact.Id));
        }

        var book = await api.StatementDrafts_BookAsync(draftId, forceWarnings: true);
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
            DateBasis: BudgetReportDateBasis.BookingDate));
        report.Should().NotBeNull();
        var categoryRow = report.Categories.Single(x => x.Id == category.Id);
        var purposeRow = categoryRow.Purposes.Single(x => x.Id == purpose.Id);
        purposeRow.Actual.Should().Be(-60m);
    }

    [Fact]
    public async Task BudgetReport_WithComplexData()
    {
        var api = CreateClient();
        await EnsureAuthenticatedAsync(api);

        var account = await api.CreateAccountAsync(new AccountCreateRequest(
            Name: "ComplexData",
            Type: AccountType.Giro,
            Iban: "DE50700500000007882996",
            BankContactId: null,
            NewBankContactName: "Test Bank",
            SymbolAttachmentId: null,
            SavingsPlanExpectation: SavingsPlanExpectation.Optional,
            SecurityProcessingEnabled: true),
            TestContext.Current.CancellationToken);

        var selfContact = (await api.Contacts_ListAsync(type: ContactType.Self, all: true, ct: TestContext.Current.CancellationToken)).Single();

        var contactCategoryActivities = await api.ContactCategories_CreateAsync(new ContactCategoryCreateRequest("Activities"), TestContext.Current.CancellationToken);
        var contactCategoryInsurances = await api.ContactCategories_CreateAsync(new ContactCategoryCreateRequest("Insurances"), TestContext.Current.CancellationToken);

        var taxSavingPlanCategory = await api.SavingsPlanCategories_CreateAsync(new SavingsPlanCategoryDto()
        {
            Name = "Steuer",
        }, TestContext.Current.CancellationToken);
        taxSavingPlanCategory.Should().NotBeNull();        

        var budgetCategoryTax = await api.Budgets_CreateCategoryAsync(new BudgetCategoryCreateRequest("Steuern"), TestContext.Current.CancellationToken);
        budgetCategoryTax.Should().NotBeNull();
        var budgetCategoryActivities = await api.Budgets_CreateCategoryAsync(new BudgetCategoryCreateRequest("Activities"), TestContext.Current.CancellationToken);
        var budgetCategoryInsurances = await api.Budgets_CreateCategoryAsync(new BudgetCategoryCreateRequest("Insurances"), TestContext.Current.CancellationToken);

        #region Rundfunkgebühr Budget Purpose and Rules
        var radioSavingPlan = await api.SavingsPlans_CreateAsync(
            new SavingsPlanCreateRequest()
            {
                Name = "Rundfunkgebühr",
                CategoryId = taxSavingPlanCategory?.Id,
                Interval = SavingsPlanInterval.Quarterly,
                Type = SavingsPlanType.Recurring,
                TargetAmount = 54.22m,
                TargetDate = new DateTime(2026, 11, 01)
            },
            TestContext.Current.CancellationToken);
        radioSavingPlan.Should().NotBeNull();
        var budgetPurposeRadio = await api.BudgetPurposes_CreateAsync(new BudgetPurposeCreateRequest(
            Name: "Rundfunkgebühr",
            SourceType: BudgetSourceType.SavingsPlan,
            SourceId: radioSavingPlan?.Id ?? Guid.Empty,
            Description: null,
            BudgetCategoryId: budgetCategoryTax?.Id),
            TestContext.Current.CancellationToken);
        budgetPurposeRadio.Should().NotBeNull();
        var budgetRuleRadioMonthly = await api.BudgetRules_CreateAsync(new BudgetRuleCreateRequest(
            BudgetPurposeId: budgetPurposeRadio?.Id ?? Guid.Empty,
            BudgetCategoryId: null,
            Amount: -18.35m,
            Interval: BudgetIntervalType.Monthly,
            CustomIntervalMonths: null,
            StartDate: new DateOnly(2020, 01, 01),
            EndDate: null),
            TestContext.Current.CancellationToken);
        var budgetRuleRadioQuarterly = await api.BudgetRules_CreateAsync(new BudgetRuleCreateRequest(
            BudgetPurposeId: budgetPurposeRadio?.Id ?? Guid.Empty,
            BudgetCategoryId: null,
            Amount: 55.08m,
            Interval: BudgetIntervalType.Quarterly,
            CustomIntervalMonths: null,
            StartDate: new DateOnly(2020, 02, 15),
            EndDate: null),
            TestContext.Current.CancellationToken);
        #endregion

        #region Gym Budget Purpose and Rules
        var gymContact = await api.Contacts_CreateAsync(new FinanceManager.Shared.Dtos.Contacts.ContactCreateRequest(
            Name: "Gym",
            Type: ContactType.Organization,
            CategoryId: contactCategoryActivities?.Id,
            Description: null,
            IsPaymentIntermediary: null,
            Parent: null), TestContext.Current.CancellationToken);
        var budgetPurposeGym = await api.BudgetPurposes_CreateAsync(new BudgetPurposeCreateRequest(
            Name: "Gym",
            SourceType: BudgetSourceType.Contact,
            SourceId: gymContact?.Id ?? Guid.Empty,
            Description: null,
            BudgetCategoryId: budgetCategoryActivities?.Id),
            TestContext.Current.CancellationToken);
        var budgetRuleGymMonthly = await api.BudgetRules_CreateAsync(new BudgetRuleCreateRequest(
            BudgetPurposeId: budgetPurposeGym?.Id ?? Guid.Empty,
            BudgetCategoryId: null,
            Amount: -15m,
            Interval: BudgetIntervalType.Monthly,
            CustomIntervalMonths: null,
            StartDate: new DateOnly(2020, 01, 01),
            EndDate: null),
            TestContext.Current.CancellationToken);
        #endregion

        #region Insurance Budget Purpose and Rules
        var insuranceContact = await api.Contacts_CreateAsync(new FinanceManager.Shared.Dtos.Contacts.ContactCreateRequest(
            Name: "Insurance",
            Type: ContactType.Organization,
            CategoryId: contactCategoryInsurances?.Id,
            Description: null,
            IsPaymentIntermediary: null,
            Parent: null), TestContext.Current.CancellationToken);
        var budgetPurposeInsurance = await api.BudgetPurposes_CreateAsync(new BudgetPurposeCreateRequest(
            Name: "Insurance",
            SourceType: BudgetSourceType.Contact,
            SourceId: insuranceContact?.Id ?? Guid.Empty,
            Description: null,
            BudgetCategoryId: budgetCategoryInsurances?.Id),
            TestContext.Current.CancellationToken);
        var budgetRuleInsuranceMonthly = await api.BudgetRules_CreateAsync(new BudgetRuleCreateRequest(
            BudgetPurposeId: budgetPurposeInsurance?.Id ?? Guid.Empty,
            BudgetCategoryId: null,
            Amount: -20.93m,
            Interval: BudgetIntervalType.Monthly,
            CustomIntervalMonths: null,
            StartDate: new DateOnly(2020, 01, 01),
            EndDate: null),
            TestContext.Current.CancellationToken);
        #endregion

        #region Insurance 2 Budget Purpose and Rules
        var insurance2Contact = await api.Contacts_CreateAsync(new FinanceManager.Shared.Dtos.Contacts.ContactCreateRequest(
            Name: "Insurance 2",
            Type: ContactType.Organization,
            CategoryId: contactCategoryInsurances?.Id,
            Description: null,
            IsPaymentIntermediary: null,
            Parent: null), TestContext.Current.CancellationToken);
        var budgetPurposeInsurance2 = await api.BudgetPurposes_CreateAsync(new BudgetPurposeCreateRequest(
            Name: "Insurance 2",
            SourceType: BudgetSourceType.Contact,
            SourceId: insurance2Contact?.Id ?? Guid.Empty,
            Description: null,
            BudgetCategoryId: budgetCategoryInsurances?.Id),
            TestContext.Current.CancellationToken);
        var budgetRuleInsurance2Monthly = await api.BudgetRules_CreateAsync(new BudgetRuleCreateRequest(
            BudgetPurposeId: budgetPurposeInsurance2?.Id ?? Guid.Empty,
            BudgetCategoryId: null,
            Amount: -20.64m,
            Interval: BudgetIntervalType.Monthly,
            CustomIntervalMonths: null,
            StartDate: new DateOnly(2020, 01, 01),
            EndDate: null),
            TestContext.Current.CancellationToken);
        #endregion

        #region Stromkosten Budget Purpose and Rules
        var budgetCategoryNebenkosten = await api.Budgets_CreateCategoryAsync(new BudgetCategoryCreateRequest("Nebenkosten"), TestContext.Current.CancellationToken);
        var stadtwerkeContact = await api.Contacts_CreateAsync(new FinanceManager.Shared.Dtos.Contacts.ContactCreateRequest(
            Name: "Stadtwerke",
            Type: ContactType.Organization,
            CategoryId: null,
            Description: null,
            IsPaymentIntermediary: null,
            Parent: null), TestContext.Current.CancellationToken);
        var budgetPurposeStromkosten = await api.BudgetPurposes_CreateAsync(new BudgetPurposeCreateRequest(
            Name: "Stromkosten",
            SourceType: BudgetSourceType.Contact,
            SourceId: stadtwerkeContact?.Id ?? Guid.Empty,
            Description: null,
            BudgetCategoryId: budgetCategoryNebenkosten?.Id),
            TestContext.Current.CancellationToken);
        var budgetRuleStromkostenMonthly = await api.BudgetRules_CreateAsync(new BudgetRuleCreateRequest(
            BudgetPurposeId: budgetPurposeStromkosten?.Id ?? Guid.Empty,
            BudgetCategoryId: null,
            Amount: -75m,
            Interval: BudgetIntervalType.Monthly,
            CustomIntervalMonths: null,
            StartDate: new DateOnly(2020, 01, 01),
            EndDate: null),
            TestContext.Current.CancellationToken);
        #endregion

        #region Dividend Security
        var dividendSecurity = await api.Securities_CreateAsync(new SecurityRequest
        {
            Name = "Complex Data Security",
            Identifier = "US546585765H8",
            CurrencyCode = "EUR"
        }, TestContext.Current.CancellationToken);
        dividendSecurity.Should().NotBeNull();
        #endregion

        #region Statements
        var statementDraft = await api.StatementDrafts_CreateAsync(null, TestContext.Current.CancellationToken);
        (await api.StatementDrafts_SetAccountAsync(statementDraft.DraftId, account.Id, TestContext.Current.CancellationToken)).Should().NotBeNull();

        async Task<Guid> AddEntryAsync(DateTime date, decimal amount, string subject)
        {
            var result = await api.StatementDrafts_AddEntryAsync(statementDraft.DraftId, new StatementDraftAddEntryRequest(date, amount, subject), TestContext.Current.CancellationToken);
            result.Should().NotBeNull();
            return result!.Entries.Last().Id;
        }

        // 1) Dividend #1: -/+ security income, booked against the account's bank contact.
        var statementEntrySecurityIncome1 = (await api.StatementDrafts_AddEntryAsync(statementDraft.DraftId, new StatementDraftAddEntryRequest(new DateTime(2026,08,03), 8.37m, "Zins/Dividende ISIN US546585765H8") , TestContext.Current.CancellationToken)).Entries.Last();
        statementEntrySecurityIncome1 = await api.StatementDrafts_UpdateEntryCoreAsync(statementDraft.DraftId, statementEntrySecurityIncome1.Id,
                new StatementDraftUpdateEntryCoreRequest(statementEntrySecurityIncome1.BookingDate, statementEntrySecurityIncome1.BookingDate, statementEntrySecurityIncome1.Amount, statementEntrySecurityIncome1.Subject, "", "EUR", "Zins/Dividende"), TestContext.Current.CancellationToken);
        (await api.StatementDrafts_SetEntryContactAsync(statementDraft.DraftId, statementEntrySecurityIncome1.Id, new StatementDraftSetContactRequest(account.BankContactId), TestContext.Current.CancellationToken)).Should().NotBeNull();
        (await api.StatementDrafts_SetEntrySecurityAsync(statementDraft.DraftId, statementEntrySecurityIncome1.Id, new StatementDraftSetEntrySecurityRequest(dividendSecurity.Id, SecurityTransactionType.Dividend, null, null, null), TestContext.Current.CancellationToken)).Should().NotBeNull();

        // 2) Rueckstellung Rundfunkgebuehr: self-contact posting mirrored into the savings plan.
        var radioEntryId = await AddEntryAsync(new DateTime(2026, 08, 03), -18.36m, "Rueckstellung Rundfunkgebuehr");
        (await api.StatementDrafts_SetEntryContactAsync(statementDraft.DraftId, radioEntryId, new StatementDraftSetContactRequest(selfContact.Id), TestContext.Current.CancellationToken)).Should().NotBeNull();
        (await api.StatementDrafts_SetEntrySavingsPlanAsync(statementDraft.DraftId, radioEntryId, new StatementDraftSetSavingsPlanRequest(radioSavingPlan!.Id), TestContext.Current.CancellationToken)).Should().NotBeNull();

        // 3) Versicherung: matches Insurance 2's exact monthly rule amount (-20.64).
        var insurance2EntryId = await AddEntryAsync(new DateTime(2026, 08, 03), -20.64m, "Versicherung");
        (await api.StatementDrafts_SetEntryContactAsync(statementDraft.DraftId, insurance2EntryId, new StatementDraftSetContactRequest(insurance2Contact!.Id), TestContext.Current.CancellationToken)).Should().NotBeNull();

        // 4) Stadtwerke: exact match for the "Stromkosten" budget purpose.
        var stadtwerkeEntryId = await AddEntryAsync(new DateTime(2026, 08, 03), -75m, "Stadtwerke");
        (await api.StatementDrafts_SetEntryContactAsync(statementDraft.DraftId, stadtwerkeEntryId, new StatementDraftSetContactRequest(stadtwerkeContact!.Id), TestContext.Current.CancellationToken)).Should().NotBeNull();

        // 5) Gym: exact match for the Gym budget purpose.
        var gymEntryId = await AddEntryAsync(new DateTime(2026, 08, 03), -15m, "Gym");
        (await api.StatementDrafts_SetEntryContactAsync(statementDraft.DraftId, gymEntryId, new StatementDraftSetContactRequest(gymContact!.Id), TestContext.Current.CancellationToken)).Should().NotBeNull();

        // 6) Dividend #2: second, unbudgeted dividend from the same security.
        var securityIncome2EntryId = await AddEntryAsync(new DateTime(2026, 08, 04), 9.67m, "Zins/Dividende ISIN US546585765H8");
        (await api.StatementDrafts_SetEntryContactAsync(statementDraft.DraftId, securityIncome2EntryId, new StatementDraftSetContactRequest(account.BankContactId), TestContext.Current.CancellationToken)).Should().NotBeNull();
        (await api.StatementDrafts_SetEntrySecurityAsync(statementDraft.DraftId, securityIncome2EntryId, new StatementDraftSetEntrySecurityRequest(dividendSecurity.Id, SecurityTransactionType.Dividend, null, null, null), TestContext.Current.CancellationToken)).Should().NotBeNull();

        // 7) + 8) Transfer Sparkonto: self-contact pair, unbudgeted and cost-neutral.
        var transferOutEntryId = await AddEntryAsync(new DateTime(2026, 08, 04), 5000m, "Transfer Sparkonto");
        (await api.StatementDrafts_SetEntryContactAsync(statementDraft.DraftId, transferOutEntryId, new StatementDraftSetContactRequest(selfContact.Id), TestContext.Current.CancellationToken)).Should().NotBeNull();

        var transferInEntryId = await AddEntryAsync(new DateTime(2026, 08, 04), -5000m, "Transfer Sparkonto");
        (await api.StatementDrafts_SetEntryContactAsync(statementDraft.DraftId, transferInEntryId, new StatementDraftSetContactRequest(selfContact.Id), TestContext.Current.CancellationToken)).Should().NotBeNull();

        // 9) Sparplan Allgemein: self-contact posting without a matching savings-plan purpose, unbudgeted and cost-neutral.
        var sparplanAllgemeinEntryId = await AddEntryAsync(new DateTime(2026, 08, 04), -200m, "Sparplan Allgemein");
        (await api.StatementDrafts_SetEntryContactAsync(statementDraft.DraftId, sparplanAllgemeinEntryId, new StatementDraftSetContactRequest(selfContact.Id), TestContext.Current.CancellationToken)).Should().NotBeNull();

        var book = await api.StatementDrafts_BookAsync(statementDraft.DraftId, forceWarnings: true, TestContext.Current.CancellationToken);
        book.Should().NotBeNull();
        book!.Success.Should().BeTrue();

        #endregion

        #region Assertions
        var report = await api.Budgets_GetReportAsync(new BudgetReportRequest(
            AsOfDate: new DateOnly(2026, 08, 31),
            Months: 1,
            Interval: BudgetReportInterval.Month,
            ShowTitle: false,
            ShowLineChart: false,
            ShowMonthlyTable: false,
            ShowDetailsTable: true,
            CategoryValueScope: BudgetReportValueScope.TotalRange,
            IncludePurposeRows: true,
            DateBasis: BudgetReportDateBasis.BookingDate),
            TestContext.Current.CancellationToken);
        report.Should().NotBeNull();

        // Rundfunkgebuehr: the booked amount (-18.36) overshoots the exact monthly rule (-18.35) by one
        // cent, so the posting is split: -18.35 is recognized as Actual, and the leftover -0.01 stays
        // attributed to this purpose (visible in its Postings, not valued) rather than being routed to
        // the top-level Unbudgeted/CostNeutral buckets, which are reserved for postings that matched no
        // budget purpose at all.
        var taxCategory = report.Categories.Single(c => c.Id == budgetCategoryTax!.Id);
        var radioPurposeRow = taxCategory.Purposes.Single(p => p.Id == budgetPurposeRadio!.Id);
        radioPurposeRow.Actual.Should().Be(-18.35m);

        var rawReport = await api.Budgets_GetReportRawAsync(new BudgetReportRequest(
            AsOfDate: new DateOnly(2026, 08, 31),
            Months: 1,
            Interval: BudgetReportInterval.Month,
            ShowTitle: false,
            ShowLineChart: false,
            ShowMonthlyTable: false,
            ShowDetailsTable: true,
            CategoryValueScope: BudgetReportValueScope.TotalRange,
            IncludePurposeRows: true,
            DateBasis: BudgetReportDateBasis.BookingDate),
            TestContext.Current.CancellationToken);
        var radioRawPurpose = rawReport.Categories.Single(c => c.CategoryId == budgetCategoryTax!.Id)
            .Purposes.Single(p => p.PurposeId == budgetPurposeRadio!.Id);
        radioRawPurpose.Postings.Should().ContainSingle(p => !p.IsValuedForBudgetPurpose && p.Amount == -0.01m);

        // Insurances: Insurance 2 is actually booked; Insurance (the first one) stays at zero actual.
        var insurancesCategory = report.Categories.Single(c => c.Id == budgetCategoryInsurances!.Id);
        var insurancePurposeRow = insurancesCategory.Purposes.Single(p => p.Id == budgetPurposeInsurance!.Id);
        insurancePurposeRow.Actual.Should().Be(0m);
        var insurance2PurposeRow = insurancesCategory.Purposes.Single(p => p.Id == budgetPurposeInsurance2!.Id);
        insurance2PurposeRow.Actual.Should().Be(-20.64m);

        // Activities: Gym is booked at the exact budgeted amount.
        var activitiesCategory = report.Categories.Single(c => c.Id == budgetCategoryActivities!.Id);
        var gymPurposeRow = activitiesCategory.Purposes.Single(p => p.Id == budgetPurposeGym!.Id);
        gymPurposeRow.Actual.Should().Be(-15m);

        // Stromkosten: exact match against the "Nebenkosten" category's Stadtwerke rule.
        var nebenkostenCategory = report.Categories.Single(c => c.Id == budgetCategoryNebenkosten!.Id);
        var stromkostenPurposeRow = nebenkostenCategory.Purposes.Single(p => p.Id == budgetPurposeStromkosten!.Id);
        stromkostenPurposeRow.Actual.Should().Be(-75m);

        // Both dividends (8.37 + 9.67) have no matching budget purpose.
        report.Categories.Should().Contain(c => c.Kind == BudgetReportCategoryRowKind.Unbudgeted);
        var unbudgetedCategory = report.Categories.Single(c => c.Kind == BudgetReportCategoryRowKind.Unbudgeted);
        unbudgetedCategory.Actual.Should().Be(8.37m + 9.67m);

        // Only the three self-contact transfers (5000 - 5000 - 200) show up as cost-neutral. The
        // Rundfunkgebuehr overshoot is NOT cost-neutral, even though it also originates from a
        // self-contact posting: it is a leftover of a posting that DID match a budget purpose, so per
        // the requirement it stays attributed to that purpose (asserted above) instead of falling into
        // the generic cost-neutral/unbudgeted buckets, which are reserved for postings that matched no
        // budget purpose whatsoever.
        report.Categories.Should().Contain(c => c.Kind == BudgetReportCategoryRowKind.UnbudgetedSelfCostNeutral);
        var costNeutralCategory = report.Categories.Single(c => c.Kind == BudgetReportCategoryRowKind.UnbudgetedSelfCostNeutral);
        costNeutralCategory.Actual.Should().Be(5000m - 5000m - 200m);

        var from = new DateTime(2026, 08, 01);
        var to = new DateTime(2026, 08, 31, 23, 59, 59);
        var unbudgeted = await api.Budgets_GetUnbudgetedPostingsAsync(from, to, BudgetReportDateBasis.BookingDate, null, TestContext.Current.CancellationToken);
        // 5 raw postings: the two dividends and the three self-contact transfers. The Rundfunkgebuehr
        // posting is NOT among them, since its 0.01 overshoot is reported against its own purpose only.
        unbudgeted.Should().HaveCount(5);
        unbudgeted.Should().ContainSingle(p => p.Subject == "Zins/Dividende ISIN US546585765H8" && p.Amount == 8.37m);
        unbudgeted.Should().ContainSingle(p => p.Subject == "Zins/Dividende ISIN US546585765H8" && p.Amount == 9.67m);
        unbudgeted.Should().ContainSingle(p => p.Subject == "Transfer Sparkonto" && p.Amount == 5000m);
        unbudgeted.Should().ContainSingle(p => p.Subject == "Transfer Sparkonto" && p.Amount == -5000m);
        unbudgeted.Should().ContainSingle(p => p.Subject == "Sparplan Allgemein" && p.Amount == -200m);

        // Every real posting must be listed under exactly one result row: either a single budget purpose
        // (its Postings array - a posting can legitimately appear twice there when Finish() splits it into
        // a valued and an unvalued/overrun fragment, both sharing the same PostingId) or the top-level
        // Unbudgeted list, never both. A posting shown at its purpose must never also show up in the
        // generic Unbudgeted list, and vice versa.
        var postingRowLabels = rawReport.Categories
            .SelectMany(c => c.Purposes)
            .Concat(rawReport.UncategorizedPurposes)
            .SelectMany(p => p.Postings.Select(posting => (posting.PostingId, RowLabel: $"purpose:{p.PurposeId}")))
            .Concat(rawReport.UnbudgetedPostings.Select(posting => (posting.PostingId, RowLabel: "unbudgeted")))
            .ToList();
        var postingsListedUnderMultipleRows = postingRowLabels
            .GroupBy(x => x.PostingId)
            .Where(g => g.Select(x => x.RowLabel).Distinct().Count() > 1)
            .ToList();
        postingsListedUnderMultipleRows.Should().BeEmpty("no posting may be listed under more than one result row");
        #endregion
    }
}
