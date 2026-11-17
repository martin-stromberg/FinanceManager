using FinanceManager.Domain.Accounts;
using FinanceManager.Domain.Postings;
using FinanceManager.Domain.Savings;
using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Reports;
using FinanceManager.Shared.Dtos.Postings;
using FinanceManager.Shared.Dtos.Reports;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FinanceManager.Tests.Reports;

/// <summary>
/// Posting-source option for savings plans in <see cref="ReportAggregationService"/>:
/// with <see cref="SavingsPlanPostingSource.LinkedAccountPostings"/> the report aggregates the
/// bank postings of the accounts linked to a plan instead of the plan's own postings; plans
/// without links keep their plan postings.
/// </summary>
public sealed class ReportAggregationServiceSavingsPlanSourceTests
{
    private static (ReportAggregationService sut, AppDbContext db, SqliteConnection conn) Create()
    {
        var conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options;
        var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        return (new ReportAggregationService(db, NullLogger<ReportAggregationService>.Instance), db, conn);
    }

    private static PostingAggregate AddAgg(AppDbContext db, PostingKind kind, Guid? accountId, Guid? savingsPlanId, DateTime periodStart, decimal amount)
    {
        var agg = new PostingAggregate(kind, accountId, null, savingsPlanId, null, periodStart, AggregatePeriod.Month);
        agg.Add(amount);
        db.PostingAggregates.Add(agg);
        return agg;
    }

    private async Task<(SavingsPlan linked, SavingsPlan plain, Account contract)> SeedAsync(AppDbContext db, Guid owner)
    {
        var linked = new SavingsPlan(owner, "Verknüpft", SavingsPlanType.Open, null, null, null);
        var plain = new SavingsPlan(owner, "Einfach", SavingsPlanType.Open, null, null, null);
        var contract = new Account(owner, AccountType.Savings, "Vertragskonto", null, Guid.NewGuid());
        db.SavingsPlans.AddRange(linked, plain);
        db.Accounts.Add(contract);
        db.SavingsPlanAccountLinks.Add(new SavingsPlanAccountLink(linked.Id, contract.Id, owner));
        var month = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        // plan postings (would be used with PlanPostings source)
        AddAgg(db, PostingKind.SavingsPlan, null, linked.Id, month, 111m);
        AddAgg(db, PostingKind.SavingsPlan, null, plain.Id, month, 55m);
        // bank postings on the linked account (used with LinkedAccountPostings source)
        AddAgg(db, PostingKind.Bank, contract.Id, null, month, 700m);
        await db.SaveChangesAsync(CancellationToken.None);
        return (linked, plain, contract);
    }

    private static ReportAggregationQuery Query(Guid owner, SavingsPlanPostingSource source)
        => new(owner, PostingKind.SavingsPlan, ReportInterval.Month, 24, false, false, false,
            Filters: null, SavingsPlanPostingSource: source);

    /// <summary>Default source: plan postings are aggregated for both plans.</summary>
    [Fact]
    public async Task QueryAsync_PlanPostingsSource_UsesPlanPostings()
    {
        var (sut, db, conn) = Create();
        var owner = Guid.NewGuid();
        var (linked, plain, _) = await SeedAsync(db, owner);

        var result = await sut.QueryAsync(Query(owner, SavingsPlanPostingSource.PlanPostings), CancellationToken.None);

        result.Points.Should().Contain(p => p.GroupKey == $"SavingsPlan:{linked.Id}" && p.Amount == 111m);
        result.Points.Should().Contain(p => p.GroupKey == $"SavingsPlan:{plain.Id}" && p.Amount == 55m);
        conn.Dispose();
    }

    /// <summary>Linked source: the linked plan shows the bank postings of its linked account.</summary>
    [Fact]
    public async Task QueryAsync_LinkedSource_UsesLinkedBankAggregates()
    {
        var (sut, db, conn) = Create();
        var owner = Guid.NewGuid();
        var (linked, _, _) = await SeedAsync(db, owner);

        var result = await sut.QueryAsync(Query(owner, SavingsPlanPostingSource.LinkedAccountPostings), CancellationToken.None);

        result.Points.Should().Contain(p => p.GroupKey == $"SavingsPlan:{linked.Id}" && p.Amount == 700m);
        result.Points.Should().NotContain(p => p.GroupKey == $"SavingsPlan:{linked.Id}" && p.Amount == 111m);
        conn.Dispose();
    }

    /// <summary>Linked source: plans without links keep their own plan postings.</summary>
    [Fact]
    public async Task QueryAsync_LinkedSource_UnlinkedPlanKeepsPlanPostings()
    {
        var (sut, db, conn) = Create();
        var owner = Guid.NewGuid();
        var (_, plain, _) = await SeedAsync(db, owner);

        var result = await sut.QueryAsync(Query(owner, SavingsPlanPostingSource.LinkedAccountPostings), CancellationToken.None);

        result.Points.Should().Contain(p => p.GroupKey == $"SavingsPlan:{plain.Id}" && p.Amount == 55m);
        conn.Dispose();
    }

    /// <summary>Bank postings of a foreign owner's linked accounts are never aggregated.</summary>
    [Fact]
    public async Task QueryAsync_LinkedSource_ForeignOwner_Excluded()
    {
        var (sut, db, conn) = Create();
        var owner = Guid.NewGuid();
        var foreign = Guid.NewGuid();
        await SeedAsync(db, owner);
        var foreignPlan = new SavingsPlan(foreign, "Fremd", SavingsPlanType.Open, null, null, null);
        var foreignAccount = new Account(foreign, AccountType.Savings, "Fremdkonto", null, Guid.NewGuid());
        db.SavingsPlans.Add(foreignPlan);
        db.Accounts.Add(foreignAccount);
        db.SavingsPlanAccountLinks.Add(new SavingsPlanAccountLink(foreignPlan.Id, foreignAccount.Id, foreign));
        AddAgg(db, PostingKind.Bank, foreignAccount.Id, null, new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1), 999m);
        await db.SaveChangesAsync(CancellationToken.None);

        var result = await sut.QueryAsync(Query(owner, SavingsPlanPostingSource.LinkedAccountPostings), CancellationToken.None);

        result.Points.Should().NotContain(p => p.GroupKey == $"SavingsPlan:{foreignPlan.Id}");
        conn.Dispose();
    }
}
