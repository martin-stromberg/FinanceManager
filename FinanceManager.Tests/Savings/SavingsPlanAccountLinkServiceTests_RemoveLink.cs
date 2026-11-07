using FinanceManager.Domain.Accounts;
using FinanceManager.Domain.Savings;
using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Savings;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Covers <see cref="SavingsPlanAccountLinkService.RemoveLinkAsync"/>: removing existing links,
/// result semantics for missing links/foreign plans and the EF cascade when a linked plan or
/// account is deleted.
/// </summary>
public sealed class SavingsPlanAccountLinkServiceTests_RemoveLink
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

    private static async Task<(SavingsPlan plan, Account account)> SeedLinkedAsync(SavingsPlanAccountLinkService sut, AppDbContext db, Guid owner)
    {
        var plan = new SavingsPlan(owner, "Plan", SavingsPlanType.Open, null, null, null);
        var account = new Account(owner, AccountType.Savings, "Konto", null, Guid.NewGuid());
        db.SavingsPlans.Add(plan);
        db.Accounts.Add(account);
        await db.SaveChangesAsync(CancellationToken.None);
        await sut.AddLinkAsync(plan.Id, account.Id, owner, CancellationToken.None);
        return (plan, account);
    }

    /// <summary>Verifies an existing link is removed and reported as removed.</summary>
    [Fact]
    public async Task RemoveLink_ExistingLink_RemovesAndReturnsTrue()
    {
        var (sut, db, conn) = Create();
        var owner = Guid.NewGuid();
        var (plan, account) = await SeedLinkedAsync(sut, db, owner);

        var removed = await sut.RemoveLinkAsync(plan.Id, account.Id, owner, CancellationToken.None);

        Assert.True(removed);
        var remaining = await db.SavingsPlanAccountLinks.CountAsync(TestContext.Current.CancellationToken);
        Assert.Equal(0, remaining);
        conn.Dispose();
    }

    /// <summary>Verifies removing a link that does not exist returns <c>false</c>.</summary>
    [Fact]
    public async Task RemoveLink_UnknownLink_ReturnsFalse()
    {
        var (sut, db, conn) = Create();
        var owner = Guid.NewGuid();
        var plan = new SavingsPlan(owner, "Plan", SavingsPlanType.Open, null, null, null);
        var account = new Account(owner, AccountType.Savings, "Konto", null, Guid.NewGuid());
        db.SavingsPlans.Add(plan);
        db.Accounts.Add(account);
        await db.SaveChangesAsync(CancellationToken.None);

        var removed = await sut.RemoveLinkAsync(plan.Id, account.Id, owner, CancellationToken.None);

        Assert.False(removed);
        conn.Dispose();
    }

    /// <summary>Verifies a caller cannot remove links on a plan owned by another user.</summary>
    [Fact]
    public async Task RemoveLink_PlanOfOtherOwner_ReturnsFalse()
    {
        var (sut, db, conn) = Create();
        var owner = Guid.NewGuid();
        var (plan, account) = await SeedLinkedAsync(sut, db, owner);

        var removed = await sut.RemoveLinkAsync(plan.Id, account.Id, Guid.NewGuid(), CancellationToken.None);

        Assert.False(removed);
        var remaining = await db.SavingsPlanAccountLinks.CountAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, remaining);
        conn.Dispose();
    }

    /// <summary>Verifies deleting a linked savings plan removes its links via the configured cascade.</summary>
    [Fact]
    public async Task RemoveLink_DeletedPlan_CascadesLinks()
    {
        var (sut, db, conn) = Create();
        var owner = Guid.NewGuid();
        var (plan, _) = await SeedLinkedAsync(sut, db, owner);

        db.SavingsPlans.Remove(plan);
        await db.SaveChangesAsync(CancellationToken.None);

        var remaining = await db.SavingsPlanAccountLinks.AsNoTracking().CountAsync(TestContext.Current.CancellationToken);
        Assert.Equal(0, remaining);
        conn.Dispose();
    }
}
