using FinanceManager.Domain.Postings;
using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Savings;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Covers <see cref="SavingsPlanService"/>'s CRUD lifecycle (create, update, archive, delete) and the
/// AnalyzeAsync projection for open-ended plans that have no target date or amount to project against.
/// </summary>
public sealed class SavingsPlanServiceTests
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

    /// <summary>Verifies the baseline path: a created savings plan can be fetched back by ID and carries the name it was created with.</summary>
    [Fact]
    public async Task CreateAndGet_ShouldWork()
    {
        var (sut, db, conn) = Create();
        var owner = Guid.NewGuid();
        var dto = await sut.CreateAsync(owner, "Testplan", SavingsPlanType.OneTime, 1000, DateTime.Today.AddMonths(6), null, null, null, CancellationToken.None);
        var fetched = await sut.GetAsync(dto.Id, owner, CancellationToken.None);
        Assert.NotNull(fetched);
        Assert.Equal("Testplan", fetched!.Name);
        conn.Dispose();
    }

    /// <summary>Verifies that updating a plan can change every mutable field at once - name, type (OneTime to Recurring), target amount, and interval - and that the returned DTO reflects all of them.</summary>
    [Fact]
    public async Task Update_ShouldChangeValues()
    {
        var (sut, db, conn) = Create();
        var owner = Guid.NewGuid();
        var dto = await sut.CreateAsync(owner, "Testplan", SavingsPlanType.OneTime, 1000, DateTime.Today.AddMonths(6), null, null, null, CancellationToken.None);
        var updated = await sut.UpdateAsync(dto.Id, owner, "Neu", SavingsPlanType.Recurring, 200, null, SavingsPlanInterval.Monthly, null, null, CancellationToken.None);
        Assert.NotNull(updated);
        Assert.Equal("Neu", updated!.Name);
        Assert.Equal(SavingsPlanType.Recurring, updated.Type);
        Assert.Equal(200, updated.TargetAmount);
        Assert.Equal(SavingsPlanInterval.Monthly, updated.Interval);
        conn.Dispose();
    }

    /// <summary>Verifies a plan can be archived and then deleted in sequence, both operations reporting success.</summary>
    [Fact]
    public async Task ArchiveAndDelete_ShouldWork()
    {
        var (sut, db, conn) = Create();
        var owner = Guid.NewGuid();
        var dto = await sut.CreateAsync(owner, "Testplan", SavingsPlanType.OneTime, 1000, DateTime.Today.AddMonths(6), null, null, null, CancellationToken.None);
        var ok = await sut.ArchiveAsync(dto.Id, owner, CancellationToken.None);
        Assert.True(ok);
        var deleted = await sut.DeleteAsync(dto.Id, owner, CancellationToken.None);
        Assert.True(deleted);
        conn.Dispose();
    }

    /// <summary>
    /// Verifies AnalyzeAsync's behavior for an "Open" plan that has neither a target amount nor a target date:
    /// it still sums the plan's postings into AccumulatedAmount, but short-circuits the projection fields
    /// (RequiredMonthly, MonthsRemaining) to zero and reports the plan as reachable by definition, instead of
    /// attempting a month-based projection that has nothing to project against.
    /// </summary>
    [Fact]
    public async Task AnalyzeAsync_ShortCircuits_When_TargetDateMissing_EvenWithPostings()
    {
        var (sut, db, conn) = Create();
        var owner = Guid.NewGuid();

        // Create an open plan with TargetAmount = 0 and no TargetDate
        var dto = await sut.CreateAsync(owner, "OpenPlan", SavingsPlanType.Open, 0m, null, null, null, null, CancellationToken.None);

        // Add postings for the plan: 100,100,100,-300,50,100 => accumulated 150
        var amounts = new decimal[] { 100m, 100m, 100m, -300m, 50m, 100m };
        foreach (var a in amounts)
        {
            var p = new Posting(
                sourceId: Guid.NewGuid(),
                kind: PostingKind.SavingsPlan,
                accountId: null,
                contactId: null,
                savingsPlanId: dto.Id,
                securityId: null,
                bookingDate: DateTime.UtcNow.Date,
                valutaDate: DateTime.UtcNow.Date,
                amount: a,
                subject: string.Empty,
                recipientName: null,
                description: null,
                securitySubType: null,
                quantity: null);
            db.Postings.Add(p);
        }
        await db.SaveChangesAsync(CancellationToken.None);

        // Act
        var analysis = await sut.AnalyzeAsync(dto.Id, owner, CancellationToken.None);

        // Assert: accumulated amount should reflect actual postings even if TargetDate missing
        Assert.NotNull(analysis);
        Assert.Equal(dto.Id, analysis.PlanId);
        Assert.Equal(0m, analysis.TargetAmount);
        Assert.Null(analysis.TargetDate);
        Assert.Equal(150m, analysis.AccumulatedAmount);
        Assert.Equal(0m, analysis.RequiredMonthly);
        Assert.Equal(0, analysis.MonthsRemaining);
        // With target 0, reachable is true
        Assert.True(analysis.TargetReachable);

        // Sanity: postings sum in DB
        var sum = await db.Postings.AsNoTracking().Where(p => p.SavingsPlanId == dto.Id).SumAsync(p => p.Amount, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(150m, sum);

        conn.Dispose();
    }
}