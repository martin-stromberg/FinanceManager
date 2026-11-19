using FinanceManager.Application;
using FinanceManager.Domain.Accounts;
using FinanceManager.Domain.Postings;
using FinanceManager.Infrastructure;
using FinanceManager.Shared.Dtos.Postings;
using FinanceManager.Web.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FinanceManager.Tests.Web;

/// <summary>
/// Tests for <see cref="PostingTypeReevaluationTaskExecutor"/>: only <see cref="PostingKind.Bank"/> postings
/// of the payload account are re-evaluated using that account's posting rules, foreign accounts abort the
/// task, and progress is reported through the context.
/// </summary>
public sealed class PostingTypeReevaluationTaskExecutorTests
{
    private sealed class PassthroughLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(name, arguments));
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
        public IStringLocalizer WithCulture(System.Globalization.CultureInfo culture) => this;
    }

    private sealed class Setup : IDisposable
    {
        public SqliteConnection Connection { get; }
        public AppDbContext Db { get; }
        public IServiceScopeFactory ScopeFactory { get; }

        public Setup()
        {
            Connection = new SqliteConnection("DataSource=:memory:");
            Connection.Open();
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(Connection).Options;
            Db = new AppDbContext(options);
            Db.Database.EnsureCreated();

            var services = new ServiceCollection();
            services.AddSingleton(Db);
            ScopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
        }

        public void Dispose() => Connection.Dispose();
    }

    private static PostingTypeReevaluationTaskExecutor CreateExecutor(IServiceScopeFactory scopeFactory)
        => new(scopeFactory, NullLogger<PostingTypeReevaluationTaskExecutor>.Instance, new PassthroughLocalizer<PostingTypeReevaluationTaskExecutor>());

    private static Account AddAccount(AppDbContext db, Guid owner, string name)
    {
        var account = new Account(owner, AccountType.Giro, name, null, Guid.NewGuid());
        db.Accounts.Add(account);
        return account;
    }

    private static Posting AddBankPosting(AppDbContext db, Account account, decimal amount, string subject)
    {
        var posting = new Posting(Guid.NewGuid(), PostingKind.Bank, account.Id, null, null, null, DateTime.Today, amount, subject, null, null, null);
        db.Postings.Add(posting);
        return posting;
    }

    /// <summary>
    /// A rule match must reclassify matching bank postings of the payload account while postings of other
    /// accounts and non-bank postings keep their previous posting type.
    /// </summary>
    [Fact]
    public async Task Execute_ReevaluatesOnlyBankPostingsOfPayloadAccount()
    {
        var setup = new Setup();
        var owner = Guid.NewGuid();
        var account = AddAccount(setup.Db, owner, "Konto A");
        var otherAccount = AddAccount(setup.Db, owner, "Konto B");
        setup.Db.AccountPostingRules.Add(new AccountPostingRule(account.Id, FinanceManager.Shared.Dtos.Accounts.PostingRuleField.Subject, "Zins", BankPostingType.Interest, 1));

        var interestPosting = AddBankPosting(setup.Db, account, 12m, "Zinsgutschrift");
        var plainOutflow = AddBankPosting(setup.Db, account, -10m, "Kartenzahlung");
        var otherAccountPosting = AddBankPosting(setup.Db, otherAccount, 12m, "Zinsgutschrift");
        var contactPosting = new Posting(Guid.NewGuid(), PostingKind.Contact, null, Guid.NewGuid(), null, null, DateTime.Today, 12m, "Zinsgutschrift", null, null, null);
        setup.Db.Postings.Add(contactPosting);
        await setup.Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var progressCalls = new List<(int processed, int? total, string? message)>();
        var context = new BackgroundTaskContext(Guid.NewGuid(), owner, $"{{\"accountId\":\"{account.Id}\"}}",
            (processed, total, message, _, _) => progressCalls.Add((processed, total, message)));

        var sut = CreateExecutor(setup.ScopeFactory);
        await sut.ExecuteAsync(context, TestContext.Current.CancellationToken);

        Assert.Equal(BankPostingType.Interest, (await setup.Db.Postings.FindAsync(new object?[] { interestPosting.Id }, TestContext.Current.CancellationToken))!.PostingType);
        Assert.Equal(BankPostingType.Outflow, (await setup.Db.Postings.FindAsync(new object?[] { plainOutflow.Id }, TestContext.Current.CancellationToken))!.PostingType);
        Assert.Equal(BankPostingType.Unspecified, (await setup.Db.Postings.FindAsync(new object?[] { otherAccountPosting.Id }, TestContext.Current.CancellationToken))!.PostingType);
        Assert.Equal(BankPostingType.Unspecified, (await setup.Db.Postings.FindAsync(new object?[] { contactPosting.Id }, TestContext.Current.CancellationToken))!.PostingType);

        Assert.NotEmpty(progressCalls);
        Assert.Equal(2, progressCalls.Last().processed);
        setup.Dispose();
    }

    /// <summary>
    /// Reversed and preliminary bank postings are regular bank postings for the re-evaluation: they must be
    /// processed like any other posting of the account (no special filtering).
    /// </summary>
    [Fact]
    public async Task Execute_ProcessesPreliminaryAndReversedPostingsNormally()
    {
        var setup = new Setup();
        var owner = Guid.NewGuid();
        var account = AddAccount(setup.Db, owner, "Konto A");
        var preliminary = AddBankPosting(setup.Db, account, -10m, "Vormerkung");
        var reversal = AddBankPosting(setup.Db, account, 10m, "Storno");
        preliminary.SetPostingType(BankPostingType.Inflow);
        reversal.SetPostingType(BankPostingType.Inflow);
        preliminary.SetIsPreliminary(true);
        await setup.Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var context = new BackgroundTaskContext(Guid.NewGuid(), owner, $"{{\"accountId\":\"{account.Id}\"}}",
            (_, _, _, _, _) => { });

        var sut = CreateExecutor(setup.ScopeFactory);
        await sut.ExecuteAsync(context, TestContext.Current.CancellationToken);

        Assert.Equal(BankPostingType.Outflow, (await setup.Db.Postings.FindAsync(new object?[] { preliminary.Id }, TestContext.Current.CancellationToken))!.PostingType);
        Assert.Equal(BankPostingType.Inflow, (await setup.Db.Postings.FindAsync(new object?[] { reversal.Id }, TestContext.Current.CancellationToken))!.PostingType);
        setup.Dispose();
    }

    /// <summary>
    /// A task whose payload account does not belong to the task owner must abort without touching any posting.
    /// </summary>
    [Fact]
    public async Task Execute_Fails_WhenAccountBelongsToOtherUser()
    {
        var setup = new Setup();
        var owner = Guid.NewGuid();
        var account = AddAccount(setup.Db, owner, "Konto A");
        var posting = AddBankPosting(setup.Db, account, 12m, "Zinsgutschrift");
        await setup.Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var foreignUser = Guid.NewGuid();
        var context = new BackgroundTaskContext(Guid.NewGuid(), foreignUser, $"{{\"accountId\":\"{account.Id}\"}}",
            (_, _, _, _, _) => { });

        var sut = CreateExecutor(setup.ScopeFactory);
        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.ExecuteAsync(context, TestContext.Current.CancellationToken));

        Assert.Equal(BankPostingType.Unspecified, (await setup.Db.Postings.FindAsync(new object?[] { posting.Id }, TestContext.Current.CancellationToken))!.PostingType);
        setup.Dispose();
    }

    /// <summary>
    /// A malformed payload must abort the task immediately without loading or changing postings.
    /// </summary>
    [Fact]
    public async Task Execute_Fails_OnInvalidPayload()
    {
        var setup = new Setup();
        var owner = Guid.NewGuid();
        var context = new BackgroundTaskContext(Guid.NewGuid(), owner, "not-json", (_, _, _, _, _) => { });

        var sut = CreateExecutor(setup.ScopeFactory);
        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.ExecuteAsync(context, TestContext.Current.CancellationToken));
        setup.Dispose();
    }
}
