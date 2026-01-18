using FinanceManager.Domain.Budget;
using FinanceManager.Domain.Postings;
using FinanceManager.Domain.Users;
using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Budget;
using FinanceManager.Shared.Dtos.Budget;
using FinanceManager.Shared.Dtos.Contacts;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FinanceManager.Tests.Budget;

/// <summary>
/// Unit tests for <see cref="BudgetReportService"/>.
/// These tests validate budget aggregation behavior (period totals, category/purpose rollups)
/// and the correctness of the derived synthetic rows (Sum/Unbudgeted/Result) across different
/// source types and date bases.
/// </summary>
public sealed class BudgetReportServiceTests
{
    private static async Task<AppDbContext> CreateDbAsync(Guid ownerId)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        var db = new AppDbContext(options);
        await db.Database.OpenConnectionAsync();
        await db.Database.EnsureCreatedAsync();

        var user = new User("test", "hash");
        user.Id = ownerId;
        db.Users.Add(user);
        await db.SaveChangesAsync();

        return db;
    }

    private static async Task<Guid> CreateSelfContactAsync(AppDbContext db, Guid ownerId)
    {
        var self = new FinanceManager.Domain.Contacts.Contact(ownerId, "Self", ContactType.Self, categoryId: null);
        db.Contacts.Add(self);
        await db.SaveChangesAsync();
        return self.Id;
    }

    private static async Task<Guid> CreateContactAsync(AppDbContext db, Guid ownerId, string name)
    {
        var c = new FinanceManager.Domain.Contacts.Contact(ownerId, name, ContactType.Person, categoryId: null);
        db.Contacts.Add(c);
        await db.SaveChangesAsync();
        return c.Id;
    }

    private static async Task<Guid> CreateContactGroupAsync(AppDbContext db, Guid ownerId, string name)
    {
        var g = new FinanceManager.Domain.Contacts.ContactCategory(ownerId, name);
        db.ContactCategories.Add(g);
        await db.SaveChangesAsync();
        return g.Id;
    }

    private static async Task AddContactToGroupAsync(AppDbContext db, Guid contactId, Guid groupId)
    {
        var c = await db.Contacts.FirstAsync(x => x.Id == contactId);
        c.SetCategory(groupId);
        await db.SaveChangesAsync();
    }

    private static async Task AddContactPostingAsync(AppDbContext db, Guid contactId, DateTime bookingDate, DateTime valutaDate, decimal amount)
    {
        var p = new Posting(Guid.NewGuid(), PostingKind.Contact, accountId: null, contactId: contactId, savingsPlanId: null, securityId: null, bookingDate, valutaDate, amount, null, null, null, subType: null);
        db.Postings.Add(p);
        await db.SaveChangesAsync();
    }

    private static async Task<Guid> CreateSavingsPlanAsync(AppDbContext db, Guid ownerId, string name)
    {
        var sp = new FinanceManager.Domain.Savings.SavingsPlan(
            ownerId,
            name,
            FinanceManager.Shared.Dtos.SavingsPlans.SavingsPlanType.Recurring,
            targetAmount: null,
            targetDate: null,
            interval: null,
            categoryId: null);
        db.SavingsPlans.Add(sp);
        await db.SaveChangesAsync();
        return sp.Id;
    }

    private static async Task AddSavingsPlanPostingAsync(AppDbContext db, Guid savingsPlanId, Guid? contactId, DateTime bookingDate, DateTime valutaDate, decimal amount)
    {
        var p = new Posting(Guid.NewGuid(), PostingKind.SavingsPlan, accountId: null, contactId: contactId, savingsPlanId: savingsPlanId, securityId: null, bookingDate, valutaDate, amount, null, null, null, subType: null);
        db.Postings.Add(p);
        await db.SaveChangesAsync();
    }

    private static async Task AddSelfContactPostingAsync(AppDbContext db, Guid selfContactId, DateTime bookingDate, DateTime valutaDate, decimal amount)
    {
        var p = new Posting(Guid.NewGuid(), PostingKind.Contact, accountId: null, contactId: selfContactId, savingsPlanId: null, securityId: null, bookingDate, valutaDate, amount, null, null, null, subType: null);
        db.Postings.Add(p);
        await db.SaveChangesAsync();
    }

    private static BudgetReportRequest CreateDefaultRequest(DateOnly asOf, int months = 1, BudgetReportValueScope scope = BudgetReportValueScope.TotalRange, BudgetReportDateBasis dateBasis = BudgetReportDateBasis.BookingDate)
        => new(
            AsOfDate: asOf,
            Months: months,
            Interval: BudgetReportInterval.Month,
            ShowTitle: false,
            ShowLineChart: false,
            ShowMonthlyTable: false,
            ShowDetailsTable: true,
            CategoryValueScope: scope,
            IncludePurposeRows: true,
            DateBasis: dateBasis);

    [Fact]
    /// <summary>
    /// Verifies that an empty system (no budget purposes and no postings) produces a report with zero totals
    /// and does not emit any category/detail rows.
    /// </summary>
    public async Task GetAsync_ShouldReturnZeroValues_WhenNoPurposesAndNoPostings()
    {
        var ownerId = Guid.NewGuid();
        await using var db = await CreateDbAsync(ownerId);
        await CreateSelfContactAsync(db, ownerId);

        var purposeSvc = new BudgetPurposeService(db);
        var catSvc = new BudgetCategoryService(db, purposeSvc);
        var svc = new BudgetReportService(purposeSvc, catSvc, db);

        var req = CreateDefaultRequest(new DateOnly(2026, 1, 31), months: 1);
        var dto = await svc.GetAsync(ownerId, req, CancellationToken.None);

        dto.Periods.Should().HaveCount(1);
        dto.Periods[0].Budget.Should().Be(0m);
        dto.Periods[0].Actual.Should().Be(0m);

        dto.Categories.Should().BeEmpty();
    }

    [Fact]
    /// <summary>
    /// Verifies that a single purpose with a monthly rule contributes to the budget totals even when there are
    /// no postings in the selected period.
    /// </summary>
    public async Task GetAsync_ShouldAggregateSinglePurposeRule_WithoutActualPostings()
    {
        var ownerId = Guid.NewGuid();
        await using var db = await CreateDbAsync(ownerId);
        await CreateSelfContactAsync(db, ownerId);

        var contactId = await CreateContactAsync(db, ownerId, "Alice");

        var purposeSvc = new BudgetPurposeService(db);
        var catSvc = new BudgetCategoryService(db, purposeSvc);
        var ruleSvc = new BudgetRuleService(db);
        var reportSvc = new BudgetReportService(purposeSvc, catSvc, db);

        var purpose = await purposeSvc.CreateAsync(ownerId, "P1", BudgetSourceType.Contact, contactId, null, null, CancellationToken.None);
        await ruleSvc.CreateAsync(ownerId, purpose.Id, 100m, BudgetIntervalType.Monthly, null, new DateOnly(2026, 1, 1), null, CancellationToken.None);

        var req = CreateDefaultRequest(new DateOnly(2026, 1, 31), months: 1);
        var dto = await reportSvc.GetAsync(ownerId, req, CancellationToken.None);

        dto.Periods.Should().HaveCount(1);
        dto.Periods[0].Budget.Should().Be(100m);
        dto.Periods[0].Actual.Should().Be(0m);

        dto.Categories.Should().ContainSingle(x => x.Kind == BudgetReportCategoryRowKind.Data && x.Name == "(Unassigned)").Which.Budget.Should().Be(100m);
        dto.Categories.Should().ContainSingle(x => x.Kind == BudgetReportCategoryRowKind.Sum).Which.Budget.Should().Be(100m);
        dto.Categories.Should().ContainSingle(x => x.Kind == BudgetReportCategoryRowKind.Result).Which.Budget.Should().Be(100m);
        dto.Categories.Should().NotContain(x => x.Kind == BudgetReportCategoryRowKind.Unbudgeted);
    }

    [Fact]
    /// <summary>
    /// Verifies that the report emits an Unbudgeted row when there are contact postings that are not covered
    /// by any purpose, and that the Result row equals the sum of budgeted actuals and unbudgeted actuals.
    /// </summary>
    public async Task GetAsync_ShouldIncludeUnbudgetedRow_WhenContactPostingsExistOutsidePurposes()
    {
        var ownerId = Guid.NewGuid();
        await using var db = await CreateDbAsync(ownerId);
        await CreateSelfContactAsync(db, ownerId);

        var contactBudgeted = await CreateContactAsync(db, ownerId, "Budgeted");
        var contactUnbudgeted = await CreateContactAsync(db, ownerId, "Unbudgeted");

        var purposeSvc = new BudgetPurposeService(db);
        var catSvc = new BudgetCategoryService(db, purposeSvc);
        var ruleSvc = new BudgetRuleService(db);
        var reportSvc = new BudgetReportService(purposeSvc, catSvc, db);

        var purpose = await purposeSvc.CreateAsync(ownerId, "P1", BudgetSourceType.Contact, contactBudgeted, null, null, CancellationToken.None);
        await ruleSvc.CreateAsync(ownerId, purpose.Id, 10m, BudgetIntervalType.Monthly, null, new DateOnly(2026, 1, 1), null, CancellationToken.None);

        // Budgeted posting (covered by purpose)
        await AddContactPostingAsync(db, contactBudgeted, new DateTime(2026, 1, 10), new DateTime(2026, 1, 10), amount: 7m);
        // Unbudgeted posting (no purpose)
        await AddContactPostingAsync(db, contactUnbudgeted, new DateTime(2026, 1, 11), new DateTime(2026, 1, 11), amount: 5m);

        var req = CreateDefaultRequest(new DateOnly(2026, 1, 31), months: 1);
        var dto = await reportSvc.GetAsync(ownerId, req, CancellationToken.None);

        dto.Periods[0].Budget.Should().Be(10m);
        dto.Periods[0].Actual.Should().Be(7m);

        var unbudgeted = dto.Categories.Should().ContainSingle(x => x.Kind == BudgetReportCategoryRowKind.Unbudgeted).Which;
        unbudgeted.Actual.Should().Be(5m);

        var result = dto.Categories.Should().ContainSingle(x => x.Kind == BudgetReportCategoryRowKind.Result).Which;
        result.Actual.Should().Be(12m);
    }

    [Fact]
    /// <summary>
    /// Verifies that purposes assigned to a category are grouped correctly and that the category actual equals
    /// the sum of the purpose actuals within that category.
    /// </summary>
    public async Task GetAsync_ShouldGroupPurposesByCategory_AndCalculateCategoryActuals()
    {
        var ownerId = Guid.NewGuid();
        await using var db = await CreateDbAsync(ownerId);
        await CreateSelfContactAsync(db, ownerId);

        var contactA = await CreateContactAsync(db, ownerId, "A");
        var contactB = await CreateContactAsync(db, ownerId, "B");

        var purposeSvc = new BudgetPurposeService(db);
        var catSvc = new BudgetCategoryService(db, purposeSvc);
        var ruleSvc = new BudgetRuleService(db);
        var reportSvc = new BudgetReportService(purposeSvc, catSvc, db);

        var category = await catSvc.CreateAsync(ownerId, "Cat1", CancellationToken.None);

        var p1 = await purposeSvc.CreateAsync(ownerId, "P1", BudgetSourceType.Contact, contactA, null, category.Id, CancellationToken.None);
        var p2 = await purposeSvc.CreateAsync(ownerId, "P2", BudgetSourceType.Contact, contactB, null, category.Id, CancellationToken.None);

        await ruleSvc.CreateAsync(ownerId, p1.Id, 100m, BudgetIntervalType.Monthly, null, new DateOnly(2026, 1, 1), null, CancellationToken.None);
        await ruleSvc.CreateAsync(ownerId, p2.Id, 50m, BudgetIntervalType.Monthly, null, new DateOnly(2026, 1, 1), null, CancellationToken.None);

        await AddContactPostingAsync(db, contactA, new DateTime(2026, 1, 5), new DateTime(2026, 1, 5), 80m);
        await AddContactPostingAsync(db, contactB, new DateTime(2026, 1, 6), new DateTime(2026, 1, 6), 40m);

        var req = CreateDefaultRequest(new DateOnly(2026, 1, 31), months: 1);
        var dto = await reportSvc.GetAsync(ownerId, req, CancellationToken.None);

        dto.Periods[0].Budget.Should().Be(150m);
        dto.Periods[0].Actual.Should().Be(120m);

        dto.Categories.Should().ContainSingle(x => x.Kind == BudgetReportCategoryRowKind.Data && x.Id == category.Id)
            .Which
            .Should()
            .Match<BudgetReportCategoryDto>(x => x.Budget == 150m && x.Actual == 120m);
    }

    [Fact]
    /// <summary>
    /// Verifies that the report uses booking date or valuta date depending on the requested date basis,
    /// affecting which postings are included in the period totals.
    /// </summary>
    public async Task GetAsync_ShouldUseValutaDate_WhenRequested()
    {
        var ownerId = Guid.NewGuid();
        await using var db = await CreateDbAsync(ownerId);
        await CreateSelfContactAsync(db, ownerId);

        var contactId = await CreateContactAsync(db, ownerId, "Alice");

        var purposeSvc = new BudgetPurposeService(db);
        var catSvc = new BudgetCategoryService(db, purposeSvc);
        var ruleSvc = new BudgetRuleService(db);
        var reportSvc = new BudgetReportService(purposeSvc, catSvc, db);

        var purpose = await purposeSvc.CreateAsync(ownerId, "P1", BudgetSourceType.Contact, contactId, null, null, CancellationToken.None);
        await ruleSvc.CreateAsync(ownerId, purpose.Id, 0m, BudgetIntervalType.Monthly, null, new DateOnly(2026, 1, 1), null, CancellationToken.None);

        // booking date in range, valuta date outside
        await AddContactPostingAsync(db, contactId, new DateTime(2026, 1, 15), new DateTime(2026, 2, 1), 10m);

        var reqBooking = CreateDefaultRequest(new DateOnly(2026, 1, 31), months: 1, dateBasis: BudgetReportDateBasis.BookingDate);
        var dtoBooking = await reportSvc.GetAsync(ownerId, reqBooking, CancellationToken.None);
        dtoBooking.Periods[0].Actual.Should().Be(10m);

        var reqValuta = CreateDefaultRequest(new DateOnly(2026, 1, 31), months: 1, dateBasis: BudgetReportDateBasis.ValutaDate);
        var dtoValuta = await reportSvc.GetAsync(ownerId, reqValuta, CancellationToken.None);
        dtoValuta.Periods[0].Actual.Should().Be(0m);
    }

    [Fact]
    /// <summary>
    /// Verifies that contact-group purposes and per-contact purposes do not double-count actuals when computing
    /// the Unbudgeted row. If a contact belongs to a budgeted group, its individual purpose must not be counted
    /// in addition to the group purpose.
    /// </summary>
    public async Task GetAsync_ShouldHandleGroupPurposeOverlaps_ForUnbudgetedComputation()
    {
        var ownerId = Guid.NewGuid();
        await using var db = await CreateDbAsync(ownerId);
        await CreateSelfContactAsync(db, ownerId);

        var groupId = await CreateContactGroupAsync(db, ownerId, "Group");
        var c1 = await CreateContactAsync(db, ownerId, "C1");
        var c2 = await CreateContactAsync(db, ownerId, "C2");
        await AddContactToGroupAsync(db, c1, groupId);
        await AddContactToGroupAsync(db, c2, groupId);

        var purposeSvc = new BudgetPurposeService(db);
        var catSvc = new BudgetCategoryService(db, purposeSvc);
        var reportSvc = new BudgetReportService(purposeSvc, catSvc, db);

        // One purpose on group + one purpose on a member contact.
        await purposeSvc.CreateAsync(ownerId, "GroupPurpose", BudgetSourceType.ContactGroup, groupId, null, null, CancellationToken.None);
        await purposeSvc.CreateAsync(ownerId, "ContactPurpose", BudgetSourceType.Contact, c1, null, null, CancellationToken.None);

        // Postings for both contacts
        await AddContactPostingAsync(db, c1, new DateTime(2026, 1, 10), new DateTime(2026, 1, 10), 5m);
        await AddContactPostingAsync(db, c2, new DateTime(2026, 1, 11), new DateTime(2026, 1, 11), 7m);

        var req = CreateDefaultRequest(new DateOnly(2026, 1, 31), months: 1);
        var dto = await reportSvc.GetAsync(ownerId, req, CancellationToken.None);

        // Without overlap correction, purposeActualTotal would undercount; with correction unbudgeted should be 0.
        dto.Categories.Should().NotContain(x => x.Kind == BudgetReportCategoryRowKind.Unbudgeted);
    }

    [Fact]
    /// <summary>
    /// Verifies that when the details table is configured for <see cref="BudgetReportValueScope.LastInterval"/>,
    /// the Unbudgeted calculation considers only postings from the last interval (currently the month of the to-date),
    /// even if the report range spans multiple months.
    /// </summary>
    public async Task GetAsync_ShouldComputeUnbudgetedForLastIntervalOnly_WhenValueScopeIsLastInterval()
    {
        var ownerId = Guid.NewGuid();
        await using var db = await CreateDbAsync(ownerId);
        await CreateSelfContactAsync(db, ownerId);

        var contactBudgeted = await CreateContactAsync(db, ownerId, "Budgeted");
        var contactUnbudgeted = await CreateContactAsync(db, ownerId, "Unbudgeted");

        var purposeSvc = new BudgetPurposeService(db);
        var catSvc = new BudgetCategoryService(db, purposeSvc);
        var ruleSvc = new BudgetRuleService(db);
        var reportSvc = new BudgetReportService(purposeSvc, catSvc, db);

        // Ensure there is at least one purpose so the report emits detail rows (Sum/Unbudgeted/Result).
        var purpose = await purposeSvc.CreateAsync(ownerId, "P1", BudgetSourceType.Contact, contactBudgeted, null, null, CancellationToken.None);
        await ruleSvc.CreateAsync(ownerId, purpose.Id, 0m, BudgetIntervalType.Monthly, null, new DateOnly(2026, 1, 1), null, CancellationToken.None);

        // Posting inside the full range but outside the last interval (Dec 2025)
        await AddContactPostingAsync(db, contactUnbudgeted, new DateTime(2025, 12, 15), new DateTime(2025, 12, 15), amount: 10m);
        // Posting inside the last interval (Jan 2026)
        await AddContactPostingAsync(db, contactUnbudgeted, new DateTime(2026, 1, 10), new DateTime(2026, 1, 10), amount: 5m);

        var req = CreateDefaultRequest(new DateOnly(2026, 1, 31), months: 12, scope: BudgetReportValueScope.LastInterval);
        var dto = await reportSvc.GetAsync(ownerId, req, CancellationToken.None);

        var unbudgeted = dto.Categories.Should().ContainSingle(x => x.Kind == BudgetReportCategoryRowKind.Unbudgeted).Which;
        unbudgeted.Actual.Should().Be(5m);
    }

    [Fact]
    /// <summary>
    /// Regression test for the interaction between savings-plan purposes and self-contact purposes.
    /// The scenario models an insurance accrual savings plan (monthly -5 plus one-time +60) mirrored as self-contact postings,
    /// plus an insurance payment on a separate insurance contact.
    /// Verifies the resulting period totals as well as the derived Unbudgeted row for the overall report.
    /// </summary>
    public async Task GetAsync_ShouldHandleSavingsPlanAndSelfContactOverlap_AndAnnualInsuranceContactPurpose()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        await using var db = await CreateDbAsync(ownerId);
        var selfContactId = await CreateSelfContactAsync(db, ownerId);

        var insuranceContactId = await CreateContactAsync(db, ownerId, "Insurance");
        var savingsPlanId = await CreateSavingsPlanAsync(db, ownerId, "Versicherung");

        var purposeSvc = new BudgetPurposeService(db);
        var catSvc = new BudgetCategoryService(db, purposeSvc);
        var ruleSvc = new BudgetRuleService(db);
        var reportSvc = new BudgetReportService(purposeSvc, catSvc, db);

        // Budget purpose on savings plan "Rückstellung Versicherung": -5 monthly + 60 monthly
        var spPurpose = await purposeSvc.CreateAsync(ownerId, "Rückstellung Versicherung", BudgetSourceType.SavingsPlan, savingsPlanId, null, null, CancellationToken.None);
        await ruleSvc.CreateAsync(ownerId, spPurpose.Id, -5m, BudgetIntervalType.Monthly, null, new DateOnly(2025, 2, 1), null, CancellationToken.None);
        await ruleSvc.CreateAsync(ownerId, spPurpose.Id, 60m, BudgetIntervalType.Monthly, null, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 1), CancellationToken.None);

        // Budget purpose for insurance contact: -60 yearly, expected in current month
        var contactPurpose = await purposeSvc.CreateAsync(ownerId, "Versicherung Jahresbeitrag", BudgetSourceType.Contact, insuranceContactId, null, null, CancellationToken.None);
        await ruleSvc.CreateAsync(ownerId, contactPurpose.Id, -60m, BudgetIntervalType.Yearly, null, new DateOnly(2026, 1, 1), null, CancellationToken.None);

        // Postings: last 12 months -5 on savings plan (and equivalent self-contact postings)
        var asOf = new DateOnly(2026, 1, 31);
        for (var i = 0; i < 12; i++)
        {
            var dt = new DateTime(2026, 1, 10).AddMonths(-i);
            await AddSavingsPlanPostingAsync(db, savingsPlanId, contactId: selfContactId, bookingDate: dt, valutaDate: dt, amount: 5m);
            await AddSelfContactPostingAsync(db, selfContactId, bookingDate: dt, valutaDate: dt, amount: -5m);
        }

        // Current month +60 on savings plan (and equivalent self-contact posting)
        var dt60 = new DateTime(2026, 1, 20);
        await AddSavingsPlanPostingAsync(db, savingsPlanId, contactId: selfContactId, bookingDate: dt60, valutaDate: dt60, amount: -60m);
        await AddSelfContactPostingAsync(db, selfContactId, bookingDate: dt60, valutaDate: dt60, amount: 60m);

        // Insurance contact payment -60 (current month)
        var dtPay = new DateTime(2026, 1, 25);
        await AddContactPostingAsync(db, insuranceContactId, bookingDate: dtPay, valutaDate: dtPay, amount: -60m);

        var req = CreateDefaultRequest(asOf, months: 12, scope: BudgetReportValueScope.TotalRange);

        // Act
        var dto = await reportSvc.GetAsync(ownerId, req, CancellationToken.None);

        // Assert
        dto.Periods.Should().HaveCount(12);
        dto.Periods.Should().ContainSingle(p => p.From == new DateOnly(2026, 1, 1)).Which.Actual.Should().Be(-5m + 60m - 60m);

        var unbudgeted = dto.Categories.Should().ContainSingle(x => x.Kind == BudgetReportCategoryRowKind.Unbudgeted).Which;
        unbudgeted.Actual.Should().Be(55m);

        // Also verify that there are no unbudgeted postings for the same window.
        // (We verify this via DB queries because the MVC controller depends on current user/localizer setup.)
        var fromDt = new DateTime(2025, 2, 1);
        var toDt = new DateTime(2026, 1, 31, 23, 59, 59);

        // Savings plan postings are considered covered by their savings plan purpose.
        var savingsPlanIds = new[] { savingsPlanId };
        var coveredSavingsPostingIds = await db.Postings.AsNoTracking()
            .Where(p => p.SavingsPlanId != null && savingsPlanIds.Contains(p.SavingsPlanId.Value))
            .Where(p => p.BookingDate >= fromDt && p.BookingDate <= toDt)
            .Select(p => p.Id)
            .ToListAsync();

        // Determine unbudgeted contact postings in the window: contact postings (excluding savings-plan-linked ones)
        // that are not covered by any contact purpose (insurance contact is covered).
        var coveredContactIds = new[] { insuranceContactId };
        var unbudgetedContactPostingIds = await db.Postings.AsNoTracking()
            .Where(p => p.Kind == PostingKind.Contact && p.ContactId != null && p.SavingsPlanId == null)
            .Where(p => p.BookingDate >= fromDt && p.BookingDate <= toDt)
            .Where(p => !coveredContactIds.Contains(p.ContactId!.Value))
            .Select(p => p.Id)
            .ToListAsync();

        // In this scenario, only self-contact mirror postings remain, but they must be fully offset by savings plan postings.
        unbudgetedContactPostingIds.Should().NotBeEmpty();
        coveredSavingsPostingIds.Should().NotBeEmpty();

        var unbudgetedSum = await db.Postings.AsNoTracking()
            .Where(p => unbudgetedContactPostingIds.Contains(p.Id))
            .SumAsync(p => (decimal?)p.Amount) ?? 0m;

        var coveredSavingsSum = await db.Postings.AsNoTracking()
            .Where(p => coveredSavingsPostingIds.Contains(p.Id))
            .SumAsync(p => (decimal?)p.Amount) ?? 0m;

        (unbudgetedSum - coveredSavingsSum).Should().Be(0m);
    }
}
