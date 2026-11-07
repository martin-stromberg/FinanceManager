using FinanceManager.Application.Budget;
using FinanceManager.Domain.Accounts;
using FinanceManager.Domain.Budget;
using FinanceManager.Domain.Postings;
using FinanceManager.Domain.Savings;
using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Budget;
using FinanceManager.Shared.Dtos.Budget;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace FinanceManager.Tests.Budget;

/// <summary>
/// Budget regression guard for the savings-plan account-link feature: budget actuals of a plan keep
/// their previous source and sign convention (plan-linked postings, <c>-Amount</c>). Bank postings
/// of linked accounts never become plan actuals, the derived <c>informal</c> evaluation changes
/// nothing, and actual-balance report corrections ("Istmeldung") are excluded from actuals.
/// </summary>
public sealed class BudgetPurposeServiceSavingsPlanActualsTests
{
    private static (BudgetPurposeService sut, AppDbContext db, SqliteConnection conn) Create()
    {
        var conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options;
        var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        var cache = new Mock<IReportCacheService>();
        return (new BudgetPurposeService(db, cache.Object), db, conn);
    }

    private static async Task<AppDbContext> SeedPlanAsync(AppDbContext db, Guid owner, string name)
    {
        var plan = new SavingsPlan(owner, name, SavingsPlanType.Open, null, null, null);
        db.SavingsPlans.Add(plan);
        await db.SaveChangesAsync(CancellationToken.None);
        return db;
    }

    private static Posting AddPlanPosting(AppDbContext db, Guid planId, DateTime date, decimal amount)
    {
        var p = new Posting(Guid.NewGuid(), PostingKind.SavingsPlan, null, null, planId, null, date, amount, null, null, null, null);
        db.Postings.Add(p);
        return p;
    }

    /// <summary>Baseline: plan postings feed actuals with the unchanged -Amount sign convention.</summary>
    [Fact]
    public async Task ListOverview_PlanPostings_ActualsKeepSignConvention()
    {
        var (sut, db, conn) = Create();
        var owner = Guid.NewGuid();
        await SeedPlanAsync(db, owner, "Plan");
        var plan = db.SavingsPlans.Local.Single();
        db.BudgetPurposes.Add(new BudgetPurpose(owner, "Sparzweck", BudgetSourceType.SavingsPlan, plan.Id, null, BudgetValuationType.ExactPostings));
        AddPlanPosting(db, plan.Id, DateTime.Today.AddDays(-5), 100m);
        await db.SaveChangesAsync(CancellationToken.None);

        var purposes = await sut.ListOverviewAsync(owner, 0, 20, null, null, DateOnly.FromDateTime(DateTime.Today.AddMonths(-1)), DateOnly.FromDateTime(DateTime.Today.AddMonths(1)), null, CancellationToken.None);

        purposes.Should().ContainSingle(p => p.ActualSum == -100m);
        conn.Dispose();
    }

    /// <summary>Bank postings on the linked account never become plan actuals - the link changes nothing.</summary>
    [Fact]
    public async Task ListOverview_LinkedAccountBankPostings_NotPlanActuals()
    {
        var (sut, db, conn) = Create();
        var owner = Guid.NewGuid();
        await SeedPlanAsync(db, owner, "Plan");
        var plan = db.SavingsPlans.Local.Single();
        var contract = new Account(owner, AccountType.Savings, "Vertragskonto", null, Guid.NewGuid());
        db.Accounts.Add(contract);
        db.SavingsPlanAccountLinks.Add(new SavingsPlanAccountLink(plan.Id, contract.Id, owner));
        db.BudgetPurposes.Add(new BudgetPurpose(owner, "Sparzweck", BudgetSourceType.SavingsPlan, plan.Id, null, BudgetValuationType.ExactPostings));
        AddPlanPosting(db, plan.Id, DateTime.Today.AddDays(-5), 100m);
        db.Postings.Add(new Posting(Guid.NewGuid(), PostingKind.Bank, contract.Id, null, null, null, DateTime.Today.AddDays(-4), 500m, null, null, null, null));
        await db.SaveChangesAsync(CancellationToken.None);

        var purposes = await sut.ListOverviewAsync(owner, 0, 20, null, null, DateOnly.FromDateTime(DateTime.Today.AddMonths(-1)), DateOnly.FromDateTime(DateTime.Today.AddMonths(1)), null, CancellationToken.None);

        purposes.Should().ContainSingle(p => p.ActualSum == -100m);
        conn.Dispose();
    }

