using FinanceManager.Domain.Accounts;
using FinanceManager.Domain.Postings;
using FinanceManager.Domain.Savings;
using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Savings;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Covers the account-based analysis of <see cref="SavingsPlanService"/> for savings plans with
/// account links (<see cref="SavingsPlanAccountLink"/>): <c>accumulatedAmount</c> then follows the
/// sum of the linked account balances (additive, no collection-account split) and the contribution
/// rhythm follows the positive bank inflow of the linked accounts, while informal funding postings
/// are not double counted. Without links the posting-based behavior stays unchanged.
/// </summary>
public sealed class SavingsPlanLinkedAnalysisTests
{
    private static (SavingsPlanService sut, SavingsPlanAccountLinkService links, AppDbContext db, SqliteConnection conn) Create()
    {
        var conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options;
        var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        var sut = new SavingsPlanService(db);
        var links = new SavingsPlanAccountLinkService(db);
        return (sut, links, db, conn);
    }

    private static SavingsPlan AddPlan(AppDbContext db, Guid owner, string name)
    {
        var plan = new SavingsPlan(owner, name, SavingsPlanType.Open, null, null, null);
        db.SavingsPlans.Add(plan);
        return plan;
    }

    private static Account AddAccount(AppDbContext db, Guid owner, string name, decimal balance)
    {
        var account = new Account(owner, AccountType.Savings, name, null, Guid.NewGuid());
        account.AdjustBalance(balance);
        db.Accounts.Add(account);
        return account;
    }

    private static Posting AddBankPosting(AppDbContext db, Guid accountId, DateTime date, decimal amount, Guid groupId)
    {
        var p = new Posting(Guid.NewGuid(), PostingKind.Bank, accountId, null, null, null, date, amount, null, null, null, null).SetGroup(groupId);
        db.Postings.Add(p);
        return p;
    }

    private static Posting AddPlanPosting(AppDbContext db, Guid planId, DateTime date, decimal amount, Guid groupId)
    {
        var p = new Posting(Guid.NewGuid(), PostingKind.SavingsPlan, null, null, planId, null, date, amount, null, null, null, null).SetGroup(groupId);
        db.Postings.Add(p);
        return p;
    }

    /// <summary>The accumulated amount of a linked plan is the linked account balance, not the plan posting sum.</summary>
    [Fact]
    public async Task AnalyzeAsync_LinkedPlan_AccumulatedFromAccountBalance()
    {
        var (sut, links, db, conn) = Create();
        var owner = Guid.NewGuid();
        var plan = AddPlan(db, owner, "Bausparen");
        var contract = AddAccount(db, owner, "Vertragskonto", 28801.86m);
        var giro = AddAccount(db, owner, "Giro", 0m);
        // giro-side funding posting: plan posting sum is far off the real balance
        var g = Guid.NewGuid();
        AddBankPosting(db, giro.Id, DateTime.Today.AddDays(-30), -760.74m, g);
        AddPlanPosting(db, plan.Id, DateTime.Today.AddDays(-30), 760.74m, g);
        await db.SaveChangesAsync(CancellationToken.None);

        await links.AddLinkAsync(plan.Id, contract.Id, owner, CancellationToken.None);

        var dto = await sut.AnalyzeAsync(plan.Id, owner, CancellationToken.None);

        Assert.Equal(28801.86m, dto.AccumulatedAmount);
        conn.Dispose();
    }

    /// <summary>Multiple linked accounts are summed additively.</summary>
    [Fact]
    public async Task AnalyzeAsync_TwoLinkedAccounts_AccumulatedIsAdditiveSum()
    {
        var (sut, links, db, conn) = Create();
        var owner = Guid.NewGuid();
        var plan = AddPlan(db, owner, "Plan");
        var acc1 = AddAccount(db, owner, "Konto1", 100m);
        var acc2 = AddAccount(db, owner, "Konto2", 250.50m);
        await db.SaveChangesAsync(CancellationToken.None);
        await links.AddLinkAsync(plan.Id, acc1.Id, owner, CancellationToken.None);
        await links.AddLinkAsync(plan.Id, acc2.Id, owner, CancellationToken.None);

        var dto = await sut.AnalyzeAsync(plan.Id, owner, CancellationToken.None);

        Assert.Equal(350.50m, dto.AccumulatedAmount);
        conn.Dispose();
    }

    /// <summary>A collection account contributes its full balance to every linked plan - no split.</summary>
    [Fact]
    public async Task AnalyzeAsync_CollectionAccount_FullBalanceForEachPlan()
    {
        var (sut, links, db, conn) = Create();
        var owner = Guid.NewGuid();
        var plan1 = AddPlan(db, owner, "Plan1");
        var plan2 = AddPlan(db, owner, "Plan2");
        var shared = AddAccount(db, owner, "Sammelkonto", 1000m);
        await db.SaveChangesAsync(CancellationToken.None);
        await links.AddLinkAsync(plan1.Id, shared.Id, owner, CancellationToken.None);
        await links.AddLinkAsync(plan2.Id, shared.Id, owner, CancellationToken.None);

        var a1 = await sut.AnalyzeAsync(plan1.Id, owner, CancellationToken.None);
        var a2 = await sut.AnalyzeAsync(plan2.Id, owner, CancellationToken.None);

        Assert.Equal(1000m, a1.AccumulatedAmount);
        Assert.Equal(1000m, a2.AccumulatedAmount);
        conn.Dispose();
    }

