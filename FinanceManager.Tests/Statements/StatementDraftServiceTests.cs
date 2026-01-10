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

namespace FinanceManager.Tests.Statements;

public sealed class StatementDraftServiceTests
{
    private sealed class TestCurrentUserService : FinanceManager.Application.ICurrentUserService
    {
        public Guid UserId { get; internal set; } = Guid.NewGuid();
        public bool IsAuthenticated => true;
        public bool IsAdmin => false;
        public string? PreferredLanguage => null;
    }

    private sealed class TestAccountService : IAccountService
    {
        public Task<AccountDto> CreateAsync(Guid ownerUserId, string name, AccountType type, string? iban, Guid bankContactId, SavingsPlanExpectation expectation, CancellationToken ct)
            => throw new NotImplementedException();

        public Task<AccountDto?> UpdateAsync(Guid id, Guid ownerUserId, string name, string? iban, Guid bankContactId, SavingsPlanExpectation expectation, CancellationToken ct)
            => throw new NotImplementedException();

        public Task<bool> DeleteAsync(Guid id, Guid ownerUserId, CancellationToken ct)
            => throw new NotImplementedException();

        public Task<IReadOnlyList<AccountDto>> ListAsync(Guid ownerUserId, int skip, int take, CancellationToken ct)
            => throw new NotImplementedException();

        public Task<AccountDto?> GetAsync(Guid id, Guid ownerUserId, CancellationToken ct)
            => throw new NotImplementedException();

        public AccountDto? Get(Guid id, Guid ownerUserId)
            => throw new NotImplementedException();

        public Task SetSymbolAttachmentAsync(Guid id, Guid ownerUserId, Guid? attachmentId, CancellationToken ct)
            => throw new NotImplementedException();
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

        var current = new TestCurrentUserService()
        {
            UserId = owner.Id
        };

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

        var accountService = new TestAccountService();
        var sut = new StatementDraftService(db, new PostingAggregateService(db), accountService, sp.GetService<IStatementFileFactory>(), sp.GetServices<IStatementFileParser>(), NullLogger<StatementDraftService>.Instance, null);
        return (sut, db, owner.Id);
    }

    private static (StatementDraftService sut, AppDbContext db, Guid ownerId) CreateWithAttachments()
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