    /// <summary>An actual-balance report ("Istmeldung") is a balance correction and must not move actuals.</summary>
    [Fact]
    public async Task ListOverview_ActualReport_ExcludedFromActuals()
    {
        var (sut, db, conn) = Create();
        var owner = Guid.NewGuid();
        await SeedPlanAsync(db, owner, "Plan");
        var plan = db.SavingsPlans.Local.Single();
        db.BudgetPurposes.Add(new BudgetPurpose(owner, "Sparzweck", BudgetSourceType.SavingsPlan, plan.Id, null, BudgetValuationType.ExactPostings));
        AddPlanPosting(db, plan.Id, DateTime.Today.AddDays(-5), 100m);
        var report = AddPlanPosting(db, plan.Id, DateTime.Today.AddDays(-4), 75.30m);
        report.SetSavingsPlanSubType(SavingsPlanPostingSubType.ActualReport);
        await db.SaveChangesAsync(CancellationToken.None);

        var purposes = await sut.ListOverviewAsync(owner, 0, 20, null, null, DateOnly.FromDateTime(DateTime.Today.AddMonths(-1)), DateOnly.FromDateTime(DateTime.Today.AddMonths(1)), null, CancellationToken.None);

        purposes.Should().ContainSingle(p => p.ActualSum == -100m);
        conn.Dispose();
    }

    /// <summary>
    /// An informal funding posting (bank counterpart on a non-linked account) stays a regular
    /// plan posting for budget purposes - informal only affects the plan analysis.
    /// </summary>
    [Fact]
    public async Task ListOverview_InformalFundingPosting_StillCountsForActuals()
    {
        var (sut, db, conn) = Create();
        var owner = Guid.NewGuid();
        await SeedPlanAsync(db, owner, "Plan");
        var plan = db.SavingsPlans.Local.Single();
        var contract = new Account(owner, AccountType.Savings, "Vertragskonto", null, Guid.NewGuid());
        var giro = new Account(owner, AccountType.Giro, "Giro", null, Guid.NewGuid());
        db.Accounts.AddRange(contract, giro);
        db.SavingsPlanAccountLinks.Add(new SavingsPlanAccountLink(plan.Id, contract.Id, owner));
        db.BudgetPurposes.Add(new BudgetPurpose(owner, "Sparzweck", BudgetSourceType.SavingsPlan, plan.Id, null, BudgetValuationType.ExactPostings));
        var g = Guid.NewGuid();
        db.Postings.Add(new Posting(Guid.NewGuid(), PostingKind.Bank, giro.Id, null, null, null, DateTime.Today.AddDays(-5), -100m, null, null, null, null).SetGroup(g));
        db.Postings.Add(new Posting(Guid.NewGuid(), PostingKind.SavingsPlan, null, null, plan.Id, null, DateTime.Today.AddDays(-5), 100m, null, null, null, null).SetGroup(g));
        await db.SaveChangesAsync(CancellationToken.None);

        var purposes = await sut.ListOverviewAsync(owner, 0, 20, null, null, DateOnly.FromDateTime(DateTime.Today.AddMonths(-1)), DateOnly.FromDateTime(DateTime.Today.AddMonths(1)), null, CancellationToken.None);

        purposes.Should().ContainSingle(p => p.ActualSum == -100m);
        conn.Dispose();
    }
}
