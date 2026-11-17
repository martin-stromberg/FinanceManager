using FinanceManager.Domain.Accounts;
using FinanceManager.Domain.Postings;
using FinanceManager.Domain.Savings;
using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Savings;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

/// <summary>
/// Covers the "Eigenanteil" (own share) reported by <see cref="SavingsPlanService.GetAsync"/>:
/// for plans with linked accounts the balance follows the linked account balances while the
/// plan's own posting sum is exposed separately; without links nothing additional is shown.
/// </summary>
public sealed class SavingsPlanServiceOwnAmountTests
{
    private static (SavingsPlanService sut, AppDbContext db, SqliteConnection conn) Create()
    {
        var conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options;
        var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        return (new SavingsPlanService(db), db, conn);
    }

    /// <summary>Linked plan: CurrentAmount follows the account balances and OwnAmount exposes the plan posting sum.</summary>
    [Fact]
    public async Task GetAsync_LinkedPlan_OwnAmountIsPlanPostingSum()
    {
        var (sut, db, conn) = Create();
        var owner = Guid.NewGuid();
        var dto = await sut.CreateAsync(owner, "Bausparen", SavingsPlanType.Recurring, null, null, null, null, null, CancellationToken.None);
        var contract = new Account(owner, AccountType.Savings, "Vertragskonto", null, Guid.NewGuid());
        contract.AdjustBalance(2400m);
        db.Accounts.Add(contract);
        db.SavingsPlanAccountLinks.Add(new SavingsPlanAccountLink(dto.Id, contract.Id, owner));
        db.Postings.Add(new Posting(Guid.NewGuid(), PostingKind.SavingsPlan, null, null, dto.Id, null, DateTime.Today.AddMonths(-2), 200m, null, null, null, null));
        db.Postings.Add(new Posting(Guid.NewGuid(), PostingKind.SavingsPlan, null, null, dto.Id, null, DateTime.Today.AddMonths(-1), 200m, null, null, null, null));
        db.Postings.Add(new Posting(Guid.NewGuid(), PostingKind.SavingsPlan, null, null, dto.Id, null, DateTime.Today.AddMonths(1), 500m, null, null, null, null));
        await db.SaveChangesAsync(CancellationToken.None);

        var fetched = await sut.GetAsync(dto.Id, owner, CancellationToken.None);

        fetched!.CurrentAmount.Should().Be(2400m);
        fetched.OwnAmount.Should().Be(400m);
        conn.Dispose();
    }

    /// <summary>Unlinked plan: OwnAmount stays null and the balance keeps its posting-based value.</summary>
    [Fact]
    public async Task GetAsync_UnlinkedPlan_OwnAmountNull()
    {
        var (sut, db, conn) = Create();
        var owner = Guid.NewGuid();
        var dto = await sut.CreateAsync(owner, "Plan", SavingsPlanType.Open, null, null, null, null, null, CancellationToken.None);
        db.Postings.Add(new Posting(Guid.NewGuid(), PostingKind.SavingsPlan, null, null, dto.Id, null, DateTime.Today.AddMonths(-1), 300m, null, null, null, null));
        await db.SaveChangesAsync(CancellationToken.None);

        var fetched = await sut.GetAsync(dto.Id, owner, CancellationToken.None);

        fetched!.CurrentAmount.Should().Be(300m);
        fetched.OwnAmount.Should().BeNull();
        conn.Dispose();
    }
}
