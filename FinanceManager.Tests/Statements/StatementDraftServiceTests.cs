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

public sealed class StatementDraftServiceTests
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

        var accountService = new StubAccountService();
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
        var accountService = new StubAccountService();
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
    public async Task ApplyBatchEntryUpdatesAsync_ShouldApplyChanges_WhenValid()
    {
        var (sut, db, owner) = Create();

        // create empty draft
        var draft = await sut.CreateEmptyDraftAsync(owner, "file.csv", CancellationToken.None);
        Assert.NotNull(draft);
        var created = await sut.AddEntryAsync(draft.DraftId, owner, DateTime.Today, 10m, "Initial", CancellationToken.None);
        Assert.NotNull(created);
        var entry = created.Entries.First();

        // prepare batch update
        var req = new FinanceManager.Shared.Dtos.Statements.BatchUpdateRequestDto();
        var newValuta = DateTime.Today.AddDays(1);
        req.Updates.Add(new FinanceManager.Shared.Dtos.Statements.EntryUpdateDto
        {
            EntryId = entry.Id,
            Fields = new Dictionary<string, object?>
            {
                ["Subject"] = "Updated",
                ["Amount"] = 15.5m,
                ["ValutaDate"] = newValuta,
                ["BookingDescription"] = "Updated description"
            }
        });
        var result = await sut.ApplyBatchEntryUpdatesAsync(draft.DraftId, owner, req, CancellationToken.None);
        Assert.True(result.Success);
        Assert.NotNull(result.SuccessResponse);
        var updated = await sut.GetDraftAsync(draft.DraftId, owner, CancellationToken.None);
        Assert.Contains(updated.Entries, e => e.Id == entry.Id && e.Subject == "Updated" && e.Amount == 15.5m && e.ValutaDate == newValuta && e.BookingDescription == "Updated description");
    }

    [Fact]
    public async Task ApplyBatchEntryUpdatesAsync_ShouldReturnErrors_WhenInvalid()
    {
        var (sut, db, owner) = Create();

        // create empty draft and entry
        var draft = await sut.CreateEmptyDraftAsync(owner, "file.csv", CancellationToken.None);
        Assert.NotNull(draft);
        var created = await sut.AddEntryAsync(draft.DraftId, owner, DateTime.Today, 10m, "Initial", CancellationToken.None);
        Assert.NotNull(created);
        var entry = created.Entries.First();

        // prepare batch update with invalid amount (zero)
        var req = new FinanceManager.Shared.Dtos.Statements.BatchUpdateRequestDto();
        req.Updates.Add(new FinanceManager.Shared.Dtos.Statements.EntryUpdateDto
        {
            EntryId = entry.Id,
            Fields = new Dictionary<string, object?>
            {
                ["Amount"] = 0m
            }
        });
        var result = await sut.ApplyBatchEntryUpdatesAsync(draft.DraftId, owner, req, CancellationToken.None);
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorResponse);
        Assert.NotEmpty(result.ErrorResponse.Errors);
        Assert.Contains(result.ErrorResponse.Errors, e => e.EntryId == entry.Id && e.FieldErrors.Any(fe => fe.Field == "Amount"));
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