    /// <summary>
    /// The contribution rhythm uses the positive bank inflow of the linked accounts; informal
    /// funding postings (giro-side) are not double counted into the received amounts.
    /// </summary>
    [Fact]
    public async Task AnalyzeAsync_InformalFundingPosting_NotCountedInReceivedAmount()
    {
        var (sut, links, db, conn) = Create();
        var owner = Guid.NewGuid();
        var plan = AddPlan(db, owner, "Bausparen");
        var contract = AddAccount(db, owner, "Vertragskonto", 300m);
        var giro = AddAccount(db, owner, "Giro", 0m);
        // three inflows directly on the contract account
        for (var i = 0; i < 3; i++)
        {
            AddBankPosting(db, contract.Id, DateTime.Today.AddDays(-90 + i * 30), 100m, Guid.NewGuid());
        }
        // giro-side funding posting of the plan (informal once linked)
        var g = Guid.NewGuid();
        AddBankPosting(db, giro.Id, DateTime.Today.AddDays(-10), -100m, g);
        AddPlanPosting(db, plan.Id, DateTime.Today.AddDays(-10), 100m, g);
        await db.SaveChangesAsync(CancellationToken.None);
        await links.AddLinkAsync(plan.Id, contract.Id, owner, CancellationToken.None);

        var dto = await sut.AnalyzeAsync(plan.Id, owner, CancellationToken.None);

        // Only the contract account inflow counts - the giro funding must not double the received sum.
        Assert.Equal(300m, dto.ReceivedToDate);
        conn.Dispose();
    }

    /// <summary>
    /// Non-informal plan postings needed for cycle detection (here a disbursement without any bank
    /// counterpart) stay part of the analysis for linked plans.
    /// </summary>
    [Fact]
    public async Task AnalyzeAsync_DisbursementWithoutBankCounterpart_StillDetected()
    {
        var (sut, links, db, conn) = Create();
        var owner = Guid.NewGuid();
        var plan = AddPlan(db, owner, "Bausparen");
        var contract = AddAccount(db, owner, "Vertragskonto", 100m);
        AddBankPosting(db, contract.Id, DateTime.Today.AddDays(-60), 100m, Guid.NewGuid());
        var p = new Posting(Guid.NewGuid(), PostingKind.SavingsPlan, null, null, plan.Id, null, DateTime.Today.AddDays(-5), -500m, null, null, null, null);
        db.Postings.Add(p);
        await db.SaveChangesAsync(CancellationToken.None);
        await links.AddLinkAsync(plan.Id, contract.Id, owner, CancellationToken.None);

        var dto = await sut.AnalyzeAsync(plan.Id, owner, CancellationToken.None);

        Assert.Equal(-500m, dto.Disbursements);
        conn.Dispose();
    }

    /// <summary>After removing the last link the analysis falls back to the plan posting sum.</summary>
    [Fact]
    public async Task AnalyzeAsync_AfterRemovingLastLink_FallsBackToPostingSum()
    {
        var (sut, links, db, conn) = Create();
        var owner = Guid.NewGuid();
        var plan = AddPlan(db, owner, "Plan");
        var contract = AddAccount(db, owner, "Vertragskonto", 999m);
        var g = Guid.NewGuid();
        AddBankPosting(db, contract.Id, DateTime.Today.AddDays(-20), 100m, g);
        AddPlanPosting(db, plan.Id, DateTime.Today.AddDays(-20), 123.45m, g);
        await db.SaveChangesAsync(CancellationToken.None);
        await links.AddLinkAsync(plan.Id, contract.Id, owner, CancellationToken.None);

        var linked = await sut.AnalyzeAsync(plan.Id, owner, CancellationToken.None);
        Assert.Equal(999m, linked.AccumulatedAmount);

        await links.RemoveLinkAsync(plan.Id, contract.Id, owner, CancellationToken.None);

        var unlinked = await sut.AnalyzeAsync(plan.Id, owner, CancellationToken.None);
        Assert.Equal(123.45m, unlinked.AccumulatedAmount);
        conn.Dispose();
    }

    /// <summary><see cref="SavingsPlanService.GetAsync"/> uses the same current source as the analysis.</summary>
    [Fact]
    public async Task GetAsync_LinkedPlan_CurrentAmountFromBalance()
    {
        var (sut, links, db, conn) = Create();
        var owner = Guid.NewGuid();
        var plan = AddPlan(db, owner, "Plan");
        var contract = AddAccount(db, owner, "Vertragskonto", 42.17m);
        var g = Guid.NewGuid();
        AddBankPosting(db, contract.Id, DateTime.Today.AddDays(-20), 42.17m, g);
        AddPlanPosting(db, plan.Id, DateTime.Today.AddDays(-20), 5m, g);
        await db.SaveChangesAsync(CancellationToken.None);
        await links.AddLinkAsync(plan.Id, contract.Id, owner, CancellationToken.None);

        var dto = await sut.GetAsync(plan.Id, owner, CancellationToken.None);

        Assert.NotNull(dto);
        Assert.Equal(42.17m, dto!.CurrentAmount);
        conn.Dispose();
    }
}
