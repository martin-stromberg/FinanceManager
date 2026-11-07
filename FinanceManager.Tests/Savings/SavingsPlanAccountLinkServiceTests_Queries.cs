using FinanceManager.Domain.Accounts;
using FinanceManager.Domain.Savings;
using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Savings;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Covers the query direction of <see cref="SavingsPlanAccountLinkService"/>: listing the linked
/// accounts of a plan and the linked plans of an account, including the <c>null</c>-result
/// contract for unknown or foreign parents.
/// </summary>
public sealed class SavingsPlanAccountLinkServiceTests_Queries
{
    private static (SavingsPlanAccountLinkService sut, AppDbContext db, SqliteConnection conn) Create()
    {
        var conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options;
        var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        var sut = new SavingsPlanAccountLinkService(db);
        return (sut, db, conn);
    }

    private static SavingsPlan AddPlan(AppDbContext db, Guid owner, string name)
    {
        var plan = new SavingsPlan(owner, name, SavingsPlanType.Open, null, null, null);
        db.SavingsPlans.Add(plan);
        return plan;
    }

    private static Account AddAccount(AppDbContext db, Guid owner, string name)
    {
        var account = new Account(owner, AccountType.Savings, name, null, Guid.NewGuid());
        db.Accounts.Add(account);
        return account;
    }

    /// <summary>Verifies all accounts linked to a plan are returned.</summary>
    [Fact]
    public async Task GetLinkedAccountIds_PlanWithLinks_ReturnsAccountIds()
    {
        var (sut, db, conn) = Create();
        var owner = Guid.NewGuid();
        var plan = AddPlan(db, owner, "Plan");
        var account1 = AddAccount(db, owner, "Konto1");
        var account2 = AddAccount(db, owner, "Konto2");
        await db.SaveChangesAsync(CancellationToken.None);
        await sut.AddLinkAsync(plan.Id, account1.Id, owner, CancellationToken.None);
        await sut.AddLinkAsync(plan.Id, account2.Id, owner, CancellationToken.None);

        var ids = await sut.GetLinkedAccountIdsAsync(plan.Id, owner, CancellationToken.None);

        Assert.NotNull(ids);
        Assert.Equal(2, ids!.Count);
        Assert.Contains(account1.Id, ids);
        Assert.Contains(account2.Id, ids);
        conn.Dispose();
    }

    /// <summary>Verifies a plan without links yields an empty list (not <c>null</c>).</summary>
    [Fact]
    public async Task GetLinkedAccountIds_PlanWithoutLinks_ReturnsEmptyList()
    {
        var (sut, db, conn) = Create();
        var owner = Guid.NewGuid();
        var plan = AddPlan(db, owner, "Plan");
        await db.SaveChangesAsync(CancellationToken.None);

        var ids = await sut.GetLinkedAccountIdsAsync(plan.Id, owner, CancellationToken.None);

        Assert.NotNull(ids);
        Assert.Empty(ids!);
        conn.Dispose();
    }

    /// <summary>Verifies querying a plan owned by another user returns <c>null</c> like an unknown plan.</summary>
    [Fact]
    public async Task GetLinkedAccountIds_PlanOfOtherOwner_ReturnsNull()
    {
        var (sut, db, conn) = Create();
        var owner = Guid.NewGuid();
        var plan = AddPlan(db, owner, "Plan");
        var account = AddAccount(db, owner, "Konto");
        await db.SaveChangesAsync(CancellationToken.None);
        await sut.AddLinkAsync(plan.Id, account.Id, owner, CancellationToken.None);

        var ids = await sut.GetLinkedAccountIdsAsync(plan.Id, Guid.NewGuid(), CancellationToken.None);

        Assert.Null(ids);
        conn.Dispose();
    }

    /// <summary>Verifies all plans linked to an account are returned (collection-account direction).</summary>
    [Fact]
    public async Task GetLinkedSavingsPlanIds_AccountWithLinks_ReturnsPlanIds()
    {
        var (sut, db, conn) = Create();
        var owner = Guid.NewGuid();
        var plan1 = AddPlan(db, owner, "Plan1");
        var plan2 = AddPlan(db, owner, "Plan2");
        var account = AddAccount(db, owner, "Konto");
        await db.SaveChangesAsync(CancellationToken.None);
        await sut.AddLinkAsync(plan1.Id, account.Id, owner, CancellationToken.None);
        await sut.AddLinkAsync(plan2.Id, account.Id, owner, CancellationToken.None);

        var ids = await sut.GetLinkedSavingsPlanIdsAsync(account.Id, owner, CancellationToken.None);

        Assert.NotNull(ids);
        Assert.Equal(2, ids!.Count);
        Assert.Contains(plan1.Id, ids);
        Assert.Contains(plan2.Id, ids);
        conn.Dispose();
    }

    /// <summary>Verifies querying an account owned by another user returns <c>null</c> like an unknown account.</summary>
    [Fact]
    public async Task GetLinkedSavingsPlanIds_AccountOfOtherOwner_ReturnsNull()
    {
        var (sut, db, conn) = Create();
        var owner = Guid.NewGuid();
        var plan = AddPlan(db, owner, "Plan");
        var account = AddAccount(db, owner, "Konto");
        await db.SaveChangesAsync(CancellationToken.None);
        await sut.AddLinkAsync(plan.Id, account.Id, owner, CancellationToken.None);

        var ids = await sut.GetLinkedSavingsPlanIdsAsync(account.Id, Guid.NewGuid(), CancellationToken.None);

        Assert.Null(ids);
        conn.Dispose();
    }
}
