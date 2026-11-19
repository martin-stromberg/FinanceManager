using FinanceManager.Domain.Accounts;
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
/// Covers the bank posting-type filter (<c>Filters.BankPostingTypes</c>) inside the cross-kind
/// projection (<c>CompareProjection = true</c>): prior-year bank events of unselected posting types
/// must produce neither a projection amount nor expected items, while the savings-plan events
/// derived via <see cref="SavingsPlanPostingSource.LinkedAccountPostings"/> intentionally stay
/// unfiltered (scope exclusion of the feature).
/// </summary>
public sealed class ReportAggregationBankPostingTypeProjectionTests
{
    private static AppDbContext CreateDb(out SqliteConnection conn)
    {
        conn = new SqliteConnection("DataSource=:memory:");
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

    private static SavingsPlan AddSavingsPlan(AppDbContext db, Guid ownerUserId, string name)
    {
        var plan = new SavingsPlan(ownerUserId, name, SavingsPlanType.Open, null, null, null, null);
        db.SavingsPlans.Add(plan);
        return plan;
    }

    private static Posting AddBankPosting(AppDbContext db, Account account, DateTime bookingDate, decimal amount,
        BankPostingType postingType, string subject = "Posting")
    {
        var posting = new Posting(
            Guid.NewGuid(), PostingKind.Bank, account.Id, null, null, null,
            bookingDate, bookingDate, amount, subject, null, null, null);
        posting.SetPostingType(postingType);
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
    /// With a bank posting-type allow-list the projection only considers prior-year events of the
    /// selected types: the inflow event stays expected while the outflow event of the same entity
    /// contributes neither to the projection amount nor to the expected items.
    /// </summary>
    [Fact]
    public async Task QueryAsync_BankProjection_RespectsBankPostingTypes()
    {
        using var db = CreateDb(out var conn);
        var user = await AddUserAsync(db, "bank-typefilter");
        var account = AddAccount(db, user.Id, "Giro");
        var analysis = new DateTime(2026, 5, 1);
        AddBankPosting(db, account, new DateTime(2025, 5, 10), 120m, BankPostingType.Inflow, "Gehalt");
        AddBankPosting(db, account, new DateTime(2025, 5, 12), -40m, BankPostingType.Outflow, "Abbuchung");
        await RebuildAggregatesAsync(db, user.Id);

        var result = await CreateSut(db).QueryAsync(new ReportAggregationQuery(
            user.Id, PostingKind.Bank, ReportInterval.Month, 1,
            IncludeCategory: false, ComparePrevious: false, CompareYear: false, CompareProjection: true,
            AnalysisDate: analysis,
            Filters: new ReportAggregationFilters(BankPostingTypes: new[] { (int)BankPostingType.Inflow })), CancellationToken.None);

        Assert.True(result.ComparedProjection);
        var point = result.Points.Single(p => p.GroupKey == $"Account:{account.Id}" && p.PeriodStart == analysis);
        Assert.Equal(120m, point.ProjectionAmount);
        var expected = Assert.Single(point.ProjectionExpectedItems!);
        Assert.Equal(120m, expected.Amount);
        Assert.Equal(new DateTime(2026, 5, 10), expected.ExpectedDate);
        conn.Dispose();
    }

    /// <summary>
    /// A prior-year posting of a type outside the selection produces no expectation at all: the
    /// entity row only survives because a selected current-year posting keeps the leaf alive, and
    /// its latest-period row carries an empty projection (amount-only, no expected items).
    /// </summary>
    [Fact]
    public async Task QueryAsync_BankProjection_BankPostingTypes_UnselectedTypesProduceNoExpectation()
    {
        using var db = CreateDb(out var conn);
        var user = await AddUserAsync(db, "bank-typefilter-none");
        var account = AddAccount(db, user.Id, "Giro");
        var analysis = new DateTime(2026, 5, 1);
        AddBankPosting(db, account, new DateTime(2025, 5, 10), -40m, BankPostingType.Outflow, "Abbuchung");
        AddBankPosting(db, account, new DateTime(2026, 3, 15), 50m, BankPostingType.Inflow, "Gehalt");
        await RebuildAggregatesAsync(db, user.Id);

        var result = await CreateSut(db).QueryAsync(new ReportAggregationQuery(
            user.Id, PostingKind.Bank, ReportInterval.Month, 1,
            IncludeCategory: false, ComparePrevious: false, CompareYear: false, CompareProjection: true,
            AnalysisDate: analysis,
            Filters: new ReportAggregationFilters(BankPostingTypes: new[] { (int)BankPostingType.Inflow })), CancellationToken.None);

        Assert.True(result.ComparedProjection);
        var point = result.Points.Single(p => p.GroupKey == $"Account:{account.Id}" && p.PeriodStart == analysis);
        Assert.Equal(0m, point.ProjectionAmount);
        Assert.Null(point.ProjectionExpectedItems);
        conn.Dispose();
    }

    /// <summary>
    /// Scope exclusion pinning: with <see cref="SavingsPlanPostingSource.LinkedAccountPostings"/> the
    /// savings plan events come from the linked account's bank postings and are intentionally not
    /// restricted by the bank posting-type filter — the outflow posting still produces the plan's
    /// expectation even though the active filter only admits interest postings. The bank row itself
    /// stays filtered, so the linked account does not surface as a report row at all.
    /// </summary>
    [Fact]
    public async Task QueryAsync_SavingsPlanProjection_LinkedSource_IgnoresBankPostingTypes()
    {
        using var db = CreateDb(out var conn);
        var user = await AddUserAsync(db, "savings-typefilter");
        var linkedPlan = AddSavingsPlan(db, user.Id, "Linked Plan");
        var contract = AddAccount(db, user.Id, "Contract Account");
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        db.SavingsPlanAccountLinks.Add(new SavingsPlanAccountLink(linkedPlan.Id, contract.Id, user.Id));
        var analysis = new DateTime(2026, 5, 1);
        AddBankPosting(db, contract, new DateTime(2025, 5, 10), -100m, BankPostingType.Outflow, "Sparrate");
        await RebuildAggregatesAsync(db, user.Id);

        var result = await CreateSut(db).QueryAsync(new ReportAggregationQuery(
            user.Id, PostingKind.SavingsPlan, ReportInterval.Month, 1,
            IncludeCategory: false, ComparePrevious: false, CompareYear: false, CompareProjection: true,
            PostingKinds: new[] { PostingKind.Bank, PostingKind.SavingsPlan },
            AnalysisDate: analysis,
            SavingsPlanPostingSource: SavingsPlanPostingSource.LinkedAccountPostings,
            Filters: new ReportAggregationFilters(BankPostingTypes: new[] { (int)BankPostingType.Interest })), CancellationToken.None);

        Assert.True(result.ComparedProjection);
        var planPoint = result.Points.Single(p => p.GroupKey == $"SavingsPlan:{linkedPlan.Id}" && p.PeriodStart == analysis);
        Assert.Equal(-100m, planPoint.ProjectionAmount);
        var expected = Assert.Single(planPoint.ProjectionExpectedItems!);
        Assert.Equal(-100m, expected.Amount);
        Assert.DoesNotContain(result.Points, p => p.GroupKey == $"Account:{contract.Id}");
        conn.Dispose();
    }
}
