using FinanceManager.Domain.Contacts;
using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Accounts;
using FinanceManager.Infrastructure.Aggregates;
using FinanceManager.Infrastructure.Budget;
using FinanceManager.Infrastructure.Contacts;
using FinanceManager.Infrastructure.Savings;
using FinanceManager.Infrastructure.Statements;
using FinanceManager.Infrastructure.Statements.Files;
using FinanceManager.Infrastructure.Statements.Parsers;
using FinanceManager.Shared.Dtos.Accounts;
using FinanceManager.Shared.Dtos.Budget;
using FinanceManager.Shared.Dtos.Contacts;
using FinanceManager.Shared.Dtos.Postings;
using FinanceManager.Shared.Dtos.SavingsPlans;
using FinanceManager.Web.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using System.Text.Json;
using FinanceManager.Infrastructure.Securities;
using FinanceManager.Application;

namespace FinanceManager.Tests.Budget;

/// <summary>
/// Contains unit tests for the BudgetReportService, verifying the generation of raw report data for a full month.
/// </summary>
/// <remarks>These tests cover a wide range of budget and posting scenarios, including grouped budget purposes,
/// budgets with both positive and negative expectations, fulfilled and unfulfilled budgets, overruns, and postings
/// without budgeted expectations. The tests ensure that the BudgetReportService correctly aggregates, categorizes, and
/// reports financial data based on the defined budgets and actual postings, including the handling of unbudgeted and
/// uncategorized transactions.</remarks>
public class BudgetReportServiceTests
{
    /// <summary>
    /// Verifies the raw report data for a full month using an in-memory setup.
    /// </summary>
    /// <remarks>
    /// This test covers the following data constellations. Each main item lists the specific budgets
    /// and postings involved:
    /// - Grouped budget purposes with shared rules and different fulfillment status
    ///     - Category 'Shopping & Food' with monthly budget of -500, applies to purposes 'Food' (contact group 'Shopping') and 'Bakeries' (contact group 'Bakeries')
    ///         - with postings for `Supermarket 6`, `Supermarket 7`, `Supermarket 8`, `Bakery 23`
    /// - Budget with negative and positive expectations, only one is fulfilled
    ///     - `Recurring Expense 3`: monthly -31.8 (unfulfilled), yearly +372.92 (fulfilled)
    ///     - `Recurring Expense 7`: monthly -8.25 (unfulfilled), yearly +99 (fulfilled)
    /// - Budget with negative and positive expectations, one is fulfilled, the other is unfulfilled but with a small residual
    ///     - `Recurring Expense 2`: monthly -13.01 and yearly +39.03 -> both fulfilled, but small residual 0.02 shown only in `UnbudgetedPostings`
    /// - Budget with negative and positive expectations, both are fullfilled
    ///     - `Recurring Expense 8`: monthly -3.82 and yearly +11.46 -> both fulfilled with postings -3.82 and +11.46 on 2026-01-02
    /// - Budget with one expectation, overrun and additinally unexpected postings
    ///     - `Lottery Company 1`: monthly -15 -> overrun with posting -25.50 on 2026-01-27 and two unexpected income postings (2x 5€)
    ///     - `Streaming Provider` (contact group): monthly -10 -> overrun with postings -4.99, -4.99 and -6.00 
    /// - Budget with one expectation fulfilled
    ///     - `savingsplan 4`: monthly -139 
    ///     - `savingsplan 5`: monthly -10 
    ///     - `Recurring Expense 4`: monthly -5.21 
    ///     - `Recurring Expense 5`: monthly -10.50 
    ///     - `Recurring Expense 6`: monthly -4.63 
    ///     - `Recurring Expense 10`: monthly -18.36 
    ///     - `Insurance 4`: monthly -20.64
    ///     - `Insurance 5`: monthly -20.93 
    ///     - `Insurance 7`: monthly -11.46 
    ///     - `Insurance 8`: monthly -381.6 
    ///     - `Auto Club` (yearly, applies in Jan): yearly -99 
    ///     - `Gym`: monthly expense -15 
    ///     - `Rent`: monthly expense -649.42 
    ///     - `Utilities`: monthly expense -75
    /// - Budget with one expectation overrun, but no matching posting
    ///     - `Salary`: monthly +3326.46 -> overrun with actual salary posting of 5767.89 on 2026-01-27 (remaining salary shown as unbudgeted posting)
    /// - Budget with one expectation, no postings at all
    /// 
    /// - Postings without budgeted expectations
    ///     - `Recurring Expense 9`: posting -20.50 with no budget -> appears in `UnbudgetedPostings` 
    ///     - `Dividend Stock 1`: posting 5.90 on 2026-01-15 with no budget -> appears in `UnbudgetedPostings`     
    ///     - `Dividend Stock 6`: posting 0.07 on 2026-01-02 with no budget -> appears in `UnbudgetedPostings`
    ///     - `Dividend Stock 7`: posting 17.66 on 2026-01-05 with no budget -> appears in `UnbudgetedPostings`
    ///     - `Dividend Stock 8`: posting 8.26 on 2026-01-09 with no budget -> appears in `UnbudgetedPostings`
    ///     - `Dividend Stock 9`: posting 13.47 on 2026-01-12 with no budget -> appears in `UnbudgetedPostings`
    ///     - `Dividend Stock 10`: posting 5.10 on 2026-01-19 with no budget -> appears in `UnbudgetedPostings`
    ///     - `Dividend Stock 11`: posting 17.61 on 2026-01-20 with no budget -> appears in `UnbudgetedPostings`
    ///     - `Service Contract 1`: one-time posting -8.00 with no budget -> appears in `UnbudgetedPostings`
    ///     - `Anlage 1`: one-time posting -3.81 with no budget -> appears in `UnbudgetedPostings`
    ///     - `Supermarket 6`: posting -20.09 with no budget -> appears in `UnbudgetedPostings`
    ///     - `Stock 3`: posting -500.00 on 2026-01-02 with no budget -> appears in `UnbudgetedPostings`
    ///     - `Stock 4`: posting -150.00 on 2026-01-02 with no budget -> appears in `UnbudgetedPostings`
    ///     - `Stock 5`: posting -25.00 on 2026-01-02 with no budget -> appears in `UnbudgetedPostings`
    ///     - `savingsplan 2`: one-time posting -200.00 with no budget -> appears in `UnbudgetedPostings`
    ///     - `savingsplan 3`: one-time posting 350.00 with no budget -> appears in `UnbudgetedPostings`
    ///     - `Bank 3`: posting 4.8 on 2026-01-09 with no budget -> appears in `UnbudgetedPostings`
    /// - Postings without budgeted expectations and valuta date in previous month
    ///     - `Dividend Stock 2`: posting 8.23 on 2026-01-02 with no budget an valuta date in previous month -> appears in `UnbudgetedPostings`

