using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Savings;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Covers <see cref="SavingsPlanService.AnalyzeAsync"/>'s reachability projection: it derives an average
/// monthly contribution from a plan's past postings and extrapolates it forward to the target date, to
/// tell the user whether they are on track. These scenarios pin down the boundary case (exactly on target)
/// and confirm that several postings falling in the same calendar month count as one contributing month
/// for the average rather than being counted per posting.
/// </summary>
public sealed class SavingsPlanAnalysisTests
{
    private static (SavingsPlanService sut, AppDbContext db, SqliteConnection conn, Guid owner) Create()
    {
        var conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options;
        var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        var sut = new SavingsPlanService(db);
        var owner = Guid.NewGuid();
        return (sut, db, conn, owner);
    }

    private static async Task<Guid> CreatePlanAsync(AppDbContext db, Guid owner, string name, decimal target, DateTime targetDate)
    {
        var plan = new FinanceManager.Domain.Savings.SavingsPlan(owner, name, SavingsPlanType.OneTime, target, targetDate, null, null);
        db.SavingsPlans.Add(plan);
        await db.SaveChangesAsync();
        return plan.Id;
    }

    private static async Task AddPlanPostingAsync(AppDbContext db, Guid planId, DateTime date, decimal amount)
    {
        var p = new FinanceManager.Domain.Postings.Posting(Guid.NewGuid(), PostingKind.SavingsPlan, null, null, planId, null, date, amount, null, null, null, null);
        db.Postings.Add(p);
        await db.SaveChangesAsync();
    }

    /// <summary>Three months of 10 each, projected over the 9 remaining months at the same rate, lands exactly on the 120 target - the boundary case where the plan is still considered reachable.</summary>
    [Fact]
    public async Task Scenario1_ThreePastMonths10_Reacheable()
    {
        var (sut, db, conn, owner) = Create();
        var planId = await CreatePlanAsync(db, owner, "P", 120m, DateTime.Today.AddMonths(9));
        await AddPlanPostingAsync(db, planId, DateTime.Today.AddMonths(-1), 10m);
        await AddPlanPostingAsync(db, planId, DateTime.Today.AddMonths(-2), 10m);
        await AddPlanPostingAsync(db, planId, DateTime.Today.AddMonths(-3), 10m);

        var result = await sut.AnalyzeAsync(planId, owner, CancellationToken.None);
        Assert.True(result.TargetReachable);
        conn.Dispose();
    }

    /// <summary>Only two months of contributions falls short of the average rate needed to hit the 120 target by the due date, so the plan is correctly flagged as not reachable.</summary>
    [Fact]
    public async Task Scenario2_TwoPastMonths10_NotReacheable()
    {
        var (sut, db, conn, owner) = Create();
        var planId = await CreatePlanAsync(db, owner, "P", 120m, DateTime.Today.AddMonths(9));
        await AddPlanPostingAsync(db, planId, DateTime.Today.AddMonths(-1), 10m);
        await AddPlanPostingAsync(db, planId, DateTime.Today.AddMonths(-2), 10m);

        var result = await sut.AnalyzeAsync(planId, owner, CancellationToken.None);
        Assert.False(result.TargetReachable);
        conn.Dispose();
    }

    /// <summary>Same two distinct contributing months as Scenario2, but with an extra posting added within one of those months - showing that two same-month postings are aggregated into that one month's average rather than each counting as a separate contributing month, which raises the effective monthly rate enough to make the plan reachable.</summary>
    [Fact]
    public async Task Scenario3_TwoMonthsAgo10_AndLastMonth10_Reachable()
    {
        var (sut, db, conn, owner) = Create();
        var planId = await CreatePlanAsync(db, owner, "P", 120m, DateTime.Today.AddMonths(9));
        await AddPlanPostingAsync(db, planId, DateTime.Today.AddMonths(-2), 10m);
        await AddPlanPostingAsync(db, planId, DateTime.Today.AddMonths(-2).AddDays(1), 10m);
        await AddPlanPostingAsync(db, planId, DateTime.Today.AddMonths(-1), 10m);

        var result = await sut.AnalyzeAsync(planId, owner, CancellationToken.None);
        Assert.True(result.TargetReachable);
        conn.Dispose();
    }

    /// <summary>Four months of contributions comfortably exceeds the rate needed for the 120 target, confirming the reachable case is stable beyond the exact-boundary scenario.</summary>
    [Fact]
    public async Task Scenario4_FourPastMonths10_Reachable()
    {
        var (sut, db, conn, owner) = Create();
        var planId = await CreatePlanAsync(db, owner, "P", 120m, DateTime.Today.AddMonths(9));
        await AddPlanPostingAsync(db, planId, DateTime.Today.AddMonths(-1), 10m);
        await AddPlanPostingAsync(db, planId, DateTime.Today.AddMonths(-2), 10m);
        await AddPlanPostingAsync(db, planId, DateTime.Today.AddMonths(-3), 10m);
        await AddPlanPostingAsync(db, planId, DateTime.Today.AddMonths(-4), 10m);

        var result = await sut.AnalyzeAsync(planId, owner, CancellationToken.None);
        Assert.True(result.TargetReachable);
        conn.Dispose();
    }
}
