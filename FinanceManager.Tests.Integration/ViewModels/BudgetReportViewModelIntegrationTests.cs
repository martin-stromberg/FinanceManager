using System;
using System.Linq;
using System.Threading.Tasks;
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
