using FinanceManager.Domain.Accounts;
using FinanceManager.Domain.Savings;
using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Savings;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FinanceManager.Tests.Savings;

/// <summary>
/// Savings-only restriction for <see cref="SavingsPlanAccountLinkService.AddLinkAsync"/>:
/// only accounts of type <see cref="AccountType.Savings"/> may be linked to a savings plan.
/// </summary>
public sealed class SavingsPlanAccountLinkServiceTests_SavingsOnly
{
    private static (SavingsPlanAccountLinkService sut, AppDbContext db, SqliteConnection conn) Create()
    {
        var conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options;
        var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        return (new SavingsPlanAccountLinkService(db), db, conn);
    }

    private static async Task<SavingsPlan> SeedPlanAsync(AppDbContext db, Guid owner)
    {
        var plan = new SavingsPlan(owner, "Plan", SavingsPlanType.Open, null, null, null);
        db.SavingsPlans.Add(plan);
        await db.SaveChangesAsync(CancellationToken.None);
        return plan;
    }

    /// <summary>A giro account cannot be linked to a savings plan.</summary>
    [Fact]
    public async Task AddLinkAsync_GiroAccount_ThrowsArgumentException()
    {
        var (sut, db, conn) = Create();
        var owner = Guid.NewGuid();
        var plan = await SeedPlanAsync(db, owner);
        var giro = new Account(owner, AccountType.Giro, "Giro", null, Guid.NewGuid());
        db.Accounts.Add(giro);
        await db.SaveChangesAsync(CancellationToken.None);

        var act = async () => await sut.AddLinkAsync(plan.Id, giro.Id, owner, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
        conn.Dispose();
    }

    /// <summary>A credit-card settlement account cannot be linked to a savings plan.</summary>
    [Fact]
    public async Task AddLinkAsync_CreditCardAccount_ThrowsArgumentException()
    {
        var (sut, db, conn) = Create();
        var owner = Guid.NewGuid();
        var plan = await SeedPlanAsync(db, owner);
        var card = new Account(owner, AccountType.CreditCard, "Karte", null, Guid.NewGuid());
        db.Accounts.Add(card);
        await db.SaveChangesAsync(CancellationToken.None);

        var act = async () => await sut.AddLinkAsync(plan.Id, card.Id, owner, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
        conn.Dispose();
    }

    /// <summary>A savings account can be linked to a savings plan.</summary>
    [Fact]
    public async Task AddLinkAsync_SavingsAccount_CreatesLink()
    {
        var (sut, db, conn) = Create();
        var owner = Guid.NewGuid();
        var plan = await SeedPlanAsync(db, owner);
        var savings = new Account(owner, AccountType.Savings, "Vertragskonto", null, Guid.NewGuid());
        db.Accounts.Add(savings);
        await db.SaveChangesAsync(CancellationToken.None);

        await sut.AddLinkAsync(plan.Id, savings.Id, owner, CancellationToken.None);

        var ids = await sut.GetLinkedAccountIdsAsync(plan.Id, owner, CancellationToken.None);
        ids.Should().ContainSingle().Which.Should().Be(savings.Id);
        conn.Dispose();
    }
}
