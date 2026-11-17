using FinanceManager.Domain.Accounts;
using FinanceManager.Domain.Contacts;
using FinanceManager.Domain.Postings;
using FinanceManager.Domain.Savings;
using FinanceManager.Domain.Securities;
using FinanceManager.Domain.Users;
using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Reports;
using FinanceManager.Shared.Dtos.Reports;
using FinanceManager.Shared.Dtos.SavingsPlans;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Tests.Reports;

/// <summary>
/// Covers <see cref="ReportPostingsQueryService"/>: period windows (current/previous/year-ago,
/// YTD, all-history), group-key selection (entity/category/type/total), owner isolation,
/// entity/category/sub-type/text filters, valuta-date handling, the linked-account savings
/// plan source, the netted security dividend path and the 5000-row truncation contract.
/// </summary>
public sealed class ReportPostingsQueryServiceTests
{
    private static (ReportPostingsQueryService sut, AppDbContext db, SqliteConnection conn) Create()
    {
        var conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options;
        var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        return (new ReportPostingsQueryService(db), db, conn);
    }

    private static User AddUser(AppDbContext db, string name)
    {
        var user = new User(name, "pw", false);
        db.Users.Add(user);
        return user;
    }

    private static Posting AddPosting(AppDbContext db, PostingKind kind, Guid? accountId, Guid? contactId, Guid? savingsPlanId, Guid? securityId, DateTime bookingDate, decimal amount, string? subject = null, string? recipientName = null, string? description = null, SecurityPostingSubType? subType = null)
    {
        var p = new Posting(Guid.NewGuid(), kind, accountId, contactId, savingsPlanId, securityId, bookingDate, amount, subject, recipientName, description, subType);
        db.Postings.Add(p);
        return p;
    }

    private static ReportPostingsQuery Query(Guid owner, string? groupKey, DateTime periodStart, ReportPostingsPeriod period,
        ReportInterval interval = ReportInterval.Month, PostingKind kind = PostingKind.Bank, IReadOnlyCollection<PostingKind>? kinds = null,
        DateTime? analysis = null, ReportAggregationFilters? filters = null, bool includeCategory = false,
        bool useValuta = false, SavingsPlanPostingSource source = SavingsPlanPostingSource.PlanPostings, bool compareProjection = false,
        int take = 24, bool compareYear = false)
        => new(owner, groupKey, periodStart, period, kind, interval, includeCategory, compareProjection, useValuta, kinds,
            analysis ?? periodStart, filters, source, take, compareYear);

    private static ReportAggregationFilters Filters(
        IReadOnlyCollection<Guid>? contactCategoryIds = null,
        IReadOnlyCollection<Guid>? savingsPlanCategoryIds = null,
        IReadOnlyCollection<Guid>? securityCategoryIds = null,
        IReadOnlyCollection<int>? securitySubTypes = null,
        IReadOnlyCollection<string>? securityTextFilters = null,
        bool includeDividendRelated = false)
        => new(AccountIds: null, ContactIds: null, SavingsPlanIds: null, SecurityIds: null,
            ContactCategoryIds: contactCategoryIds, SavingsPlanCategoryIds: savingsPlanCategoryIds, SecurityCategoryIds: securityCategoryIds,
            SecuritySubTypes: securitySubTypes, IncludeDividendRelated: includeDividendRelated,
            BankTextFilters: null, ContactTextFilters: null, SavingsPlanTextFilters: null, SecurityTextFilters: securityTextFilters);

    /// <summary>Only postings whose effective date falls into the cell's month are returned; the entity name is resolved on the server.</summary>
    [Fact]
    public async Task QueryAsync_MonthCurrent_ReturnsOnlyPostingsOfMonth()
    {
        var (sut, db, conn) = Create();
        var user = AddUser(db, "u");
        var acc = new Account(user.Id, AccountType.Giro, "Giro", null, Guid.NewGuid());
        db.Accounts.Add(acc);
        var month = new DateTime(2025, 9, 1);
        AddPosting(db, PostingKind.Bank, acc.Id, null, null, null, month.AddDays(5), 100m, "inside");
        AddPosting(db, PostingKind.Bank, acc.Id, null, null, null, month.AddDays(20), -40m, "inside2");
        AddPosting(db, PostingKind.Bank, acc.Id, null, null, null, month.AddMonths(-1), 999m, "outside");
        await db.SaveChangesAsync(CancellationToken.None);

        var result = await sut.QueryAsync(Query(user.Id, $"Account:{acc.Id}", month, ReportPostingsPeriod.Current), CancellationToken.None);

        Assert.Equal(2, result.TotalCount);
        Assert.False(result.Truncated);
        Assert.Equal(60m, result.Postings.Sum(p => p.Amount));
        Assert.Equal(60m, result.TotalAmount);
        Assert.Equal("Giro", result.Postings[0].EntityName);
        conn.Dispose();
    }

    /// <summary>The previous-period column resolves to the immediately preceding month window.</summary>
    [Fact]
    public async Task QueryAsync_MonthPrevious_UsesPreviousMonthWindow()
    {
        var (sut, db, conn) = Create();
        var user = AddUser(db, "u");
        var acc = new Account(user.Id, AccountType.Giro, "Giro", null, Guid.NewGuid());
        db.Accounts.Add(acc);
        var month = new DateTime(2025, 9, 1);
        AddPosting(db, PostingKind.Bank, acc.Id, null, null, null, month.AddDays(5), 100m);
        AddPosting(db, PostingKind.Bank, acc.Id, null, null, null, month.AddMonths(-1).AddDays(3), 77m);
        await db.SaveChangesAsync(CancellationToken.None);

        var result = await sut.QueryAsync(Query(user.Id, $"Account:{acc.Id}", month, ReportPostingsPeriod.Previous), CancellationToken.None);

        Assert.Single(result.Postings);
        Assert.Equal(77m, result.Postings[0].Amount);
        conn.Dispose();
    }

    /// <summary>The year-ago column resolves to the same month one year earlier.</summary>
    [Fact]
    public async Task QueryAsync_MonthYearAgo_UsesSameMonthLastYear()
    {
        var (sut, db, conn) = Create();
        var user = AddUser(db, "u");
        var acc = new Account(user.Id, AccountType.Giro, "Giro", null, Guid.NewGuid());
        db.Accounts.Add(acc);
        var month = new DateTime(2025, 9, 1);
        AddPosting(db, PostingKind.Bank, acc.Id, null, null, null, month.AddDays(5), 100m);
        AddPosting(db, PostingKind.Bank, acc.Id, null, null, null, month.AddYears(-1).AddDays(10), 55m);
        await db.SaveChangesAsync(CancellationToken.None);

        var result = await sut.QueryAsync(Query(user.Id, $"Account:{acc.Id}", month, ReportPostingsPeriod.YearAgo), CancellationToken.None);

        Assert.Single(result.Postings);
        Assert.Equal(55m, result.Postings[0].Amount);
        conn.Dispose();
    }

