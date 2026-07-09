using FinanceManager.Application.Accounts;
using FinanceManager.Application.Aggregates;
using FinanceManager.Application.Statements;
using FinanceManager.Domain.Accounts;
using FinanceManager.Domain.Contacts;
using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Aggregates;
using FinanceManager.Infrastructure.Statements;
using FinanceManager.Infrastructure.Statements.Files;
using FinanceManager.Infrastructure.Statements.Parsers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text;
using FinanceManager.Tests.TestHelpers;

namespace FinanceManager.Tests.Statements;

public sealed class StatementDraftServiceTests_CollectionAccount
{

    private sealed class StubStatementFileParser : IStatementFileParser
    {
        private readonly IReadOnlyList<StatementParseResult>? _results;

        public StubStatementFileParser(IReadOnlyList<StatementParseResult>? results) => _results = results;

        public IReadOnlyList<StatementParseResult>? Parse(IStatementFile statementFile) => _results;
        public IReadOnlyList<StatementParseResult>? ParseDetails(IStatementFile statementFile) => _results;
    }

    private sealed class AnyStatementFile : IStatementFile
    {
        public string FileName => "test.csv";
        public bool Load(string fileName, byte[] fileBytes) => true;
        public IEnumerable<string> ReadContent() => Enumerable.Empty<string>();
    }

    private sealed class AnyStatementFileFactory : IStatementFileFactory
    {
        public IStatementFile? Load(string fileName, byte[] fileBytes) => new AnyStatementFile();
    }

    private static (StatementDraftService sut, AppDbContext db, Guid ownerId) Create(IReadOnlyList<StatementParseResult>? parsedResults = null)
    {
        var conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(conn)
            .Options;
        var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        var owner = new FinanceManager.Domain.Users.User("owner", "hash", true);
        db.Users.Add(owner);
        db.SaveChanges();
        db.Contacts.Add(new Contact(owner.Id, "Ich", ContactType.Self, null, null));
        db.SaveChanges();

        var parsers = parsedResults is not null
            ? (IEnumerable<IStatementFileParser>)new[] { new StubStatementFileParser(parsedResults) }
            : new[] { new StubStatementFileParser(null) };

        var sut = new StatementDraftService(
            db,
            new PostingAggregateService(db),
            new StubAccountService(),
            new AnyStatementFileFactory(),
            parsers,
            NullLogger<StatementDraftService>.Instance);

        return (sut, db, owner.Id);
    }

    private static StatementParseResult MakeResult(string iban, params (DateTime date, decimal amount)[] entries)
    {
        var header = new StatementHeader { IBAN = iban, AccountNumber = iban, Description = $"Test {iban}" };
        var movements = entries.Select((e, i) => new StatementMovement
        {
            EntryNumber = i + 1,
            BookingDate = e.date,
            ValutaDate = e.date,
            Amount = e.amount,
            Subject = $"Entry {i + 1}",
            IsPreview = false,
            IsError = false
        }).ToList();
        return new StatementParseResult(header, movements);
    }

    [Fact]
    public async Task CreateDraftAsync_ShouldProduceSingleDraft_ForNormalFile()
    {
        var result = MakeResult("DE111", (DateTime.Today, 100m), (DateTime.Today.AddDays(1), -50m));
        var (sut, db, owner) = Create(new[] { result });

        var drafts = new List<StatementDraftDto>();
        await foreach (var d in sut.CreateDraftAsync(owner, "test.csv", Array.Empty<byte>(), CancellationToken.None))
            drafts.Add(d);

        Assert.Single(drafts);
        Assert.Equal(2, drafts[0].Entries.Count);
    }

    [Fact]
    public async Task CreateDraftAsync_ShouldProduceMultipleDrafts_ForCollectionAccountFile()
    {
        var result1 = MakeResult("DE111", (DateTime.Today, 100m));
        var result2 = MakeResult("DE222", (DateTime.Today.AddDays(1), 200m));
        var (sut, db, owner) = Create(new[] { result1, result2 });

        var drafts = new List<StatementDraftDto>();
        await foreach (var d in sut.CreateDraftAsync(owner, "sammel.csv", Array.Empty<byte>(), CancellationToken.None))
            drafts.Add(d);

        Assert.Equal(2, drafts.Count);
    }

    [Fact]
    public async Task CreateDraftAsync_ShouldSetDetectedAccountId_WhenIbanMatchesLinkedIban()
    {
        var iban = "DE999999999999999999";
        var result = MakeResult(iban, (DateTime.Today, 50m));
        var (sut, db, owner) = Create(new[] { result });

        // Create a collection account and link the IBAN
        var bankContact = new Contact(owner, "Bank", ContactType.Bank, null);
        db.Contacts.Add(bankContact);
        db.SaveChanges();

        var account = new Account(owner, AccountType.Savings, "Sammelkonto", null, bankContact.Id);
        account.SetIsCollectionAccount(true);
        db.Accounts.Add(account);
        db.SaveChanges();

        var linkedIban = new AccountLinkedIban(account.Id, iban);
        db.AccountLinkedIbans.Add(linkedIban);
        db.SaveChanges();

        var drafts = new List<StatementDraftDto>();
        await foreach (var d in sut.CreateDraftAsync(owner, "test.csv", Array.Empty<byte>(), CancellationToken.None))
            drafts.Add(d);

        Assert.Single(drafts);
        Assert.Equal(account.Id, drafts[0].DetectedAccountId);
    }
}
