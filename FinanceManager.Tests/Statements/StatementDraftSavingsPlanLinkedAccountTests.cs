using FinanceManager.Application.Accounts;
using FinanceManager.Application.Exceptions;
using FinanceManager.Application.Statements;
using FinanceManager.Domain.Accounts;
using FinanceManager.Domain.Contacts;
using FinanceManager.Domain.Savings;
using FinanceManager.Domain.Statements;
using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Aggregates;
using FinanceManager.Infrastructure.Statements;
using FinanceManager.Infrastructure.Statements.Files;
using FinanceManager.Tests.TestHelpers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace FinanceManager.Tests.Statements;

/// <summary>
/// Covers the savings-plan assignment guard for statement entries on linked accounts:
/// an entry of a draft whose detected account is linked to a savings plan must not be
/// assigned to a savings plan — neither via <see cref="StatementDraftService.SaveEntryAllAsync"/>,
/// nor via <see cref="StatementDraftService.AssignSavingsPlanAsync"/>, nor implicitly by the
/// classification auto-assignment. Unlinked accounts keep the previous behaviour.
/// </summary>
public sealed class StatementDraftSavingsPlanLinkedAccountTests
{
    private sealed class StubStatementFileFactory : IStatementFileFactory
    {
        public IStatementFile? Load(string fileName, byte[] fileBytes) => null;
    }

    private static (StatementDraftService sut, AppDbContext db, SqliteConnection conn, Guid owner, Contact self) Create()
    {
        var conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(conn)
            .Options;
        var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        var ownerUser = new FinanceManager.Domain.Users.User("owner", "hash", true);
        db.Users.Add(ownerUser);
        db.SaveChanges();
        var self = new Contact(ownerUser.Id, "Ich", ContactType.Self, null, null);
        db.Contacts.Add(self);
        db.SaveChanges();
        var sut = new StatementDraftService(db, new PostingAggregateService(db), new StubAccountService(), new StubStatementFileFactory(), null, NullLogger<StatementDraftService>.Instance, null);
        return (sut, db, conn, ownerUser.Id, self);
    }

    private static async Task<Account> AddSavingsAccountAsync(AppDbContext db, Guid owner)
    {
        var bank = new Contact(owner, "Bank", ContactType.Bank, null, null);
        db.Contacts.Add(bank);
        var acc = new Account(owner, AccountType.Savings, "Sparkonto", "DE00", bank.Id);
        db.Accounts.Add(acc);
        await db.SaveChangesAsync();
        return acc;
    }

    private static async Task<StatementDraftEntry> AddDraftWithEntryAsync(AppDbContext db, Guid owner, Guid accountId, string subject, Guid selfContactId)
    {
        var draft = new StatementDraft(owner, "file.csv", "", "");
        draft.SetDetectedAccount(accountId);
        db.StatementDrafts.Add(draft);
        var entry = draft.AddEntry(DateTime.Today, -10m, subject, "Ich", DateTime.Today, "EUR", null, false);
        db.Entry(entry).State = EntityState.Added;
        entry.MarkAccounted(selfContactId);
        await db.SaveChangesAsync();
        return entry;
    }

    private static SavingsPlan AddPlan(AppDbContext db, Guid owner, string name)
    {
        var plan = new SavingsPlan(owner, name, SavingsPlanType.Open, null, null, null);
        db.SavingsPlans.Add(plan);
        return plan;
    }

    /// <summary>
    /// Assigning a savings plan via the save-all path is rejected with
    /// <c>SAVINGSPLAN_ACCOUNT_LINKED</c> when the draft's detected account is linked to a plan.
    /// </summary>
    [Fact]
    public async Task SaveEntryAll_SavingsPlan_OnLinkedAccount_ThrowsDomainValidation()
    {
        var (sut, db, conn, owner, self) = Create();
        var account = await AddSavingsAccountAsync(db, owner);
        var plan = AddPlan(db, owner, "Bausparen");
        db.SavingsPlanAccountLinks.Add(new SavingsPlanAccountLink(plan.Id, account.Id, owner));
        var entry = await AddDraftWithEntryAsync(db, owner, account.Id, "Einzahlung", self.Id);

        var ex = await Assert.ThrowsAsync<DomainValidationException>(() => sut.SaveEntryAllAsync(
            entry.DraftId, entry.Id, owner, self.Id, null, plan.Id, null, null, null, null, null, null, CancellationToken.None));

        Assert.Equal("SAVINGSPLAN_ACCOUNT_LINKED", ex.Code);
        conn.Dispose();
    }

    /// <summary>
    /// The same save-all request succeeds on an unlinked account — the previous behaviour
    /// (plan assignment incl. self-contact and expectation checks) is unchanged.
    /// </summary>
    [Fact]
    public async Task SaveEntryAll_SavingsPlan_OnUnlinkedAccount_AssignsPlan()
    {
        var (sut, db, conn, owner, self) = Create();
        var account = await AddSavingsAccountAsync(db, owner);
        var plan = AddPlan(db, owner, "Bausparen");
        var entry = await AddDraftWithEntryAsync(db, owner, account.Id, "Einzahlung", self.Id);

        var dto = await sut.SaveEntryAllAsync(
            entry.DraftId, entry.Id, owner, self.Id, null, plan.Id, null, null, null, null, null, null, CancellationToken.None);

        Assert.NotNull(dto);
        Assert.Equal(plan.Id, dto!.SavingsPlanId);
        conn.Dispose();
    }

