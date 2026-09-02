using FinanceManager.Application.Accounts;
using FinanceManager.Application.Attachments;
using FinanceManager.Domain.Accounts;
using FinanceManager.Domain.Contacts;
using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Aggregates;
using FinanceManager.Infrastructure.Attachments;
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

/// <summary>
/// Covers the persistence-facing operations of <see cref="StatementDraftService"/> around an imported draft's
/// lifecycle: retrieving a persisted draft, appending a manual entry, canceling (deleting) a draft, and
/// committing it into a permanent <see cref="Domain.Statements.StatementImport"/> with its <see cref="Domain.Statements.StatementEntry"/> rows.
/// </summary>
public sealed class StatementDraftPersistenceTests
{
    private sealed class TestCurrentUserService : FinanceManager.Application.ICurrentUserService
    {
        public Guid UserId { get; internal set; } = Guid.NewGuid();
        public bool IsAuthenticated => true;
        public bool IsAdmin => false;
        public string? PreferredLanguage => null;
    }


    private static (StatementDraftService sut, AppDbContext db, Guid ownerId) Create()
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
        var ownerContact = new Contact(owner.Id, "Ich", ContactType.Self, null, null);
        db.Contacts.Add(ownerContact);
        db.SaveChanges();

        var services = new ServiceCollection();
        services.AddScoped<IAttachmentService, AttachmentService>();
        services.AddSingleton(db);
        services.AddLogging();
        services.AddScoped<IStatementFileParser, ING_CSV_StatementFileParser>();
        services.AddScoped<IStatementFileParser, ING_PDF_StatementFileParser>();
        services.AddScoped<IStatementFileParser, Barclays_PDF_StatementFileParser>();
        services.AddScoped<IStatementFileParser, Wuestenrot_StatementFileParser>();
        services.AddScoped<IStatementFileParser, Backup_JSON_StatementFileParser>();
        services.AddScoped<IStatementFile, Barclays_PDF_StatementFile>();
        services.AddScoped<IStatementFile, ING_PDF_StatementFile>();
        services.AddScoped<IStatementFile, ING_Csv_StatementFile>();
        services.AddScoped<IStatementFile, Wuestenrot_PDF_StatementFile>();
        services.AddScoped<IStatementFile, Backup_JSON_StatementFile>();
        services.AddScoped<IStatementFileFactory>(sp => new StatementFileFactory(sp));
        var sp = services.BuildServiceProvider();

        var current = new TestCurrentUserService()
        {
            UserId = owner.Id
        };