    /// </remarks>
    [Fact]
    public async Task Test_GetRawData_ForEntireMonthAsync()
    {
        var ownerUserId = Guid.NewGuid();
        var from = new DateOnly(2026, 1, 1);
        var to = new DateOnly(2026, 1, 31);
        var ct = CancellationToken.None;

        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddLogging();
        services.AddScoped<IStatementFileParser, ING_CSV_StatementFileParser>();
        services.AddScoped<IStatementFileParser, ING_PDF_StatementFileParser>();
        services.AddScoped<IStatementFileParser, Barclays_PDF_StatementFileParser>();
        services.AddScoped<IStatementFileParser, Wuestenrot_StatementFileParser>();
        services.AddScoped<IStatementFileParser, Backup_JSON_StatementFileParser>();
        services.AddScoped<IStatementFile, Barclays_PDF_StatementFile>();
        services.AddScoped<IStatementFile, ING_PDF_StatementFile>();
        services.AddScoped<IStatementFile, ING_Csv_StatementFile>();
        services.AddScoped<IStatementFile, Wuestenrot_PDF_StatementFile>();
        services.AddScoped<IStatementFile, Backup_JSON_StatementFile>();
        services.AddScoped<IStatementFileFactory>(sp => new StatementFileFactory(sp));
        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();
        var scopedProvider = scope.ServiceProvider;

        var db = scopedProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync(ct);

        var contactService = new ContactService(db);
        var contactCategoryService = new ContactCategoryService(db);
        var accountService = new AccountService(db);
        var savingsPlanService = new SavingsPlanService(db);
        var purposeService = new BudgetPurposeService(db);
        var categoryService = new BudgetCategoryService(db, purposeService);
        var ruleService = new BudgetRuleService(db);
        var postingsService = new PostingsQueryService(db);
        var securityService = new SecurityService(db);

        var statementDraftService = new StatementDraftService(
            db,
            new PostingAggregateService(db),
            accountService,
            scopedProvider.GetRequiredService<IStatementFileFactory>(),
            scopedProvider.GetServices<IStatementFileParser>(),
            NullLogger<StatementDraftService>.Instance,
            null);

        #region Self Contact
        var selfContact = await db.Contacts.AsNoTracking()
            .FirstOrDefaultAsync(c => c.OwnerUserId == ownerUserId && c.Type == ContactType.Self, ct);
        Guid selfContactId;
        if (selfContact == null)
        {
            var createdSelfContact = new Contact(ownerUserId, "Me", ContactType.Self, null);
            db.Contacts.Add(createdSelfContact);
            await db.SaveChangesAsync(ct);
            selfContactId = createdSelfContact.Id;
        }
        else
        {
            selfContactId = selfContact.Id;
        }
        #endregion

        #region Bank Accounts
        var bankContact = await contactService.CreateAsync(ownerUserId, "Bank 1", ContactType.Bank, null, null, null, ct);
        var bankAccount = await accountService.CreateAsync(ownerUserId, "Checking Account", AccountType.Giro, "DE00123456780000000000", bankContact.Id, SavingsPlanExpectation.Optional, true, ct);
        var bankContact3 = await contactService.CreateAsync(ownerUserId, "Bank 3", ContactType.Bank, null, null, null, ct);
        #endregion

        #region Create contacts
        var employerContact = await contactService.CreateAsync(ownerUserId, "Employer", ContactType.Organization, null, null, null, ct);
        var lotteryContact = await contactService.CreateAsync(ownerUserId, "Lottery Company 1", ContactType.Organization, null, null, null, ct);

        // create contact categories/groups
        var gasStationCategory = await contactCategoryService.CreateAsync(ownerUserId, "Gas Station", ct);
        var telecomCategory = await contactCategoryService.CreateAsync(ownerUserId, "Telecom Provider", ct);
        var streamingCategory = await contactCategoryService.CreateAsync(ownerUserId, "Streaming Provider", ct);
        var shoppingCategory = await contactCategoryService.CreateAsync(ownerUserId, "Shopping", ct);

        // create bakery contact group and bakeries
        var bakeryCategory = await contactCategoryService.CreateAsync(ownerUserId, "Bakeries", ct);
        var bakeries = new System.Collections.Generic.List<FinanceManager.Shared.Dtos.Contacts.ContactDto>(23);
        for (int i = 1; i <= 23; i++)
        {
            var b = await contactService.CreateAsync(ownerUserId, $"Bakery {i}", ContactType.Organization, bakeryCategory.Id, null, null, ct);
            bakeries.Add(b);
        }

        // additional contacts
        var landlordContact = await contactService.CreateAsync(ownerUserId, "Landlord", ContactType.Organization, null, null, null, ct);

        var canteenContact = await contactService.CreateAsync(ownerUserId, "Canteen 1", ContactType.Organization, null, null, null, ct);

        // supermarket contacts used for unbudgeted grocery postings
        var supermarkets = new System.Collections.Generic.List<FinanceManager.Shared.Dtos.Contacts.ContactDto>(8);
        for (int i = 1; i <= 8; i++)
        {
            var sm = await contactService.CreateAsync(ownerUserId, $"Supermarket {i}", ContactType.Organization, shoppingCategory.Id, null, null, ct);
            supermarkets.Add(sm);
        }
        var supermarket6 = supermarkets.First(p => p.Name == "Supermarket 6");
        var supermarket7 = supermarkets.First(p => p.Name == "Supermarket 7");
        var supermarket8 = supermarkets.First(p => p.Name == "Supermarket 8");

        var tank1 = await contactService.CreateAsync(ownerUserId, "Gas Station 1", ContactType.Organization, gasStationCategory.Id, null, null, ct);
        var tank2 = await contactService.CreateAsync(ownerUserId, "Gas Station 2", ContactType.Organization, gasStationCategory.Id, null, null, ct);
        var tank3 = await contactService.CreateAsync(ownerUserId, "Gas Station 3", ContactType.Organization, gasStationCategory.Id, null, null, ct);

        var autoClub = await contactService.CreateAsync(ownerUserId, "Auto Club", ContactType.Organization, null, null, null, ct);
        var fitness = await contactService.CreateAsync(ownerUserId, "Gym", ContactType.Organization, null, null, null, ct);
        var utilities = await contactService.CreateAsync(ownerUserId, "Utilities", ContactType.Organization, null, null, null, ct);

        // contact group for insurances and individual insurance contacts
        var insuranceCategory = await contactCategoryService.CreateAsync(ownerUserId, "Insurance", ct);
        var insuranceContact1 = await contactService.CreateAsync(ownerUserId, "Insurance 1", ContactType.Organization, insuranceCategory.Id, null, null, ct);
        var insuranceContact2 = await contactService.CreateAsync(ownerUserId, "Insurance 2", ContactType.Organization, insuranceCategory.Id, null, null, ct);
        var insuranceContact3 = await contactService.CreateAsync(ownerUserId, "Insurance 3", ContactType.Organization, insuranceCategory.Id, null, null, ct);
        var insuranceContact4 = await contactService.CreateAsync(ownerUserId, "Insurance 4", ContactType.Organization, insuranceCategory.Id, null, null, ct);
        var insuranceContact5 = await contactService.CreateAsync(ownerUserId, "Insurance 5", ContactType.Organization, insuranceCategory.Id, null, null, ct);
        var insuranceContact6 = await contactService.CreateAsync(ownerUserId, "Insurance 6", ContactType.Organization, insuranceCategory.Id, null, null, ct);
        var insuranceContact7 = await contactService.CreateAsync(ownerUserId, "Insurance 7", ContactType.Organization, insuranceCategory.Id, null, null, ct);
        var insuranceContact8 = await contactService.CreateAsync(ownerUserId, "Insurance 8", ContactType.Organization, insuranceCategory.Id, null, null, ct);
        var insuranceContact9 = await contactService.CreateAsync(ownerUserId, "Insurance 9", ContactType.Organization, insuranceCategory.Id, null, null, ct);
        var insuranceContact10 = await contactService.CreateAsync(ownerUserId, "Insurance 10", ContactType.Organization, insuranceCategory.Id, null, null, ct);
        var insuranceContact11 = await contactService.CreateAsync(ownerUserId, "Insurance 11", ContactType.Organization, insuranceCategory.Id, null, null, ct);
        var insuranceContact12 = await contactService.CreateAsync(ownerUserId, "Insurance 12", ContactType.Organization, insuranceCategory.Id, null, null, ct);

        var telecom1 = await contactService.CreateAsync(ownerUserId, "Telecom Provider 1", ContactType.Organization, telecomCategory.Id, null, null, ct);
        var telecom2 = await contactService.CreateAsync(ownerUserId, "Telecom Provider 2", ContactType.Organization, telecomCategory.Id, null, null, ct);
        var telecom3 = await contactService.CreateAsync(ownerUserId, "Telecom Provider 3", ContactType.Organization, telecomCategory.Id, null, null, ct);

        // Telecom Provider 1: monthly expense -54.13
        var telecom1Purpose = await purposeService.CreateAsync(ownerUserId, "Telecom Provider 1", BudgetSourceType.Contact, telecom1.Id, null, null, ct);
        _ = await ruleService.CreateAsync(ownerUserId, telecom1Purpose.Id, -54.13m, BudgetIntervalType.Monthly, null, new DateOnly(2026, 1, 1), null, ct);

        var stream1 = await contactService.CreateAsync(ownerUserId, "Streaming Provider 1", ContactType.Organization, streamingCategory.Id, null, null, ct);
        var stream2 = await contactService.CreateAsync(ownerUserId, "Streaming Provider 2", ContactType.Organization, streamingCategory.Id, null, null, ct);
        var stream3 = await contactService.CreateAsync(ownerUserId, "Streaming Provider 3", ContactType.Organization, streamingCategory.Id, null, null, ct);
        var stream4 = await contactService.CreateAsync(ownerUserId, "Streaming Provider 4", ContactType.Organization, streamingCategory.Id, null, null, ct);
        var stream5 = await contactService.CreateAsync(ownerUserId, "Streaming Provider 5", ContactType.Organization, streamingCategory.Id, null, null, ct);

        // additional service provider used for an unbudgeted outflow on 09.01
        var serviceProvider10 = await contactService.CreateAsync(ownerUserId, "Service Provider 10", ContactType.Organization, null, null, null, ct);

        // Streaming Provider: monthly expense -10
        var streamingGroupPurpose = await purposeService.CreateAsync(ownerUserId, "Streaming Provider", BudgetSourceType.ContactGroup, streamingCategory.Id, null, null, ct);
        _ = await ruleService.CreateAsync(ownerUserId, streamingGroupPurpose.Id, -10m, BudgetIntervalType.Monthly, null, new DateOnly(2026, 1, 1), null, ct);
        #endregion

        #region Sparpläne anlegen
        var savingsPlan = await savingsPlanService.CreateAsync(
            ownerUserId,
            "Investment 1",
            SavingsPlanType.OneTime,
            10000m,
            new DateTime(2040, 12, 31),
            null,
            null,
            null,
            ct);
        var serviceContractPlan = await savingsPlanService.CreateAsync(
            ownerUserId,
            "Service Contract 1",
            SavingsPlanType.OneTime,
            null,
            null,
            null,
            null,
            null,
            ct);
        // create additional one-time Investment 2
        var savingsPlan2 = await savingsPlanService.CreateAsync(
            ownerUserId,
            "Investment 2",
            SavingsPlanType.OneTime,
            5000m,
            new DateTime(2030, 12, 31),
            null,
            null,
            null,
            ct);

        // create several small savingsplans savingsplan 1..5 (recurring monthly)
        var smallPlans = new System.Collections.Generic.List<FinanceManager.Shared.Dtos.SavingsPlans.SavingsPlanDto>(5);
        for (int i = 1; i <= 5; i++)
        {
            var sp = await savingsPlanService.CreateAsync(
                ownerUserId,
                $"Savings Plan {i}",
                SavingsPlanType.Recurring,
                null,
                null,
                SavingsPlanInterval.Monthly,
                null,
                null,
                ct);
            smallPlans.Add(sp);
        }

        // create a budget purpose for `savingsplan 5` (monthly -10)
        var savingsplan5Purpose = await purposeService.CreateAsync(ownerUserId, "Savings Plan 5", BudgetSourceType.SavingsPlan, smallPlans.First(p => p.Name == "Savings Plan 5").Id, null, null, ct);
        _ = await ruleService.CreateAsync(ownerUserId, savingsplan5Purpose.Id, -10m, BudgetIntervalType.Monthly, null, new DateOnly(2026, 1, 1), null, ct);

        // create a budget purpose for `savingsplan 4` (monthly -139)
        var savingsplan4Purpose = await purposeService.CreateAsync(ownerUserId, "Savings Plan 4", BudgetSourceType.SavingsPlan, smallPlans.First(p => p.Name == "Savings Plan 4").Id, null, null, ct);
        _ = await ruleService.CreateAsync(ownerUserId, savingsplan4Purpose.Id, -139m, BudgetIntervalType.Monthly, null, new DateOnly(2026, 1, 1), null, ct);

        // create recurring expense savingsplans Recurring Expense 1..15
        var recurringPlans = new System.Collections.Generic.List<FinanceManager.Shared.Dtos.SavingsPlans.SavingsPlanDto>(15);
        for (int i = 1; i <= 15; i++)
        {
            var plan = await savingsPlanService.CreateAsync(
                ownerUserId,
                $"Recurring Expense {i}",
                SavingsPlanType.Recurring,
                null,
                null,
                SavingsPlanInterval.Monthly,
                null,
                null,
                ct);
            recurringPlans.Add(plan);
        }

        // create insurance savingsplans "Insurance 1" .. "Insurance 8"
        var insurancePlans = new System.Collections.Generic.List<FinanceManager.Shared.Dtos.SavingsPlans.SavingsPlanDto>(8);
        for (int i = 1; i <= 8; i++)
        {
            var plan = await savingsPlanService.CreateAsync(
                ownerUserId,
                $"Insurance {i}",
                SavingsPlanType.Recurring,
                null,
                null,
                SavingsPlanInterval.Monthly,
                null,
                null,
                ct);
            insurancePlans.Add(plan);
        }
        #endregion

        #region Budgetplanung anlegen
        var workCategory = await categoryService.CreateAsync(ownerUserId, "Work", ct);
        var salaryPurpose = await purposeService.CreateAsync(
            ownerUserId,
            "Salary",
            BudgetSourceType.Contact,
            employerContact.Id,
            null,
            workCategory.Id,
            ct);
        _ = await ruleService.CreateAsync(
            ownerUserId,
            salaryPurpose.Id,
            3326.46m,
            BudgetIntervalType.Monthly,
            null,
            new DateOnly(2026, 1, 1),
            null,
            ct);

        var lotteryCategory = await categoryService.CreateAsync(ownerUserId, "Lottery Company", ct);
        var lotteryPurpose = await purposeService.CreateAsync(
            ownerUserId,
            "Lottery",
            BudgetSourceType.Contact,
            lotteryContact.Id,
            null,
            lotteryCategory.Id,
            ct);
        _ = await ruleService.CreateForCategoryAsync(
            ownerUserId,
            lotteryCategory.Id,
            -15m,
            BudgetIntervalType.Monthly,
            null,
            new DateOnly(2026, 1, 1),
            null,
            ct);

        // Apartment rent as budgeted purpose (contact-based)
        var rentPurpose = await purposeService.CreateAsync(ownerUserId, "Rent", BudgetSourceType.Contact, landlordContact.Id, null, null, ct);
        _ = await ruleService.CreateAsync(ownerUserId, rentPurpose.Id, -649.42m, BudgetIntervalType.Monthly, null, new DateOnly(2026, 1, 1), null, ct);

        // Fuel as budgeted purpose for contact group Gas Station (monthly -50)
        var tankPurpose = await purposeService.CreateAsync(ownerUserId, "Fuel", BudgetSourceType.ContactGroup, gasStationCategory.Id, null, null, ct);
        _ = await ruleService.CreateAsync(ownerUserId, tankPurpose.Id, -50m, BudgetIntervalType.Monthly, null, new DateOnly(2026, 1, 1), null, ct);

        // New budget category for shopping & food with a monthly category-level budget of -500
        var shoppingBudgetCategory = await categoryService.CreateAsync(ownerUserId, "Shopping & Food", ct);
        _ = await ruleService.CreateForCategoryAsync(ownerUserId, shoppingBudgetCategory.Id, -500m, BudgetIntervalType.Monthly, null, new DateOnly(2026, 1, 1), null, ct);

        // Purpose: Food -> applies to contact category "Shopping"
        var verpflegungPurpose = await purposeService.CreateAsync(ownerUserId, "Food", BudgetSourceType.ContactGroup, shoppingCategory.Id, null, shoppingBudgetCategory.Id, ct);

        // Purpose: Bakeries -> applies to contact category/group "Bakeries"
        var baeckereienPurpose = await purposeService.CreateAsync(ownerUserId, "Bakeries", BudgetSourceType.ContactGroup, bakeryCategory.Id, null, shoppingBudgetCategory.Id, ct);

        // Auto Club: yearly expense in January (-99)
        var autoClubPurpose = await purposeService.CreateAsync(ownerUserId, "Auto Club", BudgetSourceType.Contact, autoClub.Id, null, null, ct);
        _ = await ruleService.CreateAsync(ownerUserId, autoClubPurpose.Id, -99m, BudgetIntervalType.Yearly, null, new DateOnly(2026, 1, 1), null, ct);

        // Gym: monthly expense (-15)
        var fitnessPurpose = await purposeService.CreateAsync(ownerUserId, "Gym", BudgetSourceType.Contact, fitness.Id, null, null, ct);
        _ = await ruleService.CreateAsync(ownerUserId, fitnessPurpose.Id, -15m, BudgetIntervalType.Monthly, null, new DateOnly(2026, 1, 1), null, ct);

        // Utilities: monatliche Ausgabe (-75)
        var utilitiesPurpose = await purposeService.CreateAsync(ownerUserId, "Utilities", BudgetSourceType.Contact, utilities.Id, null, null, ct);
        _ = await ruleService.CreateAsync(ownerUserId, utilitiesPurpose.Id, -75m, BudgetIntervalType.Monthly, null, new DateOnly(2026, 1, 1), null, ct);

        // Create budget purposes (no category) for the recurring savingsplans and attach purpose-scoped rules
        // Map savingsplan names to the created plans
        BudgetPurposeDto purposeFor(int number)
        {
            var name = $"Recurring Expense {number}";
            var plan = recurringPlans.FirstOrDefault(p => p.Name == name);
            if (plan == null)
            {
                // fallback: create a plan on the fly
                throw new InvalidOperationException($"Expected savingsplan '{name}' to exist");
            }
            return null!; // placeholder - will be replaced below with async calls
        }

        // create purposes and rules
        var purpose10 = await purposeService.CreateAsync(ownerUserId, "Recurring Expense 10", BudgetSourceType.SavingsPlan, recurringPlans.First(p => p.Name == "Recurring Expense 10").Id, null, null, ct);
        _ = await ruleService.CreateAsync(ownerUserId, purpose10.Id, -18.36m, BudgetIntervalType.Monthly, null, new DateOnly(2026, 1, 1), null, ct);

        var purpose4 = await purposeService.CreateAsync(ownerUserId, "Recurring Expense 4", BudgetSourceType.SavingsPlan, recurringPlans.First(p => p.Name == "Recurring Expense 4").Id, null, null, ct);
        _ = await ruleService.CreateAsync(ownerUserId, purpose4.Id, -5.21m, BudgetIntervalType.Monthly, null, new DateOnly(2026, 1, 1), null, ct);

        var purpose7 = await purposeService.CreateAsync(ownerUserId, "Recurring Expense 7", BudgetSourceType.SavingsPlan, recurringPlans.First(p => p.Name == "Recurring Expense 7").Id, null, null, ct);
        _ = await ruleService.CreateAsync(ownerUserId, purpose7.Id, -8.25m, BudgetIntervalType.Monthly, null, new DateOnly(2026, 1, 1), null, ct);
        _ = await ruleService.CreateAsync(ownerUserId, purpose7.Id, 99m, BudgetIntervalType.Yearly, null, new DateOnly(2026, 1, 1), null, ct);

        var purpose5 = await purposeService.CreateAsync(ownerUserId, "Recurring Expense 5", BudgetSourceType.SavingsPlan, recurringPlans.First(p => p.Name == "Recurring Expense 5").Id, null, null, ct);
        _ = await ruleService.CreateAsync(ownerUserId, purpose5.Id, -10.50m, BudgetIntervalType.Monthly, null, new DateOnly(2026, 1, 1), null, ct);

        var purpose6 = await purposeService.CreateAsync(ownerUserId, "Recurring Expense 6", BudgetSourceType.SavingsPlan, recurringPlans.First(p => p.Name == "Recurring Expense 6").Id, null, null, ct);
        _ = await ruleService.CreateAsync(ownerUserId, purpose6.Id, -4.63m, BudgetIntervalType.Monthly, null, new DateOnly(2026, 1, 1), null, ct);

        // ensure plan11 and plan15 exist (created earlier) and create purposes
        var purpose11 = await purposeService.CreateAsync(ownerUserId, "Recurring Expense 11", BudgetSourceType.SavingsPlan, recurringPlans.First(p => p.Name == "Recurring Expense 11").Id, null, null, ct);
        _ = await ruleService.CreateAsync(ownerUserId, purpose11.Id, -60m, BudgetIntervalType.Monthly, null, new DateOnly(2026, 1, 1), null, ct);

        var purpose15 = await purposeService.CreateAsync(ownerUserId, "Recurring Expense 15", BudgetSourceType.SavingsPlan, recurringPlans.First(p => p.Name == "Recurring Expense 15").Id, null, null, ct);
        _ = await ruleService.CreateAsync(ownerUserId, purpose15.Id, -5m, BudgetIntervalType.Monthly, null, new DateOnly(2026, 1, 1), null, ct);

        var purpose8 = await purposeService.CreateAsync(ownerUserId, "Recurring Expense 8", BudgetSourceType.SavingsPlan, recurringPlans.First(p => p.Name == "Recurring Expense 8").Id, null, null, ct);
        _ = await ruleService.CreateAsync(ownerUserId, purpose8.Id, -3.82m, BudgetIntervalType.Monthly, null, new DateOnly(2026, 1, 1), null, ct);
        _ = await ruleService.CreateAsync(ownerUserId, purpose8.Id, 11.46m, BudgetIntervalType.Quarterly, null, new DateOnly(2026, 1, 1), null, ct);

        // create purpose for "Recurring Expense 3" with mixed rules
        var purpose3 = await purposeService.CreateAsync(ownerUserId, "Recurring Expense 3", BudgetSourceType.SavingsPlan, recurringPlans.First(p => p.Name == "Recurring Expense 3").Id, null, null, ct);
        // yearly credit 372.92 starting Jan 2026
        _ = await ruleService.CreateAsync(ownerUserId, purpose3.Id, 372.92m, BudgetIntervalType.Yearly, null, new DateOnly(2026, 1, 1), null, ct);
        // yearly credit 381.6 starting Jan 2027
        _ = await ruleService.CreateAsync(ownerUserId, purpose3.Id, 381.6m, BudgetIntervalType.Yearly, null, new DateOnly(2027, 1, 1), null, ct);
        // monthly expense -31.8 starting Jan 2026
        _ = await ruleService.CreateAsync(ownerUserId, purpose3.Id, -31.8m, BudgetIntervalType.Monthly, null, new DateOnly(2026, 1, 1), null, ct);

        // Insurance 6: quartalsweise -39,03 ab Januar (contact-based budget)
        var insurance6Purpose = await purposeService.CreateAsync(ownerUserId, "Insurance 6", BudgetSourceType.Contact, insuranceContact6.Id, null, null, ct);
        _ = await ruleService.CreateAsync(ownerUserId, insurance6Purpose.Id, -39.03m, BudgetIntervalType.Quarterly, null, new DateOnly(2026, 1, 1), null, ct);

        // Insurance 8: jährliche Ausgabe -381,6 im Januar
        var insurance8Purpose = await purposeService.CreateAsync(ownerUserId, "Insurance 8", BudgetSourceType.Contact, insuranceContact8.Id, null, null, ct);
        _ = await ruleService.CreateAsync(ownerUserId, insurance8Purpose.Id, -381.60m, BudgetIntervalType.Yearly, null, new DateOnly(2026, 1, 1), null, ct);

        // Insurance 5: monatliche Ausgabe -20,93
        var insurance5Purpose = await purposeService.CreateAsync(ownerUserId, "Insurance 5", BudgetSourceType.Contact, insuranceContact5.Id, null, null, ct);
        _ = await ruleService.CreateAsync(ownerUserId, insurance5Purpose.Id, -20.93m, BudgetIntervalType.Monthly, null, new DateOnly(2026, 1, 1), null, ct);

        // Insurance 7: monatliche Ausgabe -11,46
        var insurance7Purpose = await purposeService.CreateAsync(ownerUserId, "Insurance 7", BudgetSourceType.Contact, insuranceContact7.Id, null, null, ct);
        _ = await ruleService.CreateAsync(ownerUserId, insurance7Purpose.Id, -11.46m, BudgetIntervalType.Monthly, null, new DateOnly(2026, 1, 1), null, ct);

        // Insurance 4: monatliche Ausgabe -20,64
        var insurance4Purpose = await purposeService.CreateAsync(ownerUserId, "Insurance 4", BudgetSourceType.Contact, insuranceContact4.Id, null, null, ct);
        _ = await ruleService.CreateAsync(ownerUserId, insurance4Purpose.Id, -20.64m, BudgetIntervalType.Monthly, null, new DateOnly(2026, 1, 1), null, ct);

        var purpose2 = await purposeService.CreateAsync(ownerUserId, "Recurring Expense 2", BudgetSourceType.SavingsPlan, recurringPlans.First(p => p.Name == "Recurring Expense 2").Id, null, null, ct);
        _ = await ruleService.CreateAsync(ownerUserId, purpose2.Id, 39.01m, BudgetIntervalType.Quarterly, null, new DateOnly(2026, 1, 1), null, ct);
        _ = await ruleService.CreateAsync(ownerUserId, purpose2.Id, -13.01m, BudgetIntervalType.Monthly, null, new DateOnly(2026, 1, 1), null, ct);
        #endregion

        #region Security anlegen
        // create security category and security
        var stockCategory = await new FinanceManager.Infrastructure.Securities.SecurityCategoryService(db).CreateAsync(ownerUserId, "Stocks", ct);
        var security = await new FinanceManager.Infrastructure.Securities.SecurityService(db).CreateAsync(ownerUserId, "Stock 1", "ISIN0001", null, null, "EUR", stockCategory.Id, ct);
        var security2 = await new FinanceManager.Infrastructure.Securities.SecurityService(db).CreateAsync(ownerUserId, "Stock 2", "ISIN0002", null, null, "EUR", stockCategory.Id, ct);
        var security3 = await new FinanceManager.Infrastructure.Securities.SecurityService(db).CreateAsync(ownerUserId, "Stock 3", "ISIN0003", null, null, "EUR", stockCategory.Id, ct);
        var security4 = await new FinanceManager.Infrastructure.Securities.SecurityService(db).CreateAsync(ownerUserId, "Stock 4", "ISIN0004", null, null, "EUR", stockCategory.Id, ct);
        var security5 = await new FinanceManager.Infrastructure.Securities.SecurityService(db).CreateAsync(ownerUserId, "Stock 5", "ISIN0005", null, null, "EUR", stockCategory.Id, ct);
        var security6 = await new FinanceManager.Infrastructure.Securities.SecurityService(db).CreateAsync(ownerUserId, "Stock 6", "ISIN0006", null, null, "EUR", stockCategory.Id, ct);
        var security7 = await new FinanceManager.Infrastructure.Securities.SecurityService(db).CreateAsync(ownerUserId, "Stock 7", "ISIN0007", null, null, "EUR", stockCategory.Id, ct);
        var security8 = await new FinanceManager.Infrastructure.Securities.SecurityService(db).CreateAsync(ownerUserId, "Stock 8", "ISIN0008", null, null, "EUR", stockCategory.Id, ct);
        var security9 = await new FinanceManager.Infrastructure.Securities.SecurityService(db).CreateAsync(ownerUserId, "Stock 9", "ISIN0009", null, null, "EUR", stockCategory.Id, ct);
        var security10 = await new FinanceManager.Infrastructure.Securities.SecurityService(db).CreateAsync(ownerUserId, "Stock 10", "ISIN0010", null, null, "EUR", stockCategory.Id, ct);
        var security11 = await new FinanceManager.Infrastructure.Securities.SecurityService(db).CreateAsync(ownerUserId, "Stock 11", "ISIN0011", null, null, "EUR", stockCategory.Id, ct);
        #endregion

        #region Kontoauszug anlegen
        var draft = await statementDraftService.CreateEmptyDraftAsync(ownerUserId, "Statement.csv", ct);
        await statementDraftService.SetAccountAsync(draft.DraftId, ownerUserId, bankAccount.Id, ct);


        var draftWithEntry = await statementDraftService.AddEntryAsync(
            draft.DraftId,
            ownerUserId,
            new DateTime(2026, 1, 27),
            -3.81m,
            "Investment 1",
            ct);

        var entryId = draftWithEntry!.Entries[0].Id;
        await statementDraftService.SetEntryContactAsync(draft.DraftId, entryId, selfContactId, ownerUserId, ct);
        await statementDraftService.AssignSavingsPlanAsync(draft.DraftId, entryId, savingsPlan.Id, ownerUserId, ct);

        var draftWithSecondEntry = await statementDraftService.AddEntryAsync(
            draft.DraftId,
            ownerUserId,
            new DateTime(2026, 1, 27),
            -8m,
            "Service Contract 1",
            ct);

        var secondEntryId = draftWithSecondEntry!.Entries[^1].Id;
        await statementDraftService.SetEntryContactAsync(draft.DraftId, secondEntryId, selfContactId, ownerUserId, ct);
        await statementDraftService.AssignSavingsPlanAsync(draft.DraftId, secondEntryId, serviceContractPlan.Id, ownerUserId, ct);

        var draftWithSalaryEntry = await statementDraftService.AddEntryAsync(
            draft.DraftId,
            ownerUserId,
            new DateTime(2026, 1, 27),
            5767.89m,
            "Salary",
            ct);

        var salaryEntryId = draftWithSalaryEntry!.Entries[^1].Id;
        await statementDraftService.SetEntryContactAsync(draft.DraftId, salaryEntryId, employerContact.Id, ownerUserId, ct);

        var draftWithLotteryEntry = await statementDraftService.AddEntryAsync(
            draft.DraftId,
            ownerUserId,
            new DateTime(2026, 1, 20),
            -25.5m,
            "Lottery",
            ct);

        var lotteryEntryId = draftWithLotteryEntry!.Entries[^1].Id;
        await statementDraftService.SetEntryContactAsync(draft.DraftId, lotteryEntryId, lotteryContact.Id, ownerUserId, ct);

        // additional lottery income on 07.01.2026
        var draftWithLotteryEntryOn7 = await statementDraftService.AddEntryAsync(
            draft.DraftId,
            ownerUserId,
            new DateTime(2026, 1, 7),
            5.00m,
            "Lottery",
            ct);

        var lotteryEntryOn7Id = draftWithLotteryEntryOn7!.Entries[^1].Id;
        await statementDraftService.SetEntryContactAsync(draft.DraftId, lotteryEntryOn7Id, lotteryContact.Id, ownerUserId, ct);

        var draftWithLotteryIncomeEntry = await statementDraftService.AddEntryAsync(
            draft.DraftId,
            ownerUserId,
            new DateTime(2026, 1, 20),
            5.00m,
            "Lottery",
            ct);

        var lotteryIncomeEntryId = draftWithLotteryIncomeEntry!.Entries[^1].Id;
        await statementDraftService.SetEntryContactAsync(draft.DraftId, lotteryIncomeEntryId, lotteryContact.Id, ownerUserId, ct);

        var draftWithDividendntry = await statementDraftService.AddEntryAsync(
            draft.DraftId,
            ownerUserId,
            new DateTime(2026, 1, 20),
            5.90m,
            "Dividend Stock 1",
            ct);

        var DividendntryId = draftWithDividendntry!.Entries[^1].Id;
        await statementDraftService.SetEntryContactAsync(draft.DraftId, DividendntryId, bankContact.Id, ownerUserId, ct);

        // Assign security to dividend entry with transaction type Dividend and valuta date 15.01.2026 via SaveEntryAll
        // assign security and contact correctly (implementation expects contactId as 4th param)
        await statementDraftService.SaveEntryAllAsync(
            draft.DraftId,
            DividendntryId,
            ownerUserId,
            bankContact.Id, // contactId
            null, // isCostNeutral
            null, // savingsPlanId
            null, // archiveOnBooking
            security.Id, // securityId
            FinanceManager.Shared.Dtos.Securities.SecurityTransactionType.Dividend,
            null,
            null,
            null,
            ct);

        // set valuta date for dividend entry
        await statementDraftService.UpdateEntryCoreAsync(draft.DraftId, DividendntryId, ownerUserId, new DateTime(2026, 1, 20), new DateTime(2026, 1, 15), 5.90m, "Dividend Stock 1", null, null, null, ct);

        // additional: Dividend with booking 02.01.2026 and valuta 31.12.2025 (+8.23)
        var draftWithDividendntry2 = await statementDraftService.AddEntryAsync(
            draft.DraftId,
            ownerUserId,
            new DateTime(2026, 1, 2),
            8.23m,
            "Dividend Stock 2",
            ct);

        var Dividendntry2Id = draftWithDividendntry2!.Entries[^1].Id;
        await statementDraftService.SetEntryContactAsync(draft.DraftId, Dividendntry2Id, bankContact.Id, ownerUserId, ct);

        await statementDraftService.SaveEntryAllAsync(
            draft.DraftId,
            Dividendntry2Id,
            ownerUserId,
            bankContact.Id,
            null,
            null,
            null,
            security2.Id,
            FinanceManager.Shared.Dtos.Securities.SecurityTransactionType.Dividend,
            null,
            null,
            null,
            ct);

        // set valuta date to 31.12.2025
        await statementDraftService.UpdateEntryCoreAsync(draft.DraftId, Dividendntry2Id, ownerUserId, new DateTime(2026, 1, 2), new DateTime(2025, 12, 31), 8.23m, "Dividend Stock 2", null, null, null, ct);

        // additional: savingsplan 5 contribution on 02.01.2026
        var draftWithsavingsplan5Entry = await statementDraftService.AddEntryAsync(
            draft.DraftId,
            ownerUserId,
            new DateTime(2026, 1, 2),
            -10.00m,
            "Savings Plan 5",
            ct);

        var savingsplan5EntryId = draftWithsavingsplan5Entry!.Entries[^1].Id;
        await statementDraftService.SetEntryContactAsync(draft.DraftId, savingsplan5EntryId, selfContactId, ownerUserId, ct);
        await statementDraftService.AssignSavingsPlanAsync(draft.DraftId, savingsplan5EntryId, smallPlans.First(p => p.Name == "Savings Plan 5").Id, ownerUserId, ct);

        // additional: savingsplan 4 contribution on 02.01.2026
        var draftWithsavingsplan4Entry = await statementDraftService.AddEntryAsync(
            draft.DraftId,
            ownerUserId,
            new DateTime(2026, 1, 2),
            -139.00m,
            "Savings Plan 4",
            ct);

        var savingsplan4EntryId = draftWithsavingsplan4Entry!.Entries[^1].Id;
        await statementDraftService.SetEntryContactAsync(draft.DraftId, savingsplan4EntryId, selfContactId, ownerUserId, ct);
        await statementDraftService.AssignSavingsPlanAsync(draft.DraftId, savingsplan4EntryId, smallPlans.First(p => p.Name == "Savings Plan 4").Id, ownerUserId, ct);

        // additional: savingsplan 3 income on 02.01.2026 (+350)
        var draftWithsavingsplan3Entry = await statementDraftService.AddEntryAsync(
            draft.DraftId,
            ownerUserId,
            new DateTime(2026, 1, 2),
            350.00m,
            "Savings Plan 3",
            ct);
        var savingsplan3EntryId = draftWithsavingsplan3Entry!.Entries[^1].Id;
        await statementDraftService.SetEntryContactAsync(draft.DraftId, savingsplan3EntryId, selfContactId, ownerUserId, ct);
        await statementDraftService.AssignSavingsPlanAsync(draft.DraftId, savingsplan3EntryId, smallPlans.First(p => p.Name == "Savings Plan 3").Id, ownerUserId, ct);

        // additional: savingsplan 2 contribution on 05.01.2026 (one-time -200)
        var draftWithsavingsplan2Entry = await statementDraftService.AddEntryAsync(
            draft.DraftId,
            ownerUserId,
            new DateTime(2026, 1, 5),
            -200.00m,
            "Savings Plan 2",
            ct);
        var savingsplan2EntryId = draftWithsavingsplan2Entry!.Entries[^1].Id;
        await statementDraftService.SetEntryContactAsync(draft.DraftId, savingsplan2EntryId, selfContactId, ownerUserId, ct);
        await statementDraftService.AssignSavingsPlanAsync(draft.DraftId, savingsplan2EntryId, smallPlans.First(p => p.Name == "Savings Plan 2").Id, ownerUserId, ct);

        // additional: Insurance 8 payment on 02.01.2026 (yearly, applies in Jan)
        var draftWithInsurance8Entry = await statementDraftService.AddEntryAsync(
            draft.DraftId,
            ownerUserId,
            new DateTime(2026, 1, 2),
            -381.60m,
            "Insurance 8",
            ct);

        var insurance8EntryId = draftWithInsurance8Entry!.Entries[^1].Id;
        await statementDraftService.SetEntryContactAsync(draft.DraftId, insurance8EntryId, insuranceContact8.Id, ownerUserId, ct);

        // additional: recurring savingsplan 3 contribution on 02.01.2026 (income)
        var draftWithRecurring3Entry = await statementDraftService.AddEntryAsync(
            draft.DraftId,
            ownerUserId,
            new DateTime(2026, 1, 2),
            372.92m,
            "Recurring Expense 3",
            ct);

        var recurring3EntryId = draftWithRecurring3Entry!.Entries[^1].Id;
        await statementDraftService.SetEntryContactAsync(draft.DraftId, recurring3EntryId, selfContactId, ownerUserId, ct);
        await statementDraftService.AssignSavingsPlanAsync(draft.DraftId, recurring3EntryId, recurringPlans.First(p => p.Name == "Recurring Expense 3").Id, ownerUserId, ct);

        // additional savingsplan postings on 02.01.2026
        var draftWithW2Expense = await statementDraftService.AddEntryAsync(
            draft.DraftId,
            ownerUserId,
            new DateTime(2026, 1, 2),
            -13.01m,
            "Recurring Expense 2",
            ct);
        var w2ExpenseId = draftWithW2Expense!.Entries[^1].Id;
        await statementDraftService.SetEntryContactAsync(draft.DraftId, w2ExpenseId, selfContactId, ownerUserId, ct);
        await statementDraftService.AssignSavingsPlanAsync(draft.DraftId, w2ExpenseId, recurringPlans.First(p => p.Name == "Recurring Expense 2").Id, ownerUserId, ct);

        var draftWithW9Income = await statementDraftService.AddEntryAsync(
            draft.DraftId,
            ownerUserId,
            new DateTime(2026, 1, 2),
            10.00m,
            "Recurring Expense 9",
            ct);
        var w9IncomeId = draftWithW9Income!.Entries[^1].Id;
        await statementDraftService.SetEntryContactAsync(draft.DraftId, w9IncomeId, selfContactId, ownerUserId, ct);
        await statementDraftService.AssignSavingsPlanAsync(draft.DraftId, w9IncomeId, recurringPlans.First(p => p.Name == "Recurring Expense 9").Id, ownerUserId, ct);

        // additional: Auto Club booking on 02.01.2026 (budgeted yearly -99)
        var draftWithAutoClubEntry = await statementDraftService.AddEntryAsync(
            draft.DraftId,
            ownerUserId,
            new DateTime(2026, 1, 2),
            -99.00m,
            "Auto Club",
            ct);
        var autoClubEntryId = draftWithAutoClubEntry!.Entries[^1].Id;
        await statementDraftService.SetEntryContactAsync(draft.DraftId, autoClubEntryId, autoClub.Id, ownerUserId, ct);

        // additional: Gym booking on 02.01.2026 (monthly -15)
        var draftWithFitnessEntry = await statementDraftService.AddEntryAsync(
            draft.DraftId,
            ownerUserId,
            new DateTime(2026, 1, 2),
            -15.00m,
            "Gym",
            ct);
        var fitnessEntryId = draftWithFitnessEntry!.Entries[^1].Id;
        await statementDraftService.SetEntryContactAsync(draft.DraftId, fitnessEntryId, fitness.Id, ownerUserId, ct);

        // additional: Rent booking on 02.01.2026 (monthly -649.42)
        var draftWithRentEntry = await statementDraftService.AddEntryAsync(
            draft.DraftId,
            ownerUserId,
            new DateTime(2026, 1, 2),
            -649.42m,
            "Rent",
            ct);
        var rentEntryId = draftWithRentEntry!.Entries[^1].Id;
        await statementDraftService.SetEntryContactAsync(draft.DraftId, rentEntryId, landlordContact.Id, ownerUserId, ct);

        // additional: Insurance 7 payment on 02.01.2026 (monthly -11.46)
        var draftWithInsurance7Entry = await statementDraftService.AddEntryAsync(
            draft.DraftId,
            ownerUserId,
            new DateTime(2026, 1, 2),
            -11.46m,
            "Insurance 7",
            ct);
        var insurance7EntryId = draftWithInsurance7Entry!.Entries[^1].Id;
        await statementDraftService.SetEntryContactAsync(draft.DraftId, insurance7EntryId, insuranceContact7.Id, ownerUserId, ct);

        // additional: Insurance 5 payment on 02.01.2026 (monthly -20.93)
        var draftWithInsurance5Entry = await statementDraftService.AddEntryAsync(
            draft.DraftId,
            ownerUserId,
            new DateTime(2026, 1, 2),
            -20.93m,
            "Insurance 5",
            ct);
        var insurance5EntryId = draftWithInsurance5Entry!.Entries[^1].Id;
        await statementDraftService.SetEntryContactAsync(draft.DraftId, insurance5EntryId, insuranceContact5.Id, ownerUserId, ct);

        // additional: Insurance 4 payment on 02.01.2026 (monthly -20.64)
        var draftWithInsurance4Entry = await statementDraftService.AddEntryAsync(
            draft.DraftId,
            ownerUserId,
            new DateTime(2026, 1, 2),
            -20.64m,
            "Insurance 4",
            ct);
        var insurance4EntryId = draftWithInsurance4Entry!.Entries[^1].Id;
        await statementDraftService.SetEntryContactAsync(draft.DraftId, insurance4EntryId, insuranceContact4.Id, ownerUserId, ct);

        // additional:   payment on 02.01.2026 (quarterly -39.03)
        var draftWithInsurance6Entry = await statementDraftService.AddEntryAsync(
            draft.DraftId,
            ownerUserId,
            new DateTime(2026, 1, 2),
            -39.03m,
            "Insurance 6",
            ct);
        var insurance6EntryId = draftWithInsurance6Entry!.Entries[^1].Id;
        await statementDraftService.SetEntryContactAsync(draft.DraftId, insurance6EntryId, insuranceContact6.Id, ownerUserId, ct);

        // additional: Utilities booking on 06.01.2026 (monthly -75) - realized
        var draftWithUtilitiesEntry = await statementDraftService.AddEntryAsync(
            draft.DraftId,
            ownerUserId,
            new DateTime(2026, 1, 6),
            -75.00m,
            "Utilities",
            ct);
        var utilitiesEntryId = draftWithUtilitiesEntry!.Entries[^1].Id;
        await statementDraftService.SetEntryContactAsync(draft.DraftId, utilitiesEntryId, utilities.Id, ownerUserId, ct);

        // additional: supermarket booking on 02.01.2026 (unbudgeted)
        var draftWithSupermarketEntry = await statementDraftService.AddEntryAsync(
            draft.DraftId,
            ownerUserId,
            new DateTime(2026, 1, 2),
            -20.09m,
            "Supermarket 6",
            ct);
        var supermarketEntryId = draftWithSupermarketEntry!.Entries[^1].Id;
        await statementDraftService.SetEntryContactAsync(draft.DraftId, supermarketEntryId, supermarket6.Id, ownerUserId, ct);

        // additional: Supermarket 7 bookings
        var draftWithSupermarket7Entry1 = await statementDraftService.AddEntryAsync(
            draft.DraftId,
            ownerUserId,
            new DateTime(2026, 1, 5),
            -11.19m,
            "Supermarket 7",
            ct);
        var supermarket7Entry1Id = draftWithSupermarket7Entry1!.Entries[^1].Id;
        await statementDraftService.SetEntryContactAsync(draft.DraftId, supermarket7Entry1Id, supermarket7.Id, ownerUserId, ct);

        var draftWithSupermarket7Entry2 = await statementDraftService.AddEntryAsync(
            draft.DraftId,
            ownerUserId,
            new DateTime(2026, 1, 7),
            -9.36m,
            "Supermarket 7",
            ct);
        var supermarket7Entry2Id = draftWithSupermarket7Entry2!.Entries[^1].Id;
        await statementDraftService.SetEntryContactAsync(draft.DraftId, supermarket7Entry2Id, supermarket7.Id, ownerUserId, ct);

        // additional: Supermarket 8 on 06.01 (-10.37)
        var draftWithSupermarket8Entry = await statementDraftService.AddEntryAsync(
            draft.DraftId,
            ownerUserId,
            new DateTime(2026, 1, 6),
            -10.37m,
            "Supermarket 8",
            ct);
        var supermarket8EntryId = draftWithSupermarket8Entry!.Entries[^1].Id;
        await statementDraftService.SetEntryContactAsync(draft.DraftId, supermarket8EntryId, supermarket8.Id, ownerUserId, ct);

        // streaming provider bookings: two small charges on 08.01 and one on 15.01
        var draftWithStreamEntry1 = await statementDraftService.AddEntryAsync(
            draft.DraftId,
            ownerUserId,
            new DateTime(2026, 1, 8),
            -4.99m,
            "Streaming Provider 1",
            ct);
        var streamEntry1Id = draftWithStreamEntry1!.Entries[^1].Id;
        await statementDraftService.SetEntryContactAsync(draft.DraftId, streamEntry1Id, stream1.Id, ownerUserId, ct);

        var draftWithStreamEntry2 = await statementDraftService.AddEntryAsync(
            draft.DraftId,
            ownerUserId,
            new DateTime(2026, 1, 8),
            -4.99m,
            "Streaming Provider 1",
            ct);
        var streamEntry2Id = draftWithStreamEntry2!.Entries[^1].Id;
        await statementDraftService.SetEntryContactAsync(draft.DraftId, streamEntry2Id, stream1.Id, ownerUserId, ct);

        var draftWithStreamEntry3 = await statementDraftService.AddEntryAsync(
            draft.DraftId,
            ownerUserId,
            new DateTime(2026, 1, 15),
            -6.00m,
            "Streaming Provider 1",
            ct);
        var streamEntry3Id = draftWithStreamEntry3!.Entries[^1].Id;
        await statementDraftService.SetEntryContactAsync(draft.DraftId, streamEntry3Id, stream1.Id, ownerUserId, ct);

        // additional: Bank 3 booking on 09.01.2026 (+4.80) - unbudgeted income
        var draftWithBank3Entry = await statementDraftService.AddEntryAsync(
            draft.DraftId,
            ownerUserId,
            new DateTime(2026, 1, 9),
            4.80m,
            "Bank 3",
            ct);
        var bank3EntryId = draftWithBank3Entry!.Entries[^1].Id;
        await statementDraftService.SetEntryContactAsync(draft.DraftId, bank3EntryId, bankContact3.Id, ownerUserId, ct);

        // 09.01.2026: Dienstleister 10 charge -4.00 (unbudgeted outflow)
        var draftWithServiceProvider10Entry = await statementDraftService.AddEntryAsync(
            draft.DraftId,
            ownerUserId,
            new DateTime(2026, 1, 9),
            -4.00m,
            "Service Provider 10",
            ct);
        var serviceProvider10EntryId = draftWithServiceProvider10Entry!.Entries[^1].Id;
        await statementDraftService.SetEntryContactAsync(draft.DraftId, serviceProvider10EntryId, serviceProvider10.Id, ownerUserId, ct);

        // 12.01.2026: Kantine 1 charge -5.25 (unbudgeted outflow)
        var draftWithKantineEntry = await statementDraftService.AddEntryAsync(
            draft.DraftId,
            ownerUserId,
            new DateTime(2026, 1, 12),
            -5.25m,
            "Canteen 1",
            ct);
        var canteenEntryId = draftWithKantineEntry!.Entries[^1].Id;
        await statementDraftService.SetEntryContactAsync(draft.DraftId, canteenEntryId, canteenContact.Id, ownerUserId, ct);

        // 12.01.2026: Tankstelle 1 charge -55.59 (unbudgeted outflow)
        var draftWithTankEntry = await statementDraftService.AddEntryAsync(
            draft.DraftId,
            ownerUserId,
            new DateTime(2026, 1, 12),
            -55.59m,
            "Gas Station 1",
            ct);
        var fuelEntryId = draftWithTankEntry!.Entries[^1].Id;
        await statementDraftService.SetEntryContactAsync(draft.DraftId, fuelEntryId, tank1.Id, ownerUserId, ct);

        // additional: Supermarket 6 on 14.01 (-11.40)
        var draftWithSupermarket6Entry2 = await statementDraftService.AddEntryAsync(
            draft.DraftId,
            ownerUserId,
            new DateTime(2026, 1, 14),
            -11.40m,
            "Supermarket 6",
            ct);
        var supermarket6Entry2Id = draftWithSupermarket6Entry2!.Entries[^1].Id;
        await statementDraftService.SetEntryContactAsync(draft.DraftId, supermarket6Entry2Id, supermarket6.Id, ownerUserId, ct);

        var draftWithW2Income = await statementDraftService.AddEntryAsync(
            draft.DraftId,
            ownerUserId,
            new DateTime(2026, 1, 2),
            39.03m,
            "Recurring Expense 2",
            ct);
        var w2IncomeId = draftWithW2Income!.Entries[^1].Id;
        await statementDraftService.SetEntryContactAsync(draft.DraftId, w2IncomeId, selfContactId, ownerUserId, ct);
        await statementDraftService.AssignSavingsPlanAsync(draft.DraftId, w2IncomeId, recurringPlans.First(p => p.Name == "Recurring Expense 2").Id, ownerUserId, ct);

        var draftWithW5Expense = await statementDraftService.AddEntryAsync(
            draft.DraftId,
            ownerUserId,
            new DateTime(2026, 1, 2),
            -10.5m,
            "Recurring Expense 5",
            ct);
        var w5ExpenseId = draftWithW5Expense!.Entries[^1].Id;
        await statementDraftService.SetEntryContactAsync(draft.DraftId, w5ExpenseId, selfContactId, ownerUserId, ct);
        await statementDraftService.AssignSavingsPlanAsync(draft.DraftId, w5ExpenseId, recurringPlans.First(p => p.Name == "Recurring Expense 5").Id, ownerUserId, ct);

        var draftWithW10Expense = await statementDraftService.AddEntryAsync(
            draft.DraftId,
            ownerUserId,
            new DateTime(2026, 1, 2),
            -18.36m,
            "Recurring Expense 10",
            ct);
        var w10ExpenseId = draftWithW10Expense!.Entries[^1].Id;
        await statementDraftService.SetEntryContactAsync(draft.DraftId, w10ExpenseId, selfContactId, ownerUserId, ct);
        await statementDraftService.AssignSavingsPlanAsync(draft.DraftId, w10ExpenseId, recurringPlans.First(p => p.Name == "Recurring Expense 10").Id, ownerUserId, ct);

        var draftWithW7Income = await statementDraftService.AddEntryAsync(
            draft.DraftId,
            ownerUserId,
            new DateTime(2026, 1, 2),
            99.00m,
            "Recurring Expense 7",
            ct);
        var w7IncomeId = draftWithW7Income!.Entries[^1].Id;
        await statementDraftService.SetEntryContactAsync(draft.DraftId, w7IncomeId, selfContactId, ownerUserId, ct);
        await statementDraftService.AssignSavingsPlanAsync(draft.DraftId, w7IncomeId, recurringPlans.First(p => p.Name == "Recurring Expense 7").Id, ownerUserId, ct);

        var draftWithW4Expense = await statementDraftService.AddEntryAsync(
            draft.DraftId,
            ownerUserId,
            new DateTime(2026, 1, 2),
            -5.21m,
            "Recurring Expense 4",
            ct);
        var w4ExpenseId = draftWithW4Expense!.Entries[^1].Id;
        await statementDraftService.SetEntryContactAsync(draft.DraftId, w4ExpenseId, selfContactId, ownerUserId, ct);
        await statementDraftService.AssignSavingsPlanAsync(draft.DraftId, w4ExpenseId, recurringPlans.First(p => p.Name == "Recurring Expense 4").Id, ownerUserId, ct);

        var draftWithW8Expense1 = await statementDraftService.AddEntryAsync(
            draft.DraftId,
            ownerUserId,
            new DateTime(2026, 1, 2),
            -3.82m,
            "Recurring Expense 8",
            ct);
        var w8Expense1Id = draftWithW8Expense1!.Entries[^1].Id;
        await statementDraftService.SetEntryContactAsync(draft.DraftId, w8Expense1Id, selfContactId, ownerUserId, ct);
        await statementDraftService.AssignSavingsPlanAsync(draft.DraftId, w8Expense1Id, recurringPlans.First(p => p.Name == "Recurring Expense 8").Id, ownerUserId, ct);

        // This booking was incorrectly assigned to plan 8; it belongs to plan 6
        var draftWithW6Expense = await statementDraftService.AddEntryAsync(
            draft.DraftId,
            ownerUserId,
            new DateTime(2026, 1, 2),
            -4.63m,
            "Recurring Expense 6",
            ct);
        var w6ExpenseId = draftWithW6Expense!.Entries[^1].Id;
        await statementDraftService.SetEntryContactAsync(draft.DraftId, w6ExpenseId, selfContactId, ownerUserId, ct);
        await statementDraftService.AssignSavingsPlanAsync(draft.DraftId, w6ExpenseId, recurringPlans.First(p => p.Name == "Recurring Expense 6").Id, ownerUserId, ct);

        // Credit that actually belongs to recurring plan 8 (11.46)
        var draftWithW8Income = await statementDraftService.AddEntryAsync(
            draft.DraftId,
            ownerUserId,
            new DateTime(2026, 1, 2),
            11.46m,
            "Recurring Expense 8",
            ct);
        var w8IncomeId = draftWithW8Income!.Entries[^1].Id;
        await statementDraftService.SetEntryContactAsync(draft.DraftId, w8IncomeId, selfContactId, ownerUserId, ct);
        await statementDraftService.AssignSavingsPlanAsync(draft.DraftId, w8IncomeId, recurringPlans.First(p => p.Name == "Recurring Expense 8").Id, ownerUserId, ct);

        // Purchases of additional securities on 02.01.2026 (bank contact)
        var draftWithBuy1 = await statementDraftService.AddEntryAsync(
            draft.DraftId,
            ownerUserId,
            new DateTime(2026, 1, 2),
            -500.00m,
            "Buy Stock 3",
            ct);
        var buy1Id = draftWithBuy1!.Entries[^1].Id;
        await statementDraftService.SetEntryContactAsync(draft.DraftId, buy1Id, bankContact.Id, ownerUserId, ct);
        await statementDraftService.SaveEntryAllAsync(draft.DraftId, buy1Id, ownerUserId, bankContact.Id, null, null, null, security3.Id, FinanceManager.Shared.Dtos.Securities.SecurityTransactionType.Buy, 10m, null, null, ct);

        var draftWithBuy2 = await statementDraftService.AddEntryAsync(
            draft.DraftId,
            ownerUserId,
            new DateTime(2026, 1, 2),
            -150.00m,
            "Buy Stock 4",
            ct);
        var buy2Id = draftWithBuy2!.Entries[^1].Id;
        await statementDraftService.SetEntryContactAsync(draft.DraftId, buy2Id, bankContact.Id, ownerUserId, ct);
        await statementDraftService.SaveEntryAllAsync(draft.DraftId, buy2Id, ownerUserId, bankContact.Id, null, null, null, security4.Id, FinanceManager.Shared.Dtos.Securities.SecurityTransactionType.Buy, 3m, null, null, ct);

        var draftWithBuy3 = await statementDraftService.AddEntryAsync(
            draft.DraftId,
            ownerUserId,
            new DateTime(2026, 1, 2),
            -25.00m,
            "Buy Stock 5",
            ct);
        var buy3Id = draftWithBuy3!.Entries[^1].Id;
        await statementDraftService.SetEntryContactAsync(draft.DraftId, buy3Id, bankContact.Id, ownerUserId, ct);
        await statementDraftService.SaveEntryAllAsync(draft.DraftId, buy3Id, ownerUserId, bankContact.Id, null, null, null, security5.Id, FinanceManager.Shared.Dtos.Securities.SecurityTransactionType.Buy, 1m, null, null, ct);

        // Additional dividend postings for new securities
        var div6 = await statementDraftService.AddEntryAsync(draft.DraftId, ownerUserId, new DateTime(2026, 1, 2), 0.07m, "Dividend Stock 6", ct);
        var div6Id = div6!.Entries[^1].Id;
        await statementDraftService.SetEntryContactAsync(draft.DraftId, div6Id, bankContact.Id, ownerUserId, ct);
        await statementDraftService.SaveEntryAllAsync(draft.DraftId, div6Id, ownerUserId, bankContact.Id, null, null, null, security6.Id, FinanceManager.Shared.Dtos.Securities.SecurityTransactionType.Dividend, null, null, null, ct);
        await statementDraftService.UpdateEntryCoreAsync(draft.DraftId, div6Id, ownerUserId, new DateTime(2026,1,2), new DateTime(2026,1,2), 0.07m, "Dividend Stock 6", null, null, null, ct);

        var div7 = await statementDraftService.AddEntryAsync(draft.DraftId, ownerUserId, new DateTime(2026, 1, 5), 17.66m, "Dividend Stock 7", ct);
        var div7Id = div7!.Entries[^1].Id;
        await statementDraftService.SetEntryContactAsync(draft.DraftId, div7Id, bankContact.Id, ownerUserId, ct);
        await statementDraftService.SaveEntryAllAsync(draft.DraftId, div7Id, ownerUserId, bankContact.Id, null, null, null, security7.Id, FinanceManager.Shared.Dtos.Securities.SecurityTransactionType.Dividend, null, null, null, ct);
        await statementDraftService.UpdateEntryCoreAsync(draft.DraftId, div7Id, ownerUserId, new DateTime(2026,1,5), new DateTime(2026,1,5), 17.66m, "Dividend Stock 7", null, null, null, ct);

        var div8 = await statementDraftService.AddEntryAsync(draft.DraftId, ownerUserId, new DateTime(2026, 1, 9), 17.66m, "Dividend Stock 8", ct);
        var div8Id = div8!.Entries[^1].Id;
        await statementDraftService.SetEntryContactAsync(draft.DraftId, div8Id, bankContact.Id, ownerUserId, ct);
        await statementDraftService.SaveEntryAllAsync(draft.DraftId, div8Id, ownerUserId, bankContact.Id, null, null, null, security8.Id, FinanceManager.Shared.Dtos.Securities.SecurityTransactionType.Dividend, null, null, null, ct);
        await statementDraftService.UpdateEntryCoreAsync(draft.DraftId, div8Id, ownerUserId, new DateTime(2026,1,9), new DateTime(2026,1,9), 8.26m, "Dividend Stock 8", null, null, null, ct);

        var div9 = await statementDraftService.AddEntryAsync(draft.DraftId, ownerUserId, new DateTime(2026, 1, 12), 13.47m, "Dividend Stock 9", ct);
        var div9Id = div9!.Entries[^1].Id;
        await statementDraftService.SetEntryContactAsync(draft.DraftId, div9Id, bankContact.Id, ownerUserId, ct);
        await statementDraftService.SaveEntryAllAsync(draft.DraftId, div9Id, ownerUserId, bankContact.Id, null, null, null, security9.Id, FinanceManager.Shared.Dtos.Securities.SecurityTransactionType.Dividend, null, null, null, ct);
        await statementDraftService.UpdateEntryCoreAsync(draft.DraftId, div9Id, ownerUserId, new DateTime(2026,1,12), new DateTime(2026,1,12), 13.47m, "Dividend Stock 9", null, null, null, ct);

        var div10 = await statementDraftService.AddEntryAsync(draft.DraftId, ownerUserId, new DateTime(2026, 1, 19), 5.10m, "Dividend Stock 10", ct);
        var div10Id = div10!.Entries[^1].Id;
        await statementDraftService.SetEntryContactAsync(draft.DraftId, div10Id, bankContact.Id, ownerUserId, ct);
        await statementDraftService.SaveEntryAllAsync(draft.DraftId, div10Id, ownerUserId, bankContact.Id, null, null, null, security10.Id, FinanceManager.Shared.Dtos.Securities.SecurityTransactionType.Dividend, null, null, null, ct);
        await statementDraftService.UpdateEntryCoreAsync(draft.DraftId, div10Id, ownerUserId, new DateTime(2026,1,19), new DateTime(2026,1,19), 5.10m, "Dividend Stock 10", null, null, null, ct);

        var div11 = await statementDraftService.AddEntryAsync(draft.DraftId, ownerUserId, new DateTime(2026, 1, 20), 17.61m, "Dividend Stock 11", ct);
        var div11Id = div11!.Entries[^1].Id;
        await statementDraftService.SetEntryContactAsync(draft.DraftId, div11Id, bankContact.Id, ownerUserId, ct);
        await statementDraftService.SaveEntryAllAsync(draft.DraftId, div11Id, ownerUserId, bankContact.Id, null, null, null, security11.Id, FinanceManager.Shared.Dtos.Securities.SecurityTransactionType.Dividend, null, null, null, ct);
        await statementDraftService.UpdateEntryCoreAsync(draft.DraftId, div11Id, ownerUserId, new DateTime(2026,1,20), new DateTime(2026,1,20), 17.61m, "Dividend Stock 11", null, null, null, ct);

        var booking = await statementDraftService.BookAsync(draft.DraftId, null, ownerUserId, true, ct);
        booking.Success.Should().BeTrue();
        #endregion
        
        #region Test
        var cacheService = new ReportCacheService(db, new BackgroundTaskManager());
        var brs = new BudgetReportService(purposeService, categoryService, ruleService, postingsService, contactService, savingsPlanService, securityService, cacheService);

        // First: run report using booking date as basis (existing expected assertions target this)
        var actual = await brs.GetRawDataAsync(ownerUserId, from, to, BudgetReportDateBasis.BookingDate, ct);

        // Also run report using valuta date as basis. Posts with a valuta date outside the range
        // (e.g. "Dividend Stock 2" has valuta 2025-12-31) must NOT appear in the result when
        // the valuta date is used as the filtering basis.
        var actualValuta = await brs.GetRawDataAsync(ownerUserId, from, to, BudgetReportDateBasis.ValutaDate, ct);
        actualValuta.UnbudgetedPostings.Should().NotContain(p => p.Description == "Dividend Stock 2");
        var expected = new BudgetReportRawDataDto() 
        {
            PeriodEnd = DateTime.Parse("2026-01-31 23:59:59.9999999", CultureInfo.InvariantCulture),
            PeriodStart = DateTime.Parse("2026-01-01 00:00:00", CultureInfo.InvariantCulture),
            Categories = new[]
            {
                new BudgetReportCategoryRawDataDto
                {
                    CategoryId = workCategory.Id,
                    CategoryName = "Work",
                    BudgetedAmount = 0m,
                    Purposes = new[]
                    {
                        new BudgetReportPurposeRawDataDto
                        {
                            PurposeId = salaryPurpose.Id,
                            PurposeName = "Salary",
                            BudgetedIncome = 3326.46m,
                            BudgetedTarget = 3326.46m,
                            BudgetSourceType = BudgetSourceType.Contact,
                            SourceId = employerContact.Id,
                            SourceName = "Employer",
                            Postings = new[]
                            {
                                new BudgetReportPostingRawDataDto
                                {
                                    PostingId = Guid.Empty,
                                    BookingDate = new DateTime(2026, 1, 27),
                                    ValutaDate = new DateTime(2026, 1, 27),
                                    Amount = 3326.46m,
                                    PostingKind = PostingKind.Contact,
                                    Description = "Salary",
                                    BudgetCategoryName = "Work",
                                    BudgetPurposeName = "Salary",
                                    AccountId = null,
                                    AccountName = bankAccount.Name,
                                    ContactId = employerContact.Id,
                                    ContactName = "Employer",
                                    SavingsPlanId = null,
                                    SavingsPlanName = null,
                                    SecurityId = null,
                                    SecurityName = null
                                }
                            }
                        }
                    }
                },                
                // Shopping & food category: Verpflegung (supermarkets) and Bäckereien
                new BudgetReportCategoryRawDataDto
                {
                    CategoryId = shoppingBudgetCategory.Id,
                    CategoryName = "Shopping & Food",
                    BudgetedExpense = -500m,
                    BudgetedTarget = -500m,
                    Purposes = new[]
                    {
                        new BudgetReportPurposeRawDataDto
                        {
                            PurposeId = baeckereienPurpose.Id,
                            PurposeName = "Bakeries",
                            BudgetedExpense = 0m,
                            BudgetedTarget = 0m,
                            BudgetSourceType = BudgetSourceType.ContactGroup,
                            SourceId = bakeryCategory.Id,
                            SourceName = bakeryCategory.Name,
                            Postings = Array.Empty<BudgetReportPostingRawDataDto>()
                        },
                
                        new BudgetReportPurposeRawDataDto
                        {
                            PurposeId = verpflegungPurpose.Id,
                            PurposeName = "Food",
                            BudgetedExpense = 0m,
                            BudgetedTarget = 0m,
                            BudgetSourceType = BudgetSourceType.ContactGroup,
                            SourceId = shoppingCategory.Id,
                            SourceName = shoppingCategory.Name,
                            Postings = new[]
                            {
                                new BudgetReportPostingRawDataDto()
                                {
                                    Amount = -20.09m,
                                    BookingDate = new DateTime(2026, 1, 2),
                                    ValutaDate = new DateTime(2026, 1, 2),
                                    PostingId = Guid.Empty,
                                    PostingKind = PostingKind.Contact,
                            Description = "Supermarket 6",
                            BudgetPurposeName = "Food",
                                    BudgetCategoryName = shoppingBudgetCategory.Name,
                                    AccountId = null,
                                    AccountName = bankAccount.Name,
                                    ContactId = supermarket6.Id,
                                    ContactName = supermarket6.Name,
                                    SavingsPlanId = null,
                                    SavingsPlanName = null,
                                    SecurityId = null,
                                    SecurityName = null
                                },
                                new BudgetReportPostingRawDataDto()
                                {
                                    Amount = -11.19m,
                                    BookingDate = new DateTime(2026, 1, 5),
                                    ValutaDate = new DateTime(2026, 1, 5),
                                    PostingId = Guid.Empty,
                                    PostingKind = PostingKind.Contact,
                                    Description = "Supermarket 7",
                                    BudgetPurposeName = "Food",
                                    BudgetCategoryName = shoppingBudgetCategory.Name,
                                    AccountId = null,
                                    AccountName = bankAccount.Name,
                                    ContactId = supermarket7.Id,
                                    ContactName = supermarket7.Name,
                                    SavingsPlanId = null,
                                    SavingsPlanName = null,
                                    SecurityId = null,
                                    SecurityName = null
                                },
                                new BudgetReportPostingRawDataDto()
                                {
                                    Amount = -10.37m,
                                    BookingDate = new DateTime(2026, 1, 6),
                                    ValutaDate = new DateTime(2026, 1, 6),
                                    PostingId = Guid.Empty,
                                    PostingKind = PostingKind.Contact,
                                    Description = "Supermarket 8",
                                    BudgetPurposeName = "Food",
                                    BudgetCategoryName = shoppingBudgetCategory.Name,
                                    AccountId = null,
                                    AccountName = bankAccount.Name,
                                    ContactId = supermarket8.Id,
                                    ContactName = supermarket8.Name,
                                    SavingsPlanId = null,
                                    SavingsPlanName = null,
                                    SecurityId = null,
                                    SecurityName = null
                                },
                                new BudgetReportPostingRawDataDto()
                                {
                                    Amount = -9.36m,
                                    BookingDate = new DateTime(2026, 1, 7),
                                    ValutaDate = new DateTime(2026, 1, 7),
                                    PostingId = Guid.Empty,
                                    PostingKind = PostingKind.Contact,
                                    Description = "Supermarket 7",
                                    BudgetPurposeName = "Food",
                                    BudgetCategoryName = shoppingBudgetCategory.Name,
                                    AccountId = null,
                                    AccountName = bankAccount.Name,
                                    ContactId = supermarket7.Id,
                                    ContactName = supermarket7.Name,
                                    SavingsPlanId = null,
                                    SavingsPlanName = null,
                                    SecurityId = null,
                                    SecurityName = null
                                },
                                new BudgetReportPostingRawDataDto()
                                {
                                    Amount = -11.40m,
                                    BookingDate = new DateTime(2026, 1, 14),
                                    ValutaDate = new DateTime(2026, 1, 14),
                                    PostingId = Guid.Empty,
                                    PostingKind = PostingKind.Contact,
                                    Description = "Supermarket 6",
                                    BudgetPurposeName = "Food",
                                    BudgetCategoryName = shoppingBudgetCategory.Name,
                                    AccountId = null,
                                    AccountName = bankAccount.Name,
                                    ContactId = supermarket6.Id,
                                    ContactName = supermarket6.Name,
                                    SavingsPlanId = null,
                                    SavingsPlanName = null,
                                    SecurityId = null,
                                    SecurityName = null
                                }
                                
                            }
                        }
                    }
                },

                // (rent purpose is uncategorized and therefore appears in UncategorizedPurposes)
                new BudgetReportCategoryRawDataDto
                {
                    CategoryId = lotteryCategory.Id,
                    CategoryName = "Lottery Company",
                    BudgetedExpense = -15m,
                    BudgetedTarget = -15m,
                    Purposes = new[]
                    {
                        new BudgetReportPurposeRawDataDto
                        {
                            PurposeId = lotteryPurpose.Id,
                            PurposeName = "Lottery",
                            BudgetedTarget = 0m,
                            BudgetSourceType = BudgetSourceType.Contact,
                            SourceId = lotteryContact.Id,
                            SourceName = "Lottery Company 1",
                            Postings = new[]
                            {
                                new BudgetReportPostingRawDataDto
                                {
                                    PostingId = Guid.Empty,
                                    BookingDate = new DateTime(2026, 1, 20),
                                    ValutaDate = new DateTime(2026, 1, 20),
                                    // category-level budget (-15) assigned to this purpose (partial of actual -25.5)
                                    Amount = -15m,
                                    PostingKind = PostingKind.Contact,
                                    Description = "Lottery",
                                    BudgetCategoryName = "Lottery Company",
                                    BudgetPurposeName = "Lottery",
                                    AccountId = null,
                                    AccountName = bankAccount.Name,
                                    ContactId = lotteryContact.Id,
                                    ContactName = "Lottery Company 1",
                                    SavingsPlanId = null,
                                    SavingsPlanName = null,
                                    SecurityId = null,
                                    SecurityName = null
                                }
                            }
                        }
                    }
                }
            },
            UncategorizedPurposes = new BudgetReportPurposeRawDataDto[]
            {
                new BudgetReportPurposeRawDataDto
                {
                    PurposeId = Guid.Empty,
                    PurposeName = "Rent",
                    BudgetedExpense = -649.42m,
                    BudgetedTarget = -649.42m,
                    BudgetSourceType = BudgetSourceType.Contact,
                    SourceId = landlordContact.Id,
                    SourceName = landlordContact.Name,
                    Postings = new[]
                    {
                        new BudgetReportPostingRawDataDto()
                        {
                            Amount = -649.42m,
                            BookingDate = new DateTime(2026, 1, 2),
                            ValutaDate = new DateTime(2026, 1, 2),
                            PostingId = Guid.Empty,
                            PostingKind = PostingKind.Contact,
                            Description = "Rent",
                            BudgetPurposeName = "Rent",
                            AccountId = null,
                            AccountName = bankAccount.Name,
                            ContactId = landlordContact.Id,
                            ContactName = landlordContact.Name,
                            SavingsPlanId = null,
                            SavingsPlanName = null,
                            SecurityId = null,
                            SecurityName = null
                        }
                    }
                },
                new BudgetReportPurposeRawDataDto
                {
                    PurposeId = Guid.Empty,
                    PurposeName = "Auto Club",
                    BudgetedExpense = -99m,
                    BudgetedTarget = -99m,
                    BudgetSourceType = BudgetSourceType.Contact,
                    SourceId = autoClub.Id,
                    SourceName = autoClub.Name,
                    Postings = new[]
                    {
                        new BudgetReportPostingRawDataDto()
                        {
                            Amount = -99.00m,
                            BookingDate = new DateTime(2026, 1, 2),
                            ValutaDate = new DateTime(2026, 1, 2),
                            PostingId = Guid.Empty,
                            PostingKind = PostingKind.Contact,
                            Description = "Auto Club",
                            BudgetPurposeName = "Auto Club",
                            AccountId = null,
                            AccountName = bankAccount.Name,
                            ContactId = autoClub.Id,
                            ContactName = autoClub.Name,
                            SavingsPlanId = null,
                            SavingsPlanName = null,
                            SecurityId = null,
                            SecurityName = null
                        }
                    }
                },
                new BudgetReportPurposeRawDataDto
                {
                    PurposeId = Guid.Empty,
                    PurposeName = "Gym",
                    BudgetedExpense = -15m,
                    BudgetedTarget = -15m,
                    BudgetSourceType = BudgetSourceType.Contact,
                    SourceId = fitness.Id,
                    SourceName = fitness.Name,
                    Postings = new[]
                    {
                        new BudgetReportPostingRawDataDto()
                        {
                            Amount = -15.00m,
                            BookingDate = new DateTime(2026, 1, 2),
                            ValutaDate = new DateTime(2026, 1, 2),
                            PostingId = Guid.Empty,
                            PostingKind = PostingKind.Contact,
                            Description = "Gym",
                            BudgetPurposeName = "Gym",
                            AccountId = null,
                            AccountName = bankAccount.Name,
                            ContactId = fitness.Id,
                            ContactName = fitness.Name,
                            SavingsPlanId = null,
                            SavingsPlanName = null,
                            SecurityId = null,
                            SecurityName = null
                        }
                    }
                },
                new BudgetReportPurposeRawDataDto
                {
                    PurposeId = Guid.Empty,
                    PurposeName = "Utilities",
                    BudgetedExpense = -75m,
                    BudgetedTarget = -75m,
                    BudgetSourceType = BudgetSourceType.Contact,
                    SourceId = utilities.Id,
                    SourceName = utilities.Name,
                    Postings = new[]
                    {
                        new BudgetReportPostingRawDataDto()
                        {
                            Amount = -75.00m,
                            BookingDate = new DateTime(2026, 1, 6),
                            ValutaDate = new DateTime(2026, 1, 6),
                            PostingId = Guid.Empty,
                            PostingKind = PostingKind.Contact,
                            Description = "Utilities",
                            BudgetPurposeName = "Utilities",
                            AccountId = null,
                            AccountName = bankAccount.Name,
                            ContactId = utilities.Id,
                            ContactName = utilities.Name,
                            SavingsPlanId = null,
                            SavingsPlanName = null,
                            SecurityId = null,
                            SecurityName = null
                        }
                    }
                },
                // savingsplan 5
                new BudgetReportPurposeRawDataDto
                {
                    PurposeId = Guid.Empty,
                    PurposeName = "Savings Plan 5",
                    BudgetedExpense = -10m,
                    BudgetedTarget = -10m,
                    BudgetSourceType = BudgetSourceType.SavingsPlan,
                    SourceId = smallPlans.First(p => p.Name == "Savings Plan 5").Id,
                    SourceName = smallPlans.First(p => p.Name == "Savings Plan 5").Name,
                    Postings = new[]
                    {
                        new BudgetReportPostingRawDataDto()
                        {
                            Amount = -10.00m,
                            BookingDate = new DateTime(2026, 1, 2),
                            ValutaDate = new DateTime(2026, 1, 2),
                            PostingId = Guid.Empty,
                            PostingKind = PostingKind.Contact,
                            Description = "Savings Plan 5",
                            BudgetPurposeName = "Savings Plan 5",
                            AccountId = null,
                            AccountName = bankAccount.Name,
                            ContactId = selfContactId,
                            ContactName = "Me",
                            SavingsPlanId = smallPlans.First(p => p.Name == "Savings Plan 5").Id,
                            SavingsPlanName = smallPlans.First(p => p.Name == "Savings Plan 5").Name,
                            SecurityId = null,
                            SecurityName = null
                        }
                    }
                },
                // Recurring Expense 3 (yearly)
                new BudgetReportPurposeRawDataDto
                {
                    PurposeId = Guid.Empty,
                    PurposeName = "Recurring Expense 3",
                    BudgetedExpense = -31.80m,
                    BudgetedIncome = 372.92m,
                    BudgetedTarget = 341.12m,
                    BudgetSourceType = BudgetSourceType.SavingsPlan,
                    SourceId = recurringPlans.First(p => p.Name == "Recurring Expense 3").Id,
                    SourceName = recurringPlans.First(p => p.Name == "Recurring Expense 3").Name,
                    Postings = new[]
                    {
                        new BudgetReportPostingRawDataDto()
                        {
                            Amount = 372.92m,
                            BookingDate = new DateTime(2026, 1, 2),
                            ValutaDate = new DateTime(2026, 1, 2),
                            PostingId = Guid.Empty,
                            PostingKind = PostingKind.Contact,
                            Description = "Recurring Expense 3",
                            BudgetPurposeName = "Recurring Expense 3",
                            AccountId = null,
                            AccountName = bankAccount.Name,
                            ContactId = selfContactId,
                            ContactName = "Me",
                            SavingsPlanId = recurringPlans.First(p => p.Name == "Recurring Expense 3").Id,
                            SavingsPlanName = recurringPlans.First(p => p.Name == "Recurring Expense 3").Name,
                            SecurityId = null,
                            SecurityName = null
                        }
                    }
                },
                new BudgetReportPurposeRawDataDto
                {
                    PurposeId = Guid.Empty,
                    PurposeName = "Savings Plan 4",
                    BudgetedExpense = -139m,
                    BudgetedTarget = -139m,
                    BudgetSourceType = BudgetSourceType.SavingsPlan,
                    SourceId = smallPlans.First(p => p.Name == "Savings Plan 4").Id,
                    SourceName = smallPlans.First(p => p.Name == "Savings Plan 4").Name,
                    Postings = new[]
                    {
                        new BudgetReportPostingRawDataDto()
                        {
                            Amount = -139.00m,
                            BookingDate = new DateTime(2026, 1, 2),
                            ValutaDate = new DateTime(2026, 1, 2),
                            PostingId = Guid.Empty,
                            PostingKind = PostingKind.Contact,
                            Description = "Savings Plan 4",
                            BudgetPurposeName = "Savings Plan 4",
                            AccountId = null,
                            AccountName = bankAccount.Name,
                            ContactId = selfContactId,
                            ContactName = "Me",
                            SavingsPlanId = smallPlans.First(p => p.Name == "Savings Plan 4").Id,
                            SavingsPlanName = smallPlans.First(p => p.Name == "Savings Plan 4").Name,
                            SecurityId = null,
                            SecurityName = null
                        }
                    }
                },
                new BudgetReportPurposeRawDataDto
                {
                    PurposeId = Guid.Empty,
                    PurposeName = "Insurance 5",
                    BudgetedExpense = -20.93m,
                    BudgetedTarget = -20.93m,
                    BudgetSourceType = BudgetSourceType.Contact,
                    SourceId = insuranceContact5.Id,
                    SourceName = insuranceContact5.Name,
                    Postings = new[]
                    {
                        new BudgetReportPostingRawDataDto()
                        {
                            Amount = -20.93m,
                            BookingDate = new DateTime(2026, 1, 2),
                            ValutaDate = new DateTime(2026, 1, 2),
                            PostingId = Guid.Empty,
                            PostingKind = PostingKind.Contact,
                            Description = "Insurance 5",
                            BudgetPurposeName = "Insurance 5",
                            AccountId = null,
                            AccountName = bankAccount.Name,
                            ContactId = insuranceContact5.Id,
                            ContactName = insuranceContact5.Name,
                            SavingsPlanId = null,
                            SavingsPlanName = null,
                            SecurityId = null,
                            SecurityName = null
                        }
                    }
                },
                new BudgetReportPurposeRawDataDto
                {
                    PurposeId = Guid.Empty,
                    PurposeName = "Insurance 4",
                    BudgetedExpense = -20.64m,
                    BudgetedTarget = -20.64m,
                    BudgetSourceType = BudgetSourceType.Contact,
                    SourceId = insuranceContact4.Id,
                    SourceName = insuranceContact4.Name,
                    Postings = new[]
                    {
                        new BudgetReportPostingRawDataDto()
                        {
                            Amount = -20.64m,
                            BookingDate = new DateTime(2026, 1, 2),
                            ValutaDate = new DateTime(2026, 1, 2),
                            PostingId = Guid.Empty,
                            PostingKind = PostingKind.Contact,
                            Description = "Insurance 4",
                            BudgetPurposeName = "Insurance 4",
                            AccountId = null,
                            AccountName = bankAccount.Name,
                            ContactId = insuranceContact4.Id,
                            ContactName = insuranceContact4.Name,
                            SavingsPlanId = null,
                            SavingsPlanName = null,
                            SecurityId = null,
                            SecurityName = null
                        }
                    }
                },
                new BudgetReportPurposeRawDataDto
                {
                    PurposeId = Guid.Empty,
                    PurposeName = "Insurance 7",
                    BudgetedExpense = -11.46m,
                    BudgetedTarget = -11.46m,
                    BudgetSourceType = BudgetSourceType.Contact,
                    SourceId = insuranceContact7.Id,
                    SourceName = insuranceContact7.Name,
                    Postings = new[]
                    {
                        new BudgetReportPostingRawDataDto()
                        {
                            Amount = -11.46m,
                            BookingDate = new DateTime(2026, 1, 2),
                            ValutaDate = new DateTime(2026, 1, 2),
                            PostingId = Guid.Empty,
                            PostingKind = PostingKind.Contact,
                            Description = "Insurance 7",
                            BudgetPurposeName = "Insurance 7",
                            AccountId = null,
                            AccountName = bankAccount.Name,
                            ContactId = insuranceContact7.Id,
                            ContactName = insuranceContact7.Name,
                            SavingsPlanId = null,
                            SavingsPlanName = null,
                            SecurityId = null,
                            SecurityName = null
                        }
                    }
                },
                new BudgetReportPurposeRawDataDto
                {
                    PurposeId = Guid.Empty,
                    PurposeName = "Insurance 8",
                    BudgetedExpense = -381.6m,
                    BudgetedTarget = -381.6m,
                    BudgetSourceType = BudgetSourceType.Contact,
                    SourceId = insuranceContact8.Id,
                    SourceName = insuranceContact8.Name,
                    Postings = new[]
                    {
                        new BudgetReportPostingRawDataDto()
                        {
                            Amount = -381.60m,
                            BookingDate = new DateTime(2026, 1, 2),
                            ValutaDate = new DateTime(2026, 1, 2),
                            PostingId = Guid.Empty,
                            PostingKind = PostingKind.Contact,
                            Description = "Insurance 8",
                            BudgetPurposeName = "Insurance 8",
                            AccountId = null,
                            AccountName = bankAccount.Name,
                            ContactId = insuranceContact8.Id,
                            ContactName = insuranceContact8.Name,
                            SavingsPlanId = null,
                            SavingsPlanName = null,
                            SecurityId = null,
                            SecurityName = null
                        }
                    }
                },
                new BudgetReportPurposeRawDataDto
                {
                    PurposeId = Guid.Empty,
                    PurposeName = "Telecom Provider 1",
                    BudgetedExpense = -54.13m,
                    BudgetedTarget = -54.13m,
                    BudgetSourceType = BudgetSourceType.Contact,
                    SourceId = telecom1.Id,
                    SourceName = telecom1.Name,
                    Postings = Array.Empty<BudgetReportPostingRawDataDto>()
                },
                new BudgetReportPurposeRawDataDto
                {
                    PurposeId = Guid.Empty,
                    PurposeName = "Insurance 6",
                    BudgetedExpense = -39.03m,
                    BudgetedTarget = -39.03m,
                    BudgetSourceType = BudgetSourceType.Contact,
                    SourceId = insuranceContact6.Id,
                    SourceName = insuranceContact6.Name,
                    Postings = new[]
                    {
                        new BudgetReportPostingRawDataDto()
                        {
                            Amount = -39.03m,
                            BookingDate = new DateTime(2026, 1, 2),
                            ValutaDate = new DateTime(2026, 1, 2),
                            PostingId = Guid.Empty,
                            PostingKind = PostingKind.Contact,
                            Description = "Insurance 6",
                            BudgetPurposeName = "Insurance 6",
                            AccountId = null,
                            AccountName = bankAccount.Name,
                            ContactId = insuranceContact6.Id,
                            ContactName = insuranceContact6.Name,
                            SavingsPlanId = null,
                            SavingsPlanName = null,
                            SecurityId = null,
                            SecurityName = null
                        }
                    }
                },
                new BudgetReportPurposeRawDataDto
                {
                    PurposeId = Guid.Empty,
                    PurposeName = "Fuel",
                    BudgetedExpense = -50m,
                    BudgetedTarget = -50m,
                    BudgetSourceType = BudgetSourceType.ContactGroup,
                    SourceId = gasStationCategory.Id,
                    SourceName = gasStationCategory.Name,
                    Postings = new[]
                    {
                        new BudgetReportPostingRawDataDto()
                        {
                            Amount = -50.00m,
                            BookingDate = new DateTime(2026, 1, 12),
                            ValutaDate = new DateTime(2026, 1, 12),
                            PostingId = Guid.Empty,
                            PostingKind = PostingKind.Contact,
                            Description = "Gas Station 1",
                            BudgetPurposeName = "Fuel",
                            AccountId = null,
                            AccountName = bankAccount.Name,
                            ContactId = tank1.Id,
                            ContactName = tank1.Name,
                            SavingsPlanId = null,
                            SavingsPlanName = null,
                            SecurityId = null,
                            SecurityName = null
                        }
                    }
                },
                new BudgetReportPurposeRawDataDto
                {
                    PurposeId = Guid.Empty,
                    PurposeName = "Streaming Provider",
                    BudgetedExpense = -10m,
                    BudgetedTarget = -10m,
                    BudgetSourceType = BudgetSourceType.ContactGroup,
                    SourceId = streamingCategory.Id,
                    SourceName = streamingCategory.Name,
                    Postings = new[]
                    {
                        new BudgetReportPostingRawDataDto()
                        {
                            Amount = -4.99m,
                            BookingDate = new DateTime(2026, 1, 8),
                            ValutaDate = new DateTime(2026, 1, 8),
                            PostingId = Guid.Empty,
                            PostingKind = PostingKind.Contact,
                            Description = "Streaming Provider 1",
                            BudgetPurposeName = "Streaming Provider",
                            AccountId = null,
                            AccountName = bankAccount.Name,
                            ContactId = stream1.Id,
                            ContactName = stream1.Name,
                            SavingsPlanId = null,
                            SavingsPlanName = null,
                            SecurityId = null,
                            SecurityName = null
                        },
                        new BudgetReportPostingRawDataDto()
                        {
                            Amount = -4.99m,
                            BookingDate = new DateTime(2026, 1, 8),
                            ValutaDate = new DateTime(2026, 1, 8),
                            PostingId = Guid.Empty,
                            PostingKind = PostingKind.Contact,
                            Description = "Streaming Provider 1",
                            BudgetPurposeName = "Streaming Provider",
                            AccountId = null,
                            AccountName = bankAccount.Name,
                            ContactId = stream1.Id,
                            ContactName = stream1.Name,
                            SavingsPlanId = null,
                            SavingsPlanName = null,
                            SecurityId = null,
                            SecurityName = null
                        },
                        new BudgetReportPostingRawDataDto()
                        {
                            Amount = -0.02m,
                            BookingDate = new DateTime(2026, 1, 15),
                            ValutaDate = new DateTime(2026, 1, 15),
                            PostingId = Guid.Empty,
                            PostingKind = PostingKind.Contact,
                            Description = "Streaming Provider 1",
                            BudgetPurposeName = "Streaming Provider",
                            AccountId = null,
                            AccountName = bankAccount.Name,
                            ContactId = stream1.Id,
                            ContactName = stream1.Name,
                            SavingsPlanId = null,
                            SavingsPlanName = null,
                            SecurityId = null,
                            SecurityName = null
                        }
                    }
                },
                // Recurring Expense 10 posting
                new BudgetReportPurposeRawDataDto
                {
                    PurposeId = Guid.Empty,
                    PurposeName = "Recurring Expense 10",
                    BudgetedExpense = -18.36m,
                    BudgetedTarget = -18.36m,
                    BudgetSourceType = BudgetSourceType.SavingsPlan,
                    SourceId = recurringPlans.First(p => p.Name == "Recurring Expense 10").Id,
                    SourceName = recurringPlans.First(p => p.Name == "Recurring Expense 10").Name,
                    Postings = new[]
                    {
                        new BudgetReportPostingRawDataDto()
                        {
                            Amount = -18.36m,
                            BookingDate = new DateTime(2026, 1, 2),
                            ValutaDate = new DateTime(2026, 1, 2),
                            PostingId = Guid.Empty,
                            PostingKind = PostingKind.Contact,
                            Description = "Recurring Expense 10",
                            BudgetPurposeName = "Recurring Expense 10",
                            AccountId = null,
                            AccountName = bankAccount.Name,
                            ContactId = selfContactId,
                            ContactName = "Me",
                            SavingsPlanId = recurringPlans.First(p => p.Name == "Recurring Expense 10").Id,
                            SavingsPlanName = recurringPlans.First(p => p.Name == "Recurring Expense 10").Name,
                            SecurityId = null,
                            SecurityName = null
                        }
                    }
                },
                // Recurring Expense 11, 15
                new BudgetReportPurposeRawDataDto
                {
                    PurposeId = Guid.Empty,
                    PurposeName = "Recurring Expense 11",
                    BudgetedExpense = -60m,
                    BudgetedTarget = -60m,
                    BudgetSourceType = BudgetSourceType.SavingsPlan,
                    SourceId = recurringPlans.First(p => p.Name == "Recurring Expense 11").Id,
                    SourceName = recurringPlans.First(p => p.Name == "Recurring Expense 11").Name,
                    Postings = Array.Empty<BudgetReportPostingRawDataDto>()
                },
                new BudgetReportPurposeRawDataDto
                {
                    PurposeId = Guid.Empty,
                    PurposeName = "Recurring Expense 15",
                    BudgetedExpense = -5m,
                    BudgetedTarget = -5m,
                    BudgetSourceType = BudgetSourceType.SavingsPlan,
                    SourceId = recurringPlans.First(p => p.Name == "Recurring Expense 15").Id,
                    SourceName = recurringPlans.First(p => p.Name == "Recurring Expense 15").Name,
                    Postings = Array.Empty<BudgetReportPostingRawDataDto>()
                },
                // Recurring Expense 2 postings
                new BudgetReportPurposeRawDataDto
                {
                    PurposeId = Guid.Empty,
                    PurposeName = "Recurring Expense 2",
                    BudgetedIncome = 39.01m,
                    BudgetedTarget = 26.00m,
                    BudgetedExpense = -13.01m,
                    BudgetSourceType = BudgetSourceType.SavingsPlan,
                    SourceId = recurringPlans.First(p => p.Name == "Recurring Expense 2").Id,
                    SourceName = recurringPlans.First(p => p.Name == "Recurring Expense 2").Name,
                    Postings = new[]
                    {
                        new BudgetReportPostingRawDataDto()
                        {
                            Amount = -13.01m,
                            BookingDate = new DateTime(2026, 1, 2),
                            ValutaDate = new DateTime(2026, 1, 2),
                            PostingId = Guid.Empty,
                            PostingKind = PostingKind.Contact,
                            Description = "Recurring Expense 2",
                            BudgetPurposeName = "Recurring Expense 2",
                            AccountId = null,
                            AccountName = bankAccount.Name,
                            ContactId = selfContactId,
                            ContactName = "Me",
                            SavingsPlanId = recurringPlans.First(p => p.Name == "Recurring Expense 2").Id,
                            SavingsPlanName = recurringPlans.First(p => p.Name == "Recurring Expense 2").Name,
                            SecurityId = null,
                            SecurityName = null
                        },
                        new BudgetReportPostingRawDataDto()
                        {
                            Amount = 39.01m,
                            BookingDate = new DateTime(2026, 1, 2),
                            ValutaDate = new DateTime(2026, 1, 2),
                            PostingId = Guid.Empty,
                            PostingKind = PostingKind.Contact,
                            Description = "Recurring Expense 2",
                            BudgetPurposeName = "Recurring Expense 2",
                            AccountId = null,
                            AccountName = bankAccount.Name,
                            ContactId = selfContactId,
                            ContactName = "Me",
                            SavingsPlanId = recurringPlans.First(p => p.Name == "Recurring Expense 2").Id,
                            SavingsPlanName = recurringPlans.First(p => p.Name == "Recurring Expense 2").Name,
                            SecurityId = null,
                            SecurityName = null
                        }
                    }
                },
                // Recurring Expense 4 posting
                new BudgetReportPurposeRawDataDto
                {
                    PurposeId = Guid.Empty,
                    PurposeName = "Recurring Expense 4",
                    BudgetedExpense = -5.21m,
                    BudgetedTarget = -5.21m,
                    BudgetSourceType = BudgetSourceType.SavingsPlan,
                    SourceId = recurringPlans.First(p => p.Name == "Recurring Expense 4").Id,
                    SourceName = recurringPlans.First(p => p.Name == "Recurring Expense 4").Name,
                    Postings = new[]
                    {
                        new BudgetReportPostingRawDataDto()
                        {
                            Amount = -5.21m,
                            BookingDate = new DateTime(2026, 1, 2),
                            ValutaDate = new DateTime(2026, 1, 2),
                            PostingId = Guid.Empty,
                            PostingKind = PostingKind.Contact,
                            Description = "Recurring Expense 4",
                            BudgetPurposeName = "Recurring Expense 4",
                            AccountId = null,
                            AccountName = bankAccount.Name,
                            ContactId = selfContactId,
                            ContactName = "Me",
                            SavingsPlanId = recurringPlans.First(p => p.Name == "Recurring Expense 4").Id,
                            SavingsPlanName = recurringPlans.First(p => p.Name == "Recurring Expense 4").Name,
                            SecurityId = null,
                            SecurityName = null
                        }
                    }
                },
                // Recurring Expense 6 (no posting here)
                new BudgetReportPurposeRawDataDto
                {
                    PurposeId = Guid.Empty,
                    PurposeName = "Recurring Expense 6",
                    BudgetedExpense = -4.63m,
                    BudgetedTarget = -4.63m,
                    BudgetSourceType = BudgetSourceType.SavingsPlan,
                    SourceId = recurringPlans.First(p => p.Name == "Recurring Expense 6").Id,
                    SourceName = recurringPlans.First(p => p.Name == "Recurring Expense 6").Name,
                    Postings = new[]
                    {
                        new BudgetReportPostingRawDataDto()
                        {
                            Amount = -4.63m,
                            BookingDate = new DateTime(2026, 1, 2),
                            ValutaDate = new DateTime(2026, 1, 2),
                            PostingId = Guid.Empty,
                            PostingKind = PostingKind.Contact,
                            Description = "Recurring Expense 6",
                            BudgetPurposeName = "Recurring Expense 6",
                            AccountId = null,
                            AccountName = bankAccount.Name,
                            ContactId = selfContactId,
                            ContactName = "Me",
                            SavingsPlanId = recurringPlans.First(p => p.Name == "Recurring Expense 6").Id,
                            SavingsPlanName = recurringPlans.First(p => p.Name == "Recurring Expense 6").Name,
                            SecurityId = null,
                            SecurityName = null
                        }
                    }
                },
                // Recurring Expense 7 posting
                new BudgetReportPurposeRawDataDto
                {
                    PurposeId = Guid.Empty,
                    PurposeName = "Recurring Expense 7",
                    BudgetedIncome = 99.00m,
                    BudgetedExpense = -8.25m,
                    BudgetedTarget = 90.75m,
                    BudgetSourceType = BudgetSourceType.SavingsPlan,
                    SourceId = recurringPlans.First(p => p.Name == "Recurring Expense 7").Id,
                    SourceName = recurringPlans.First(p => p.Name == "Recurring Expense 7").Name,
                    Postings = new[]
                    {
                        new BudgetReportPostingRawDataDto()
                        {
                            Amount = 99.00m,
                            BookingDate = new DateTime(2026, 1, 2),
                            ValutaDate = new DateTime(2026, 1, 2),
                            PostingId = Guid.Empty,
                            PostingKind = PostingKind.Contact,
                            Description = "Recurring Expense 7",
                            BudgetPurposeName = "Recurring Expense 7",
                            AccountId = null,
                            AccountName = bankAccount.Name,
                            ContactId = selfContactId,
                            ContactName = "Me",
                            SavingsPlanId = recurringPlans.First(p => p.Name == "Recurring Expense 7").Id,
                            SavingsPlanName = recurringPlans.First(p => p.Name == "Recurring Expense 7").Name,
                            SecurityId = null,
                            SecurityName = null
                        }
                    }
                },
                // Recurring Expense 8 postings
                new BudgetReportPurposeRawDataDto
                {
                    PurposeId = Guid.Empty,
                    PurposeName = "Recurring Expense 8",
                    BudgetedIncome = 11.46m,
                    BudgetedExpense = -3.82m,
                    BudgetedTarget = 7.64m,
                    BudgetSourceType = BudgetSourceType.SavingsPlan,
                    SourceId = recurringPlans.First(p => p.Name == "Recurring Expense 8").Id,
                    SourceName = recurringPlans.First(p => p.Name == "Recurring Expense 8").Name,
                    Postings = new[]
                    {
                        new BudgetReportPostingRawDataDto()
                        {
                            Amount = -3.82m,
                            BookingDate = new DateTime(2026, 1, 2),
                            ValutaDate = new DateTime(2026, 1, 2),
                            PostingId = Guid.Empty,
                            PostingKind = PostingKind.Contact,
                            Description = "Recurring Expense 8",
                            BudgetPurposeName = "Recurring Expense 8",
                            AccountId = null,
                            AccountName = bankAccount.Name,
                            ContactId = selfContactId,
                            ContactName = "Me",
                            SavingsPlanId = recurringPlans.First(p => p.Name == "Recurring Expense 8").Id,
                            SavingsPlanName = recurringPlans.First(p => p.Name == "Recurring Expense 8").Name,
                            SecurityId = null,
                            SecurityName = null
                        },
                        new BudgetReportPostingRawDataDto()
                        {
                            Amount = 11.46m,
                            BookingDate = new DateTime(2026, 1, 2),
                            ValutaDate = new DateTime(2026, 1, 2),
                            PostingId = Guid.Empty,
                            PostingKind = PostingKind.Contact,
                            Description = "Recurring Expense 8",
                            BudgetPurposeName = "Recurring Expense 8",
                            AccountId = null,
                            AccountName = bankAccount.Name,
                            ContactId = selfContactId,
                            ContactName = "Me",
                            SavingsPlanId = recurringPlans.First(p => p.Name == "Recurring Expense 8").Id,
                            SavingsPlanName = recurringPlans.First(p => p.Name == "Recurring Expense 8").Name,
                            SecurityId = null,
                            SecurityName = null
                        }
                    }
                },
                // Other postings: savingsplan 3, Recurring Expense 5 and 9
                new BudgetReportPurposeRawDataDto
                {
                    PurposeId = Guid.Empty,
                    PurposeName = "Recurring Expense 5",
                    BudgetedExpense = -10.5m,
                    BudgetedTarget = -10.5m,
                    BudgetSourceType = BudgetSourceType.SavingsPlan,
                    SourceId = recurringPlans.First(p => p.Name == "Recurring Expense 5").Id,
                    SourceName = recurringPlans.First(p => p.Name == "Recurring Expense 5").Name,
                    Postings = new[]
                    {
                        new BudgetReportPostingRawDataDto()
                        {
                            Amount = -10.5m,
                            BookingDate = new DateTime(2026, 1, 2),
                            ValutaDate = new DateTime(2026, 1, 2),
                            PostingId = Guid.Empty,
                            PostingKind = PostingKind.Contact,
                            Description = "Recurring Expense 5",
                            BudgetPurposeName = "Recurring Expense 5",
                            AccountId = null,
                            AccountName = bankAccount.Name,
                            ContactId = selfContactId,
                    ContactName = "Me",
                    SavingsPlanId = recurringPlans.First(p => p.Name == "Recurring Expense 5").Id,
                    SavingsPlanName = recurringPlans.First(p => p.Name == "Recurring Expense 5").Name,
                            SecurityId = null,
                            SecurityName = null
                        }
                    }
                },
            },
            UnbudgetedPostings = new BudgetReportPostingRawDataDto[]
            {
                // Anlage 1 (one-time savingsplan) - unbudgeted outflow
                new BudgetReportPostingRawDataDto()
                {
                    Amount = -3.81m,
                    BookingDate = new DateTime(2026, 1, 27),
                    ValutaDate = new DateTime(2026, 1, 27),
                    PostingId = Guid.Empty,
                    PostingKind = PostingKind.Contact,
                    Description = "Investment 1",
                    AccountId = null,
                    AccountName = bankAccount.Name,
                    ContactId = selfContactId,
                    ContactName = "Me",
                    SavingsPlanId = savingsPlan.Id,
                    SavingsPlanName = savingsPlan.Name,
                    SecurityId = null,
                    SecurityName = null
                },
                // Service Contract 1 - unbudgeted outflow
                new BudgetReportPostingRawDataDto()
                {
                    Amount = -8m,
                    BookingDate = new DateTime(2026, 1, 27),
                    ValutaDate = new DateTime(2026, 1, 27),
                    PostingId = Guid.Empty,
                    PostingKind = PostingKind.Contact,
                    Description = "Service Contract 1",
                    AccountId = null,
                    AccountName = bankAccount.Name,
                    ContactId = selfContactId,
                    ContactName = "Me",
                    SavingsPlanId = serviceContractPlan.Id,
                    SavingsPlanName = serviceContractPlan.Name,
                    SecurityId = null,
                    SecurityName = null
                },
                // savingsplan 3 income (unbudgeted posting)
                new BudgetReportPostingRawDataDto()
                {
                    Amount = 350.00m,
                    BookingDate = new DateTime(2026, 1, 2),
                    ValutaDate = new DateTime(2026, 1, 2),
                    PostingId = Guid.Empty,
                    PostingKind = PostingKind.Contact,
                    Description = "Savings Plan 3",
                    AccountId = null,
                    AccountName = bankAccount.Name,
                    ContactId = selfContactId,
                    ContactName = "Me",
                            SavingsPlanId = smallPlans.First(p => p.Name == "Savings Plan 3").Id,
                            SavingsPlanName = smallPlans.First(p => p.Name == "Savings Plan 3").Name,
                    SecurityId = null,
                    SecurityName = null
                },
                // savingsplan 2 contribution (unbudgeted posting)
                new BudgetReportPostingRawDataDto()
                {
                    Amount = -200.00m,
                    BookingDate = new DateTime(2026, 1, 5),
                    ValutaDate = new DateTime(2026, 1, 5),
                    PostingId = Guid.Empty,
                    PostingKind = PostingKind.Contact,
                    Description = "Savings Plan 2",
                    AccountId = null,
                    AccountName = bankAccount.Name,
                    ContactId = selfContactId,
                    ContactName = "Me",
                    SavingsPlanId = smallPlans.First(p => p.Name == "Savings Plan 2").Id,
                    SavingsPlanName = smallPlans.First(p => p.Name == "Savings Plan 2").Name,
                    SecurityId = null,
                    SecurityName = null
                },
                // Dividend Stock 1 (unbudgeted income) - valuta date set to 15.01.2026
                new BudgetReportPostingRawDataDto()
                {
                    Amount = 5.90m,
                    BookingDate = new DateTime(2026, 1, 20),
                    ValutaDate = new DateTime(2026, 1, 15),
                    PostingId = Guid.Empty,
                    PostingKind = PostingKind.Contact,
                    Description = "Dividend Stock 1",
                    AccountId = null,
                    AccountName = bankAccount.Name,
                    ContactId = bankContact.Id,
                    ContactName = bankContact.Name,
                    SavingsPlanId = null,
                    SavingsPlanName = null,
                    SecurityId = security.Id,
                    SecurityName = security.Name
                },
                // New dividend postings
                new BudgetReportPostingRawDataDto()
                {
                    Amount = 0.07m,
                    BookingDate = new DateTime(2026, 1, 2),
                    ValutaDate = new DateTime(2026, 1, 2),
                    PostingId = Guid.Empty,
                    PostingKind = PostingKind.Contact,
                    Description = "Dividend Stock 6",
                    AccountId = null,
                    AccountName = bankAccount.Name,
                    ContactId = bankContact.Id,
                    ContactName = bankContact.Name,
                    SavingsPlanId = null,
                    SavingsPlanName = null,
                    SecurityId = security6.Id,
                    SecurityName = security6.Name
                },
                new BudgetReportPostingRawDataDto()
                {
                    Amount = 17.66m,
                    BookingDate = new DateTime(2026, 1, 5),
                    ValutaDate = new DateTime(2026, 1, 5),
                    PostingId = Guid.Empty,
                    PostingKind = PostingKind.Contact,
                    Description = "Dividend Stock 7",
                    AccountId = null,
                    AccountName = bankAccount.Name,
                    ContactId = bankContact.Id,
                    ContactName = bankContact.Name,
                    SavingsPlanId = null,
                    SavingsPlanName = null,
                    SecurityId = security7.Id,
                    SecurityName = security7.Name
                },
                new BudgetReportPostingRawDataDto()
                {
                    Amount = 8.26m,
                    BookingDate = new DateTime(2026, 1, 9),
                    ValutaDate = new DateTime(2026, 1, 9),
                    PostingId = Guid.Empty,
                    PostingKind = PostingKind.Contact,
                    Description = "Dividend Stock 8",
                    AccountId = null,
                    AccountName = bankAccount.Name,
                    ContactId = bankContact.Id,
                    ContactName = bankContact.Name,
                    SavingsPlanId = null,
                    SavingsPlanName = null,
                    SecurityId = security8.Id,
                    SecurityName = security8.Name
                },
                new BudgetReportPostingRawDataDto()
                {
                    Amount = 13.47m,
                    BookingDate = new DateTime(2026, 1, 12),
                    ValutaDate = new DateTime(2026, 1, 12),
                    PostingId = Guid.Empty,
                    PostingKind = PostingKind.Contact,
                    Description = "Dividend Stock 9",
                    AccountId = null,
                    AccountName = bankAccount.Name,
                    ContactId = bankContact.Id,
                    ContactName = bankContact.Name,
                    SavingsPlanId = null,
                    SavingsPlanName = null,
                    SecurityId = security9.Id,
                    SecurityName = security9.Name
                },
                new BudgetReportPostingRawDataDto()
                {
                    Amount = 5.10m,
                    BookingDate = new DateTime(2026, 1, 19),
                    ValutaDate = new DateTime(2026, 1, 19),
                    PostingId = Guid.Empty,
                    PostingKind = PostingKind.Contact,
                    Description = "Dividend Stock 10",
                    AccountId = null,
                    AccountName = bankAccount.Name,
                    ContactId = bankContact.Id,
                    ContactName = bankContact.Name,
                    SavingsPlanId = null,
                    SavingsPlanName = null,
                    SecurityId = security10.Id,
                    SecurityName = security10.Name
                },
                new BudgetReportPostingRawDataDto()
                {
                    Amount = 17.61m,
                    BookingDate = new DateTime(2026, 1, 20),
                    ValutaDate = new DateTime(2026, 1, 20),
                    PostingId = Guid.Empty,
                    PostingKind = PostingKind.Contact,
                    Description = "Dividend Stock 11",
                    AccountId = null,
                    AccountName = bankAccount.Name,
                    ContactId = bankContact.Id,
                    ContactName = bankContact.Name,
                    SavingsPlanId = null,
                    SavingsPlanName = null,
                    SecurityId = security11.Id,
                    SecurityName = security11.Name
                },
                // Purchases of securities (unbudgeted purchases)
                new BudgetReportPostingRawDataDto()
                {
                    Amount = -500.00m,
                    BookingDate = new DateTime(2026, 1, 2),
                    ValutaDate = new DateTime(2026, 1, 2),
                    PostingId = Guid.Empty,
                    PostingKind = PostingKind.Contact,
                    Description = "Buy Stock 3",
                    AccountId = null,
                    AccountName = bankAccount.Name,
                    ContactId = bankContact.Id,
                    ContactName = bankContact.Name,
                    SavingsPlanId = null,
                    SavingsPlanName = null,
                    SecurityId = security3.Id,
                    SecurityName = security3.Name
                },
                new BudgetReportPostingRawDataDto()
                {
                    Amount = -150.00m,
                    BookingDate = new DateTime(2026, 1, 2),
                    ValutaDate = new DateTime(2026, 1, 2),
                    PostingId = Guid.Empty,
                    PostingKind = PostingKind.Contact,
                    Description = "Buy Stock 4",
                    AccountId = null,
                    AccountName = bankAccount.Name,
                    ContactId = bankContact.Id,
                    ContactName = bankContact.Name,
                    SavingsPlanId = null,
                    SavingsPlanName = null,
                    SecurityId = security4.Id,
                    SecurityName = security4.Name
                },
                // Bank 3 one-time income (unbudgeted)
                new BudgetReportPostingRawDataDto()
                {
                    Amount = 4.80m,
                    BookingDate = new DateTime(2026, 1, 9),
                    ValutaDate = new DateTime(2026, 1, 9),
                    PostingId = Guid.Empty,
                    PostingKind = PostingKind.Contact,
                    Description = "Bank 3",
                    AccountId = null,
                    AccountName = bankAccount.Name,
                    ContactId = bankContact3.Id,
                    ContactName = bankContact3.Name,
                    SavingsPlanId = null,
                    SavingsPlanName = null,
                    SecurityId = null,
                    SecurityName = null
                },
                // Dienstleister 10 (unbudgeted outflow)
                new BudgetReportPostingRawDataDto()
                {
                    Amount = -4.00m,
                    BookingDate = new DateTime(2026, 1, 9),
                    ValutaDate = new DateTime(2026, 1, 9),
                    PostingId = Guid.Empty,
                    PostingKind = PostingKind.Contact,
                    Description = "Service Provider 10",
                    AccountId = null,
                    AccountName = bankAccount.Name,
                    ContactId = serviceProvider10.Id,
                    ContactName = serviceProvider10.Name,
                    SavingsPlanId = null,
                    SavingsPlanName = null,
                    SecurityId = null,
                    SecurityName = null
                },
                // Canteen 1 (unbudgeted outflow)
                new BudgetReportPostingRawDataDto()
                {
                    Amount = -5.25m,
                    BookingDate = new DateTime(2026, 1, 12),
                    ValutaDate = new DateTime(2026, 1, 12),
                    PostingId = Guid.Empty,
                    PostingKind = PostingKind.Contact,
                    Description = "Canteen 1",
                    AccountId = null,
                    AccountName = bankAccount.Name,
                    ContactId = canteenContact.Id,
                    ContactName = canteenContact.Name,
                    SavingsPlanId = null,
                    SavingsPlanName = null,
                    SecurityId = null,
                    SecurityName = null
                },
                // Gas Station 1 remainder (unbudgeted outflow of 5.59 after assigning 50 to budget)
                new BudgetReportPostingRawDataDto()
                {
                    Amount = -5.59m,
                    BookingDate = new DateTime(2026, 1, 12),
                    ValutaDate = new DateTime(2026, 1, 12),
                    PostingId = Guid.Empty,
                    PostingKind = PostingKind.Contact,
                    Description = "Gas Station 1",
                    AccountId = null,
                    AccountName = bankAccount.Name,
                    ContactId = tank1.Id,
                    ContactName = tank1.Name,
                    BudgetPurposeName = "Fuel",
                    SavingsPlanId = null,
                    SavingsPlanName = null,
                    SecurityId = null,
                    SecurityName = null
                },
                new BudgetReportPostingRawDataDto()
                {
                    Amount = -25.00m,
                    BookingDate = new DateTime(2026, 1, 2),
                    ValutaDate = new DateTime(2026, 1, 2),
                    PostingId = Guid.Empty,
                    PostingKind = PostingKind.Contact,
                    Description = "Buy Stock 5",
                    AccountId = null,
                    AccountName = bankAccount.Name,
                    ContactId = bankContact.Id,
                    ContactName = bankContact.Name,
                    SavingsPlanId = null,
                    SavingsPlanName = null,
                    SecurityId = security5.Id,
                    SecurityName = security5.Name
                },
                // Dividend Stock 1 (unbudgeted income) - booking 02.01.2026, valuta 31.12.2025
                new BudgetReportPostingRawDataDto()
                {
                    Amount = 8.23m,
                    BookingDate = new DateTime(2026, 1, 2),
                    ValutaDate = new DateTime(2025, 12, 31),
                    PostingId = Guid.Empty,
                    PostingKind = PostingKind.Contact,
                    Description = "Dividend Stock 2",
                    AccountId = null,
                    AccountName = bankAccount.Name,
                    ContactId = bankContact.Id,
                    ContactName = bankContact.Name,
                    SavingsPlanId = null,
                    SavingsPlanName = null,
                    SecurityId = security2.Id,
                    SecurityName = security2.Name
                },
                // Lotterie income on 07.01.2026 (unbudgeted)
                new BudgetReportPostingRawDataDto()
                {
                    Amount = 5.00m,
                    BookingDate = new DateTime(2026, 1, 7),
                    ValutaDate = new DateTime(2026, 1, 7),
                    PostingId = Guid.Empty,
                    PostingKind = PostingKind.Contact,
                    Description = "Lottery",
                    BudgetCategoryName = "Lottery Company",
                    BudgetPurposeName = "Lottery",
                    AccountId = null,
                    AccountName = bankAccount.Name,
                    ContactId = lotteryContact.Id,
                    ContactName = lotteryContact.Name,
                    SavingsPlanId = null,
                    SavingsPlanName = null,
                    SecurityId = null,
                    SecurityName = null
                },
                // Lotterie income on 20.01.2026 (unbudgeted)
                new BudgetReportPostingRawDataDto()
                {
                    Amount = 5.00m,
                    BookingDate = new DateTime(2026, 1, 20),
                    ValutaDate = new DateTime(2026, 1, 20),
                    PostingId = Guid.Empty,
                    PostingKind = PostingKind.Contact,
                    Description = "Lottery",
                    BudgetCategoryName = "Lottery Company",
                    BudgetPurposeName = "Lottery",
                    AccountId = null,
                    AccountName = bankAccount.Name,
                    ContactId = lotteryContact.Id,
                    ContactName = lotteryContact.Name,
                    SavingsPlanId = null,
                    SavingsPlanName = null,
                    SecurityId = null,
                    SecurityName = null
                },
                // Remaining category-level portion of the Lotterie (-25.5 actual minus -15 budget) -> -10 unbudgeted
                new BudgetReportPostingRawDataDto()
                {
                    Amount = -10.50m,
                    BookingDate = new DateTime(2026, 1, 20),
                    ValutaDate = new DateTime(2026, 1, 20),
                    PostingId = Guid.Empty,
                    PostingKind = PostingKind.Contact,
                    Description = "Lottery",
                    BudgetCategoryName = "Lottery Company",
                    BudgetPurposeName = "Lottery",
                    AccountId = null,
                    AccountName = bankAccount.Name,
                    ContactId = lotteryContact.Id,
                    ContactName = lotteryContact.Name,
                    SavingsPlanId = null,
                    SavingsPlanName = null,
                    SecurityId = null,
                    SecurityName = null
                },
                // Unbudgeted posting for Recurring Expense 9 (10.00)
                new BudgetReportPostingRawDataDto()
                {
                    Amount = 10.00m,
                    BookingDate = new DateTime(2026, 1, 2),
                    ValutaDate = new DateTime(2026, 1, 2),
                    PostingId = Guid.Empty,
                    PostingKind = PostingKind.Contact,
                    Description = "Recurring Expense 9",
                    AccountId = null,
                    AccountName = bankAccount.Name,
                    ContactId = selfContactId,
                    ContactName = "Me",
                    SavingsPlanId = recurringPlans.First(p => p.Name == "Recurring Expense 9").Id,
                    SavingsPlanName = recurringPlans.First(p => p.Name == "Recurring Expense 9").Name,
                    SecurityId = null,
                    SecurityName = null
                },
                // 2 cent overage for Recurring Expense 2 (unbudgeted positive difference)
                new BudgetReportPostingRawDataDto()
                {
                    Amount = 0.02m,
                    BookingDate = new DateTime(2026, 1, 2),
                    ValutaDate = new DateTime(2026, 1, 2),
                    PostingId = Guid.Empty,
                    PostingKind = PostingKind.Contact,
                            Description = "Recurring Expense 2",
                            BudgetPurposeName = "Recurring Expense 2",
                    AccountId = null,
                    AccountName = bankAccount.Name,
                    ContactId = selfContactId,
                    ContactName = "Me",
                    SavingsPlanId = recurringPlans.First(p => p.Name == "Recurring Expense 2").Id,
                    SavingsPlanName = recurringPlans.First(p => p.Name == "Recurring Expense 2")?.Name,
                    SecurityId = null,
                    SecurityName = null
                },
                // Aggregated unbudgeted streaming charges
                new BudgetReportPostingRawDataDto()
                {
                    Amount = -5.98m,
                    BookingDate = new DateTime(2026, 1, 15),
                    ValutaDate = new DateTime(2026, 1, 15),
                    PostingId = Guid.Empty,
                    PostingKind = PostingKind.Contact,
                    Description = "Streaming Provider 1",
                    BudgetPurposeName = "Streaming Provider",
                    AccountId = null,
                    AccountName = bankAccount.Name,
                    ContactId = stream1.Id,
                    ContactName = stream1.Name,
                    SavingsPlanId = null,
                    SavingsPlanName = null,
                    SecurityId = null,
                    SecurityName = null
                },
                // Salary remainder (actual salary minus budgeted portion) - unbudgeted income
                new BudgetReportPostingRawDataDto()
                {
                    Amount = 5767.89m - 3326.46m,
                    BookingDate = new DateTime(2026, 1, 27),
                    ValutaDate = new DateTime(2026, 1, 27),
                    PostingId = Guid.Empty,
                    PostingKind = PostingKind.Contact,
                    Description = "Salary",
                    BudgetCategoryName = "Work",
                    BudgetPurposeName = "Salary",
                    AccountId = null,
                    AccountName = bankAccount.Name,
                    ContactId = employerContact.Id,
                    ContactName = "Employer",
                    SavingsPlanId = null,
                    SavingsPlanName = null,
                    SecurityId = null,
                    SecurityName = null
                }
                ,
            }
        };
        var expectedUnbudgetedPostings = string.Join("\r\n",
            expected.UnbudgetedPostings
                .OrderBy(p => p.ContactName)
                .ThenBy(p => p.SavingsPlanName)
                .ThenBy(p => p.Amount)
                .Select(p => string.Format(CultureInfo.CurrentCulture, "{0:F2}, {1}, {2}", p.Amount, p.ContactName, p.SavingsPlanName)));

        var actualUnbudgetPostings = string.Join("\r\n",
            actual.UnbudgetedPostings
                .OrderBy(p => p.ContactName)
                .ThenBy(p => p.SavingsPlanName)
                .ThenBy(p => p.Amount)
                .Select(p => string.Format(CultureInfo.CurrentCulture, "{0:F2}, {1}, {2}", p.Amount, p.ContactName, p.SavingsPlanName)));

        actualUnbudgetPostings.Should().Be(expectedUnbudgetedPostings);

        string FormatPosting(BudgetReportPostingRawDataDto pp) => string.Format(CultureInfo.CurrentCulture, "{0:F2}, {1}, {2}", pp.Amount, pp.ContactName, pp.SavingsPlanName);

        var expecteduncategorizedPurposes = string.Join("\r\n",
            expected.UncategorizedPurposes
                .OrderBy(p => p.PurposeName)
                .Select(p => string.Format(CultureInfo.CurrentCulture, "{0:F2}:-{1:F2} - {2}, \r\n  {3}", p.BudgetedIncome, p.BudgetedExpense, p.PurposeName,
                    string.Join("\r\n  ", p.Postings.Select(pp => FormatPosting(pp))))));

        var actualUncategorizedPurposes = string.Join("\r\n",
            actual.UncategorizedPurposes
                .OrderBy(p => p.PurposeName)
                .Select(p => string.Format(CultureInfo.CurrentCulture, "{0:F2}:-{1:F2} - {2}, \r\n  {3}", p.BudgetedIncome, p.BudgetedExpense, p.PurposeName,
                    string.Join("\r\n  ", p.Postings.Select(pp => FormatPosting(pp))))));

        actualUncategorizedPurposes.Should().Be(expecteduncategorizedPurposes);

        var expectedCategorizedPorposes = string.Join("\r\n",
            expected.Categories.OrderBy(c => c.CategoryName).Select(c => string.Format(CultureInfo.CurrentCulture, "{0} ({1:F2}):\r\n  {2}",
                c.CategoryName, c.BudgetedExpense,
                string.Join("\r\n  ", c.Purposes.Select(p => string.Format(CultureInfo.CurrentCulture, "{0:F2}:-{1:F2} - {2}, \r\n    {3}",
                    p.BudgetedIncome, p.BudgetedExpense, p.PurposeName,
                    string.Join("\r\n    ", p.Postings.OrderBy(pp => pp.BookingDate).ThenBy(pp => pp.Amount).Select(pp => FormatPosting(pp)))))))));

        var actualCategorizedPurposes = string.Join("\r\n",
            actual.Categories.OrderBy(c => c.CategoryName).Select(c => string.Format(CultureInfo.CurrentCulture, "{0} ({1:F2}):\r\n  {2}",
                c.CategoryName, c.BudgetedExpense,
                string.Join("\r\n  ", c.Purposes.Select(p => string.Format(CultureInfo.CurrentCulture, "{0:F2}:-{1:F2} - {2}, \r\n    {3}",
                    p.BudgetedIncome, p.BudgetedExpense, p.PurposeName,
                    string.Join("\r\n    ", p.Postings.OrderBy(pp => pp.BookingDate).ThenBy(pp => pp.Amount).Select(pp => FormatPosting(pp)))))))));

        actualCategorizedPurposes.Should().Be(expectedCategorizedPorposes);

        actual.Should().BeEquivalentTo(expected, opts => opts
            .WithoutStrictOrdering()
            // ignore identifier properties which are generated during setup
            .Excluding(info => info.Path.EndsWith("PostingId"))
            .Excluding(info => info.Path.EndsWith("CategoryId"))
            //.Excluding(info => info.Path.EndsWith("PurposeId")));
            .Excluding(info => info.Path.EndsWith("PurposeId"))
            .Excluding(info => info.Path.EndsWith("SourceId"))
            .Excluding(info => info.Path.EndsWith("AccountId"))
            .Excluding(info => info.Path.EndsWith("ContactId"))
            .Excluding(info => info.Path.EndsWith("SavingsPlanId"))
            .Excluding(info => info.Path.EndsWith("SecurityId")));        

        // Full-object equivalence removed — test asserts focused properties above.
        // Diagnostic output: print postings for Recurring Expense 2 to help identify
        // why a small remaining amount might be included inside purpose postings.
        var diagW2 = actual.UncategorizedPurposes.FirstOrDefault(p => p.PurposeName == "Recurring Expense 2");
        if (diagW2 != null)
        {
            Console.WriteLine("DIAG: Purpose 'Recurring Expense 2' postings:");
            foreach (var pp in diagW2.Postings ?? Array.Empty<FinanceManager.Shared.Dtos.Budget.BudgetReportPostingRawDataDto>())
            {
                Console.WriteLine($"  PostingId={pp.PostingId}, Amount={pp.Amount}, SavingsPlanId={pp.SavingsPlanId}, BookingDate={pp.BookingDate}, Description='{pp.Description}'");
            }
        }

        // Ensure that trivial leftover (0.02) is not present inside the purpose postings
        diagW2.Should().NotBeNull();
        diagW2!.Postings.Should().NotContain(p => p.Amount == 0.02m, "small residuals must appear only in UnbudgetedPostings");

        var actualKPIValuta = await brs.GetMonthlyKpiAsync(ownerUserId, to, BudgetReportDateBasis.ValutaDate, ct);
        var actualKPIBooking = await brs.GetMonthlyKpiAsync(ownerUserId, to, BudgetReportDateBasis.BookingDate, ct);

        // Compose component sums so derived properties are clearly expressed as additions
        decimal plannedExpense =
            13.01m /* Recurring Expense 2 (monthly negative component) */
            + 649.42m /* Rent (monthly) */
            + 50m /* Fuel (contact group gas station, monthly) */
            + 3.82m /* Recurring Expense 8 (monthly negative component) */
            + 99m /* Auto Club (yearly, applies in Jan) */
            + 5m /* Recurring Expense 15 (monthly) */
            + 15m /* Gym (monthly) */
            + 75m /* Utilities (monthly) */
            + 10.5m /* Recurring Expense 5 */
            + 60m /* Recurring Expense 11 (monthly: -60) */
            + 39.03m /* Insurance 6 (quarterly, applies in Jan) */
            + 10m /* savingsplan 5 (monthly) */
            + 381.6m /* Insurance 8 (yearly) */
            + 4.63m /* Recurring Expense 6 (monthly) */
            + 54.13m /* Telecom Provider 1 (monthly) */
            + 8.25m /* Recurring Expense 7 (monthly -8.25) */
            + 5.21m /* Recurring Expense 4 (monthly) */
            + 18.36m /* Recurring Expense 10 (monthly) */
            + 139m /* savingsplan 4 (monthly) */
            + 15m /* Lottery category budget (monthly) */
            + 20.93m /* Insurance 5 (monthly) */
            + 11.46m /* Insurance 7 (monthly) */
            + 10m /* Streaming Provider (contact group) */
            + 20.64m /* Insurance 4 (monthly) */
            + 31.8m /* Recurring Expense 3 (monthly negative component) */
            + 500m /* Shopping & Food category-level budget */
            ;
        // Summe (PlannedExpenseAbs) = 2250.79m

        decimal unbudgetedExpense =
            3.81m /* Anlage 1 (unbudgeted outflow) */
            + 8m /* Service Contract 1 (unbudgeted outflow) */
            + 10.5m /* Remaining category-level portion of Lotterie (unbudgeted) */
            + 500m /* Unbudgeted purchase of security 3 (treated as expense) */
            + 150m /* Unbudgeted purchase of security 4 (treated as expense) */
            + 25m /* Unbudgeted purchase of security 5 (treated as expense) */
            + 200m /* savingsplan 2 (unbudgeted) */
            + 5.98m /* Remaining unbudgeted streaming charges */
            + 4.00m /* Dienstleister 10 (unbudgeted outflow) */
            + 5.25m /* Kantine 1 (unbudgeted outflow) */
            + 5.59m /* Gas Station 1 (unbudgeted remainder after budget allocation of 50) */
            ;
        // Summe (UnbudgetedExpenseAbs) = 918.13m

        decimal budgetedRealizedExpense =
            15m /* Category budget Lottery is realized */
            + 50m /* Fuel (budget realized from Fuel 1: 50 of 55.59) */
            + 4.63m /* Recurring Expense 6 (realized) */
            + 3.82m /* Recurring Expense 8 (realized) */
            + 381.60m /* Insurance 8 (yearly) */
            + 5.21m /* Recurring Expense 4 (realized) */
            + 18.36m /* Recurring Expense 10 (realized) */
            + 10.5m /* Recurring Expense 5 (realized) */
            + 13.01m /* Recurring Expense 2 (realized) */
            + 10m /* savingsplan 5 */
            + 139m /* savingsplan 4 (realized) */
            + 99m /* Auto Club (realized) */
            + 11.46m /* Insurance 7 (realized) */
            + 39.03m /* Insurance 6 (realized) */
            + 15m /* Gym (realized) */
            + 20.93m /* Insurance 5 (realized) */
            + 20.64m /* Insurance 4 (realized) */
            + 649.42m /* Rent (realized) */
            + 20.09m /* Supermarket 6 */
            + 11.19m /* Supermarket 7 (05.01) */
            + 9.36m /* Supermarket 7 (07.01) */
            + 11.40m /* Supermarket 6 (14.01) */
            + 10.37m /* Supermarket 8 (06.01) */
            + 75m /* Utilities (realized) */
            + 10m /* Streaming Provider realized (two -4.99 + -0.02 to match monthly budget) */
            ;
        // Summe (BudgetedRealizedExpenseAbs) = 1654.02m

        decimal unbudgetedIncome =
            5.90m /* Dividend (20.01) */
            + 8.23m /* Dividend (02.01, valuta 31.12.2025) */
            + 5.00m /* Lottery gain on 20.01.2026 */
            + 5.00m /* Lottery gain on 07.01.2026 */
            + 10.00m /* Recurring Expense 9 */
            + 0.02m /* Difference Recurring Expense 2 (unbudgeted residual) */
            + 0.07m /* Dividend Stock 6 */
            + 17.66m /* Dividend Stock 7 */
            + 8.26m /* Dividend Stock 8 */
            + 13.47m /* Dividend Stock 9 */
            + 5.10m /* Dividend Stock 10 */
            + 17.61m /* Dividend Stock 11 */
            + 350m /* savingsplan 3 */
            + 4.80m /* Bank 3 one-time income */
            + (5767.89m - 3326.46m) /* Gehaltsrest (actual salary minus planned portion) */
            ;
        // Summe (UnbudgetedIncome) = 2892.55m

        decimal budgetedRealizedIncome =
            39.01m /* Recurring Expense 2 (realized with monthly amount) */
            + 3326.46m /* Gehalt (monthly planned) */
            + 99m /* Recurring Expense 7 (yearly positive component) */
            + 372.92m /* Recurring Expense 3 (yearly, applies in Jan) */
            + 11.46m /* Recurring Expense 8 (quarterly 11.46) */
            ;
        // Summe (BudgetedRealizedIncome) = 3848.85m

        decimal plannedIncome =
            3326.46m /* Gehalt */
            + 39.01m /* Recurring Expense 2 (monthly positive component) */
            + 99.00m /* Recurring Expense 7 (yearly positive component) */
            + 372.92m /* Recurring Expense 3 (yearly) */
            + 11.46m /* Recurring Expense 8 (quarterly) */
            ;
        // Summe (PlannedIncome) = 3848.85m

        decimal actualExpense = unbudgetedExpense + budgetedRealizedExpense;
        // Summe (ActualExpenseAbs) = 2571.15m
        decimal actualIncome = unbudgetedIncome + budgetedRealizedIncome;
        // Summe (ActualIncome) = 6736.60m

        // Adjust RemainingPlannedExpenseAbs sum comment
        // Summe (RemainingPlannedExpenseAbs) = PlannedExpenseAbs - BudgetedRealizedExpenseAbs = 2250.79m - 1604.02m = 646.77m        
        var expectedKPIBooking = new MonthlyBudgetKpiDto()
        {
            PlannedExpenseAbs = plannedExpense,
            PlannedIncome = plannedIncome,
            PlannedResult = plannedIncome - plannedExpense,

            UnbudgetedExpenseAbs = unbudgetedExpense,
            // exclude Dividend Stock 2 when using ValutaDate
            UnbudgetedIncome = unbudgetedIncome,

            BudgetedRealizedExpenseAbs = budgetedRealizedExpense,
            BudgetedRealizedIncome = budgetedRealizedIncome,

            // Actual = unbudgeted + budgeted realized (adjusted for excluded valuta posting)
            ActualExpenseAbs = actualExpense,
            ActualIncome = budgetedRealizedIncome + (unbudgetedIncome),
            ActualResult = (budgetedRealizedIncome + (unbudgetedIncome)) - actualExpense,

            // Remaining planned = planned - already realized (both available as sums above)
            RemainingPlannedExpenseAbs = plannedExpense - budgetedRealizedExpense,
            RemainingPlannedIncome = plannedIncome - budgetedRealizedIncome,

            ExpectedExpenseAbs = actualExpense + (plannedExpense - budgetedRealizedExpense),
            ExpectedIncome = budgetedRealizedIncome + (unbudgetedIncome),
            ExpectedTargetResult = (budgetedRealizedIncome + (unbudgetedIncome)) - (actualExpense + (plannedExpense - budgetedRealizedExpense))
        };
        // For ValutaDate basis the dividend with valuta 2025-12-31 must be excluded (Dividend Stock 2 = 8.23)
        var excludedValutaAmount = 8.23m;
        var expectedKPIValuta = expectedKPIBooking with
        {
            UnbudgetedIncome = expectedKPIBooking.UnbudgetedIncome - excludedValutaAmount,
            ActualIncome = expectedKPIBooking.ActualIncome - excludedValutaAmount,
            ActualResult = expectedKPIBooking.ActualResult - excludedValutaAmount,
            ExpectedIncome = expectedKPIBooking.ExpectedIncome - excludedValutaAmount,
            ExpectedTargetResult = expectedKPIBooking.ExpectedTargetResult - excludedValutaAmount
        };

        // debug output to help diagnose KPI mismatches
        Console.WriteLine("Actual KPI: " + JsonSerializer.Serialize(actualKPIValuta));
        actualKPIValuta.Should().BeEquivalentTo(expectedKPIValuta);
        actualKPIBooking.Should().BeEquivalentTo(expectedKPIBooking);
        #endregion
    }
}

