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
}
