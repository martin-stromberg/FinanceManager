using FinanceManager.Domain.Accounts;
using FinanceManager.Domain.Postings;
using FinanceManager.Domain.Savings;
using FinanceManager.Domain.Users;
using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Reports;
using FinanceManager.Shared.Dtos.Reports;
using FinanceManager.Shared.Dtos.SavingsPlans;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Tests.Reports;

/// <summary>
/// Pins the intentional asymmetry of <see cref="ReportPostingsQueryService"/> for the
/// <see cref="SavingsPlanPostingSource.LinkedAccountPostings"/> source: the bank posting-type
/// filter (<c>Filters.BankPostingTypes</c>) applies to direct bank rows of the drilldown but
/// not to the source bank postings of linked savings plans — consistent with the scope
/// exclusion that keeps linked-source rows unfiltered in aggregation and projection.
/// </summary>
public sealed class ReportPostingsQueryServiceTests_LinkedSourceBankPostingTypes
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

    private static Posting AddBankPosting(AppDbContext db, Guid accountId, DateTime bookingDate, decimal amount, BankPostingType postingType)
    {
        var posting = new Posting(Guid.NewGuid(), PostingKind.Bank, accountId, null, null, null, bookingDate, amount);
        posting.SetPostingType(postingType);
        db.Postings.Add(posting);
        return posting;
    }

    private static ReportPostingsQuery Query(Guid owner, string? groupKey, DateTime periodStart, PostingKind kind,
        ReportAggregationFilters? filters = null, SavingsPlanPostingSource source = SavingsPlanPostingSource.PlanPostings)
        => new(owner, groupKey, periodStart, ReportPostingsPeriod.Current, kind, ReportInterval.Month,
            IncludeCategory: false, CompareProjection: false, UseValutaDate: false, PostingKinds: null,
            AnalysisDate: periodStart, Filters: filters, SavingsPlanPostingSource: source, Take: 24, CompareYear: false);

    /// <summary>
    /// Under the linked-account source the savings plan cell lists the source bank postings of the
    /// linked account unfiltered — even when their posting type is not selected — while the same
    /// filter keeps excluding the posting from direct bank cells.
    /// </summary>
    [Fact]
    public async Task QueryAsync_LinkedSavingsPlanSource_IgnoresBankPostingTypes()
    {
        var (sut, db, conn) = Create();
        var user = AddUser(db, "u");
        var plan = new SavingsPlan(user.Id, "Verknüpft", SavingsPlanType.Open, null, null, null);
        var contract = new Account(user.Id, AccountType.Savings, "Vertragskonto", null, Guid.NewGuid());
        db.SavingsPlans.Add(plan);
        db.Accounts.Add(contract);
        db.SavingsPlanAccountLinks.Add(new SavingsPlanAccountLink(plan.Id, contract.Id, user.Id));
        var month = new DateTime(2025, 9, 1);
        AddBankPosting(db, contract.Id, month.AddDays(5), -100m, BankPostingType.Outflow);
        await db.SaveChangesAsync(CancellationToken.None);

        // The filter admits only interest postings; the outflow source posting survives in the plan cell.
        var filters = new ReportAggregationFilters(BankPostingTypes: new[] { (int)BankPostingType.Interest });
        var planResult = await sut.QueryAsync(Query(user.Id, $"SavingsPlan:{plan.Id}", month, PostingKind.SavingsPlan,
            filters: filters, source: SavingsPlanPostingSource.LinkedAccountPostings), CancellationToken.None);
        var planPosting = Assert.Single(planResult.Postings);
        Assert.Equal(-100m, planPosting.Amount);
        Assert.Equal(PostingKind.Bank, planPosting.Kind);
        Assert.Equal("Vertragskonto", planPosting.EntityName);

        // Direct bank rows stay filtered: the same posting does not surface in the account cell.
        var bankResult = await sut.QueryAsync(Query(user.Id, $"Account:{contract.Id}", month, PostingKind.Bank,
            filters: filters), CancellationToken.None);
        Assert.Empty(bankResult.Postings);
        conn.Dispose();
    }
}
