using FinanceManager.Domain.Accounts;
using FinanceManager.Domain.Contacts;
using FinanceManager.Domain.Postings;
using FinanceManager.Domain.Savings;
using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Aggregates;
using FinanceManager.Infrastructure.Reports;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace FinanceManager.Tests.Reports;

/// <summary>
/// Covers the kind-independent projection (<c>CompareProjection = true</c>) of the standard
/// aggregation path: bank, contact and savings plan entities project recurring prior-year
/// postings into the current window, multi-kind selections project every kind, entity leafs
/// roll up into category and type rows without double counting, and the filters/ownership
/// rules of the report apply to the projected events as well.
/// </summary>
public sealed class ReportAggregationCrossKindProjectionTests
{
    private static AppDbContext CreateDb()
    {
        var conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options;
        var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    private static async Task<FinanceManager.Domain.Users.User> AddUserAsync(AppDbContext db, string name)
    {
        var user = new FinanceManager.Domain.Users.User(name, "pw", false);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private static Account AddAccount(AppDbContext db, Guid ownerUserId, string name)
    {
        var account = new Account(ownerUserId, AccountType.Giro, name, null, Guid.NewGuid());
        db.Accounts.Add(account);
        return account;
    }

    private static Contact AddContact(AppDbContext db, Guid ownerUserId, string name, Guid? categoryId = null)
    {
        var contact = new Contact(ownerUserId, name, ContactType.Person, categoryId, null);
        db.Contacts.Add(contact);
        return contact;
    }

    private static SavingsPlan AddSavingsPlan(AppDbContext db, Guid ownerUserId, string name, Guid? categoryId = null)
    {
        var plan = new SavingsPlan(ownerUserId, name, SavingsPlanType.Open, null, null, null, categoryId);
        db.SavingsPlans.Add(plan);
        return plan;
    }

    private static Posting AddBankPosting(AppDbContext db, Account account, DateTime bookingDate, decimal amount, string subject = "Posting", DateTime? valutaDate = null)
    {
        var posting = new Posting(
            Guid.NewGuid(), PostingKind.Bank, account.Id, null, null, null,
            bookingDate, valutaDate ?? bookingDate, amount, subject, null, null, null);
        db.Postings.Add(posting);
        return posting;
    }

    private static Posting AddContactPosting(AppDbContext db, Contact contact, DateTime bookingDate, decimal amount, string subject = "Posting")
    {
        var posting = new Posting(
            Guid.NewGuid(), PostingKind.Contact, null, contact.Id, null, null,
            bookingDate, bookingDate, amount, subject, null, null, null);
        db.Postings.Add(posting);
        return posting;
    }

    private static Posting AddPlanPosting(AppDbContext db, SavingsPlan plan, DateTime bookingDate, decimal amount, string subject = "Posting", SavingsPlanPostingSubType? subType = null)
    {
        var posting = new Posting(
            Guid.NewGuid(), PostingKind.SavingsPlan, null, null, plan.Id, null,
            bookingDate, bookingDate, amount, subject, null, null, null);
        if (subType.HasValue)
        {
            posting.SetSavingsPlanSubType(subType.Value);
        }
        db.Postings.Add(posting);
        return posting;
    }

    private static async Task RebuildAggregatesAsync(AppDbContext db, Guid ownerUserId)
    {
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        await new PostingAggregateService(db).RebuildForUserAsync(ownerUserId, (_, _) => { }, TestContext.Current.CancellationToken);
    }

    private static ReportAggregationService CreateSut(AppDbContext db)
        => new(db, new NullLogger<ReportAggregationService>());

    /// <summary>
    /// A prior-year annual bank posting that is already booked again in the current period counts as
    /// confirmed: the projection equals the booked amount and no expected items are emitted.
    /// </summary>
    [Fact]
    public async Task QueryAsync_BankProjection_Annual_Confirmed()
    {
        using var db = CreateDb();
        var user = await AddUserAsync(db, "bank-annual-confirmed");
        var account = AddAccount(db, user.Id, "Giro");
        var analysis = new DateTime(2026, 5, 1);
        AddBankPosting(db, account, new DateTime(2025, 5, 10), 120m);
        AddBankPosting(db, account, new DateTime(2026, 5, 12), 130m);
        await RebuildAggregatesAsync(db, user.Id);

        var result = await CreateSut(db).QueryAsync(new ReportAggregationQuery(
            user.Id, PostingKind.Bank, ReportInterval.Month, 1,
            IncludeCategory: false, ComparePrevious: false, CompareYear: false, CompareProjection: true,
            AnalysisDate: analysis), CancellationToken.None);

        Assert.True(result.ComparedProjection);
        var point = result.Points.Single(p => p.GroupKey == $"Account:{account.Id}" && p.PeriodStart == analysis);
        Assert.Equal(130m, point.Amount);
        Assert.Equal(130m, point.ProjectionAmount);
        Assert.Null(point.ProjectionExpectedItems);
    }

    /// <summary>
    /// With an established monthly posting pattern (six or more regular monthly events in the prior
    /// year) the still-open analysis month receives an expectation, while an already-paid month of the
    /// current year is treated as confirmed.
    /// </summary>
    [Fact]
    public async Task QueryAsync_BankProjection_MonthlyPattern()
    {
        using var db = CreateDb();
        var user = await AddUserAsync(db, "bank-monthly");
        var account = AddAccount(db, user.Id, "Giro");
        for (var month = 1; month <= 7; month++)
        {
            AddBankPosting(db, account, new DateTime(2025, month, 10), 10m, "Rate");
        }
        foreach (var month in new[] { 1, 2, 3, 5, 6 })
        {
            AddBankPosting(db, account, new DateTime(2026, month, 10), 10m, "Rate");
        }
        await RebuildAggregatesAsync(db, user.Id);

        var result = await CreateSut(db).QueryAsync(new ReportAggregationQuery(
            user.Id, PostingKind.Bank, ReportInterval.Month, 7,
            IncludeCategory: false, ComparePrevious: false, CompareYear: false, CompareProjection: true,
            AnalysisDate: new DateTime(2026, 7, 1)), CancellationToken.None);

        var july = result.Points.Single(p => p.GroupKey == $"Account:{account.Id}" && p.PeriodStart == new DateTime(2026, 7, 1));
        Assert.Equal(0m, july.Amount);
        Assert.Equal(10m, july.ProjectionAmount);
        var expected = Assert.Single(july.ProjectionExpectedItems!);
        Assert.Equal(new DateTime(2026, 7, 10), expected.ExpectedDate);
    }

    /// <summary>
    /// Two prior-year postings in distinct quarters form a quarterly pattern: the still-open current
    /// quarter receives an expectation while elapsed quarters of the current year do not.
    /// </summary>
    [Fact]
    public async Task QueryAsync_ContactProjection_QuarterlyPattern()
    {
        using var db = CreateDb();
        var user = await AddUserAsync(db, "contact-quarterly");
        var contact = AddContact(db, user.Id, "Quarterly Contact");
        AddContactPosting(db, contact, new DateTime(2025, 3, 10), 60m, "Quartal");
        AddContactPosting(db, contact, new DateTime(2025, 6, 12), 60m, "Quartal");
        await RebuildAggregatesAsync(db, user.Id);

        var result = await CreateSut(db).QueryAsync(new ReportAggregationQuery(
            user.Id, PostingKind.Contact, ReportInterval.Month, 6,
            IncludeCategory: false, ComparePrevious: false, CompareYear: false, CompareProjection: true,
            AnalysisDate: new DateTime(2026, 6, 1)), CancellationToken.None);

        var june = result.Points.Single(p => p.GroupKey == $"Contact:{contact.Id}" && p.PeriodStart == new DateTime(2026, 6, 1));
        Assert.Equal(0m, june.Amount);
        Assert.Equal(60m, june.ProjectionAmount);
        Assert.Equal(new DateTime(2026, 6, 12), Assert.Single(june.ProjectionExpectedItems!).ExpectedDate);
        var march = result.Points.SingleOrDefault(p => p.GroupKey == $"Contact:{contact.Id}" && p.PeriodStart == new DateTime(2026, 3, 1));
        if (march is not null)
        {
            Assert.Null(march.ProjectionExpectedItems);
        }
    }

    /// <summary>
    /// A savings plan posting marked <see cref="SavingsPlanPostingSubType.ActualReport"/> counts as a
    /// normal recurring event: a single prior-year posting produces an annual expectation.
    /// </summary>
    [Fact]
    public async Task QueryAsync_SavingsPlanProjection_AnnualPattern_IncludesActualReport()
    {
        using var db = CreateDb();
        var user = await AddUserAsync(db, "savings-annual");
        var plan = AddSavingsPlan(db, user.Id, "Plan");
        var analysis = new DateTime(2026, 5, 1);
        AddPlanPosting(db, plan, new DateTime(2025, 5, 10), 80m, "Ist-Meldung", SavingsPlanPostingSubType.ActualReport);
        await RebuildAggregatesAsync(db, user.Id);

        var result = await CreateSut(db).QueryAsync(new ReportAggregationQuery(
            user.Id, PostingKind.SavingsPlan, ReportInterval.Month, 1,
            IncludeCategory: false, ComparePrevious: false, CompareYear: false, CompareProjection: true,
            AnalysisDate: analysis), CancellationToken.None);

        Assert.True(result.ComparedProjection);
        var point = result.Points.Single(p => p.GroupKey == $"SavingsPlan:{plan.Id}" && p.PeriodStart == analysis);
        Assert.Equal(80m, point.ProjectionAmount);
        var expected = Assert.Single(point.ProjectionExpectedItems!);
        Assert.Equal(PostingKind.SavingsPlan, expected.Kind);
        Assert.Equal(plan.Id, expected.EntityId);
    }

    /// <summary>
    /// With <see cref="SavingsPlanPostingSource.LinkedAccountPostings"/> the events of a linked plan
    /// come from the bank postings of its linked account, while the plan's own postings are shadowed;
    /// an unlinked plan keeps its own plan postings.
    /// </summary>
    [Fact]
    public async Task QueryAsync_SavingsPlanProjection_LinkedAccountPostings()
    {
        using var db = CreateDb();
        var user = await AddUserAsync(db, "savings-linked");
        var linkedPlan = AddSavingsPlan(db, user.Id, "Linked Plan");
        var plainPlan = AddSavingsPlan(db, user.Id, "Plain Plan");
        var contract = AddAccount(db, user.Id, "Contract Account");
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        db.SavingsPlanAccountLinks.Add(new SavingsPlanAccountLink(linkedPlan.Id, contract.Id, user.Id));
        var analysis = new DateTime(2026, 5, 1);
        AddBankPosting(db, contract, new DateTime(2025, 5, 10), 100m, "Contract Booking");
        AddPlanPosting(db, linkedPlan, new DateTime(2025, 5, 11), 50m, "Shadowed Own Posting");
        AddPlanPosting(db, plainPlan, new DateTime(2025, 5, 12), 60m, "Own Posting");
        await RebuildAggregatesAsync(db, user.Id);

        var result = await CreateSut(db).QueryAsync(new ReportAggregationQuery(
            user.Id, PostingKind.SavingsPlan, ReportInterval.Month, 1,
            IncludeCategory: false, ComparePrevious: false, CompareYear: false, CompareProjection: true,
            AnalysisDate: analysis,
            SavingsPlanPostingSource: SavingsPlanPostingSource.LinkedAccountPostings), CancellationToken.None);

        var linked = result.Points.Single(p => p.GroupKey == $"SavingsPlan:{linkedPlan.Id}" && p.PeriodStart == analysis);
        Assert.Equal(100m, linked.ProjectionAmount);
        var expected = Assert.Single(linked.ProjectionExpectedItems!);
        Assert.Equal(100m, expected.Amount);
        Assert.Equal(new DateTime(2025, 5, 10), expected.PriorYearDate);
        var plain = result.Points.Single(p => p.GroupKey == $"SavingsPlan:{plainPlan.Id}" && p.PeriodStart == analysis);
        Assert.Equal(60m, plain.ProjectionAmount);
    }

    /// <summary>
    /// A multi-kind report projects every selected kind independently: both the account and the
    /// contact leaf rows carry their own expectation and the per-kind type rows aggregate them.
    /// </summary>
    [Fact]
    public async Task QueryAsync_MultiKindProjection_ProjectsEachKind()
    {
        using var db = CreateDb();
        var user = await AddUserAsync(db, "multi-projection");
        var account = AddAccount(db, user.Id, "Giro");
        var contact = AddContact(db, user.Id, "Contact");
        var analysis = new DateTime(2026, 5, 1);
        AddBankPosting(db, account, new DateTime(2025, 5, 10), 120m);
        AddContactPosting(db, contact, new DateTime(2025, 5, 15), 80m);
        await RebuildAggregatesAsync(db, user.Id);

        var result = await CreateSut(db).QueryAsync(new ReportAggregationQuery(
            user.Id, PostingKind.Bank, ReportInterval.Month, 1,
            IncludeCategory: false, ComparePrevious: false, CompareYear: false, CompareProjection: true,
            PostingKinds: new[] { PostingKind.Bank, PostingKind.Contact },
            AnalysisDate: analysis), CancellationToken.None);

        Assert.True(result.ComparedProjection);
        Assert.Equal(120m, result.Points.Single(p => p.GroupKey == $"Account:{account.Id}" && p.PeriodStart == analysis).ProjectionAmount);
        Assert.Equal(80m, result.Points.Single(p => p.GroupKey == $"Contact:{contact.Id}" && p.PeriodStart == analysis).ProjectionAmount);
        Assert.Equal(120m, result.Points.Single(p => p.GroupKey == "Type:Bank" && p.PeriodStart == analysis).ProjectionAmount);
        Assert.Equal(80m, result.Points.Single(p => p.GroupKey == "Type:Contact" && p.PeriodStart == analysis).ProjectionAmount);
    }

    /// <summary>
    /// In a multi-kind report with categories, the expectation of a categorized contact rolls up
    /// exactly once: the category row carries it and the type row sums only the category row, never
    /// the leaf below it again.
    /// </summary>
    [Fact]
    public async Task QueryAsync_MultiKindProjection_TypeRollupCountsCategoryChildrenOnce()
    {
        using var db = CreateDb();
        var user = await AddUserAsync(db, "multi-category");
        var category = new ContactCategory(user.Id, "Private");
        db.ContactCategories.Add(category);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var account = AddAccount(db, user.Id, "Giro");
        var contact = AddContact(db, user.Id, "Categorized Contact", category.Id);
        var analysis = new DateTime(2026, 5, 1);
        AddBankPosting(db, account, new DateTime(2025, 5, 10), 120m);
        AddContactPosting(db, contact, new DateTime(2025, 5, 15), 80m);
        await RebuildAggregatesAsync(db, user.Id);

        var result = await CreateSut(db).QueryAsync(new ReportAggregationQuery(
            user.Id, PostingKind.Contact, ReportInterval.Month, 1,
            IncludeCategory: true, ComparePrevious: false, CompareYear: false, CompareProjection: true,
            PostingKinds: new[] { PostingKind.Bank, PostingKind.Contact },
            AnalysisDate: analysis), CancellationToken.None);

        Assert.True(result.ComparedProjection);
        var categoryKey = $"Category:Contact:{category.Id}";
        Assert.Equal(80m, result.Points.Single(p => p.GroupKey == categoryKey && p.PeriodStart == analysis).ProjectionAmount);
        var contactType = result.Points.Single(p => p.GroupKey == "Type:Contact" && p.PeriodStart == analysis);
        Assert.Equal(80m, contactType.ProjectionAmount);
        Assert.Equal(80m, contactType.ProjectionExpectedItems!.Sum(x => x.Amount));
    }

    /// <summary>
    /// In the standard path, security expectations land only on the dividend sub-type row of a
    /// security; the buy/sell rows of the same security stay without a projection amount.
    /// </summary>
    [Fact]
    public async Task QueryAsync_MultiKindProjection_SecurityExpectationOnDividendRowOnly()
    {
        using var db = CreateDb();
        var user = await AddUserAsync(db, "multi-security");
        var account = AddAccount(db, user.Id, "Giro");
        var security = new FinanceManager.Domain.Securities.Security(user.Id, "Sec", "ISIN-1", null, null, "EUR", null);
        db.Securities.Add(security);
        var analysis = new DateTime(2026, 5, 1);
        AddBankPosting(db, account, new DateTime(2025, 5, 10), 120m);
        var group = Guid.NewGuid();
        db.Postings.Add(new Posting(Guid.NewGuid(), PostingKind.Security, null, null, null, security.Id, new DateTime(2025, 5, 10), new DateTime(2025, 5, 10), 80m, "Dividend", null, null, SecurityPostingSubType.Dividend).SetGroup(group));
        db.Postings.Add(new Posting(Guid.NewGuid(), PostingKind.Security, null, null, null, security.Id, new DateTime(2024, 1, 1), new DateTime(2024, 1, 1), -200m, "Buy", null, null, SecurityPostingSubType.Buy, 10m));
        await RebuildAggregatesAsync(db, user.Id);

        var result = await CreateSut(db).QueryAsync(new ReportAggregationQuery(
            user.Id, PostingKind.Security, ReportInterval.Month, 1,
            IncludeCategory: false, ComparePrevious: false, CompareYear: false, CompareProjection: true,
            PostingKinds: new[] { PostingKind.Security, PostingKind.Bank },
            AnalysisDate: analysis), CancellationToken.None);

        Assert.True(result.ComparedProjection);
        var dividendRow = result.Points.Single(p => p.GroupKey == $"Security:{security.Id}:Dividend" && p.PeriodStart == analysis);
        Assert.Equal(80m, dividendRow.ProjectionAmount);
        var expected = Assert.Single(dividendRow.ProjectionExpectedItems!);
        Assert.Equal(PostingKind.Security, expected.Kind);
        Assert.Equal(security.Id, expected.EntityId);
        var buyRows = result.Points.Where(p => p.GroupKey == $"Security:{security.Id}:Buy");
        Assert.All(buyRows, p => Assert.Null(p.ProjectionAmount));
    }

    /// <summary>
    /// <c>AllHistory</c> reports never project: <c>ComparedProjection</c> stays false and no point
    /// carries a projection amount even with prior-year postings present.
    /// </summary>
    [Fact]
    public async Task QueryAsync_Projection_AllHistoryExcluded()
    {
        using var db = CreateDb();
        var user = await AddUserAsync(db, "all-history");
        var account = AddAccount(db, user.Id, "Giro");
        AddBankPosting(db, account, new DateTime(2025, 5, 10), 120m);
        await RebuildAggregatesAsync(db, user.Id);

        var result = await CreateSut(db).QueryAsync(new ReportAggregationQuery(
            user.Id, PostingKind.Bank, ReportInterval.AllHistory, 1,
            IncludeCategory: false, ComparePrevious: false, CompareYear: false, CompareProjection: true,
            AnalysisDate: new DateTime(2026, 5, 1)), CancellationToken.None);

        Assert.False(result.ComparedProjection);
        Assert.All(result.Points, p => Assert.Null(p.ProjectionAmount));
    }

    /// <summary>
    /// A single-kind contact report with categories rolls the leaf expectation up into the
    /// <c>Category:Contact:{id}</c> row, including the expected-item details.
    /// </summary>
    [Fact]
    public async Task QueryAsync_ContactProjection_CategoryRollup()
    {
        using var db = CreateDb();
        var user = await AddUserAsync(db, "contact-category");
        var category = new ContactCategory(user.Id, "Vendors");
        db.ContactCategories.Add(category);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var contact = AddContact(db, user.Id, "Categorized Contact", category.Id);
        var analysis = new DateTime(2026, 5, 1);
        AddContactPosting(db, contact, new DateTime(2025, 5, 15), 80m);
        await RebuildAggregatesAsync(db, user.Id);

        var result = await CreateSut(db).QueryAsync(new ReportAggregationQuery(
            user.Id, PostingKind.Contact, ReportInterval.Month, 1,
            IncludeCategory: true, ComparePrevious: false, CompareYear: false, CompareProjection: true,
            AnalysisDate: analysis), CancellationToken.None);

        var categoryPoint = result.Points.Single(p => p.GroupKey == $"Category:Contact:{category.Id}" && p.PeriodStart == analysis);
        Assert.Equal(80m, categoryPoint.ProjectionAmount);
        Assert.Single(categoryPoint.ProjectionExpectedItems!);
    }

    /// <summary>
    /// A single-kind savings plan report with categories rolls the leaf expectation up into the
    /// <c>Category:SavingsPlan:{id}</c> row, including the expected-item details.
    /// </summary>
    [Fact]
    public async Task QueryAsync_SavingsPlanProjection_CategoryRollup()
    {
        using var db = CreateDb();
        var user = await AddUserAsync(db, "savings-category");
        var category = new SavingsPlanCategory(user.Id, "Contracts");
        db.SavingsPlanCategories.Add(category);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var plan = AddSavingsPlan(db, user.Id, "Categorized Plan", category.Id);
        var analysis = new DateTime(2026, 5, 1);
        AddPlanPosting(db, plan, new DateTime(2025, 5, 15), 80m);
        await RebuildAggregatesAsync(db, user.Id);

        var result = await CreateSut(db).QueryAsync(new ReportAggregationQuery(
            user.Id, PostingKind.SavingsPlan, ReportInterval.Month, 1,
            IncludeCategory: true, ComparePrevious: false, CompareYear: false, CompareProjection: true,
            AnalysisDate: analysis), CancellationToken.None);

        var categoryPoint = result.Points.Single(p => p.GroupKey == $"Category:SavingsPlan:{category.Id}" && p.PeriodStart == analysis);
        Assert.Equal(80m, categoryPoint.ProjectionAmount);
        Assert.Single(categoryPoint.ProjectionExpectedItems!);
    }

    /// <summary>
    /// Bank text filters restrict both the displayed rows and the projected events: only postings
    /// matching at least one filter pattern produce expectations.
    /// </summary>
    [Fact]
    public async Task QueryAsync_BankProjection_RespectsBankTextFilters()
    {
        using var db = CreateDb();
        var user = await AddUserAsync(db, "bank-textfilter");
        var account = AddAccount(db, user.Id, "Giro");
        var analysis = new DateTime(2026, 5, 1);
        AddBankPosting(db, account, new DateTime(2025, 5, 10), 100m, "Rent payment");
        AddBankPosting(db, account, new DateTime(2025, 5, 12), 50m, "Other posting");
        await RebuildAggregatesAsync(db, user.Id);

        var result = await CreateSut(db).QueryAsync(new ReportAggregationQuery(
            user.Id, PostingKind.Bank, ReportInterval.Month, 1,
            IncludeCategory: false, ComparePrevious: false, CompareYear: false, CompareProjection: true,
            AnalysisDate: analysis,
            Filters: new ReportAggregationFilters(BankTextFilters: new[] { "Rent" })), CancellationToken.None);

        var point = result.Points.Single(p => p.GroupKey == $"Account:{account.Id}" && p.PeriodStart == analysis);
        Assert.Equal(100m, point.ProjectionAmount);
        Assert.Single(point.ProjectionExpectedItems!);
    }

    /// <summary>
    /// The account allow-list restricts the projection: entities outside <c>Filters.AccountIds</c>
    /// produce neither rows nor expectations.
    /// </summary>
    [Fact]
    public async Task QueryAsync_BankProjection_RespectsAccountIdsFilter()
    {
        using var db = CreateDb();
        var user = await AddUserAsync(db, "bank-entityfilter");
        var allowed = AddAccount(db, user.Id, "Allowed");
        var blocked = AddAccount(db, user.Id, "Blocked");
        var analysis = new DateTime(2026, 5, 1);
        AddBankPosting(db, allowed, new DateTime(2025, 5, 10), 100m);
        AddBankPosting(db, blocked, new DateTime(2025, 5, 10), 200m);
        await RebuildAggregatesAsync(db, user.Id);

        var result = await CreateSut(db).QueryAsync(new ReportAggregationQuery(
            user.Id, PostingKind.Bank, ReportInterval.Month, 1,
            IncludeCategory: false, ComparePrevious: false, CompareYear: false, CompareProjection: true,
            AnalysisDate: analysis,
            Filters: new ReportAggregationFilters(AccountIds: new[] { allowed.Id })), CancellationToken.None);

        Assert.True(result.ComparedProjection);
        Assert.DoesNotContain(result.Points, p => p.GroupKey == $"Account:{blocked.Id}");
        var point = result.Points.Single(p => p.GroupKey == $"Account:{allowed.Id}" && p.PeriodStart == analysis);
        Assert.Equal(100m, point.ProjectionAmount);
    }

    /// <summary>
    /// Postings of a foreign owner's accounts never enter the projection: another user's prior-year
    /// posting produces no events and no expectations in the owner's report.
    /// </summary>
    [Fact]
    public async Task QueryAsync_BankProjection_RespectsOwnerIsolation()
    {
        using var db = CreateDb();
        var user = await AddUserAsync(db, "bank-owner");
        var other = await AddUserAsync(db, "bank-other");
        var own = AddAccount(db, user.Id, "Own");
        var foreign = AddAccount(db, other.Id, "Foreign");
        var analysis = new DateTime(2026, 5, 1);
        AddBankPosting(db, own, new DateTime(2025, 5, 10), 100m);
        AddBankPosting(db, foreign, new DateTime(2025, 5, 10), 200m);
        await RebuildAggregatesAsync(db, user.Id);
        await RebuildAggregatesAsync(db, other.Id);

        var result = await CreateSut(db).QueryAsync(new ReportAggregationQuery(
            user.Id, PostingKind.Bank, ReportInterval.Month, 1,
            IncludeCategory: false, ComparePrevious: false, CompareYear: false, CompareProjection: true,
            AnalysisDate: analysis), CancellationToken.None);

        Assert.DoesNotContain(result.Points, p => p.GroupKey == $"Account:{foreign.Id}");
        var point = result.Points.Single(p => p.GroupKey == $"Account:{own.Id}" && p.PeriodStart == analysis);
        Assert.Equal(100m, point.ProjectionAmount);
        Assert.All(point.ProjectionExpectedItems!, e => Assert.Equal(own.Id, e.EntityId));
    }

    /// <summary>
    /// <c>UseValutaDate</c> determines the effective event date of bank postings; postings with an
    /// unset valuta date fall back to their booking date.
    /// </summary>
    [Fact]
    public async Task QueryAsync_BankProjection_UsesValutaDateAndBookingFallback()
    {
        using var db = CreateDb();
        var user = await AddUserAsync(db, "bank-valuta");
        var account = AddAccount(db, user.Id, "Giro");
        var analysis = new DateTime(2026, 5, 1);
        AddBankPosting(db, account, new DateTime(2025, 4, 30), 30m, "Valuta", valutaDate: new DateTime(2025, 5, 2));
        AddBankPosting(db, account, new DateTime(2025, 5, 4), 70m, "Booking Fallback", valutaDate: DateTime.MinValue);
        await RebuildAggregatesAsync(db, user.Id);

        var result = await CreateSut(db).QueryAsync(new ReportAggregationQuery(
            user.Id, PostingKind.Bank, ReportInterval.Month, 1,
            IncludeCategory: false, ComparePrevious: false, CompareYear: false, CompareProjection: true,
            AnalysisDate: analysis,
            UseValutaDate: true), CancellationToken.None);

        var point = result.Points.Single(p => p.GroupKey == $"Account:{account.Id}" && p.PeriodStart == analysis);
        Assert.Equal(100m, point.ProjectionAmount);
        var expectedDates = point.ProjectionExpectedItems!.Select(e => e.ExpectedDate).OrderBy(d => d).ToArray();
        Assert.Equal(new[] { new DateTime(2026, 5, 2), new DateTime(2026, 5, 4) }, expectedDates);
    }

    /// <summary>
    /// The year-to-date interval projects prior-year postings into the current year's YTD bucket
    /// when their expected date lies at or before the analysis month.
    /// </summary>
    [Fact]
    public async Task QueryAsync_BankProjection_YtdInterval()
    {
        using var db = CreateDb();
        var user = await AddUserAsync(db, "bank-ytd");
        var account = AddAccount(db, user.Id, "Giro");
        AddBankPosting(db, account, new DateTime(2025, 4, 10), 100m);
        await RebuildAggregatesAsync(db, user.Id);

        var result = await CreateSut(db).QueryAsync(new ReportAggregationQuery(
            user.Id, PostingKind.Bank, ReportInterval.Ytd, 1,
            IncludeCategory: false, ComparePrevious: false, CompareYear: false, CompareProjection: true,
            AnalysisDate: new DateTime(2026, 5, 1)), CancellationToken.None);

        Assert.True(result.ComparedProjection);
        var point = result.Points.Single(p => p.GroupKey == $"Account:{account.Id}" && p.PeriodStart == new DateTime(2026, 1, 1));
        Assert.Equal(100m, point.ProjectionAmount);
        Assert.Single(point.ProjectionExpectedItems!);
    }

    /// <summary>
    /// The year interval projects prior-year postings into the expected year's bucket: an annual
    /// posting that is still missing adds its amount to the current year's row.
    /// </summary>
    [Fact]
    public async Task QueryAsync_BankProjection_YearInterval()
    {
        using var db = CreateDb();
        var user = await AddUserAsync(db, "bank-year");
        var account = AddAccount(db, user.Id, "Giro");
        AddBankPosting(db, account, new DateTime(2025, 5, 10), 100m);
        await RebuildAggregatesAsync(db, user.Id);

        var result = await CreateSut(db).QueryAsync(new ReportAggregationQuery(
            user.Id, PostingKind.Bank, ReportInterval.Year, 2,
            IncludeCategory: false, ComparePrevious: false, CompareYear: false, CompareProjection: true,
            AnalysisDate: new DateTime(2026, 5, 1)), CancellationToken.None);

        Assert.True(result.ComparedProjection);
        var point = result.Points.Single(p => p.GroupKey == $"Account:{account.Id}" && p.PeriodStart == new DateTime(2026, 1, 1));
        Assert.Equal(100m, point.ProjectionAmount);
        Assert.Single(point.ProjectionExpectedItems!);
    }

    /// <summary>
    /// Entities without any prior-year events produce no expectations; the projection amount of a
    /// booked period then equals the booked amount.
    /// </summary>
    [Fact]
    public async Task QueryAsync_BankProjection_NoPriorYearEvents()
    {
        using var db = CreateDb();
        var user = await AddUserAsync(db, "bank-no-history");
        var account = AddAccount(db, user.Id, "Giro");
        var analysis = new DateTime(2026, 5, 1);
        AddBankPosting(db, account, new DateTime(2026, 5, 10), 100m);
        await RebuildAggregatesAsync(db, user.Id);

        var result = await CreateSut(db).QueryAsync(new ReportAggregationQuery(
            user.Id, PostingKind.Bank, ReportInterval.Month, 1,
            IncludeCategory: false, ComparePrevious: false, CompareYear: false, CompareProjection: true,
            AnalysisDate: analysis), CancellationToken.None);

        Assert.True(result.ComparedProjection);
        var point = result.Points.Single(p => p.GroupKey == $"Account:{account.Id}" && p.PeriodStart == analysis);
        Assert.Equal(100m, point.Amount);
        Assert.Equal(100m, point.ProjectionAmount);
        Assert.Null(point.ProjectionExpectedItems);
    }

    /// <summary>
    /// A row whose latest period is empty except for a projection must survive the empty-latest-row
    /// cleanup that runs for comparison columns — otherwise projection-only rows would disappear.
    /// </summary>
    [Fact]
    public async Task QueryAsync_Projection_ComparePrevious_LatestRowRetained()
    {
        using var db = CreateDb();
        var user = await AddUserAsync(db, "bank-cleanup");
        var account = AddAccount(db, user.Id, "Giro");
        var analysis = new DateTime(2026, 5, 1);
        AddBankPosting(db, account, new DateTime(2025, 5, 10), 120m);
        await RebuildAggregatesAsync(db, user.Id);

        var result = await CreateSut(db).QueryAsync(new ReportAggregationQuery(
            user.Id, PostingKind.Bank, ReportInterval.Month, 2,
            IncludeCategory: false, ComparePrevious: true, CompareYear: false, CompareProjection: true,
            AnalysisDate: analysis), CancellationToken.None);

        var point = result.Points.SingleOrDefault(p => p.GroupKey == $"Account:{account.Id}" && p.PeriodStart == analysis);
        Assert.NotNull(point);
        Assert.Equal(0m, point!.Amount);
        Assert.Equal(120m, point.ProjectionAmount);
    }

    /// <summary>
    /// A bank entity can carry several independent recurring series: an unrelated current-year
    /// posting matching the same text filter must not suppress the still-open annual expectation
    /// of a prior-year year-end posting (regression — the adopted dividend rule suppressed every
    /// expectation once any current event existed).
    /// </summary>
    [Fact]
    public async Task QueryAsync_BankProjection_Annual_RemainsExpected_WhenOtherCurrentEventExists()
    {
        using var db = CreateDb();
        var user = await AddUserAsync(db, "bank-annual-open");
        var account = AddAccount(db, user.Id, "Giro");
        var analysis = new DateTime(2026, 5, 1);
        AddBankPosting(db, account, new DateTime(2025, 12, 30), 120m, "Zinsgutschrift");
        AddBankPosting(db, account, new DateTime(2026, 3, 15), 30m, "Zinsgutschrift");
        await RebuildAggregatesAsync(db, user.Id);

        var result = await CreateSut(db).QueryAsync(new ReportAggregationQuery(
            user.Id, PostingKind.Bank, ReportInterval.Year, 2,
            IncludeCategory: false, ComparePrevious: false, CompareYear: false, CompareProjection: true,
            AnalysisDate: analysis,
            Filters: new ReportAggregationFilters(BankTextFilters: new[] { "Zins" })), CancellationToken.None);

        Assert.True(result.ComparedProjection);
        var point = result.Points.Single(p => p.GroupKey == $"Account:{account.Id}" && p.PeriodStart == new DateTime(2026, 1, 1));
        Assert.Equal(30m, point.Amount);
        Assert.Equal(150m, point.ProjectionAmount);
        var expected = Assert.Single(point.ProjectionExpectedItems!);
        Assert.Equal(new DateTime(2026, 12, 30), expected.ExpectedDate);
        Assert.Equal(new DateTime(2025, 12, 30), expected.PriorYearDate);
    }

    /// <summary>
    /// For irregular patterns each candidate is confirmed individually: a current event inside the
    /// confirmation window suppresses only its own expectation while the other unmatched prior-year
    /// events of the entity stay expected.
    /// </summary>
    [Fact]
    public async Task QueryAsync_BankProjection_Irregular_RemainsExpectedForUnmatchedCandidates()
    {
        using var db = CreateDb();
        var user = await AddUserAsync(db, "bank-irregular-open");
        var account = AddAccount(db, user.Id, "Giro");
        var analysis = new DateTime(2026, 5, 1);
        AddBankPosting(db, account, new DateTime(2025, 1, 10), 100m);
        AddBankPosting(db, account, new DateTime(2025, 6, 15), 100m);
        AddBankPosting(db, account, new DateTime(2025, 12, 20), 100m);
        AddBankPosting(db, account, new DateTime(2026, 1, 12), 100m);
        await RebuildAggregatesAsync(db, user.Id);

        var result = await CreateSut(db).QueryAsync(new ReportAggregationQuery(
            user.Id, PostingKind.Bank, ReportInterval.Year, 2,
            IncludeCategory: false, ComparePrevious: false, CompareYear: false, CompareProjection: true,
            AnalysisDate: analysis), CancellationToken.None);

        var point = result.Points.Single(p => p.GroupKey == $"Account:{account.Id}" && p.PeriodStart == new DateTime(2026, 1, 1));
        Assert.Equal(100m, point.Amount);
        Assert.Equal(300m, point.ProjectionAmount);
        var expectedDates = point.ProjectionExpectedItems!.Select(e => e.ExpectedDate).OrderBy(d => d).ToArray();
        Assert.Equal(new[] { new DateTime(2026, 6, 15), new DateTime(2026, 12, 20) }, expectedDates);
    }

    /// <summary>
    /// Securities keep the adopted dividend semantics: a single current-year dividend event
    /// confirms the year's annual expectation even when it arrived in a different month.
    /// </summary>
    [Fact]
    public async Task QueryAsync_SecurityProjection_Annual_SuppressedByAnyCurrentEvent()
    {
        using var db = CreateDb();
        var user = await AddUserAsync(db, "sec-annual-confirmed");
        var security = new FinanceManager.Domain.Securities.Security(user.Id, "Sec", "ISIN-3", null, null, "EUR", null);
        db.Securities.Add(security);
        var analysis = new DateTime(2026, 5, 1);
        var group = Guid.NewGuid();
        var group2 = Guid.NewGuid();
        db.Postings.Add(new Posting(Guid.NewGuid(), PostingKind.Security, null, null, null, security.Id, new DateTime(2024, 1, 1), new DateTime(2024, 1, 1), -200m, "Buy", null, null, SecurityPostingSubType.Buy, 10m));
        db.Postings.Add(new Posting(Guid.NewGuid(), PostingKind.Security, null, null, null, security.Id, new DateTime(2025, 12, 10), new DateTime(2025, 12, 10), 80m, "Dividend", null, null, SecurityPostingSubType.Dividend).SetGroup(group));
        db.Postings.Add(new Posting(Guid.NewGuid(), PostingKind.Security, null, null, null, security.Id, new DateTime(2026, 2, 5), new DateTime(2026, 2, 5), 90m, "Dividend", null, null, SecurityPostingSubType.Dividend).SetGroup(group2));
        await RebuildAggregatesAsync(db, user.Id);

        var result = await CreateSut(db).QueryAsync(new ReportAggregationQuery(
            user.Id, PostingKind.Security, ReportInterval.Month, 5,
            IncludeCategory: false, ComparePrevious: false, CompareYear: false, CompareProjection: true,
            PostingKinds: new[] { PostingKind.Security, PostingKind.Bank },
            AnalysisDate: analysis), CancellationToken.None);

        Assert.True(result.ComparedProjection);
        var dividendRow = result.Points.Single(p => p.GroupKey == $"Security:{security.Id}:Dividend" && p.PeriodStart == new DateTime(2026, 2, 1));
        Assert.Equal(90m, dividendRow.Amount);
        Assert.Equal(90m, dividendRow.ProjectionAmount);
        Assert.All(result.Points.Where(p => p.GroupKey == $"Security:{security.Id}:Dividend"), p => Assert.Null(p.ProjectionExpectedItems));
    }

    /// <summary>
    /// A fully sold security produces no expectation in the standard path either (positive-holding
    /// rule), while the bank selection of the same multi-kind report keeps projecting.
    /// </summary>
    [Fact]
    public async Task QueryAsync_MultiKindProjection_FullySoldSecurity_NoExpectation()
    {
        using var db = CreateDb();
        var user = await AddUserAsync(db, "multi-sold");
        var account = AddAccount(db, user.Id, "Giro");
        var security = new FinanceManager.Domain.Securities.Security(user.Id, "Sold Sec", "ISIN-2", null, null, "EUR", null);
        db.Securities.Add(security);
        var analysis = new DateTime(2026, 5, 1);
        AddBankPosting(db, account, new DateTime(2025, 5, 10), 120m);
        var group = Guid.NewGuid();
        db.Postings.Add(new Posting(Guid.NewGuid(), PostingKind.Security, null, null, null, security.Id, new DateTime(2025, 5, 10), new DateTime(2025, 5, 10), 80m, "Dividend", null, null, SecurityPostingSubType.Dividend).SetGroup(group));
        db.Postings.Add(new Posting(Guid.NewGuid(), PostingKind.Security, null, null, null, security.Id, new DateTime(2024, 1, 1), new DateTime(2024, 1, 1), -200m, "Buy", null, null, SecurityPostingSubType.Buy, 10m));
        db.Postings.Add(new Posting(Guid.NewGuid(), PostingKind.Security, null, null, null, security.Id, new DateTime(2025, 12, 1), new DateTime(2025, 12, 1), 250m, "Sell", null, null, SecurityPostingSubType.Sell, 10m));
        await RebuildAggregatesAsync(db, user.Id);

        var result = await CreateSut(db).QueryAsync(new ReportAggregationQuery(
            user.Id, PostingKind.Security, ReportInterval.Month, 1,
            IncludeCategory: false, ComparePrevious: false, CompareYear: false, CompareProjection: true,
            PostingKinds: new[] { PostingKind.Security, PostingKind.Bank },
            AnalysisDate: analysis), CancellationToken.None);

        Assert.True(result.ComparedProjection);
        var dividendRow = result.Points.Single(p => p.GroupKey == $"Security:{security.Id}:Dividend" && p.PeriodStart == analysis);
        Assert.Equal(0m, dividendRow.ProjectionAmount);
        Assert.Null(dividendRow.ProjectionExpectedItems);
        Assert.Equal(120m, result.Points.Single(p => p.GroupKey == $"Account:{account.Id}" && p.PeriodStart == analysis).ProjectionAmount);
    }
}