        var agg = new PostingAggregateService(db);
        var attachments = new AttachmentService(db, NullLogger<AttachmentService>.Instance);
        var accountService = new TestAccountService();
        var sut = new StatementDraftService(db, agg, accountService, sp.GetService<IStatementFileFactory>(), sp.GetServices<IStatementFileParser>(), NullLogger<StatementDraftService>.Instance, attachments);
        return (sut, db, owner.Id);
    }

    [Fact]
    public async Task CreateDraftAsync_ShouldReturnEntries_AndAutoDetectAccount_WhenSingleAccount()
    {
        var (sut, db, owner) = Create();
        db.Accounts.Add(new Account(owner, AccountType.Giro, "Test", null, Guid.NewGuid()));
        db.SaveChanges();

        var counter = 0;
        var bytes = Encoding.UTF8.GetBytes($"{{\"Type\":\"Backup\",\"Version\":2}}\n{{ \"BankAccounts\": [{{ \"IBAN\": \"\"}}], \"BankAccountLedgerEntries\": [], \"BankAccountJournalLines\": [{{\"Id\": 1,\"PostingDate\": \"2017-07-15T00:00:00\",\"ValutaDate\": \"2017-07-15T00:00:00\",\"PostingDescription\": \"Lastschrift\",\"SourceName\": \"GEZ\",\"Description\": \"GEZ Gebuehr\",\"CurrencyCode\": \"EUR\",\"Amount\": -97.95,\"CreatedAt\": \"2017-07-16T12:33:42.000041\"}}] }}");
        await foreach (var draft in sut.CreateDraftAsync(owner, "file.csv", bytes, CancellationToken.None))
        {
            Assert.Equal(1, draft.Entries.Count);
            Assert.NotNull(draft.DetectedAccountId);
            Assert.Equal("file.csv", draft.OriginalFileName);
            counter++;
        }
        Assert.Equal(1, counter);
    }

    [Fact]
    public async Task CreateDraftAsync_ShouldHaveNullDetectedAccount_WhenNoAccounts()
    {
        var (sut, _, owner) = Create();

        var counter = 0;
        var bytes = Encoding.UTF8.GetBytes($"{{\"Type\":\"Backup\",\"Version\":2}}\n{{ \"BankAccounts\": [{{ \"IBAN\": \"DE123456\"}}], \"BankAccountLedgerEntries\": [], \"BankAccountJournalLines\": [{{\"Id\": 1,\"PostingDate\": \"2017-07-15T00:00:00\",\"ValutaDate\": \"2017-07-15T00:00:00\",\"PostingDescription\": \"Lastschrift\",\"SourceName\": \"GEZ\",\"Description\": \"GEZ Gebuehr\",\"CurrencyCode\": \"EUR\",\"Amount\": -97.95,\"CreatedAt\": \"2017-07-16T12:33:42.000041\"}}] }}");
        await foreach (var draft in sut.CreateDraftAsync(owner, "f.csv", bytes, CancellationToken.None))
        {
            Assert.Null(draft.DetectedAccountId);
            counter++;
        }
        Assert.Equal(1, counter);
    }

    [Fact]
    public async Task CommitAsync_ShouldReturnResult()
    {
        var (sut, db, owner) = Create();
        var accountId = Guid.NewGuid();

        // Arrange: Account und Draft anlegen
        db.Accounts.Add(new Account(owner, AccountType.Giro, "Testkonto", null, Guid.NewGuid()));
        db.SaveChanges();

        var draft = new FinanceManager.Domain.Statements.StatementDraft(owner, "file.csv", "", null);
        draft.AddEntry(DateTime.UtcNow.Date.AddDays(-2), 123.45m, "Sample Payment A");
        draft.AddEntry(DateTime.UtcNow.Date.AddDays(-1), -49.99m, "Sample Debit B");
        db.StatementDrafts.Add(draft);
        db.SaveChanges();

        // Act
        var result = await sut.CommitAsync(draft.Id, owner, db.Accounts.Single().Id, ImportFormat.Csv, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result!.TotalEntries);
    }

    [Fact]
    public async Task CreateDraftAsync_ShouldCreateAttachment_ForOriginalFile()
    {
        var (sut, db, owner) = CreateWithAttachments();
        // Single account so detected account gets set
        db.Accounts.Add(new Account(owner, AccountType.Giro, "Test", null, Guid.NewGuid()));
        db.SaveChanges();

        var bytes = Encoding.UTF8.GetBytes($"{{\"Type\":\"Backup\",\"Version\":2}}\n{{ \"BankAccounts\": [{{ \"IBAN\": \"\"}}], \"BankAccountLedgerEntries\": [], \"BankAccountJournalLines\": [{{\"Id\": 1,\"PostingDate\": \"2017-07-15T00:00:00\",\"ValutaDate\": \"2017-07-15T00:00:00\",\"PostingDescription\": \"Lastschrift\",\"SourceName\": \"GEZ\",\"Description\": \"GEZ Gebuehr\",\"CurrencyCode\": \"EUR\",\"Amount\": -97.95,\"CreatedAt\": \"2017-07-16T12:33:42.000041\"}}] }}");
        Guid createdDraftId = Guid.Empty;
        await foreach (var draft in sut.CreateDraftAsync(owner, "original.ndjson", bytes, CancellationToken.None))
        {
            createdDraftId = draft.DraftId;
        }
        Assert.NotEqual(Guid.Empty, createdDraftId);

        // Verify attachment stored
        var att = await db.Attachments.FirstOrDefaultAsync(a => a.OwnerUserId == owner && a.EntityKind == FinanceManager.Domain.Attachments.AttachmentEntityKind.StatementDraft && a.EntityId == createdDraftId);
        Assert.NotNull(att);
        Assert.Equal("original.ndjson", att!.FileName);
        Assert.Equal("application/octet-stream", att.ContentType);
        Assert.NotNull(att.Content);
        Assert.True(att.Content!.Length > 0);
    }
}
