using FinanceManager.Domain.Postings;
using FinanceManager.Domain.Savings;
using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Savings;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Covers <see cref="SavingsPlanService.AnalyzeAsync"/>'s schedule-deficit projection: the backlog of a
/// plan against its own observed deposit rhythm (observed rate x elapsed contribution periods minus
/// received deposits), counted within the current financing cycle only. The cycle starts at the last
/// due disbursement (a withdrawal within 20 % of the target amount, else the largest one), else at the
/// last reached target date, else at the first deposit. The scenarios pin down the cycle reset after a
/// target-sized payout, reversal handling (a reversed deposit and its storno drop out of both sides),
/// the separation of extraordinary disbursements from the deficit, interval normalization of the
/// observed rate, the dominant amount class over micro postings (interest credits), the null
/// deficit of revolving plans and the null result for plans without a detectable rhythm.
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

    private static async Task<Guid> CreatePlanAsync(AppDbContext db, Guid owner, SavingsPlanType type, SavingsPlanInterval? interval, decimal? targetAmount = 1000m, DateTime? targetDate = null)
    {
        var plan = new SavingsPlan(owner, "P", type, targetAmount, targetDate ?? DateTime.Today.AddMonths(12), interval, null);
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

    /// <summary>A corrected double booking (outlier amount reversed later) must not leak into the dominant-class monthly rate or the received sum.</summary>
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

    /// <summary>
    /// A large extraordinary withdrawal opens a new financing cycle: it is reported as disbursement
    /// (whole-history total), only the deposits since the payout count as received, and the pre-cycle
    /// history leaves neither a deficit nor a mark on the (negative) accumulated balance.
    /// </summary>
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
        Assert.Equal(200m, result.ReceivedToDate);
        Assert.Equal(200m, result.ExpectedToDate);
        Assert.Equal(-10600m, result.AccumulatedAmount);
        conn.Dispose();
    }

    /// <summary>
    /// A target-sized disbursement opens a new financing cycle: deposits and missed periods of the
    /// completed cycle drop out of received/expected, so a gap before the payout never counts.
    /// </summary>
    [Fact]
    public async Task AnalyzeAsync_ShouldResetCycle_WhenTargetSizedWithdrawalOccurred()
    {
        var (sut, db, conn, owner) = Create();
        var planId = await CreatePlanAsync(db, owner, SavingsPlanType.Recurring, SavingsPlanInterval.Monthly, targetAmount: 1000m);
        // completed cycle: two deposits, then a deliberately skipped month, then the due payout
        await AddPostingAsync(db, planId, DateTime.Today.AddMonths(-12), 100m);
        await AddPostingAsync(db, planId, DateTime.Today.AddMonths(-11), 100m);
        await AddPostingAsync(db, planId, DateTime.Today.AddMonths(-10), -1000m);
        // current cycle: ten deposits in the ten elapsed months incl. the payout month
        for (var m = -10; m <= -1; m++)
        {
            await AddPostingAsync(db, planId, DateTime.Today.AddMonths(m), 100m);
        }

        var result = await sut.AnalyzeAsync(planId, owner, CancellationToken.None);

        Assert.Equal(0m, result.ScheduleDeficit);
        Assert.Equal(1000m, result.ExpectedToDate);
        Assert.Equal(1000m, result.ReceivedToDate);
        Assert.Equal(100m, result.ObservedMonthlyRate);
        Assert.Equal(-1000m, result.Disbursements);
        conn.Dispose();
    }

    /// <summary>A deposit missing inside the current cycle produces a deficit of exactly one observed rate (the ADAC-style annual plan).</summary>
    [Fact]
    public async Task AnalyzeAsync_ShouldReturnRateAsDeficit_WhenDepositIsMissingInCurrentCycle()
    {
        var (sut, db, conn, owner) = Create();
        var planId = await CreatePlanAsync(db, owner, SavingsPlanType.Recurring, SavingsPlanInterval.Monthly, targetAmount: 990m);
        await AddPostingAsync(db, planId, DateTime.Today.AddMonths(-10), -990m);
        // nine of the ten due months of the current cycle carry a deposit; one was skipped
        foreach (var m in new[] { -10, -9, -8, -7, -6, -5, -4, -2, -1 })
        {
            await AddPostingAsync(db, planId, DateTime.Today.AddMonths(m), 100m);
        }

        var result = await sut.AnalyzeAsync(planId, owner, CancellationToken.None);

        Assert.Equal(100m, result.ScheduleDeficit);
        Assert.Equal(1000m, result.ExpectedToDate);
        Assert.Equal(900m, result.ReceivedToDate);
        Assert.Equal(100m, result.ObservedMonthlyRate);
        conn.Dispose();
    }

    /// <summary>
    /// A revolving reserve (deposits and disbursements balancing each cycle, e.g. a credit-card
    /// settlement buffer) carries no backlog: the deficit stays null - "cyclically balanced" - while
    /// the factual rhythm fields are still reported.
    /// </summary>
    [Fact]
    public async Task AnalyzeAsync_ShouldReturnNullDeficit_WhenPlanIsRevolving()
    {
        var (sut, db, conn, owner) = Create();
        var planId = await CreatePlanAsync(db, owner, SavingsPlanType.Open, SavingsPlanInterval.Monthly, targetAmount: null, targetDate: null);
        for (var m = -6; m <= -1; m++)
        {
            var date = DateTime.Today.AddMonths(m);
            await AddPostingAsync(db, planId, date, 500m);
            await AddPostingAsync(db, planId, date, -500m);
        }

        var result = await sut.AnalyzeAsync(planId, owner, CancellationToken.None);

        Assert.Null(result.ScheduleDeficit);
        Assert.Equal(3000m, result.ExpectedToDate);
        Assert.Equal(3000m, result.ReceivedToDate);
        Assert.Equal(500m, result.ObservedMonthlyRate);
        Assert.Equal(-3000m, result.Disbursements);
        conn.Dispose();
    }

    /// <summary>After a payout the observed rate derives from the current cycle's dominant amount class only - a rate change across the boundary does not leak through.</summary>
    [Fact]
    public async Task AnalyzeAsync_ShouldComputeRateFromCycleMonthsOnly_WhenRateChangedAfterPayout()
    {
        var (sut, db, conn, owner) = Create();
        var planId = await CreatePlanAsync(db, owner, SavingsPlanType.Recurring, SavingsPlanInterval.Monthly, targetAmount: 1000m);
        await AddPostingAsync(db, planId, DateTime.Today.AddMonths(-6), 500m);
        await AddPostingAsync(db, planId, DateTime.Today.AddMonths(-5), 500m);
        await AddPostingAsync(db, planId, DateTime.Today.AddMonths(-4), 500m);
        await AddPostingAsync(db, planId, DateTime.Today.AddMonths(-3), -1000m);
        await AddPostingAsync(db, planId, DateTime.Today.AddMonths(-3), 100m);
        await AddPostingAsync(db, planId, DateTime.Today.AddMonths(-2), 100m);
        await AddPostingAsync(db, planId, DateTime.Today.AddMonths(-1), 100m);

        var result = await sut.AnalyzeAsync(planId, owner, CancellationToken.None);

        Assert.Equal(100m, result.ObservedMonthlyRate);
        Assert.Equal(300m, result.ExpectedToDate);
        Assert.Equal(300m, result.ReceivedToDate);
        Assert.Equal(0m, result.ScheduleDeficit);
        conn.Dispose();
    }

    /// <summary>Without a matching withdrawal a passed target date bounds the cycle: only deposits since then count against the expectation.</summary>
    [Fact]
    public async Task AnalyzeAsync_ShouldStartCycleAtPassedTargetDate_WhenNoMatchingWithdrawalExists()
    {
        var (sut, db, conn, owner) = Create();
        var planId = await CreatePlanAsync(db, owner, SavingsPlanType.OneTime, SavingsPlanInterval.Monthly,
            targetAmount: 1000m, targetDate: DateTime.Today.AddMonths(-5));
        // deposits across ten months with a gap BEFORE the due date - it must not leak into the cycle
        foreach (var m in new[] { -10, -9, -8, -6, -5, -4, -3, -2, -1 })
        {
            await AddPostingAsync(db, planId, DateTime.Today.AddMonths(m), 100m);
        }

        var result = await sut.AnalyzeAsync(planId, owner, CancellationToken.None);

        Assert.Equal(0m, result.ScheduleDeficit);
        Assert.Equal(500m, result.ExpectedToDate);
        Assert.Equal(500m, result.ReceivedToDate);
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

    /// <summary>Interval normalization applies inside the current cycle too: after a payout the quarterly rhythm of the new cycle yields the monthly-normalized rate.</summary>
    [Fact]
    public async Task AnalyzeAsync_ShouldNormalizeQuarterlyRateWithinCycle_WhenWithdrawalResetCycle()
    {
        var (sut, db, conn, owner) = Create();
        var planId = await CreatePlanAsync(db, owner, SavingsPlanType.Recurring, SavingsPlanInterval.Quarterly, targetAmount: 1200m);
        await AddPostingAsync(db, planId, DateTime.Today.AddMonths(-12), 300m);
        await AddPostingAsync(db, planId, DateTime.Today.AddMonths(-9), 300m);
        await AddPostingAsync(db, planId, DateTime.Today.AddMonths(-6), -1200m);
        await AddPostingAsync(db, planId, DateTime.Today.AddMonths(-6), 300m);
        await AddPostingAsync(db, planId, DateTime.Today.AddMonths(-3), 300m);

        var result = await sut.AnalyzeAsync(planId, owner, CancellationToken.None);

        Assert.Equal(100m, result.ObservedMonthlyRate);
        Assert.Equal(600m, result.ExpectedToDate);
        Assert.Equal(600m, result.ReceivedToDate);
        Assert.Equal(0m, result.ScheduleDeficit);
        conn.Dispose();
    }

    /// <summary>
    /// Micro postings (small interest credits) pollute neither the observed rate nor the received
    /// sum even when they outnumber the real deposits: the most frequent amount class loses
    /// dominance to the comparably frequent larger class, so the rate stays at the recurring
    /// deposit and the skipped months count as missing periods.
    /// </summary>
    [Fact]
    public async Task AnalyzeAsync_ShouldIgnoreMicroPostings_WhenInterestCreditsOutnumberDeposits()
    {
        var (sut, db, conn, owner) = Create();
        var planId = await CreatePlanAsync(db, owner, SavingsPlanType.Recurring, SavingsPlanInterval.Monthly);
        // interest credits in five months, real deposits only in the last four
        foreach (var m in new[] { -9, -8, -7, -6, -5 })
        {
            await AddPostingAsync(db, planId, DateTime.Today.AddMonths(m), 0.68m);
        }
        foreach (var m in new[] { -4, -3, -2, -1 })
        {
            await AddPostingAsync(db, planId, DateTime.Today.AddMonths(m), 100m);
        }

        var result = await sut.AnalyzeAsync(planId, owner, CancellationToken.None);

        Assert.Equal(100m, result.ObservedMonthlyRate);
        Assert.Equal(400m, result.ReceivedToDate);
        Assert.Equal(900m, result.ExpectedToDate);
        Assert.Equal(500m, result.ScheduleDeficit);
        conn.Dispose();
    }

    /// <summary>
    /// ADAC-style: nine of the ten due months carry the dominant deposit plus a small interest
    /// credit, one month carries interest only - the deficit is exactly one dominant-class rate
    /// and received excludes the interest noise.
    /// </summary>
    [Fact]
    public async Task AnalyzeAsync_ShouldReturnRateAsDeficit_WhenDominantDepositMonthIsMissing()
    {
        var (sut, db, conn, owner) = Create();
        var planId = await CreatePlanAsync(db, owner, SavingsPlanType.Recurring, SavingsPlanInterval.Monthly);
        foreach (var m in new[] { -10, -9, -8, -7, -6, -5, -4, -3, -2, -1 })
        {
            await AddPostingAsync(db, planId, DateTime.Today.AddMonths(m), 0.68m);
        }
        foreach (var m in new[] { -10, -9, -8, -7, -6, -5, -4, -2, -1 })
        {
            await AddPostingAsync(db, planId, DateTime.Today.AddMonths(m), 100m);
        }

        var result = await sut.AnalyzeAsync(planId, owner, CancellationToken.None);

        Assert.Equal(100m, result.ObservedMonthlyRate);
        Assert.Equal(900m, result.ReceivedToDate);
        Assert.Equal(1000m, result.ExpectedToDate);
        Assert.Equal(100m, result.ScheduleDeficit);
        conn.Dispose();
    }

    /// <summary>
    /// Mixed amount classes: the more frequent class wins over the larger but rarer one-off
    /// amounts, which do not count as received rhythm evidence either.
    /// </summary>
    [Fact]
    public async Task AnalyzeAsync_ShouldLetDominantClassWin_WhenAmountsAreMixed()
    {
        var (sut, db, conn, owner) = Create();
        var planId = await CreatePlanAsync(db, owner, SavingsPlanType.Recurring, SavingsPlanInterval.Monthly);
        foreach (var m in new[] { -5, -4, -3, -2, -1 })
        {
            await AddPostingAsync(db, planId, DateTime.Today.AddMonths(m), 100m);
        }
        await AddPostingAsync(db, planId, DateTime.Today.AddMonths(-3), 500m);
        await AddPostingAsync(db, planId, DateTime.Today.AddMonths(-1), 500m);

        var result = await sut.AnalyzeAsync(planId, owner, CancellationToken.None);

        Assert.Equal(100m, result.ObservedMonthlyRate);
        Assert.Equal(500m, result.ReceivedToDate);
        Assert.Equal(500m, result.ExpectedToDate);
        Assert.Equal(0m, result.ScheduleDeficit);
        conn.Dispose();
    }

    /// <summary>
    /// Interest postings below half of the dominant class amount are micro postings: they add
    /// neither to the monthly totals nor to the received sum, even when they land in the same
    /// month as the deposit.
    /// </summary>
    [Fact]
    public async Task AnalyzeAsync_ShouldIgnoreInterestBelowHalfOfDominantClass_WhenPostedAlongsideDeposit()
    {
        var (sut, db, conn, owner) = Create();
        var planId = await CreatePlanAsync(db, owner, SavingsPlanType.Recurring, SavingsPlanInterval.Monthly);
        foreach (var m in new[] { -3, -2, -1 })
        {
            var date = DateTime.Today.AddMonths(m);
            await AddPostingAsync(db, planId, date, 100m);
            await AddPostingAsync(db, planId, date, 40m);
        }

        var result = await sut.AnalyzeAsync(planId, owner, CancellationToken.None);

        Assert.Equal(100m, result.ObservedMonthlyRate);
        Assert.Equal(300m, result.ReceivedToDate);
        Assert.Equal(300m, result.ExpectedToDate);
        Assert.Equal(0m, result.ScheduleDeficit);
        conn.Dispose();
    }

    /// <summary>When every deposit amount occurs at most once there is no recurring contribution - the rhythm fields stay null even with an interval while the received sum remains factual.</summary>
    [Fact]
    public async Task AnalyzeAsync_ShouldReturnNullRhythm_WhenEveryDepositAmountDiffers()
    {
        var (sut, db, conn, owner) = Create();
        var planId = await CreatePlanAsync(db, owner, SavingsPlanType.Recurring, SavingsPlanInterval.Monthly);
        await AddPostingAsync(db, planId, DateTime.Today.AddMonths(-3), 10m);
        await AddPostingAsync(db, planId, DateTime.Today.AddMonths(-2), 20m);
        await AddPostingAsync(db, planId, DateTime.Today.AddMonths(-1), 30m);

        var result = await sut.AnalyzeAsync(planId, owner, CancellationToken.None);

        Assert.Null(result.ScheduleDeficit);
        Assert.Null(result.ExpectedToDate);
        Assert.Null(result.ObservedMonthlyRate);
        Assert.Equal(60m, result.ReceivedToDate);
        conn.Dispose();
    }
}
