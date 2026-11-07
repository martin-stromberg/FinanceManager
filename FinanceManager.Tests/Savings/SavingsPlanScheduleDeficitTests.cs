using FinanceManager.Domain.Postings;
using FinanceManager.Domain.Savings;
using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Savings;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Covers <see cref="SavingsPlanService.AnalyzeAsync"/>'s schedule-deficit projection: the backlog of a
/// plan against its own observed deposit rhythm (observed rate x elapsed contribution periods minus
/// received deposits). The scenarios pin down reversal handling (a reversed deposit and its storno drop
/// out of both sides), the separation of extraordinary disbursements from the deficit, interval
/// normalization of the observed rate and the null result for plans without a detectable rhythm.
/// </summary>
public sealed class SavingsPlanScheduleDeficitTests
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

    private static async Task<Guid> CreatePlanAsync(AppDbContext db, Guid owner, SavingsPlanType type, SavingsPlanInterval? interval)
    {
        var plan = new SavingsPlan(owner, "P", type, 1000m, DateTime.Today.AddMonths(12), interval, null);
        db.SavingsPlans.Add(plan);
        await db.SaveChangesAsync();
        return plan.Id;
    }

    private static Posting NewPlanPosting(Guid planId, DateTime date, decimal amount)
        => new(Guid.NewGuid(), PostingKind.SavingsPlan, null, null, planId, null, date, amount, null, null, null, null);

    private static async Task AddPostingAsync(AppDbContext db, Guid planId, DateTime date, decimal amount)
    {
        db.Postings.Add(NewPlanPosting(planId, date, amount));
        await db.SaveChangesAsync();
    }

    private static async Task AddReversedPostingPairAsync(AppDbContext db, Guid planId, DateTime date, decimal amount)
    {
        var original = NewPlanPosting(planId, date, amount);
        var reversal = NewPlanPosting(planId, date, -amount);
        original.SetReversedBy(reversal, Guid.NewGuid());
        reversal.SetReversalFor(original);
        db.Postings.AddRange(original, reversal);
        await db.SaveChangesAsync();
    }

    /// <summary>Three consecutive monthly deposits match the rhythm-derived expectation exactly, so the plan shows no backlog.</summary>
    [Fact]
    public async Task AnalyzeAsync_ShouldReturnZeroDeficit_WhenMonthlyRhythmHasNoGap()
    {
        var (sut, db, conn, owner) = Create();
        var planId = await CreatePlanAsync(db, owner, SavingsPlanType.Recurring, SavingsPlanInterval.Monthly);
        await AddPostingAsync(db, planId, DateTime.Today.AddMonths(-3), 100m);
        await AddPostingAsync(db, planId, DateTime.Today.AddMonths(-2), 100m);
        await AddPostingAsync(db, planId, DateTime.Today.AddMonths(-1), 100m);

        var result = await sut.AnalyzeAsync(planId, owner, CancellationToken.None);

        Assert.Equal(0m, result.ScheduleDeficit);
        Assert.Equal(300m, result.ExpectedToDate);
        Assert.Equal(300m, result.ReceivedToDate);
        Assert.Equal(100m, result.ObservedMonthlyRate);
        Assert.Equal(0m, result.Disbursements);
        conn.Dispose();
    }

    /// <summary>With one elapsed month skipped the received deposits lag the expected three periods by exactly the observed rate.</summary>
    [Fact]
    public async Task AnalyzeAsync_ShouldReturnRateAsDeficit_WhenOneDepositIsMissing()
    {
        var (sut, db, conn, owner) = Create();
        var planId = await CreatePlanAsync(db, owner, SavingsPlanType.Recurring, SavingsPlanInterval.Monthly);
        await AddPostingAsync(db, planId, DateTime.Today.AddMonths(-3), 100m);
        await AddPostingAsync(db, planId, DateTime.Today.AddMonths(-1), 100m);

        var result = await sut.AnalyzeAsync(planId, owner, CancellationToken.None);

        Assert.Equal(100m, result.ScheduleDeficit);
        Assert.Equal(300m, result.ExpectedToDate);
        Assert.Equal(200m, result.ReceivedToDate);
        conn.Dispose();
    }

    /// <summary>A cancelled duplicate deposit drops out of both the received sum and the rhythm, leaving an intact schedule at zero deficit.</summary>
    [Fact]
    public async Task AnalyzeAsync_ShouldReturnZeroDeficit_WhenDepositWasReversed()
    {
        var (sut, db, conn, owner) = Create();
        var planId = await CreatePlanAsync(db, owner, SavingsPlanType.Recurring, SavingsPlanInterval.Monthly);
        await AddPostingAsync(db, planId, DateTime.Today.AddMonths(-3), 100m);
        await AddPostingAsync(db, planId, DateTime.Today.AddMonths(-2), 100m);
        await AddPostingAsync(db, planId, DateTime.Today.AddMonths(-1), 100m);
        await AddReversedPostingPairAsync(db, planId, DateTime.Today.AddMonths(-2), 100m);

        var result = await sut.AnalyzeAsync(planId, owner, CancellationToken.None);

        Assert.Equal(0m, result.ScheduleDeficit);
        Assert.Equal(300m, result.ReceivedToDate);
        Assert.Equal(0m, result.Disbursements);
        conn.Dispose();
    }

    /// <summary>A corrected double booking (outlier amount reversed later) must not leak into the median-derived monthly rate or the received sum.</summary>
    [Fact]
    public async Task AnalyzeAsync_ShouldKeepObservedRateUnchanged_WhenDoubleBookingWasCorrected()
    {
        var (sut, db, conn, owner) = Create();
        var planId = await CreatePlanAsync(db, owner, SavingsPlanType.Recurring, SavingsPlanInterval.Monthly);
        await AddPostingAsync(db, planId, DateTime.Today.AddMonths(-2), 100m);
        await AddPostingAsync(db, planId, DateTime.Today.AddMonths(-1), 100m);
        await AddReversedPostingPairAsync(db, planId, DateTime.Today.AddMonths(-1), 500m);

        var result = await sut.AnalyzeAsync(planId, owner, CancellationToken.None);

        Assert.Equal(100m, result.ObservedMonthlyRate);
        Assert.Equal(200m, result.ReceivedToDate);
        Assert.Equal(0m, result.ScheduleDeficit);
        conn.Dispose();
    }

    /// <summary>A large extraordinary withdrawal is reported as disbursement and leaves both the rhythm deficit at zero and the (negative) accumulated balance untouched.</summary>
    [Fact]
    public async Task AnalyzeAsync_ShouldReportDisbursementSeparately_WhenPlanWasWithdrawn()
    {
        var (sut, db, conn, owner) = Create();
        var planId = await CreatePlanAsync(db, owner, SavingsPlanType.Recurring, SavingsPlanInterval.Monthly);
        await AddPostingAsync(db, planId, DateTime.Today.AddMonths(-5), 100m);
        await AddPostingAsync(db, planId, DateTime.Today.AddMonths(-4), 100m);
        await AddPostingAsync(db, planId, DateTime.Today.AddMonths(-3), 100m);
        await AddPostingAsync(db, planId, DateTime.Today.AddMonths(-2), -11100m);
        await AddPostingAsync(db, planId, DateTime.Today.AddMonths(-2), 100m);
        await AddPostingAsync(db, planId, DateTime.Today.AddMonths(-1), 100m);

        var result = await sut.AnalyzeAsync(planId, owner, CancellationToken.None);

        Assert.Equal(-11100m, result.Disbursements);
        Assert.Equal(0m, result.ScheduleDeficit);
        Assert.Equal(500m, result.ReceivedToDate);
        Assert.Equal(-10600m, result.AccumulatedAmount);
        conn.Dispose();
    }

    /// <summary>Without any posting there is nothing to derive a rhythm from - all schedule fields stay null.</summary>
    [Fact]
    public async Task AnalyzeAsync_ShouldReturnNullScheduleFields_WhenPlanHasNoPostings()
    {
        var (sut, db, conn, owner) = Create();
        var planId = await CreatePlanAsync(db, owner, SavingsPlanType.Recurring, SavingsPlanInterval.Monthly);

        var result = await sut.AnalyzeAsync(planId, owner, CancellationToken.None);

        Assert.Null(result.ScheduleDeficit);
        Assert.Null(result.ExpectedToDate);
        Assert.Null(result.ReceivedToDate);
        Assert.Null(result.ObservedMonthlyRate);
        Assert.Null(result.Disbursements);
        conn.Dispose();
    }

    /// <summary>Scattered one-off top-ups on a plan without interval do not establish a rhythm - the rhythm fields stay null while received/disbursement sums remain factual.</summary>
    [Fact]
    public async Task AnalyzeAsync_ShouldReturnNullRhythm_WhenDepositsAreIrregular()
    {
        var (sut, db, conn, owner) = Create();
        var planId = await CreatePlanAsync(db, owner, SavingsPlanType.OneTime, null);
        await AddPostingAsync(db, planId, DateTime.Today.AddMonths(-10), 100m);
        await AddPostingAsync(db, planId, DateTime.Today.AddMonths(-4), 100m);
        await AddPostingAsync(db, planId, DateTime.Today.AddMonths(-1), 100m);

        var result = await sut.AnalyzeAsync(planId, owner, CancellationToken.None);

        Assert.Null(result.ScheduleDeficit);
        Assert.Null(result.ExpectedToDate);
        Assert.Null(result.ObservedMonthlyRate);
        Assert.Equal(300m, result.ReceivedToDate);
        conn.Dispose();
    }

    /// <summary>A quarterly plan pays 300 per period: the observed rate normalizes to 100 per month and two elapsed quarters produce neither surplus nor deficit.</summary>
    [Fact]
    public async Task AnalyzeAsync_ShouldNormalizeObservedRateToMonthly_WhenIntervalIsQuarterly()
    {
        var (sut, db, conn, owner) = Create();
        var planId = await CreatePlanAsync(db, owner, SavingsPlanType.Recurring, SavingsPlanInterval.Quarterly);
        await AddPostingAsync(db, planId, DateTime.Today.AddMonths(-6), 300m);
        await AddPostingAsync(db, planId, DateTime.Today.AddMonths(-3), 300m);

        var result = await sut.AnalyzeAsync(planId, owner, CancellationToken.None);

        Assert.Equal(100m, result.ObservedMonthlyRate);
        Assert.Equal(600m, result.ExpectedToDate);
        Assert.Equal(0m, result.ScheduleDeficit);
        conn.Dispose();
    }
}
