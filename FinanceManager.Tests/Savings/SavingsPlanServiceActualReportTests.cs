using FinanceManager.Domain.Accounts;
using FinanceManager.Domain.Postings;
using FinanceManager.Domain.Savings;
using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Savings;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Covers <see cref="SavingsPlanService.ReportActualBalanceAsync"/> ("Istmeldung"): it books the
/// difference between the reported balance and the current accumulated amount as a marked
/// correction posting with no account movement, only for plans without linked accounts.
/// </summary>
public sealed class SavingsPlanServiceActualReportTests
{
    private static (SavingsPlanService sut, AppDbContext db, SqliteConnection conn) Create()
    {
        var conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options;
        var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        var sut = new SavingsPlanService(db);
        return (sut, db, conn);
    }

    private static SavingsPlan AddPlan(AppDbContext db, Guid owner, string name)
    {
        var plan = new SavingsPlan(owner, name, SavingsPlanType.Open, null, null, null);
        db.SavingsPlans.Add(plan);
        return plan;
    }

    /// <summary>The report books the difference and the marker identifies it as a correction without account movement.</summary>
    [Fact]
    public async Task ReportActualBalanceAsync_CreatesMarkedDifferencePostingWithoutAccount()
    {
        var (sut, db, conn) = Create();
        var owner = Guid.NewGuid();
        var plan = AddPlan(db, owner, "Klamotten");
        var existing = new Posting(Guid.NewGuid(), PostingKind.SavingsPlan, null, null, plan.Id, null, DateTime.Today.AddDays(-10), -25.30m, null, null, null, null);
        db.Postings.Add(existing);
        await db.SaveChangesAsync(CancellationToken.None);

        var result = await sut.ReportActualBalanceAsync(plan.Id, owner, 50m, CancellationToken.None);

        Assert.Equal(75.30m, result.DifferenceAmount);
        Assert.Equal(50m, result.AccumulatedAmount);
        var posting = await db.Postings.SingleAsync(p => p.Id == result.PostingId, TestContext.Current.CancellationToken);
        Assert.Equal(PostingKind.SavingsPlan, posting.Kind);
        Assert.Equal(SavingsPlanPostingSubType.ActualReport, posting.SavingsPlanSubType);
        Assert.Null(posting.AccountId);
        Assert.Equal(plan.Id, posting.SavingsPlanId);
        conn.Dispose();
    }

    /// <summary>After the report the plan's accumulated amount equals the reported balance.</summary>
    [Fact]
    public async Task ReportActualBalanceAsync_AccumulatedMatchesReportedBalance()
    {
        var (sut, db, conn) = Create();
        var owner = Guid.NewGuid();
        var plan = AddPlan(db, owner, "Plan");
        var existing = new Posting(Guid.NewGuid(), PostingKind.SavingsPlan, null, null, plan.Id, null, DateTime.Today.AddDays(-10), 100m, null, null, null, null);
        db.Postings.Add(existing);
        await db.SaveChangesAsync(CancellationToken.None);

        await sut.ReportActualBalanceAsync(plan.Id, owner, 80m, CancellationToken.None);

        var dto = await sut.GetAsync(plan.Id, owner, CancellationToken.None);
        Assert.NotNull(dto);
        Assert.Equal(80m, dto!.CurrentAmount);
        conn.Dispose();
    }

    /// <summary>A downward correction creates a negative posting.</summary>
    [Fact]
    public async Task ReportActualBalanceAsync_BelowAccumulated_CreatesNegativePosting()
    {
        var (sut, db, conn) = Create();
        var owner = Guid.NewGuid();
        var plan = AddPlan(db, owner, "Plan");
        var existing = new Posting(Guid.NewGuid(), PostingKind.SavingsPlan, null, null, plan.Id, null, DateTime.Today.AddDays(-10), 100m, null, null, null, null);
        db.Postings.Add(existing);
        await db.SaveChangesAsync(CancellationToken.None);

        var result = await sut.ReportActualBalanceAsync(plan.Id, owner, 60m, CancellationToken.None);

        Assert.Equal(-40m, result.DifferenceAmount);
        conn.Dispose();
    }

    /// <summary>Reports are rejected for plans with linked accounts - the balance is the single truth there.</summary>
    [Fact]
    public async Task ReportActualBalanceAsync_LinkedPlan_ThrowsInvalidOperationException()
    {
        var (sut, db, conn) = Create();
        var owner = Guid.NewGuid();
        var plan = AddPlan(db, owner, "Plan");
        var account = new Account(owner, AccountType.Savings, "Vertragskonto", null, Guid.NewGuid());
        db.Accounts.Add(account);
        db.SavingsPlanAccountLinks.Add(new SavingsPlanAccountLink(plan.Id, account.Id, owner));
        await db.SaveChangesAsync(CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.ReportActualBalanceAsync(plan.Id, owner, 100m, CancellationToken.None));
        Assert.Empty(db.Postings);
        conn.Dispose();
    }

    /// <summary>Unknown or foreign plans are rejected.</summary>
    [Fact]
    public async Task ReportActualBalanceAsync_UnknownPlan_ThrowsArgumentException()
    {
        var (sut, db, conn) = Create();
        var owner = Guid.NewGuid();

        await Assert.ThrowsAsync<ArgumentException>(
            () => sut.ReportActualBalanceAsync(Guid.NewGuid(), owner, 100m, CancellationToken.None));
        conn.Dispose();
    }
}