    /// <summary>Quarter cells cover a three-month window: the anchored quarter, the aligned previous quarter and the same quarter one year ago. The window start is inclusive, the end exclusive.</summary>
    [Fact]
    public async Task QueryAsync_QuarterWindows_SelectExpectedPeriods()
    {
        var (sut, db, conn) = Create();
        var user = AddUser(db, "u");
        var acc = new Account(user.Id, AccountType.Giro, "Giro", null, Guid.NewGuid());
        db.Accounts.Add(acc);
        var quarter = new DateTime(2025, 7, 1); // Q3 2025
        AddPosting(db, PostingKind.Bank, acc.Id, null, null, null, new DateTime(2025, 8, 15), 100m); // inside current
        AddPosting(db, PostingKind.Bank, acc.Id, null, null, null, quarter, 10m); // window start is inclusive
        AddPosting(db, PostingKind.Bank, acc.Id, null, null, null, new DateTime(2025, 10, 1), 999m); // window end is exclusive (Q4)
        AddPosting(db, PostingKind.Bank, acc.Id, null, null, null, new DateTime(2025, 5, 10), 50m); // previous quarter (Q2)
        AddPosting(db, PostingKind.Bank, acc.Id, null, null, null, new DateTime(2024, 8, 15), 25m); // same quarter a year ago
        await db.SaveChangesAsync(CancellationToken.None);

        var current = await sut.QueryAsync(Query(user.Id, $"Account:{acc.Id}", quarter, ReportPostingsPeriod.Current, interval: ReportInterval.Quarter), CancellationToken.None);
        Assert.Equal(110m, current.Postings.Sum(p => p.Amount));

        var previous = await sut.QueryAsync(Query(user.Id, $"Account:{acc.Id}", quarter, ReportPostingsPeriod.Previous, interval: ReportInterval.Quarter), CancellationToken.None);
        Assert.Equal(50m, previous.Postings.Sum(p => p.Amount));
        Assert.Single(previous.Postings);

        var yearAgo = await sut.QueryAsync(Query(user.Id, $"Account:{acc.Id}", quarter, ReportPostingsPeriod.YearAgo, interval: ReportInterval.Quarter), CancellationToken.None);
        Assert.Equal(25m, yearAgo.Postings.Sum(p => p.Amount));
        Assert.Single(yearAgo.Postings);
        conn.Dispose();
    }

    /// <summary>Half-year cells cover a six-month window: the anchored half year, the aligned previous half year and the same half year one year ago.</summary>
    [Fact]
    public async Task QueryAsync_HalfYearWindows_SelectExpectedPeriods()
    {
        var (sut, db, conn) = Create();
        var user = AddUser(db, "u");
        var acc = new Account(user.Id, AccountType.Giro, "Giro", null, Guid.NewGuid());
        db.Accounts.Add(acc);
        var halfYear = new DateTime(2025, 7, 1); // H2 2025
        AddPosting(db, PostingKind.Bank, acc.Id, null, null, null, new DateTime(2025, 8, 15), 100m); // inside current
        AddPosting(db, PostingKind.Bank, acc.Id, null, null, null, new DateTime(2025, 12, 31), 10m); // inside current
        AddPosting(db, PostingKind.Bank, acc.Id, null, null, null, new DateTime(2026, 2, 15), 999m); // outside (H1 2026)
        AddPosting(db, PostingKind.Bank, acc.Id, null, null, null, new DateTime(2025, 3, 15), 50m); // previous half year (H1 2025)
        AddPosting(db, PostingKind.Bank, acc.Id, null, null, null, new DateTime(2024, 10, 15), 25m); // same half year a year ago
        AddPosting(db, PostingKind.Bank, acc.Id, null, null, null, new DateTime(2024, 3, 15), 888m); // outside (H1 2024)
        await db.SaveChangesAsync(CancellationToken.None);

        var current = await sut.QueryAsync(Query(user.Id, $"Account:{acc.Id}", halfYear, ReportPostingsPeriod.Current, interval: ReportInterval.HalfYear), CancellationToken.None);
        Assert.Equal(110m, current.Postings.Sum(p => p.Amount));

        var previous = await sut.QueryAsync(Query(user.Id, $"Account:{acc.Id}", halfYear, ReportPostingsPeriod.Previous, interval: ReportInterval.HalfYear), CancellationToken.None);
        Assert.Equal(50m, previous.Postings.Sum(p => p.Amount));
        Assert.Single(previous.Postings);

        var yearAgo = await sut.QueryAsync(Query(user.Id, $"Account:{acc.Id}", halfYear, ReportPostingsPeriod.YearAgo, interval: ReportInterval.HalfYear), CancellationToken.None);
        Assert.Equal(25m, yearAgo.Postings.Sum(p => p.Amount));
        Assert.Single(yearAgo.Postings);
        conn.Dispose();
    }

    /// <summary>Year cells cover a twelve-month window; the previous period and the year-ago column resolve to the identical previous-year window.</summary>
    [Fact]
    public async Task QueryAsync_YearWindows_SelectExpectedPeriods()
    {
        var (sut, db, conn) = Create();
        var user = AddUser(db, "u");
        var acc = new Account(user.Id, AccountType.Giro, "Giro", null, Guid.NewGuid());
        db.Accounts.Add(acc);
        var year = new DateTime(2025, 1, 1);
        AddPosting(db, PostingKind.Bank, acc.Id, null, null, null, new DateTime(2025, 6, 15), 100m); // inside current
        AddPosting(db, PostingKind.Bank, acc.Id, null, null, null, new DateTime(2026, 6, 15), 999m); // outside (next year)
        AddPosting(db, PostingKind.Bank, acc.Id, null, null, null, new DateTime(2024, 6, 15), 50m); // previous year
        AddPosting(db, PostingKind.Bank, acc.Id, null, null, null, new DateTime(2024, 12, 31), 5m); // previous year
        AddPosting(db, PostingKind.Bank, acc.Id, null, null, null, new DateTime(2023, 6, 15), 888m); // outside (two years ago)
        await db.SaveChangesAsync(CancellationToken.None);

        var current = await sut.QueryAsync(Query(user.Id, $"Account:{acc.Id}", year, ReportPostingsPeriod.Current, interval: ReportInterval.Year), CancellationToken.None);
        Assert.Equal(100m, current.Postings.Sum(p => p.Amount));
        Assert.Single(current.Postings);

        // for the year interval the previous period and the year-ago column are the same window
        var previous = await sut.QueryAsync(Query(user.Id, $"Account:{acc.Id}", year, ReportPostingsPeriod.Previous, interval: ReportInterval.Year), CancellationToken.None);
        Assert.Equal(55m, previous.Postings.Sum(p => p.Amount));

        var yearAgo = await sut.QueryAsync(Query(user.Id, $"Account:{acc.Id}", year, ReportPostingsPeriod.YearAgo, interval: ReportInterval.Year), CancellationToken.None);
        Assert.Equal(55m, yearAgo.Postings.Sum(p => p.Amount));
        conn.Dispose();
    }

    /// <summary>A Type:{kind} key selects all entities of that kind; the total row (null key) unions all requested kinds.</summary>
    [Fact]
    public async Task QueryAsync_TypeAndTotal_SelectAcrossEntitiesAndKinds()
    {
        var (sut, db, conn) = Create();
        var user = AddUser(db, "u");
        var acc1 = new Account(user.Id, AccountType.Giro, "A1", null, Guid.NewGuid());
        var acc2 = new Account(user.Id, AccountType.Giro, "A2", null, Guid.NewGuid());
        var contact = new Contact(user.Id, "C", ContactType.Person, null, null);
        db.Accounts.AddRange(acc1, acc2);
        db.Contacts.Add(contact);
        var month = new DateTime(2025, 9, 1);
        AddPosting(db, PostingKind.Bank, acc1.Id, null, null, null, month, 10m);
        AddPosting(db, PostingKind.Bank, acc2.Id, null, null, null, month, 20m);
        AddPosting(db, PostingKind.Contact, null, contact.Id, null, null, month, 30m);
        await db.SaveChangesAsync(CancellationToken.None);

        var type = await sut.QueryAsync(Query(user.Id, $"Type:{PostingKind.Bank}", month, ReportPostingsPeriod.Current), CancellationToken.None);
        Assert.Equal(2, type.TotalCount);
        Assert.Equal(30m, type.Postings.Sum(p => p.Amount));

        var total = await sut.QueryAsync(Query(user.Id, null, month, ReportPostingsPeriod.Current,
            kinds: new[] { PostingKind.Bank, PostingKind.Contact }), CancellationToken.None);
        Assert.Equal(3, total.TotalCount);
        Assert.Equal(60m, total.Postings.Sum(p => p.Amount));
        conn.Dispose();
    }

