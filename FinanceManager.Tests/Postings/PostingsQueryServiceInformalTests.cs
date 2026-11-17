using FinanceManager.Domain.Accounts;
using FinanceManager.Domain.Postings;
using FinanceManager.Domain.Savings;
using FinanceManager.Infrastructure;
using FinanceManager.Web.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Covers the derived <c>Informal</c> flag and the <c>SavingsPlanSubType</c> marker in
/// <see cref="PostingsQueryService.GetSavingsPlanPostingsAsync"/>: a plan posting is informal only
/// when its plan has at least one linked account and its posting group holds a bank posting on a
/// non-linked account.
/// </summary>
public sealed class PostingsQueryServiceInformalTests
{
    private static (PostingsQueryService sut, AppDbContext db, SqliteConnection conn) Create()
    {
        var conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options;
        var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        return (new PostingsQueryService(db), db, conn);
    }

    private static async Task<(SavingsPlan plan, Account giro, Account contract)> SeedAsync(AppDbContext db, Guid owner)
    {
        var plan = new SavingsPlan(owner, "Bausparen", SavingsPlanType.Open, null, null, null);
        var giro = new Account(owner, AccountType.Giro, "Giro", null, Guid.NewGuid());
        var contract = new Account(owner, AccountType.Savings, "Vertragskonto", null, Guid.NewGuid());
        db.SavingsPlans.Add(plan);
        db.Accounts.AddRange(giro, contract);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return (plan, giro, contract);
    }

    private static void AddPair(AppDbContext db, Guid planId, Guid bankAccountId, DateTime date, Guid groupId)
    {
        db.Postings.Add(new Posting(Guid.NewGuid(), PostingKind.Bank, bankAccountId, null, null, null, date, -100m, null, null, null, null).SetGroup(groupId));
        db.Postings.Add(new Posting(Guid.NewGuid(), PostingKind.SavingsPlan, null, null, planId, null, date, 100m, null, null, null, null).SetGroup(groupId));
    }

    /// <summary>A funding posting whose bank counterpart sits on a non-linked account is informal.</summary>
    [Fact]
    public async Task GetSavingsPlanPostings_ForeignBankCounterpart_IsInformal()
    {
        var (sut, db, conn) = Create();
        var owner = Guid.NewGuid();
        var (plan, giro, contract) = await SeedAsync(db, owner);
        db.SavingsPlanAccountLinks.Add(new SavingsPlanAccountLink(plan.Id, contract.Id, owner));
        var g = Guid.NewGuid();
        AddPair(db, plan.Id, giro.Id, DateTime.Today.AddDays(-5), g);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var rows = await sut.GetSavingsPlanPostingsAsync(plan.Id, 0, 10, null, null, null, owner, TestContext.Current.CancellationToken);

        var posting = Assert.Single(rows);
        Assert.True(posting.Informal);
        Assert.Null(posting.SavingsPlanSubType);
        conn.Dispose();
    }

    /// <summary>A posting whose bank counterpart sits on the linked contract account is not informal.</summary>
    [Fact]
    public async Task GetSavingsPlanPostings_LinkedBankCounterpart_NotInformal()
    {
        var (sut, db, conn) = Create();
        var owner = Guid.NewGuid();
        var (plan, _, contract) = await SeedAsync(db, owner);
        db.SavingsPlanAccountLinks.Add(new SavingsPlanAccountLink(plan.Id, contract.Id, owner));
        var g = Guid.NewGuid();
        AddPair(db, plan.Id, contract.Id, DateTime.Today.AddDays(-5), g);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var rows = await sut.GetSavingsPlanPostingsAsync(plan.Id, 0, 10, null, null, null, owner, TestContext.Current.CancellationToken);

        var posting = Assert.Single(rows);
        Assert.False(posting.Informal);
        conn.Dispose();
    }

    /// <summary>A plan posting without a bank counterpart (e.g. disbursement) is never informal.</summary>
    [Fact]
    public async Task GetSavingsPlanPostings_NoBankCounterpart_NotInformal()
    {
        var (sut, db, conn) = Create();
        var owner = Guid.NewGuid();
        var (plan, _, contract) = await SeedAsync(db, owner);
        db.SavingsPlanAccountLinks.Add(new SavingsPlanAccountLink(plan.Id, contract.Id, owner));
        db.Postings.Add(new Posting(Guid.NewGuid(), PostingKind.SavingsPlan, null, null, plan.Id, null, DateTime.Today.AddDays(-5), -500m, null, null, null, null));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var rows = await sut.GetSavingsPlanPostingsAsync(plan.Id, 0, 10, null, null, null, owner, TestContext.Current.CancellationToken);

        var posting = Assert.Single(rows);
        Assert.False(posting.Informal);
        conn.Dispose();
    }

    /// <summary>An actual-balance report is not informal and exposes its marker.</summary>
    [Fact]
    public async Task GetSavingsPlanPostings_ActualReport_NotInformalAndMarked()
    {
        var (sut, db, conn) = Create();
        var owner = Guid.NewGuid();
        var (plan, _, _) = await SeedAsync(db, owner);
        db.Postings.Add(new Posting(Guid.NewGuid(), PostingKind.SavingsPlan, null, null, plan.Id, null, DateTime.Today.AddDays(-5), 75.30m, "Istmeldung", null, null, null)
            .SetSavingsPlanSubType(SavingsPlanPostingSubType.ActualReport));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var rows = await sut.GetSavingsPlanPostingsAsync(plan.Id, 0, 10, null, null, null, owner, TestContext.Current.CancellationToken);

        var posting = Assert.Single(rows);
        Assert.False(posting.Informal);
        Assert.Equal(SavingsPlanPostingSubType.ActualReport, posting.SavingsPlanSubType);
        conn.Dispose();
    }

    /// <summary>After removing the last link the same postings are no longer informal - the flag is derived, not stored.</summary>
    [Fact]
    public async Task GetSavingsPlanPostings_AfterLinkRemoved_NotInformal()
    {
        var (sut, db, conn) = Create();
        var owner = Guid.NewGuid();
        var (plan, giro, contract) = await SeedAsync(db, owner);
        var link = new SavingsPlanAccountLink(plan.Id, contract.Id, owner);
        db.SavingsPlanAccountLinks.Add(link);
        var g = Guid.NewGuid();
        AddPair(db, plan.Id, giro.Id, DateTime.Today.AddDays(-5), g);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        db.SavingsPlanAccountLinks.Remove(link);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var rows = await sut.GetSavingsPlanPostingsAsync(plan.Id, 0, 10, null, null, null, owner, TestContext.Current.CancellationToken);

        var posting = Assert.Single(rows);
        Assert.False(posting.Informal);
        conn.Dispose();
    }
}