    /// <summary>
    /// The dedicated assign endpoint path applies the same guard: linked accounts reject
    /// the plan assignment with <c>SAVINGSPLAN_ACCOUNT_LINKED</c>.
    /// </summary>
    [Fact]
    public async Task AssignSavingsPlan_OnLinkedAccount_ThrowsDomainValidation()
    {
        var (sut, db, conn, owner, self) = Create();
        var account = await AddSavingsAccountAsync(db, owner);
        var plan = AddPlan(db, owner, "Bausparen");
        db.SavingsPlanAccountLinks.Add(new SavingsPlanAccountLink(plan.Id, account.Id, owner));
        var entry = await AddDraftWithEntryAsync(db, owner, account.Id, "Einzahlung", self.Id);

        var ex = await Assert.ThrowsAsync<DomainValidationException>(() => sut.AssignSavingsPlanAsync(
            entry.DraftId, entry.Id, plan.Id, owner, CancellationToken.None));

        Assert.Equal("SAVINGSPLAN_ACCOUNT_LINKED", ex.Code);
        conn.Dispose();
    }

    /// <summary>
    /// Clearing an existing plan assignment on a linked account stays allowed — only new
    /// assignments are blocked.
    /// </summary>
    [Fact]
    public async Task AssignSavingsPlan_Clear_OnLinkedAccount_StaysAllowed()
    {
        var (sut, db, conn, owner, self) = Create();
        var account = await AddSavingsAccountAsync(db, owner);
        var plan = AddPlan(db, owner, "Bausparen");
        var entry = await AddDraftWithEntryAsync(db, owner, account.Id, "Einzahlung", self.Id);
        // stale assignment predating the link
        entry.AssignSavingsPlan(plan.Id);
        db.SavingsPlanAccountLinks.Add(new SavingsPlanAccountLink(plan.Id, account.Id, owner));
        await db.SaveChangesAsync();

        var dto = await sut.AssignSavingsPlanAsync(entry.DraftId, entry.Id, null, owner, CancellationToken.None);

        Assert.NotNull(dto);
        Assert.Null(dto!.Entries.Single(e => e.Id == entry.Id).SavingsPlanId);
        conn.Dispose();
    }

    /// <summary>
    /// The unlinked happy path of the dedicated assign endpoint still works.
    /// </summary>
    [Fact]
    public async Task AssignSavingsPlan_OnUnlinkedAccount_AssignsPlan()
    {
        var (sut, db, conn, owner, self) = Create();
        var account = await AddSavingsAccountAsync(db, owner);
        var plan = AddPlan(db, owner, "Bausparen");
        var entry = await AddDraftWithEntryAsync(db, owner, account.Id, "Einzahlung", self.Id);

        var dto = await sut.AssignSavingsPlanAsync(entry.DraftId, entry.Id, plan.Id, owner, CancellationToken.None);

        Assert.NotNull(dto);
        Assert.Equal(plan.Id, dto!.Entries.Single(e => e.Id == entry.Id).SavingsPlanId);
        conn.Dispose();
    }

    /// <summary>
    /// Classification must not auto-assign a matching savings plan on a linked account —
    /// the entry keeps its self contact but no plan.
    /// </summary>
    [Fact]
    public async Task ClassifyAsync_LinkedAccount_DoesNotAutoAssignPlan()
    {
        var (sut, db, conn, owner, self) = Create();
        var account = await AddSavingsAccountAsync(db, owner);
        var plan = AddPlan(db, owner, "Bausparen");
        db.SavingsPlanAccountLinks.Add(new SavingsPlanAccountLink(plan.Id, account.Id, owner));
        var entry = await AddDraftWithEntryAsync(db, owner, account.Id, "Bauspareinlage", self.Id);
        await db.SaveChangesAsync();

        var dto = await sut.ClassifyAsync(entry.DraftId, entry.Id, owner, CancellationToken.None);

        Assert.NotNull(dto);
        Assert.Null(dto!.Entries.Single(e => e.Id == entry.Id).SavingsPlanId);
        conn.Dispose();
    }

    /// <summary>
    /// On an unlinked account the classification keeps auto-assigning the matching plan —
    /// the heuristic itself is unchanged.
    /// </summary>
    [Fact]
    public async Task ClassifyAsync_UnlinkedAccount_StillAutoAssignsPlan()
    {
        var (sut, db, conn, owner, self) = Create();
        var account = await AddSavingsAccountAsync(db, owner);
        var plan = AddPlan(db, owner, "Bausparen");
        var entry = await AddDraftWithEntryAsync(db, owner, account.Id, "Bauspareinlage", self.Id);
        await db.SaveChangesAsync();

        var dto = await sut.ClassifyAsync(entry.DraftId, entry.Id, owner, CancellationToken.None);

        Assert.NotNull(dto);
        Assert.Equal(plan.Id, dto!.Entries.Single(e => e.Id == entry.Id).SavingsPlanId);
        conn.Dispose();
    }
}