    /// <summary>A Category:{kind}:{id} key only selects postings of entities within that category.</summary>
    [Fact]
    public async Task QueryAsync_CategoryKey_SelectsCategoryMembersOnly()
    {
        var (sut, db, conn) = Create();
        var user = AddUser(db, "u");
        var cat = new ContactCategory(user.Id, "Cat");
        db.ContactCategories.Add(cat);
        var inCat = new Contact(user.Id, "In", ContactType.Person, cat.Id, null);
        var outCat = new Contact(user.Id, "Out", ContactType.Person, null, null);
        db.Contacts.AddRange(inCat, outCat);
        var month = new DateTime(2025, 9, 1);
        AddPosting(db, PostingKind.Contact, null, inCat.Id, null, null, month, 12m);
        AddPosting(db, PostingKind.Contact, null, outCat.Id, null, null, month, 99m);
        await db.SaveChangesAsync(CancellationToken.None);

        var result = await sut.QueryAsync(Query(user.Id, $"Category:{PostingKind.Contact}:{cat.Id}", month, ReportPostingsPeriod.Current,
            kind: PostingKind.Contact, includeCategory: true), CancellationToken.None);

        Assert.Single(result.Postings);
        Assert.Equal(12m, result.Postings[0].Amount);
        Assert.Equal("In", result.Postings[0].EntityName);
        conn.Dispose();
    }

    /// <summary>The _none category key selects entities without a category assignment.</summary>
    [Fact]
    public async Task QueryAsync_NoneCategoryKey_SelectsUncategorizedEntities()
    {
        var (sut, db, conn) = Create();
        var user = AddUser(db, "u");
        var cat = new ContactCategory(user.Id, "Cat");
        db.ContactCategories.Add(cat);
        var inCat = new Contact(user.Id, "In", ContactType.Person, cat.Id, null);
        var none = new Contact(user.Id, "None", ContactType.Person, null, null);
        db.Contacts.AddRange(inCat, none);
        var month = new DateTime(2025, 9, 1);
        AddPosting(db, PostingKind.Contact, null, inCat.Id, null, null, month, 1m);
        AddPosting(db, PostingKind.Contact, null, none.Id, null, null, month, 2m);
        await db.SaveChangesAsync(CancellationToken.None);

        var result = await sut.QueryAsync(Query(user.Id, $"Category:{PostingKind.Contact}:_none", month, ReportPostingsPeriod.Current,
            kind: PostingKind.Contact, includeCategory: true), CancellationToken.None);

        Assert.Single(result.Postings);
        Assert.Equal(2m, result.Postings[0].Amount);
        conn.Dispose();
    }