        var accountService = new StubAccountService();
        var sut = new StatementDraftService(db, new PostingAggregateService(db), accountService, sp.GetRequiredService<IStatementFileFactory>(), sp.GetServices<IStatementFileParser>(), NullLogger<StatementDraftService>.Instance, null);
        return (sut, db, owner.Id);
    }

    /// <summary>
    /// After <c>CreateDraftAsync</c> persists a newly imported draft, <c>GetDraftAsync</c> must be able to
    /// re-fetch it by id for the owning user and return the same entries - confirming the import actually
    /// round-trips through the database rather than only existing in the in-memory stream.
    /// </summary>
    [Fact]
    public async Task GetDraftAsync_ShouldReturnPersistedDraft()
    {
        var (sut, db, owner) = Create();
        var account = new Account(owner, AccountType.Giro, "A", null, Guid.NewGuid());
        db.Accounts.Add(account);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var counter = 0;
        var bytes = Encoding.UTF8.GetBytes($"{{\"Type\":\"Backup\",\"Version\":2}}\n{{ \"BankAccounts\": [{{ \"IBAN\": \"{account.Iban}\"}}], \"BankAccountLedgerEntries\": [], \"BankAccountJournalLines\": [{{\"Id\": 1,\"PostingDate\": \"2017-07-15T00:00:00\",\"ValutaDate\": \"2017-07-15T00:00:00\",\"PostingDescription\": \"Lastschrift\",\"SourceName\": \"GEZ\",\"Description\": \"GEZ Gebuehr\",\"CurrencyCode\": \"EUR\",\"Amount\": -97.95,\"CreatedAt\": \"2017-07-16T12:33:42.000041\"}}] }}");
        await foreach (var created in sut.CreateDraftAsync(owner, "x.csv", bytes, CancellationToken.None))
        {
            var fetched = await sut.GetDraftAsync(created.DraftId, owner, CancellationToken.None);
            Assert.NotNull(fetched);
            Assert.Equal(created.Entries.Count, fetched!.Entries.Count);
            counter++;
        }
        Assert.Equal(1, counter);
    }

    /// <summary>
    /// Manually adding an entry to an existing draft via <c>AddEntryAsync</c> must append it to the persisted
    /// entry collection (increasing the count by one) and make the new entry retrievable with its given subject -
    /// supporting the "add a transaction the bank statement missed" workflow.
    /// </summary>
    [Fact]
    public async Task AddEntryAsync_ShouldAppendEntry()
    {
        var (sut, db, owner) = Create();
        var account = new Account(owner, AccountType.Giro, "A", null, Guid.NewGuid());
        db.Accounts.Add(account);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var counter = 0;
        var bytes = Encoding.UTF8.GetBytes($"{{\"Type\":\"Backup\",\"Version\":2}}\n{{ \"BankAccounts\": [{{ \"IBAN\": \"{account.Iban}\"}}], \"BankAccountLedgerEntries\": [], \"BankAccountJournalLines\": [{{\"Id\": 1,\"PostingDate\": \"2017-07-15T00:00:00\",\"ValutaDate\": \"2017-07-15T00:00:00\",\"PostingDescription\": \"Lastschrift\",\"SourceName\": \"GEZ\",\"Description\": \"GEZ Gebuehr\",\"CurrencyCode\": \"EUR\",\"Amount\": -97.95,\"CreatedAt\": \"2017-07-16T12:33:42.000041\"}}] }}");
        await foreach (var draft in sut.CreateDraftAsync(owner, "y.csv", bytes, CancellationToken.None))
        {
            var updated = await sut.AddEntryAsync(draft.DraftId, owner, DateTime.UtcNow.Date, 10m, "Manual", CancellationToken.None);

            Assert.NotNull(updated);
            Assert.Equal(draft.Entries.Count + 1, updated!.Entries.Count);
            Assert.True(updated.Entries.Any(e => e.Subject == "Manual"));
            counter++;
        }
        Assert.Equal(1, counter);
    }

    /// <summary>
    /// Canceling a draft must delete it entirely, so that a subsequent <c>GetDraftAsync</c> for the same id
    /// returns null - ensures canceled imports don't linger as orphaned records.
    /// </summary>
    [Fact]
    public async Task CancelAsync_ShouldRemoveDraft()
    {
        var (sut, db, owner) = Create();
        var account = new Account(owner, AccountType.Giro, "A", null, Guid.NewGuid());
        db.Accounts.Add(account);
        var counter = 0;
        var bytes = Encoding.UTF8.GetBytes($"{{\"Type\":\"Backup\",\"Version\":2}}\n{{ \"BankAccounts\": [{{ \"IBAN\": \"{account.Iban}\"}}], \"BankAccountLedgerEntries\": [], \"BankAccountJournalLines\": [{{\"Id\": 1,\"PostingDate\": \"2017-07-15T00:00:00\",\"ValutaDate\": \"2017-07-15T00:00:00\",\"PostingDescription\": \"Lastschrift\",\"SourceName\": \"GEZ\",\"Description\": \"GEZ Gebuehr\",\"CurrencyCode\": \"EUR\",\"Amount\": -97.95,\"CreatedAt\": \"2017-07-16T12:33:42.000041\"}}] }}");
        await foreach (var draft in sut.CreateDraftAsync(owner, "z.csv", bytes, CancellationToken.None))
        {
            var ok = await sut.CancelAsync(draft.DraftId, owner, CancellationToken.None);
            Assert.True(ok);

            var fetched = await sut.GetDraftAsync(draft.DraftId, owner, CancellationToken.None);
            Assert.Null(fetched);
            counter++;
        }
        Assert.Equal(1, counter);
    }

    /// <summary>
    /// Committing a draft must create exactly one <c>StatementImport</c> record, persist all of its entries as
    /// permanent <c>StatementEntry</c> rows (used later for duplicate-import detection), and flip the draft's
    /// status to <see cref="StatementDraftStatus.Committed"/> - the transition from a transient draft into
    /// permanent import history.
    /// </summary>
    [Fact]
    public async Task CommitAsync_ShouldPersistImportAndEntries_AndMarkDraftCommitted()
    {
        var (sut, db, owner) = Create();
        var account = new Account(owner, AccountType.Giro, "Acc", null, Guid.NewGuid());
        db.Accounts.Add(account);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var counter = 0;
        var bytes = Encoding.UTF8.GetBytes($"{{\"Type\":\"Backup\",\"Version\":2}}\n{{ \"BankAccounts\": [{{ \"IBAN\": \"{account.Iban}\"}}], \"BankAccountLedgerEntries\": [], \"BankAccountJournalLines\": [{{\"Id\": 1,\"PostingDate\": \"2017-07-15T00:00:00\",\"ValutaDate\": \"2017-07-15T00:00:00\",\"PostingDescription\": \"Lastschrift\",\"SourceName\": \"GEZ\",\"Description\": \"GEZ Gebuehr\",\"CurrencyCode\": \"EUR\",\"Amount\": -97.95,\"CreatedAt\": \"2017-07-16T12:33:42.000041\"}}] }}");
        await foreach (var draft in sut.CreateDraftAsync(owner, "c.csv", bytes, CancellationToken.None))
        {
            var result = await sut.CommitAsync(draft.DraftId, owner, account.Id, ImportFormat.Csv, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(1, db.StatementImports.Count());
            Assert.Equal(draft.Entries.Count, db.StatementEntries.Count());
            var persistedDraft = await sut.GetDraftAsync(draft.DraftId, owner, CancellationToken.None);
            Assert.Equal(StatementDraftStatus.Committed, persistedDraft!.Status);
            counter++;
        }
        Assert.Equal(1, counter);
    }
}
