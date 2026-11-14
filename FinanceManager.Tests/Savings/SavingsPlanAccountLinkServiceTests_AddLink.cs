using FinanceManager.Domain.Accounts;
using FinanceManager.Domain.Savings;
using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Savings;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Covers <see cref="SavingsPlanAccountLinkService.AddLinkAsync"/>: persisting links, owner scoping
/// (plan and account must belong to the caller), duplicate rejection and the n:m cardinality
/// (multiple accounts per plan, multiple plans per account).
/// </summary>
public sealed class SavingsPlanAccountLinkServiceTests_AddLink
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

    /// <summary>Verifies a link between an owned plan and an owned account is persisted with owner and creation metadata.</summary>
    [Fact]
    public async Task AddLink_ValidPlanAndAccount_PersistsLink()
    {
        var (sut, db, conn) = Create();
        var owner = Guid.NewGuid();
        var plan = AddPlan(db, owner, "Plan");
        var account = AddAccount(db, owner, "Konto");
        await db.SaveChangesAsync(CancellationToken.None);

        await sut.AddLinkAsync(plan.Id, account.Id, owner, CancellationToken.None);

        var link = await db.SavingsPlanAccountLinks.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(plan.Id, link.SavingsPlanId);
        Assert.Equal(account.Id, link.AccountId);
        Assert.Equal(owner, link.OwnerUserId);
        Assert.NotEqual(default, link.CreatedUtc);
        conn.Dispose();
    }

    /// <summary>Verifies a plan can be linked to more than one account.</summary>
    [Fact]
    public async Task AddLink_SecondAccountOnSamePlan_AllowsMultipleAccountsPerPlan()
    {
        var (sut, db, conn) = Create();
        var owner = Guid.NewGuid();
        var plan = AddPlan(db, owner, "Plan");
        var account1 = AddAccount(db, owner, "Konto1");
        var account2 = AddAccount(db, owner, "Konto2");
        await db.SaveChangesAsync(CancellationToken.None);

        await sut.AddLinkAsync(plan.Id, account1.Id, owner, CancellationToken.None);
        await sut.AddLinkAsync(plan.Id, account2.Id, owner, CancellationToken.None);

        var count = await db.SavingsPlanAccountLinks.CountAsync(l => l.SavingsPlanId == plan.Id, TestContext.Current.CancellationToken);
        Assert.Equal(2, count);
        conn.Dispose();
    }

    /// <summary>Verifies an account can serve more than one plan (collection-account scenario).</summary>
    [Fact]
    public async Task AddLink_SecondPlanOnSameAccount_AllowsMultiplePlansPerAccount()
    {
        var (sut, db, conn) = Create();
        var owner = Guid.NewGuid();
        var plan1 = AddPlan(db, owner, "Plan1");
        var plan2 = AddPlan(db, owner, "Plan2");
        var account = AddAccount(db, owner, "Konto");
        await db.SaveChangesAsync(CancellationToken.None);

        await sut.AddLinkAsync(plan1.Id, account.Id, owner, CancellationToken.None);
        await sut.AddLinkAsync(plan2.Id, account.Id, owner, CancellationToken.None);

        var count = await db.SavingsPlanAccountLinks.CountAsync(l => l.AccountId == account.Id, TestContext.Current.CancellationToken);
        Assert.Equal(2, count);
        conn.Dispose();
    }

    /// <summary>Verifies a duplicate plan/account pair is rejected.</summary>
    [Fact]
    public async Task AddLink_DuplicatePair_ThrowsInvalidOperationException()
    {
        var (sut, db, conn) = Create();
        var owner = Guid.NewGuid();
        var plan = AddPlan(db, owner, "Plan");
        var account = AddAccount(db, owner, "Konto");
        await db.SaveChangesAsync(CancellationToken.None);
        await sut.AddLinkAsync(plan.Id, account.Id, owner, CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.AddLinkAsync(plan.Id, account.Id, owner, CancellationToken.None));
        conn.Dispose();
    }

    /// <summary>Verifies that linking an account owned by a different user is rejected like a missing account.</summary>
    [Fact]
    public async Task AddLink_AccountOfOtherOwner_ThrowsArgumentException()
    {
        var (sut, db, conn) = Create();
        var owner = Guid.NewGuid();
        var other = Guid.NewGuid();
        var plan = AddPlan(db, owner, "Plan");
        var foreignAccount = AddAccount(db, other, "Fremdes Konto");
        await db.SaveChangesAsync(CancellationToken.None);

        await Assert.ThrowsAsync<ArgumentException>(() => sut.AddLinkAsync(plan.Id, foreignAccount.Id, owner, CancellationToken.None));
        conn.Dispose();
    }

    /// <summary>Verifies that linking a plan owned by a different user is rejected like a missing plan.</summary>
    [Fact]
    public async Task AddLink_PlanOfOtherOwner_ThrowsArgumentException()
    {
        var (sut, db, conn) = Create();
        var owner = Guid.NewGuid();
        var other = Guid.NewGuid();
        var foreignPlan = AddPlan(db, other, "Fremder Plan");
        var account = AddAccount(db, owner, "Konto");
        await db.SaveChangesAsync(CancellationToken.None);

        await Assert.ThrowsAsync<ArgumentException>(() => sut.AddLinkAsync(foreignPlan.Id, account.Id, owner, CancellationToken.None));
        conn.Dispose();
    }

    /// <summary>Verifies an unknown savings plan id is rejected.</summary>
    [Fact]
    public async Task AddLink_UnknownPlan_ThrowsArgumentException()
    {
        var (sut, db, conn) = Create();
        var owner = Guid.NewGuid();
        var account = AddAccount(db, owner, "Konto");
        await db.SaveChangesAsync(CancellationToken.None);

        await Assert.ThrowsAsync<ArgumentException>(() => sut.AddLinkAsync(Guid.NewGuid(), account.Id, owner, CancellationToken.None));
        conn.Dispose();
    }

    /// <summary>Verifies an unknown account id is rejected.</summary>
    [Fact]
    public async Task AddLink_UnknownAccount_ThrowsArgumentException()
    {
        var (sut, db, conn) = Create();
        var owner = Guid.NewGuid();
        var plan = AddPlan(db, owner, "Plan");
        await db.SaveChangesAsync(CancellationToken.None);

        await Assert.ThrowsAsync<ArgumentException>(() => sut.AddLinkAsync(plan.Id, Guid.NewGuid(), owner, CancellationToken.None));
        conn.Dispose();
    }
}