    /// <summary>Unknown prefixes and malformed ids are rejected with ArgumentException.</summary>
    [Fact]
    public async Task QueryAsync_InvalidGroupKey_ThrowsArgument()
    {
        var (sut, db, conn) = Create();
        var user = AddUser(db, "u");
        await db.SaveChangesAsync(CancellationToken.None);
        var month = new DateTime(2025, 9, 1);

        await Assert.ThrowsAsync<ArgumentException>(() => sut.QueryAsync(Query(user.Id, "Foo:bar", month, ReportPostingsPeriod.Current), CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => sut.QueryAsync(Query(user.Id, "Account:not-a-guid", month, ReportPostingsPeriod.Current), CancellationToken.None));
        conn.Dispose();
    }

    /// <summary>Comparison columns are rejected for the all-history interval.</summary>
    [Fact]
    public async Task QueryAsync_AllHistoryComparisonPeriod_Throws()
    {
        var (sut, db, conn) = Create();
        var user = AddUser(db, "u");
        await db.SaveChangesAsync(CancellationToken.None);
        var month = new DateTime(2025, 9, 1);

        await Assert.ThrowsAsync<ArgumentException>(() => sut.QueryAsync(Query(user.Id, null, month, ReportPostingsPeriod.Previous, interval: ReportInterval.AllHistory), CancellationToken.None));
        conn.Dispose();
    }

    /// <summary>The all-history current column returns postings without a date window.</summary>
    [Fact]
    public async Task QueryAsync_AllHistoryCurrent_IsUnbounded()
    {
        var (sut, db, conn) = Create();
        var user = AddUser(db, "u");
        var acc = new Account(user.Id, AccountType.Giro, "Giro", null, Guid.NewGuid());
        db.Accounts.Add(acc);
        AddPosting(db, PostingKind.Bank, acc.Id, null, null, null, new DateTime(2020, 1, 15), 1m);
        AddPosting(db, PostingKind.Bank, acc.Id, null, null, null, new DateTime(2025, 9, 1), 2m);
        await db.SaveChangesAsync(CancellationToken.None);

        var result = await sut.QueryAsync(Query(user.Id, $"Account:{acc.Id}", DateTime.MinValue, ReportPostingsPeriod.Current, interval: ReportInterval.AllHistory), CancellationToken.None);

        Assert.Equal(2, result.TotalCount);
        conn.Dispose();
    }

    /// <summary>The YTD current column covers January through the analysis month of the cell year.</summary>
    [Fact]
    public async Task QueryAsync_YtdCurrent_UsesJanToAnalysisMonth()
    {
        var (sut, db, conn) = Create();
        var user = AddUser(db, "u");
        var acc = new Account(user.Id, AccountType.Giro, "Giro", null, Guid.NewGuid());
        db.Accounts.Add(acc);
        var analysis = new DateTime(2025, 9, 1);
        AddPosting(db, PostingKind.Bank, acc.Id, null, null, null, new DateTime(2025, 1, 15), 10m);
        AddPosting(db, PostingKind.Bank, acc.Id, null, null, null, new DateTime(2025, 8, 20), 20m);
        AddPosting(db, PostingKind.Bank, acc.Id, null, null, null, new DateTime(2025, 10, 5), 99m);
        AddPosting(db, PostingKind.Bank, acc.Id, null, null, null, new DateTime(2024, 6, 1), 88m);
        await db.SaveChangesAsync(CancellationToken.None);

        var result = await sut.QueryAsync(Query(user.Id, $"Account:{acc.Id}", new DateTime(2025, 1, 1), ReportPostingsPeriod.Current,
            interval: ReportInterval.Ytd, analysis: analysis), CancellationToken.None);

        Assert.Equal(30m, result.Postings.Sum(p => p.Amount));
        conn.Dispose();
    }

    /// <summary>The YTD previous column covers the same Jan-to-analysis-month range one year earlier.</summary>
    [Fact]
    public async Task QueryAsync_YtdPrevious_UsesPreviousYearJanToAnalysisMonth()
    {
        var (sut, db, conn) = Create();
        var user = AddUser(db, "u");
        var acc = new Account(user.Id, AccountType.Giro, "Giro", null, Guid.NewGuid());
        db.Accounts.Add(acc);
        var analysis = new DateTime(2025, 9, 1);
        AddPosting(db, PostingKind.Bank, acc.Id, null, null, null, new DateTime(2024, 3, 15), 10m);
        AddPosting(db, PostingKind.Bank, acc.Id, null, null, null, new DateTime(2024, 11, 20), 99m);
        AddPosting(db, PostingKind.Bank, acc.Id, null, null, null, new DateTime(2025, 3, 15), 88m);
        await db.SaveChangesAsync(CancellationToken.None);

        var result = await sut.QueryAsync(Query(user.Id, $"Account:{acc.Id}", new DateTime(2025, 1, 1), ReportPostingsPeriod.Previous,
            interval: ReportInterval.Ytd, analysis: analysis), CancellationToken.None);

        Assert.Single(result.Postings);
        Assert.Equal(10m, result.Postings[0].Amount);
        conn.Dispose();
    }

    /// <summary>Postings referencing entities of another owner are never returned, for entity keys and the total row alike.</summary>
    [Fact]
    public async Task QueryAsync_ForeignOwner_IsExcluded()
    {
        var (sut, db, conn) = Create();
        var user = AddUser(db, "u");
        var other = AddUser(db, "o");
        var foreignAcc = new Account(other.Id, AccountType.Giro, "Fremd", null, Guid.NewGuid());
        db.Accounts.Add(foreignAcc);
        var month = new DateTime(2025, 9, 1);
        AddPosting(db, PostingKind.Bank, foreignAcc.Id, null, null, null, month, 100m);
        await db.SaveChangesAsync(CancellationToken.None);

        // entity key of a foreign account resolves to nothing for the current owner
        var entity = await sut.QueryAsync(Query(user.Id, $"Account:{foreignAcc.Id}", month, ReportPostingsPeriod.Current), CancellationToken.None);
        Assert.Empty(entity.Postings);

        var total = await sut.QueryAsync(Query(user.Id, null, month, ReportPostingsPeriod.Current), CancellationToken.None);
        Assert.Empty(total.Postings);
        conn.Dispose();
    }

    /// <summary>An active entity filter restricts the selectable entities even for broader group keys.</summary>
    [Fact]
    public async Task QueryAsync_EntityFilter_LimitsSelection()
    {
        var (sut, db, conn) = Create();
        var user = AddUser(db, "u");
        var acc1 = new Account(user.Id, AccountType.Giro, "A1", null, Guid.NewGuid());
        var acc2 = new Account(user.Id, AccountType.Giro, "A2", null, Guid.NewGuid());
        db.Accounts.AddRange(acc1, acc2);
        var month = new DateTime(2025, 9, 1);
        AddPosting(db, PostingKind.Bank, acc1.Id, null, null, null, month, 10m);
        AddPosting(db, PostingKind.Bank, acc2.Id, null, null, null, month, 20m);
        await db.SaveChangesAsync(CancellationToken.None);

        var filters = new ReportAggregationFilters(AccountIds: new[] { acc1.Id }, ContactIds: null, SavingsPlanIds: null, SecurityIds: null,
            ContactCategoryIds: null, SavingsPlanCategoryIds: null, SecurityCategoryIds: null, SecuritySubTypes: null,
            IncludeDividendRelated: false, BankTextFilters: null, ContactTextFilters: null, SavingsPlanTextFilters: null, SecurityTextFilters: null);

        var result = await sut.QueryAsync(Query(user.Id, $"Type:{PostingKind.Bank}", month, ReportPostingsPeriod.Current, filters: filters), CancellationToken.None);

        Assert.Single(result.Postings);
        Assert.Equal(10m, result.Postings[0].Amount);
        conn.Dispose();
    }

    /// <summary>Contact category filters restrict the eligible entities only when IncludeCategory is active; without it the category allow-list is inert.</summary>
    [Fact]
    public async Task QueryAsync_ContactCategoryFilter_AppliesOnlyWithIncludeCategory()
    {
        var (sut, db, conn) = Create();
        var user = AddUser(db, "u");
        var catA = new ContactCategory(user.Id, "A");
        var catB = new ContactCategory(user.Id, "B");
        db.ContactCategories.AddRange(catA, catB);
        var inA = new Contact(user.Id, "InA", ContactType.Person, catA.Id, null);
        var inB = new Contact(user.Id, "InB", ContactType.Person, catB.Id, null);
        db.Contacts.AddRange(inA, inB);
        var month = new DateTime(2025, 9, 1);
        AddPosting(db, PostingKind.Contact, null, inA.Id, null, null, month, 12m);
        AddPosting(db, PostingKind.Contact, null, inB.Id, null, null, month, 99m);
        await db.SaveChangesAsync(CancellationToken.None);

        var filters = Filters(contactCategoryIds: new[] { catA.Id });

        var filtered = await sut.QueryAsync(Query(user.Id, $"Type:{PostingKind.Contact}", month, ReportPostingsPeriod.Current,
            kind: PostingKind.Contact, includeCategory: true, filters: filters), CancellationToken.None);
        Assert.Single(filtered.Postings);
        Assert.Equal(12m, filtered.Postings[0].Amount);
        Assert.Equal("InA", filtered.Postings[0].EntityName);

        // without IncludeCategory the category filter does not restrict the entity set
        var unfiltered = await sut.QueryAsync(Query(user.Id, $"Type:{PostingKind.Contact}", month, ReportPostingsPeriod.Current,
            kind: PostingKind.Contact, includeCategory: false, filters: filters), CancellationToken.None);
        Assert.Equal(2, unfiltered.TotalCount);
        conn.Dispose();
    }

    /// <summary>Savings plan category filters restrict the eligible plans of a type row when IncludeCategory is active.</summary>
    [Fact]
    public async Task QueryAsync_SavingsPlanCategoryFilter_LimitsSelection()
    {
        var (sut, db, conn) = Create();
        var user = AddUser(db, "u");
        var cat = new SavingsPlanCategory(user.Id, "Cat");
        db.SavingsPlanCategories.Add(cat);
        var inCat = new SavingsPlan(user.Id, "In", SavingsPlanType.Open, null, null, null, cat.Id);
        var outCat = new SavingsPlan(user.Id, "Out", SavingsPlanType.Open, null, null, null, null);
        db.SavingsPlans.AddRange(inCat, outCat);
        var month = new DateTime(2025, 9, 1);
        AddPosting(db, PostingKind.SavingsPlan, null, null, inCat.Id, null, month, 33m);
        AddPosting(db, PostingKind.SavingsPlan, null, null, outCat.Id, null, month, 77m);
        await db.SaveChangesAsync(CancellationToken.None);

        var result = await sut.QueryAsync(Query(user.Id, $"Type:{PostingKind.SavingsPlan}", month, ReportPostingsPeriod.Current,
            kind: PostingKind.SavingsPlan, includeCategory: true, filters: Filters(savingsPlanCategoryIds: new[] { cat.Id })), CancellationToken.None);

        Assert.Single(result.Postings);
        Assert.Equal(33m, result.Postings[0].Amount);
        Assert.Equal("In", result.Postings[0].EntityName);
        conn.Dispose();
    }

    /// <summary>Security category filters restrict the eligible securities of a type row when IncludeCategory is active.</summary>
    [Fact]
    public async Task QueryAsync_SecurityCategoryFilter_LimitsSelection()
    {
        var (sut, db, conn) = Create();
        var user = AddUser(db, "u");
        var cat = new SecurityCategory(user.Id, "Cat");
        db.SecurityCategories.Add(cat);
        var inCat = new Security(user.Id, "In", "IN1", null, null, "EUR", cat.Id);
        var outCat = new Security(user.Id, "Out", "OUT1", null, null, "EUR", null);
        db.Securities.AddRange(inCat, outCat);
        var month = new DateTime(2025, 9, 1);
        AddPosting(db, PostingKind.Security, null, null, null, inCat.Id, month, 44m);
        AddPosting(db, PostingKind.Security, null, null, null, outCat.Id, month, 66m);
        await db.SaveChangesAsync(CancellationToken.None);

        var result = await sut.QueryAsync(Query(user.Id, $"Type:{PostingKind.Security}", month, ReportPostingsPeriod.Current,
            kind: PostingKind.Security, includeCategory: true, filters: Filters(securityCategoryIds: new[] { cat.Id })), CancellationToken.None);

        Assert.Single(result.Postings);
        Assert.Equal(44m, result.Postings[0].Amount);
        Assert.Equal("In", result.Postings[0].EntityName);
        conn.Dispose();
    }

    /// <summary>The SecuritySubTypes filter keeps only postings of the selected sub types; postings without a sub type are excluded.</summary>
    [Fact]
    public async Task QueryAsync_SecuritySubTypesFilter_KeepsOnlySelectedSubTypes()
    {
        var (sut, db, conn) = Create();
        var user = AddUser(db, "u");
        var sec = new Security(user.Id, "SecA", "IDA", null, null, "EUR", null);
        db.Securities.Add(sec);
        var month = new DateTime(2025, 9, 1);
        AddPosting(db, PostingKind.Security, null, null, null, sec.Id, month, -500m, subType: SecurityPostingSubType.Buy);
        AddPosting(db, PostingKind.Security, null, null, null, sec.Id, month, 250m, subType: SecurityPostingSubType.Sell);
        AddPosting(db, PostingKind.Security, null, null, null, sec.Id, month, 100m, subType: SecurityPostingSubType.Dividend);
        AddPosting(db, PostingKind.Security, null, null, null, sec.Id, month, 7m); // no sub type
        await db.SaveChangesAsync(CancellationToken.None);

        var filters = Filters(securitySubTypes: new[] { (int)SecurityPostingSubType.Buy, (int)SecurityPostingSubType.Sell });

        var result = await sut.QueryAsync(Query(user.Id, $"Type:{PostingKind.Security}", month, ReportPostingsPeriod.Current,
            kind: PostingKind.Security, filters: filters), CancellationToken.None);

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(-250m, result.Postings.Sum(p => p.Amount));
        Assert.All(result.Postings, p => Assert.True(p.SecuritySubType is SecurityPostingSubType.Buy or SecurityPostingSubType.Sell));
        conn.Dispose();
    }

    /// <summary>Text filters match against subject and description and exclude non-matching postings.</summary>
    [Fact]
    public async Task QueryAsync_TextFilter_MatchesSubjectAndDescription()
    {
        var (sut, db, conn) = Create();
        var user = AddUser(db, "u");
        var acc = new Account(user.Id, AccountType.Giro, "Giro", null, Guid.NewGuid());
        db.Accounts.Add(acc);
        var month = new DateTime(2025, 9, 1);
        AddPosting(db, PostingKind.Bank, acc.Id, null, null, null, month, 10m, subject: "Gehalt Müller");
        AddPosting(db, PostingKind.Bank, acc.Id, null, null, null, month, 20m, description: "Enthält Gehalt hier");
        AddPosting(db, PostingKind.Bank, acc.Id, null, null, null, month, 30m, subject: "Miete");
        await db.SaveChangesAsync(CancellationToken.None);

        var filters = new ReportAggregationFilters(AccountIds: null, ContactIds: null, SavingsPlanIds: null, SecurityIds: null,
            ContactCategoryIds: null, SavingsPlanCategoryIds: null, SecurityCategoryIds: null, SecuritySubTypes: null,
            IncludeDividendRelated: false, BankTextFilters: new[] { "Gehalt" }, ContactTextFilters: null, SavingsPlanTextFilters: null, SecurityTextFilters: null);

        var result = await sut.QueryAsync(Query(user.Id, $"Account:{acc.Id}", month, ReportPostingsPeriod.Current, filters: filters), CancellationToken.None);

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(30m, result.Postings.Sum(p => p.Amount));
        conn.Dispose();
    }

    /// <summary>Zero-amount postings are excluded exactly like in the aggregation.</summary>
    [Fact]
    public async Task QueryAsync_ZeroAmountPostings_AreExcluded()
    {
        var (sut, db, conn) = Create();
        var user = AddUser(db, "u");
        var acc = new Account(user.Id, AccountType.Giro, "Giro", null, Guid.NewGuid());
        db.Accounts.Add(acc);
        var month = new DateTime(2025, 9, 1);
        AddPosting(db, PostingKind.Bank, acc.Id, null, null, null, month, 0m);
        AddPosting(db, PostingKind.Bank, acc.Id, null, null, null, month, 5m);
        await db.SaveChangesAsync(CancellationToken.None);

        var result = await sut.QueryAsync(Query(user.Id, $"Account:{acc.Id}", month, ReportPostingsPeriod.Current), CancellationToken.None);

        Assert.Single(result.Postings);
        conn.Dispose();
    }

    /// <summary>With valuta mode the valuta date decides the cell assignment; DateTime.MinValue falls back to the booking date.</summary>
    [Fact]
    public async Task QueryAsync_UseValutaDate_UsesValutaAndFallsBackOnMinValue()
    {
        var (sut, db, conn) = Create();
        var user = AddUser(db, "u");
        var acc = new Account(user.Id, AccountType.Giro, "Giro", null, Guid.NewGuid());
        db.Accounts.Add(acc);
        var month = new DateTime(2025, 9, 1);
        // booking date in August, valuta in September -> inside the September window only with valuta
        AddPosting(db, PostingKind.Bank, acc.Id, null, null, null, new DateTime(2025, 8, 30), 10m).SetValutaDate(new DateTime(2025, 9, 2));
        // valuta MinValue -> falls back to booking date (August) -> outside
        AddPosting(db, PostingKind.Bank, acc.Id, null, null, null, new DateTime(2025, 8, 15), 20m).SetValutaDate(DateTime.MinValue);
        // booking date in September, valuta MinValue -> inside
        AddPosting(db, PostingKind.Bank, acc.Id, null, null, null, month.AddDays(4), 30m).SetValutaDate(DateTime.MinValue);
        await db.SaveChangesAsync(CancellationToken.None);

        var result = await sut.QueryAsync(Query(user.Id, $"Account:{acc.Id}", month, ReportPostingsPeriod.Current, useValuta: true), CancellationToken.None);

        Assert.Equal(40m, result.Postings.Sum(p => p.Amount));
        conn.Dispose();
    }

    /// <summary>With the linked-account source the plan cell lists the bank postings of linked accounts; unlinked plans keep their own postings.</summary>
    [Fact]
    public async Task QueryAsync_LinkedSavingsPlanSource_ReturnsLinkedBankPostings()
    {
        var (sut, db, conn) = Create();
        var user = AddUser(db, "u");
        var linked = new SavingsPlan(user.Id, "Verknüpft", SavingsPlanType.Open, null, null, null);
        var plain = new SavingsPlan(user.Id, "Einfach", SavingsPlanType.Open, null, null, null);
        var contract = new Account(user.Id, AccountType.Savings, "Vertragskonto", null, Guid.NewGuid());
        db.SavingsPlans.AddRange(linked, plain);
        db.Accounts.Add(contract);
        db.SavingsPlanAccountLinks.Add(new SavingsPlanAccountLink(linked.Id, contract.Id, user.Id));
        var month = new DateTime(2025, 9, 1);
        AddPosting(db, PostingKind.SavingsPlan, null, null, linked.Id, null, month, 111m);
        AddPosting(db, PostingKind.SavingsPlan, null, null, plain.Id, null, month, 55m);
        AddPosting(db, PostingKind.Bank, contract.Id, null, null, null, month, 700m);
        await db.SaveChangesAsync(CancellationToken.None);

        // the linked plan's cell is composed of the linked account's bank postings
        var linkedResult = await sut.QueryAsync(Query(user.Id, $"SavingsPlan:{linked.Id}", month, ReportPostingsPeriod.Current,
            kind: PostingKind.SavingsPlan, source: SavingsPlanPostingSource.LinkedAccountPostings), CancellationToken.None);
        Assert.Single(linkedResult.Postings);
        Assert.Equal(700m, linkedResult.Postings[0].Amount);
        Assert.Equal("Vertragskonto", linkedResult.Postings[0].EntityName);

        // plans without links keep their own plan postings
        var plainResult = await sut.QueryAsync(Query(user.Id, $"SavingsPlan:{plain.Id}", month, ReportPostingsPeriod.Current,
            kind: PostingKind.SavingsPlan, source: SavingsPlanPostingSource.LinkedAccountPostings), CancellationToken.None);
        Assert.Single(plainResult.Postings);
        Assert.Equal(55m, plainResult.Postings[0].Amount);
        Assert.Equal("Einfach", plainResult.Postings[0].EntityName);
        conn.Dispose();
    }

    /// <summary>Type and total rows mix linked bank postings and own plan postings under the linked-account source; the bank type row is unaffected.</summary>
    [Fact]
    public async Task QueryAsync_LinkedSavingsPlanSource_TypeAndTotalRows_MixLinkedAndUnlinked()
    {
        var (sut, db, conn) = Create();
        var user = AddUser(db, "u");
        var linked = new SavingsPlan(user.Id, "Verknüpft", SavingsPlanType.Open, null, null, null);
        var plain = new SavingsPlan(user.Id, "Einfach", SavingsPlanType.Open, null, null, null);
        var contract = new Account(user.Id, AccountType.Savings, "Vertragskonto", null, Guid.NewGuid());
        var giro = new Account(user.Id, AccountType.Giro, "Giro", null, Guid.NewGuid());
        db.SavingsPlans.AddRange(linked, plain);
        db.Accounts.AddRange(contract, giro);
        db.SavingsPlanAccountLinks.Add(new SavingsPlanAccountLink(linked.Id, contract.Id, user.Id));
        var month = new DateTime(2025, 9, 1);
        AddPosting(db, PostingKind.SavingsPlan, null, null, linked.Id, null, month, 111m); // shadowed by the account link
        AddPosting(db, PostingKind.SavingsPlan, null, null, plain.Id, null, month, 55m);
        AddPosting(db, PostingKind.Bank, contract.Id, null, null, null, month, 700m);
        AddPosting(db, PostingKind.Bank, giro.Id, null, null, null, month, 42m);
        await db.SaveChangesAsync(CancellationToken.None);

        // Type:SavingsPlan resolves linked plans to the linked account's bank postings; unlinked plans keep their own postings
        var type = await sut.QueryAsync(Query(user.Id, $"Type:{PostingKind.SavingsPlan}", month, ReportPostingsPeriod.Current,
            kind: PostingKind.SavingsPlan, source: SavingsPlanPostingSource.LinkedAccountPostings), CancellationToken.None);
        Assert.Equal(2, type.TotalCount);
        var derived = Assert.Single(type.Postings.Where(p => p.Kind == PostingKind.Bank));
        Assert.Equal(700m, derived.Amount);
        Assert.Equal("Vertragskonto", derived.EntityName);
        var ownPlan = Assert.Single(type.Postings.Where(p => p.Kind == PostingKind.SavingsPlan));
        Assert.Equal(55m, ownPlan.Amount);
        Assert.Equal("Einfach", ownPlan.EntityName);

        // Type:Bank is unaffected by the savings plan source: plain bank postings of owned accounts
        var bankType = await sut.QueryAsync(Query(user.Id, $"Type:{PostingKind.Bank}", month, ReportPostingsPeriod.Current,
            kind: PostingKind.Bank, source: SavingsPlanPostingSource.LinkedAccountPostings), CancellationToken.None);
        Assert.Equal(2, bankType.TotalCount);
        Assert.Equal(742m, bankType.Postings.Sum(p => p.Amount));
        Assert.All(bankType.Postings, p => Assert.Equal(PostingKind.Bank, p.Kind));

        // the total row unions the per-kind selections exactly like the aggregation does:
        // bank postings + the linked plan's derived bank postings + the unlinked plan's own postings
        var total = await sut.QueryAsync(Query(user.Id, null, month, ReportPostingsPeriod.Current,
            kinds: new[] { PostingKind.Bank, PostingKind.SavingsPlan }, source: SavingsPlanPostingSource.LinkedAccountPostings), CancellationToken.None);
        Assert.Equal(4, total.TotalCount);
        Assert.Equal(1497m, total.Postings.Sum(p => p.Amount));
        Assert.Equal(3, total.Postings.Count(p => p.Kind == PostingKind.Bank));
        Assert.Single(total.Postings.Where(p => p.Kind == PostingKind.SavingsPlan));
        conn.Dispose();
    }

    /// <summary>The netted dividend path lists dividend/fee/tax group members inside the aggregation load window even outside the cell window so the overlay sum matches the cell.</summary>
    [Fact]
    public async Task QueryAsync_NettedDividends_ReturnsAllGroupMembers()
    {
        var (sut, db, conn) = Create();
        var user = AddUser(db, "u");
        var sec = new Security(user.Id, "SecA", "IDA", null, null, "EUR", null);
        db.Securities.Add(sec);
        var month = new DateTime(2025, 9, 1);
        var group = Guid.NewGuid();
        AddPosting(db, PostingKind.Security, null, null, null, sec.Id, month.AddDays(10), 100m, subType: SecurityPostingSubType.Dividend).SetGroup(group);
        // fee/tax members outside the cell window but inside the load window still belong to the netted cell value
        AddPosting(db, PostingKind.Security, null, null, null, sec.Id, month.AddMonths(-1).AddDays(2), -10m, subType: SecurityPostingSubType.Fee).SetGroup(group);
        AddPosting(db, PostingKind.Security, null, null, null, sec.Id, month.AddDays(11), -5m, subType: SecurityPostingSubType.Tax).SetGroup(group);
        // a buy posting of the same security is not part of the group and must not be listed
        AddPosting(db, PostingKind.Security, null, null, null, sec.Id, month.AddDays(3), -1000m, subType: SecurityPostingSubType.Buy).SetGroup(Guid.NewGuid());
        await db.SaveChangesAsync(CancellationToken.None);

        var filters = new ReportAggregationFilters(AccountIds: null, ContactIds: null, SavingsPlanIds: null, SecurityIds: null,
            ContactCategoryIds: null, SavingsPlanCategoryIds: null, SecurityCategoryIds: null, SecuritySubTypes: null,
            IncludeDividendRelated: true, BankTextFilters: null, ContactTextFilters: null, SavingsPlanTextFilters: null, SecurityTextFilters: null);

        var result = await sut.QueryAsync(Query(user.Id, $"Security:{sec.Id}", month, ReportPostingsPeriod.Current,
            kind: PostingKind.Security, filters: filters), CancellationToken.None);

        Assert.Equal(3, result.TotalCount);
        Assert.Equal(85m, result.Postings.Sum(p => p.Amount));
        conn.Dispose();
    }

    /// <summary>With SecurityTextFilters the netted dividend path only lists groups whose dividend anchor matches the pattern.</summary>
    [Fact]
    public async Task QueryAsync_NettedDividends_SecurityTextFilterAppliesToAnchorPostings()
    {
        var (sut, db, conn) = Create();
        var user = AddUser(db, "u");
        var sec = new Security(user.Id, "SecA", "IDA", null, null, "EUR", null);
        db.Securities.Add(sec);
        var month = new DateTime(2025, 9, 1);
        var matching = Guid.NewGuid();
        var other = Guid.NewGuid();
        AddPosting(db, PostingKind.Security, null, null, null, sec.Id, month.AddDays(10), 100m, subject: "ACME Dividende", subType: SecurityPostingSubType.Dividend).SetGroup(matching);
        AddPosting(db, PostingKind.Security, null, null, null, sec.Id, month.AddDays(11), -10m, subType: SecurityPostingSubType.Fee).SetGroup(matching);
        AddPosting(db, PostingKind.Security, null, null, null, sec.Id, month.AddDays(12), 200m, subject: "BARS Dividende", subType: SecurityPostingSubType.Dividend).SetGroup(other);
        AddPosting(db, PostingKind.Security, null, null, null, sec.Id, month.AddDays(13), -20m, subType: SecurityPostingSubType.Tax).SetGroup(other);
        await db.SaveChangesAsync(CancellationToken.None);

        var result = await sut.QueryAsync(Query(user.Id, $"Security:{sec.Id}", month, ReportPostingsPeriod.Current,
            kind: PostingKind.Security, filters: Filters(includeDividendRelated: true, securityTextFilters: new[] { "ACME" })), CancellationToken.None);

        // only the group whose dividend anchor matches the text filter is listed
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(90m, result.Postings.Sum(p => p.Amount));
        Assert.Contains(result.Postings, p => p.Subject == "ACME Dividende");
        Assert.DoesNotContain(result.Postings, p => p.Subject == "BARS Dividende");
        conn.Dispose();
    }

    /// <summary>A Security:{id}:{subType} key only lists postings of that sub type.</summary>
    [Fact]
    public async Task QueryAsync_SubTypeEntityKey_FiltersPostingsBySubType()
    {
        var (sut, db, conn) = Create();
        var user = AddUser(db, "u");
        var sec = new Security(user.Id, "SecA", "IDA", null, null, "EUR", null);
        db.Securities.Add(sec);
        var month = new DateTime(2025, 9, 1);
        AddPosting(db, PostingKind.Security, null, null, null, sec.Id, month, -500m, subType: SecurityPostingSubType.Buy);
        AddPosting(db, PostingKind.Security, null, null, null, sec.Id, month, 200m, subType: SecurityPostingSubType.Dividend);
        await db.SaveChangesAsync(CancellationToken.None);

        var result = await sut.QueryAsync(Query(user.Id, $"Security:{sec.Id}:{SecurityPostingSubType.Buy}", month, ReportPostingsPeriod.Current,
            kind: PostingKind.Security), CancellationToken.None);

        Assert.Single(result.Postings);
        Assert.Equal(-500m, result.Postings[0].Amount);
        conn.Dispose();
    }

    /// <summary>Rows are sorted by effective date, newest first.</summary>
    [Fact]
    public async Task QueryAsync_ResultIsSortedByEffectiveDateDescending()
    {
        var (sut, db, conn) = Create();
        var user = AddUser(db, "u");
        var acc = new Account(user.Id, AccountType.Giro, "Giro", null, Guid.NewGuid());
        db.Accounts.Add(acc);
        var month = new DateTime(2025, 9, 1);
        AddPosting(db, PostingKind.Bank, acc.Id, null, null, null, month.AddDays(1), 1m);
        AddPosting(db, PostingKind.Bank, acc.Id, null, null, null, month.AddDays(20), 2m);
        AddPosting(db, PostingKind.Bank, acc.Id, null, null, null, month.AddDays(10), 3m);
        await db.SaveChangesAsync(CancellationToken.None);

        var result = await sut.QueryAsync(Query(user.Id, $"Account:{acc.Id}", month, ReportPostingsPeriod.Current), CancellationToken.None);

        Assert.Equal(new[] { 2m, 3m, 1m }, result.Postings.Select(p => p.Amount).ToArray());
        conn.Dispose();
    }

    /// <summary>More than 5000 matches are truncated to 5000 rows while TotalCount keeps the complete count.</summary>
    [Fact]
    public async Task QueryAsync_TruncatesAtLimitButReportsFullCount()
    {
        var (sut, db, conn) = Create();
        var user = AddUser(db, "u");
        var acc = new Account(user.Id, AccountType.Giro, "Giro", null, Guid.NewGuid());
        db.Accounts.Add(acc);
        var month = new DateTime(2025, 9, 1);
        for (var i = 0; i < 5005; i++)
        {
            AddPosting(db, PostingKind.Bank, acc.Id, null, null, null, month.AddDays(1 + (i % 27)), 1m);
        }
        await db.SaveChangesAsync(CancellationToken.None);

        var result = await sut.QueryAsync(Query(user.Id, $"Account:{acc.Id}", month, ReportPostingsPeriod.Current), CancellationToken.None);

        Assert.True(result.Truncated);
        Assert.Equal(5005, result.TotalCount);
        Assert.Equal(5000, result.Postings.Count);
        // TotalAmount is the exact sum of the untruncated selection, not the page sum
        Assert.Equal(5005m, result.TotalAmount);
        Assert.Equal(5000m, result.Postings.Sum(p => p.Amount));
        conn.Dispose();
    }

    /// <summary>
    /// Fee/tax members of a dividend group only contribute inside the aggregation's load
    /// window ([startMonth, analysis+1M) with startMonth driven by Take): a member booked
    /// after the analysis month or before the take window never reaches the netted cell
    /// value and must not be listed either.
    /// </summary>
    [Fact]
    public async Task QueryAsync_NettedDividends_MembersOutsideLoadWindow_AreExcluded()
    {
        var (sut, db, conn) = Create();
        var user = AddUser(db, "u");
        var sec = new Security(user.Id, "SecA", "IDA", null, null, "EUR", null);
        db.Securities.Add(sec);
        var month = new DateTime(2025, 9, 1);
        var group = Guid.NewGuid();
        AddPosting(db, PostingKind.Security, null, null, null, sec.Id, month.AddDays(10), 100m, subType: SecurityPostingSubType.Dividend).SetGroup(group);
        AddPosting(db, PostingKind.Security, null, null, null, sec.Id, month.AddDays(11), -10m, subType: SecurityPostingSubType.Fee).SetGroup(group);
        // booked after the analysis month -> outside [startMonth, analysis+1M)
        AddPosting(db, PostingKind.Security, null, null, null, sec.Id, month.AddMonths(1).AddDays(2), -20m, subType: SecurityPostingSubType.Tax).SetGroup(group);
        // booked before the take window (take=2 -> startMonth = August 2025)
        AddPosting(db, PostingKind.Security, null, null, null, sec.Id, new DateTime(2025, 7, 15), -30m, subType: SecurityPostingSubType.Fee).SetGroup(group);
        await db.SaveChangesAsync(CancellationToken.None);

        var filters = Filters(includeDividendRelated: true);
        var result = await sut.QueryAsync(Query(user.Id, $"Security:{sec.Id}", month, ReportPostingsPeriod.Current,
            kind: PostingKind.Security, filters: filters, take: 2), CancellationToken.None);

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(90m, result.TotalAmount);
        Assert.Equal(90m, result.Postings.Sum(p => p.Amount));

        // widening the take window by one month lets the July fee join the group again
        var wider = await sut.QueryAsync(Query(user.Id, $"Security:{sec.Id}", month, ReportPostingsPeriod.Current,
            kind: PostingKind.Security, filters: filters, take: 3), CancellationToken.None);
        Assert.Equal(3, wider.TotalCount);
        Assert.Equal(60m, wider.TotalAmount);
        conn.Dispose();
    }

    /// <summary>
    /// For the all-history interval there is no cell window, but the aggregation still loads
    /// postings only inside [startMonth, analysis+1M): anchors and members outside that
    /// take-bounded window (including ungrouped GroupId.Empty members) are excluded.
    /// </summary>
    [Fact]
    public async Task QueryAsync_NettedDividends_AllHistory_UsesTakeBoundedLoadWindow()
    {
        var (sut, db, conn) = Create();
        var user = AddUser(db, "u");
        var sec = new Security(user.Id, "SecA", "IDA", null, null, "EUR", null);
        db.Securities.Add(sec);
        var analysis = new DateTime(2025, 9, 1);
        var group = Guid.NewGuid();
        // anchor inside the take window (take=6 -> startMonth = April 2025)
        AddPosting(db, PostingKind.Security, null, null, null, sec.Id, new DateTime(2025, 7, 10), 100m, subType: SecurityPostingSubType.Dividend).SetGroup(group);
        AddPosting(db, PostingKind.Security, null, null, null, sec.Id, new DateTime(2025, 7, 11), -10m, subType: SecurityPostingSubType.Fee).SetGroup(group);
        // member of the same group after the analysis month -> outside the load window
        AddPosting(db, PostingKind.Security, null, null, null, sec.Id, new DateTime(2025, 10, 2), -50m, subType: SecurityPostingSubType.Tax).SetGroup(group);
        // a group whose anchor lies before the take window contributes nothing
        var oldGroup = Guid.NewGuid();
        AddPosting(db, PostingKind.Security, null, null, null, sec.Id, new DateTime(2025, 1, 10), 999m, subType: SecurityPostingSubType.Dividend).SetGroup(oldGroup);
        AddPosting(db, PostingKind.Security, null, null, null, sec.Id, new DateTime(2025, 1, 11), -9m, subType: SecurityPostingSubType.Fee).SetGroup(oldGroup);
        // ungrouped (GroupId.Empty) dividend inside the window + its ungrouped fee outside
        AddPosting(db, PostingKind.Security, null, null, null, sec.Id, new DateTime(2025, 7, 12), 60m, subType: SecurityPostingSubType.Dividend);
        AddPosting(db, PostingKind.Security, null, null, null, sec.Id, new DateTime(2025, 10, 3), -7m, subType: SecurityPostingSubType.Fee);
        await db.SaveChangesAsync(CancellationToken.None);

        var result = await sut.QueryAsync(Query(user.Id, $"Security:{sec.Id}", new DateTime(2000, 1, 1), ReportPostingsPeriod.Current,
            interval: ReportInterval.AllHistory, kind: PostingKind.Security, analysis: analysis,
            filters: Filters(includeDividendRelated: true), take: 6), CancellationToken.None);

        Assert.Equal(3, result.TotalCount);
        Assert.Equal(150m, result.TotalAmount);
        conn.Dispose();
    }

    /// <summary>
    /// The year-ago comparison widens the load window by one year: a year-ago cell's anchor
    /// is only found when CompareYear is set, and members up to the widened bound are listed.
    /// </summary>
    [Fact]
    public async Task QueryAsync_NettedDividends_CompareYear_WidensLoadWindow()
    {
        var (sut, db, conn) = Create();
        var user = AddUser(db, "u");
        var sec = new Security(user.Id, "SecA", "IDA", null, null, "EUR", null);
        db.Securities.Add(sec);
        var month = new DateTime(2025, 9, 1);
        var group = Guid.NewGuid();
        AddPosting(db, PostingKind.Security, null, null, null, sec.Id, new DateTime(2024, 9, 10), 100m, subType: SecurityPostingSubType.Dividend).SetGroup(group);
        AddPosting(db, PostingKind.Security, null, null, null, sec.Id, new DateTime(2024, 9, 12), -10m, subType: SecurityPostingSubType.Fee).SetGroup(group);
        await db.SaveChangesAsync(CancellationToken.None);

        var filters = Filters(includeDividendRelated: true);
        // without the year-ago comparison the load window starts at the take bound
        // (take=12 -> September 2024 is still inside); take=1 -> startMonth = analysis month
        var withoutCompare = await sut.QueryAsync(Query(user.Id, $"Security:{sec.Id}", month, ReportPostingsPeriod.YearAgo,
            kind: PostingKind.Security, filters: filters, take: 1), CancellationToken.None);
        Assert.Empty(withoutCompare.Postings);

        var withCompare = await sut.QueryAsync(Query(user.Id, $"Security:{sec.Id}", month, ReportPostingsPeriod.YearAgo,
            kind: PostingKind.Security, filters: filters, take: 1, compareYear: true), CancellationToken.None);
        Assert.Equal(2, withCompare.TotalCount);
        Assert.Equal(90m, withCompare.TotalAmount);
        conn.Dispose();
    }

    /// <summary>A sub-typed Security:{id}:{subType} key has no counterpart cell in netted mode and is rejected.</summary>
    [Fact]
    public async Task QueryAsync_NettedDividends_SubTypeKey_Throws()
    {
        var (sut, db, conn) = Create();
        var user = AddUser(db, "u");
        var sec = new Security(user.Id, "SecA", "IDA", null, null, "EUR", null);
        db.Securities.Add(sec);
        var month = new DateTime(2025, 9, 1);
        await db.SaveChangesAsync(CancellationToken.None);

        await Assert.ThrowsAsync<ArgumentException>(() => sut.QueryAsync(
            Query(user.Id, $"Security:{sec.Id}:{SecurityPostingSubType.Sell}", month, ReportPostingsPeriod.Current,
                kind: PostingKind.Security, filters: Filters(includeDividendRelated: true)), CancellationToken.None));
        conn.Dispose();
    }

    /// <summary>Numeric names of undefined enum members in group keys are rejected like unknown keys.</summary>
    [Fact]
    public async Task QueryAsync_NumericGroupKeyNames_Throw()
    {
        var (sut, db, conn) = Create();
        var user = AddUser(db, "u");
        await db.SaveChangesAsync(CancellationToken.None);
        var month = new DateTime(2025, 9, 1);

        await Assert.ThrowsAsync<ArgumentException>(() => sut.QueryAsync(Query(user.Id, "Type:5", month, ReportPostingsPeriod.Current), CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => sut.QueryAsync(Query(user.Id, "Category:9:_none", month, ReportPostingsPeriod.Current), CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => sut.QueryAsync(Query(user.Id, $"Security:{Guid.NewGuid()}:5", month, ReportPostingsPeriod.Current), CancellationToken.None));
        conn.Dispose();
    }

    /// <summary>Undefined enum members and a negative take are rejected before any data is touched.</summary>
    [Fact]
    public async Task QueryAsync_UndefinedEnumsAndNegativeTake_Throw()
    {
        var (sut, db, conn) = Create();
        var user = AddUser(db, "u");
        await db.SaveChangesAsync(CancellationToken.None);
        var month = new DateTime(2025, 9, 1);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => sut.QueryAsync(Query(user.Id, null, month, (ReportPostingsPeriod)9), CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => sut.QueryAsync(Query(user.Id, null, month, ReportPostingsPeriod.Current, interval: (ReportInterval)99), CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => sut.QueryAsync(Query(user.Id, null, month, ReportPostingsPeriod.Current, kind: (PostingKind)7), CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => sut.QueryAsync(Query(user.Id, null, month, ReportPostingsPeriod.Current, kinds: new[] { PostingKind.Bank, (PostingKind)7 }), CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => sut.QueryAsync(Query(user.Id, null, month, ReportPostingsPeriod.Current, source: (SavingsPlanPostingSource)9), CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => sut.QueryAsync(Query(user.Id, null, month, ReportPostingsPeriod.Current, take: -1), CancellationToken.None));
        conn.Dispose();
    }
}
