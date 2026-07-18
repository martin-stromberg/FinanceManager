using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.IO.Compression;
using System.Threading.Tasks;
using System.Xml.Linq;
using FinanceManager.Application;
using FinanceManager.Shared;
using FinanceManager.Shared.Dtos.Accounts;
using FinanceManager.Shared.Dtos.Budget;
using FinanceManager.Shared.Dtos.Contacts;
using FinanceManager.Shared.Dtos.Statements;
using FinanceManager.Web.ViewModels.Budget;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FinanceManager.Tests.Integration.ViewModels;

/// <summary>
/// Integration tests for the budget report view model.
/// </summary>
public sealed class BudgetReportViewModelIntegrationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public BudgetReportViewModelIntegrationTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// Verifies that a one-year budget report keeps the regular housing bookings budgeted
    /// and leaves the traffic booking unbudgeted.
    /// </summary>
    [Fact]
    public async Task InitializeAsync_TotalRange_ShouldShowHousingBookingsBudgeted_AndTrafficBookingUnbudgeted_WhenLoadedForCurrentYear()
    {
        var today = DateTime.Today;
        var asOfDate = new DateOnly(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month));
        _factory.FixedUtcNow = DateTime.SpecifyKind(today, DateTimeKind.Utc);

        var api = await CreateAuthenticatedApiClientAsync();

        var account = await EnsureAccountAsync(api);
        var rentContact = await api.Contacts_CreateAsync(new ContactCreateRequest(
            Name: "Miete",
            Type: ContactType.Organization,
            CategoryId: null,
            Description: null,
            IsPaymentIntermediary: null));
        var stromContact = await api.Contacts_CreateAsync(new ContactCreateRequest(
            Name: "Strom",
            Type: ContactType.Organization,
            CategoryId: null,
            Description: null,
            IsPaymentIntermediary: null));

        var category = await api.Budgets_CreateCategoryAsync(new BudgetCategoryCreateRequest("Wohnen"));

        var rentPurpose = await api.BudgetPurposes_CreateAsync(new BudgetPurposeCreateRequest(
            Name: "Miete",
            SourceType: BudgetSourceType.Contact,
            SourceId: rentContact.Id,
            Description: null,
            BudgetCategoryId: category.Id));
        var stromPurpose = await api.BudgetPurposes_CreateAsync(new BudgetPurposeCreateRequest(
            Name: "Strom",
            SourceType: BudgetSourceType.Contact,
            SourceId: stromContact.Id,
            Description: null,
            BudgetCategoryId: category.Id));

        var ruleStart = new DateOnly(today.Year, today.Month, 1).AddMonths(-23);
        await api.BudgetRules_CreateAsync(new BudgetRuleCreateRequest(
            BudgetPurposeId: rentPurpose.Id,
            BudgetCategoryId: null,
            Amount: -500m,
            Interval: BudgetIntervalType.Monthly,
            CustomIntervalMonths: null,
            StartDate: ruleStart,
            EndDate: null));
        await api.BudgetRules_CreateAsync(new BudgetRuleCreateRequest(
            BudgetPurposeId: stromPurpose.Id,
            BudgetCategoryId: null,
            Amount: -80m,
            Interval: BudgetIntervalType.Monthly,
            CustomIntervalMonths: null,
            StartDate: ruleStart,
            EndDate: null,
            PurposePattern: "KNR-4711",
            UseRegex: false));

        var draft = await api.StatementDrafts_CreateAsync();
        draft.Should().NotBeNull();
        await api.StatementDrafts_SetAccountAsync(draft!.DraftId, account.Id);

        var seedMonth = new DateOnly(today.Year, today.Month, 2);
        for (var offset = 23; offset >= 0; offset--)
        {
            var bookingMonth = seedMonth.AddMonths(-offset);

            await AddEntryAsync(api, draft.DraftId, bookingMonth, -500m, $"Miete {bookingMonth:yyyy-MM}", rentContact.Id);
            await AddEntryAsync(api, draft.DraftId, bookingMonth, -80m, $"Abrechnung KNR-4711 {bookingMonth:yyyy-MM}", stromContact.Id);
        }

        await AddEntryAsync(api, draft.DraftId, new DateOnly(today.Year, today.Month, 20).AddMonths(-3), -120.00m, "Abrechnung KNR-4711 Nachzahlung", stromContact.Id);
        await AddEntryAsync(api, draft.DraftId, new DateOnly(today.Year, today.Month, 15), -49.90m, "Verkehrsabo VABO-9000", stromContact.Id);

        var booking = await api.StatementDrafts_BookAsync(draft.DraftId, forceWarnings: true);
        booking.Should().NotBeNull();
        booking!.Success.Should().BeTrue();

        var vm = CreateViewModel(api);
        await vm.ApplySettingsAsync(BudgetReportSettings.Default with
        {
            AsOfDate = asOfDate,
            Months = 12,
            CategoryValueScope = FinanceManager.Web.ViewModels.Budget.BudgetReportValueScope.TotalRange
        });

        vm.Periods.Should().HaveCount(12);

        var wohnen = vm.Categories.Should().ContainSingle(c => c.Name == "Wohnen" && c.Kind == BudgetReportCategoryRowKind.Data).Subject;
        wohnen.Budget.Should().Be(-6960m);
        wohnen.Actual.Should().Be(-6960m);
        wohnen.Delta.Should().Be(0m);

        var miete = wohnen.Purposes.Should().ContainSingle(p => p.Name == "Miete").Subject;
        miete.Budget.Should().Be(-6000m);
        miete.Actual.Should().Be(-6000m);

        var strom = wohnen.Purposes.Should().ContainSingle(p => p.Name == "Strom").Subject;
        strom.Budget.Should().Be(-960m);
        strom.Actual.Should().Be(-960m);

        var unbudgeted = vm.Categories.Should().ContainSingle(c => c.Kind == BudgetReportCategoryRowKind.Unbudgeted).Subject;
        unbudgeted.Actual.Should().Be(-169.90m);
        unbudgeted.Purposes.Should().BeEmpty();
    }

    /// <summary>
    /// Verifies that a one-year budget report keeps the regular housing bookings budgeted
    /// and leaves the traffic booking unbudgeted.
    /// </summary>
    [Fact]
    public async Task InitializeAsync_LastInterval_ShouldShowHousingBookingsBudgeted_AndTrafficBookingUnbudgeted_WhenLoadedForCurrentYear()
    {
        var today = DateTime.Today;
        var asOfDate = new DateOnly(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month));
        _factory.FixedUtcNow = DateTime.SpecifyKind(today, DateTimeKind.Utc);

        var api = await CreateAuthenticatedApiClientAsync();

        var account = await EnsureAccountAsync(api);
        var rentContact = await api.Contacts_CreateAsync(new ContactCreateRequest(
            Name: "Miete",
            Type: ContactType.Organization,
            CategoryId: null,
            Description: null,
            IsPaymentIntermediary: null));
        var stromContact = await api.Contacts_CreateAsync(new ContactCreateRequest(
            Name: "Strom",
            Type: ContactType.Organization,
            CategoryId: null,
            Description: null,
            IsPaymentIntermediary: null));

        var category = await api.Budgets_CreateCategoryAsync(new BudgetCategoryCreateRequest("Wohnen"));

        var rentPurpose = await api.BudgetPurposes_CreateAsync(new BudgetPurposeCreateRequest(
            Name: "Miete",
            SourceType: BudgetSourceType.Contact,
            SourceId: rentContact.Id,
            Description: null,
            BudgetCategoryId: category.Id));
        var stromPurpose = await api.BudgetPurposes_CreateAsync(new BudgetPurposeCreateRequest(
            Name: "Strom",
            SourceType: BudgetSourceType.Contact,
            SourceId: stromContact.Id,
            Description: null,
            BudgetCategoryId: category.Id));

        var ruleStart = new DateOnly(today.Year, today.Month, 1).AddMonths(-23);
        await api.BudgetRules_CreateAsync(new BudgetRuleCreateRequest(
            BudgetPurposeId: rentPurpose.Id,
            BudgetCategoryId: null,
            Amount: -500m,
            Interval: BudgetIntervalType.Monthly,
            CustomIntervalMonths: null,
            StartDate: ruleStart,
            EndDate: null));
        await api.BudgetRules_CreateAsync(new BudgetRuleCreateRequest(
            BudgetPurposeId: stromPurpose.Id,
            BudgetCategoryId: null,
            Amount: -80m,
            Interval: BudgetIntervalType.Monthly,
            CustomIntervalMonths: null,
            StartDate: ruleStart,
            EndDate: null,
            PurposePattern: "KNR-4711",
            UseRegex: false));

        var draft = await api.StatementDrafts_CreateAsync();
        draft.Should().NotBeNull();
        await api.StatementDrafts_SetAccountAsync(draft!.DraftId, account.Id);

        var seedMonth = new DateOnly(today.Year, today.Month, 2);
        for (var offset = 23; offset >= 0; offset--)
        {
            var bookingMonth = seedMonth.AddMonths(-offset);

            await AddEntryAsync(api, draft.DraftId, bookingMonth, -500m, $"Miete {bookingMonth:yyyy-MM}", rentContact.Id);
            await AddEntryAsync(api, draft.DraftId, bookingMonth, -80m, $"Abrechnung KNR-4711 {bookingMonth:yyyy-MM}", stromContact.Id);
        }

        await AddEntryAsync(api, draft.DraftId, new DateOnly(today.Year, today.Month, 20).AddMonths(-3), -120.00m, "Abrechnung KNR-4711 Nachzahlung", stromContact.Id);
        await AddEntryAsync(api, draft.DraftId, new DateOnly(today.Year, today.Month, 15), -49.90m, "Verkehrsabo VABO-9000", stromContact.Id);

        var booking = await api.StatementDrafts_BookAsync(draft.DraftId, forceWarnings: true);
        booking.Should().NotBeNull();
        booking!.Success.Should().BeTrue();

        var vm = CreateViewModel(api);
        await vm.ApplySettingsAsync(BudgetReportSettings.Default with
        {
            AsOfDate = asOfDate,
            Months = 12,
            CategoryValueScope = FinanceManager.Web.ViewModels.Budget.BudgetReportValueScope.LastInterval
        });

        vm.Periods.Should().HaveCount(12);

        var wohnen = vm.Categories.Should().ContainSingle(c => c.Name == "Wohnen" && c.Kind == BudgetReportCategoryRowKind.Data).Subject;
        wohnen.Budget.Should().Be(-580m);
        wohnen.Actual.Should().Be(-580m);
        wohnen.Delta.Should().Be(0m);

        var miete = wohnen.Purposes.Should().ContainSingle(p => p.Name == "Miete").Subject;
        miete.Budget.Should().Be(-500m);
        miete.Actual.Should().Be(-500m);

        var strom = wohnen.Purposes.Should().ContainSingle(p => p.Name == "Strom").Subject;
        strom.Budget.Should().Be(-80m);
        strom.Actual.Should().Be(-80m);

        await vm.ShowPurposePostingsAsync(strom);
        vm.PurposePostingsVisible.Should().BeTrue();
        vm.PurposePostingsKind.Should().Be(BudgetReportViewModel.PostingsOverlayKind.Purpose);
        vm.PurposePostingsPurpose.Should().NotBeNull();
        vm.PurposePostings.Should().ContainSingle();
        vm.PurposePostings[0].Amount.Should().Be(-80m);
        vm.PurposePostings[0].ContactId.Should().Be(stromContact.Id);
        vm.PurposePostings[0].Subject.Should().Contain("KNR-4711");
        vm.PurposePostings[0].Description.Should().Be("Lastschrift");

        await vm.ShowUnbudgetedPostingsAsync();
        vm.PurposePostingsVisible.Should().BeTrue();
        vm.PurposePostingsKind.Should().Be(BudgetReportViewModel.PostingsOverlayKind.Unbudgeted);
        vm.PurposePostingsPurpose.Should().BeNull();
        vm.PurposePostings.Should().ContainSingle();
        vm.PurposePostings[0].Amount.Should().Be(-49.90m);
        vm.PurposePostings[0].ContactId.Should().Be(stromContact.Id);
        vm.PurposePostings[0].Description.Should().Be("Lastschrift");
        vm.PurposePostings[0].Subject.Should().Contain("Verkehrsabo");

        var unbudgeted = vm.Categories.Should().ContainSingle(c => c.Kind == BudgetReportCategoryRowKind.Unbudgeted).Subject;
        unbudgeted.Actual.Should().Be(-49.90m);
        unbudgeted.Purposes.Should().BeEmpty();
    }

    /// <summary>
    /// Verifies that purpose budgets are included in the parent category budget for the total report range.
    /// </summary>
    [Fact]
    public async Task InitializeAsync_TotalRange_ShouldAggregatePurposeBudgetsIntoCategoryBudget_AndUseActualMinusBudgetDelta()
    {
        var today = DateTime.Today;
        _factory.FixedUtcNow = DateTime.SpecifyKind(today, DateTimeKind.Utc);

        var api = await CreateAuthenticatedApiClientAsync();
        var asOfDate = await SeedEntertainmentBudgetAsync(api, today);

        var vm = CreateViewModel(api);
        await vm.ApplySettingsAsync(BudgetReportSettings.Default with
        {
            AsOfDate = asOfDate,
            Months = 1,
            CategoryValueScope = FinanceManager.Web.ViewModels.Budget.BudgetReportValueScope.TotalRange
        });

        AssertEntertainmentBudget(vm);
    }

    /// <summary>
    /// Verifies that purpose budgets are included in the parent category budget for the last interval.
    /// </summary>
    [Fact]
    public async Task InitializeAsync_LastInterval_ShouldAggregatePurposeBudgetsIntoCategoryBudget_AndUseActualMinusBudgetDelta()
    {
        var today = DateTime.Today;
        _factory.FixedUtcNow = DateTime.SpecifyKind(today, DateTimeKind.Utc);

        var api = await CreateAuthenticatedApiClientAsync();
        var asOfDate = await SeedEntertainmentBudgetAsync(api, today);

        var vm = CreateViewModel(api);
        await vm.ApplySettingsAsync(BudgetReportSettings.Default with
        {
            AsOfDate = asOfDate,
            Months = 1,
            CategoryValueScope = FinanceManager.Web.ViewModels.Budget.BudgetReportValueScope.LastInterval
        });

        AssertEntertainmentBudget(vm);
    }

    /// <summary>
    /// Verifies that the XLSX CurrentMonth sheet uses the same category budget and delta calculation.
    /// </summary>
    [Fact]
    public async Task GenerateXlsxAsync_CurrentMonth_ShouldAggregatePurposeBudgetsIntoCategoryBudget_AndUseActualMinusBudgetDelta()
    {
        var today = DateTime.Today;
        _factory.FixedUtcNow = DateTime.SpecifyKind(today, DateTimeKind.Utc);

        var api = await CreateAuthenticatedApiClientAsync();
        var asOfDate = await SeedEntertainmentBudgetAsync(api, today);

        var request = new BudgetReportExportRequest(asOfDate, 1, FinanceManager.Shared.Dtos.Budget.BudgetReportDateBasis.BookingDate);
        var (_, _, contentBytes) = await api.Budgets_ExportAsync(request);

        var sheets = ReadXlsxSheets(contentBytes);
        var rows = sheets.Values.Should().ContainSingle(sheetRows => sheetRows.Any(r =>
            string.Equals(GetText(r, "Category", "Kategorie"), "Unterhaltung & Aktivitaeten", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(GetText(r, "Purpose", "Zweck")))).Subject;

        var category = rows.Should().ContainSingle(r =>
            string.Equals(GetText(r, "Category", "Kategorie"), "Unterhaltung & Aktivitaeten", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(GetText(r, "Purpose", "Zweck"))).Subject;
        GetDecimal(category, "Budget").Should().Be(-40m);
        GetDecimal(category, "Actual", "Ist").Should().Be(-30m);
        GetDecimal(category, "Delta", "Abweichung").Should().Be(10m);

        var streaming = rows.Should().ContainSingle(r =>
            string.Equals(GetText(r, "Category", "Kategorie"), "Unterhaltung & Aktivitaeten", StringComparison.OrdinalIgnoreCase)
            && string.Equals(GetText(r, "Purpose", "Zweck"), "Streaming", StringComparison.OrdinalIgnoreCase)).Subject;
        GetDecimal(streaming, "Budget").Should().Be(-10m);
        GetDecimal(streaming, "Actual", "Ist").Should().Be(0m);
        GetDecimal(streaming, "Delta", "Abweichung").Should().Be(10m);
    }

    private async Task<FinanceManager.Shared.ApiClient> CreateAuthenticatedApiClientAsync()
    {
        var http = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        var api = new FinanceManager.Shared.ApiClient(http);
        var username = $"user_{Guid.NewGuid():N}";
        await api.Auth_RegisterAsync(new RegisterRequest(username, "Secret123", PreferredLanguage: null, TimeZoneId: null));
        return api;
    }

    private static async Task<AccountDto> EnsureAccountAsync(FinanceManager.Shared.ApiClient api)
    {
        var account = (await api.GetAccountsAsync()).FirstOrDefault();
        if (account != null)
        {
            return account;
        }

        return (await api.CreateAccountAsync(new AccountCreateRequest(
            Name: "Budget Report Account",
            Type: AccountType.Giro,
            Iban: "DE50700500000007882999",
            BankContactId: null,
            NewBankContactName: "Test Bank",
            SymbolAttachmentId: null,
            SavingsPlanExpectation: SavingsPlanExpectation.Optional,
            SecurityProcessingEnabled: true)))!;
    }

    private static BudgetReportViewModel CreateViewModel(FinanceManager.Shared.ApiClient api)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ICurrentUserService>(new TestCurrentUserService());
        services.AddSingleton<IApiClient>(api);

        var serviceProvider = services.BuildServiceProvider();
        return new BudgetReportViewModel(serviceProvider);
    }

    private static async Task<DateOnly> SeedEntertainmentBudgetAsync(FinanceManager.Shared.ApiClient api, DateTime today)
    {
        var asOfDate = new DateOnly(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month));
        var account = await EnsureAccountAsync(api);
        var fitnessContact = await api.Contacts_CreateAsync(new ContactCreateRequest(
            Name: "Fitnessstudio",
            Type: ContactType.Organization,
            CategoryId: null,
            Description: null,
            IsPaymentIntermediary: null));
        var gamblingContact = await api.Contacts_CreateAsync(new ContactCreateRequest(
            Name: "Gluecksspiel",
            Type: ContactType.Organization,
            CategoryId: null,
            Description: null,
            IsPaymentIntermediary: null));
        var streamingContact = await api.Contacts_CreateAsync(new ContactCreateRequest(
            Name: "Streaming",
            Type: ContactType.Organization,
            CategoryId: null,
            Description: null,
            IsPaymentIntermediary: null));

        var category = await api.Budgets_CreateCategoryAsync(new BudgetCategoryCreateRequest("Unterhaltung & Aktivitaeten"));

        var fitnessPurpose = await api.BudgetPurposes_CreateAsync(new BudgetPurposeCreateRequest(
            Name: "Fitnessstudio",
            SourceType: BudgetSourceType.Contact,
            SourceId: fitnessContact.Id,
            Description: null,
            BudgetCategoryId: category.Id));
        var gamblingPurpose = await api.BudgetPurposes_CreateAsync(new BudgetPurposeCreateRequest(
            Name: "Gluecksspiel",
            SourceType: BudgetSourceType.Contact,
            SourceId: gamblingContact.Id,
            Description: null,
            BudgetCategoryId: category.Id));
        var streamingPurpose = await api.BudgetPurposes_CreateAsync(new BudgetPurposeCreateRequest(
            Name: "Streaming",
            SourceType: BudgetSourceType.Contact,
            SourceId: streamingContact.Id,
            Description: null,
            BudgetCategoryId: category.Id));

        var ruleStart = new DateOnly(today.Year, today.Month, 1);
        await api.BudgetRules_CreateAsync(new BudgetRuleCreateRequest(fitnessPurpose.Id, null, -15m, BudgetIntervalType.Monthly, null, ruleStart, null));
        await api.BudgetRules_CreateAsync(new BudgetRuleCreateRequest(gamblingPurpose.Id, null, -15m, BudgetIntervalType.Monthly, null, ruleStart, null));
        await api.BudgetRules_CreateAsync(new BudgetRuleCreateRequest(streamingPurpose.Id, null, -10m, BudgetIntervalType.Monthly, null, ruleStart, null));

        var draft = await api.StatementDrafts_CreateAsync();
        draft.Should().NotBeNull();
        await api.StatementDrafts_SetAccountAsync(draft!.DraftId, account.Id);

        await AddEntryAsync(api, draft.DraftId, ruleStart.AddDays(2), -15m, "Fitnessstudio Beitrag", fitnessContact.Id);
        await AddEntryAsync(api, draft.DraftId, ruleStart.AddDays(3), -15m, "Gluecksspiel Einsatz", gamblingContact.Id);

        var booking = await api.StatementDrafts_BookAsync(draft.DraftId, forceWarnings: true);
        booking.Should().NotBeNull();
        booking!.Success.Should().BeTrue();

        return asOfDate;
    }

    private static void AssertEntertainmentBudget(BudgetReportViewModel vm)
    {
        var category = vm.Categories.Should().ContainSingle(c =>
            c.Name == "Unterhaltung & Aktivitaeten" && c.Kind == BudgetReportCategoryRowKind.Data).Subject;
        category.Budget.Should().Be(-40m);
        category.Actual.Should().Be(-30m);
        category.Delta.Should().Be(10m);

        var fitness = category.Purposes.Should().ContainSingle(p => p.Name == "Fitnessstudio").Subject;
        fitness.Budget.Should().Be(-15m);
        fitness.Actual.Should().Be(-15m);
        fitness.Delta.Should().Be(0m);

        var gambling = category.Purposes.Should().ContainSingle(p => p.Name == "Gluecksspiel").Subject;
        gambling.Budget.Should().Be(-15m);
        gambling.Actual.Should().Be(-15m);
        gambling.Delta.Should().Be(0m);

        var streaming = category.Purposes.Should().ContainSingle(p => p.Name == "Streaming").Subject;
        streaming.Budget.Should().Be(-10m);
        streaming.Actual.Should().Be(0m);
        streaming.Delta.Should().Be(10m);
    }

    private static Dictionary<string, List<Dictionary<string, object>>> ReadXlsxSheets(byte[] contentBytes)
    {
        using var ms = new System.IO.MemoryStream(contentBytes);
        using var archive = new ZipArchive(ms, ZipArchiveMode.Read, leaveOpen: false);
        var sharedStrings = ReadSharedStrings(archive);
        var relationshipTargets = ReadWorkbookRelationships(archive);
        var result = new Dictionary<string, List<Dictionary<string, object>>>(StringComparer.OrdinalIgnoreCase);

        var workbookEntry = archive.GetEntry("xl/workbook.xml");
        workbookEntry.Should().NotBeNull();
        using var workbookStream = workbookEntry!.Open();
        var workbook = XDocument.Load(workbookStream);

        foreach (var sheet in workbook.Descendants().Where(e => e.Name.LocalName == "sheet"))
        {
            var name = sheet.Attribute("name")?.Value;
            var relId = sheet.Attributes().FirstOrDefault(a => a.Name.LocalName == "id")?.Value;
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(relId) || !relationshipTargets.TryGetValue(relId, out var target))
            {
                continue;
            }

            var targetPath = target.StartsWith("/xl/", StringComparison.OrdinalIgnoreCase)
                ? target.TrimStart('/')
                : "xl/" + target.TrimStart('/');
            var sheetEntry = archive.GetEntry(targetPath);
            if (sheetEntry == null)
            {
                continue;
            }

            using var sheetStream = sheetEntry.Open();
            var sheetDoc = XDocument.Load(sheetStream);
            var rows = sheetDoc.Descendants().Where(e => e.Name.LocalName == "row").ToList();
            if (rows.Count == 0)
            {
                result[name] = new List<Dictionary<string, object>>();
                continue;
            }

            var headers = ReadRow(rows[0], sharedStrings).Select(v => v.ToString() ?? string.Empty).ToList();
            var dataRows = new List<Dictionary<string, object>>();
            foreach (var row in rows.Skip(1))
            {
                var values = ReadRow(row, sharedStrings);
                var dict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                for (var i = 0; i < headers.Count; i++)
                {
                    dict[headers[i]] = i < values.Count ? values[i] : string.Empty;
                }

                dataRows.Add(dict);
            }

            result[name] = dataRows;
        }

        return result;
    }

    private static List<string> ReadSharedStrings(ZipArchive archive)
    {
        var entry = archive.GetEntry("xl/sharedStrings.xml");
        if (entry == null)
        {
            return new List<string>();
        }

        using var stream = entry.Open();
        var doc = XDocument.Load(stream);
        return doc.Descendants()
            .Where(e => e.Name.LocalName == "si")
            .Select(e => string.Concat(e.Descendants().Where(t => t.Name.LocalName == "t").Select(t => t.Value)))
            .ToList();
    }

    private static Dictionary<string, string> ReadWorkbookRelationships(ZipArchive archive)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var entry = archive.GetEntry("xl/_rels/workbook.xml.rels");
        if (entry == null)
        {
            return result;
        }

        using var stream = entry.Open();
        var doc = XDocument.Load(stream);
        foreach (var rel in doc.Descendants().Where(e => e.Name.LocalName == "Relationship"))
        {
            var id = rel.Attributes().FirstOrDefault(a => a.Name.LocalName == "Id")?.Value;
            var target = rel.Attributes().FirstOrDefault(a => a.Name.LocalName == "Target")?.Value?.Replace("../", string.Empty).Replace("..\\", string.Empty).Replace('\\', '/');
            if (!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(target))
            {
                result[id] = target;
            }
        }

        return result;
    }

    private static List<object> ReadRow(XElement row, IReadOnlyList<string> sharedStrings)
    {
        var values = new List<object>();
        foreach (var cell in row.Elements().Where(e => e.Name.LocalName == "c"))
        {
            var type = cell.Attribute("t")?.Value;
            var raw = cell.Elements().FirstOrDefault(e => e.Name.LocalName == "v")?.Value ?? string.Empty;
            if (type == "s" && raw == "-1")
            {
                values.Add(string.Empty);
            }
            else if (type == "s" && int.TryParse(raw, out var sharedIndex) && sharedIndex >= 0 && sharedIndex < sharedStrings.Count)
            {
                values.Add(sharedStrings[sharedIndex]);
            }
            else if (decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var decimalValue))
            {
                values.Add(decimalValue);
            }
            else
            {
                values.Add(raw);
            }
        }

        return values;
    }

    private static string GetText(IReadOnlyDictionary<string, object> row, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (row.TryGetValue(key, out var value))
            {
                return value.ToString() ?? string.Empty;
            }
        }

        return string.Empty;
    }

    private static decimal GetDecimal(IReadOnlyDictionary<string, object> row, params string[] keys)
    {
        var value = GetValue(row, keys);
        value.Should().BeOfType<decimal>();
        return (decimal)value;
    }

    private static object GetValue(IReadOnlyDictionary<string, object> row, IReadOnlyList<string> keys)
    {
        foreach (var key in keys)
        {
            if (row.TryGetValue(key, out var value))
            {
                return value;
            }
        }

        throw new KeyNotFoundException($"None of the expected keys were present: {string.Join(", ", keys)}");
    }

    private static async Task AddEntryAsync(
        FinanceManager.Shared.ApiClient api,
        Guid draftId,
        DateOnly bookingDate,
        decimal amount,
        string subject,
        Guid contactId,
        string bookingDescription = "Lastschrift")
    {
        var draft = await api.StatementDrafts_AddEntryAsync(
            draftId,
            new StatementDraftAddEntryRequest(bookingDate.ToDateTime(TimeOnly.MinValue), amount, subject));
        draft.Should().NotBeNull();

        var entry = draft!.Entries.Single(e => e.Subject == subject && e.Amount == amount);
        var updatedCore = await api.StatementDrafts_UpdateEntryCoreAsync(
            draftId,
            entry.Id,
            new StatementDraftUpdateEntryCoreRequest(entry.BookingDate, entry.ValutaDate, entry.Amount, entry.Subject, entry.RecipientName, null, bookingDescription));
        updatedCore.Should().NotBeNull();

        var updated = await api.StatementDrafts_SetEntryContactAsync(draftId, entry.Id, new StatementDraftSetContactRequest(contactId));
        updated.Should().NotBeNull();
    }

    private sealed class TestCurrentUserService : ICurrentUserService
    {
        public Guid UserId { get; } = Guid.NewGuid();
        public string? PreferredLanguage { get; } = null;
        public bool IsAuthenticated { get; } = true;
        public bool IsAdmin { get; } = false;
    }
}
