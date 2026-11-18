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
/// Covers general <see cref="StatementDraftService"/> operations beyond booking and classification: import-time
/// account auto-detection, empty-draft creation plus batch entry updates/creates/deletes (including atomicity and
/// status-transition edge cases for announced/already-booked entries), commit result reporting, and storing the
/// original uploaded file as an attachment on the draft.
/// </summary>
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
        var sut = new StatementDraftService(db, new PostingAggregateService(db), accountService, sp.GetRequiredService<IStatementFileFactory>(), sp.GetServices<IStatementFileParser>(), NullLogger<StatementDraftService>.Instance, null);
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
        var sut = new StatementDraftService(db, agg, accountService, sp.GetRequiredService<IStatementFileFactory>(), sp.GetServices<IStatementFileParser>(), NullLogger<StatementDraftService>.Instance, attachments);
        return (sut, db, owner.Id);
    }

    /// <summary>
    /// Importing a file when the owner has exactly one account must auto-detect and assign that account to the
    /// resulting draft, and preserve the original uploaded file name on the draft DTO.
    /// </summary>
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

    /// <summary>
    /// A batch update request with a valid field-value payload (subject, amount, valuta date, booking
    /// description) must be applied to the targeted entry and be visible when the draft is re-fetched - the core
    /// "edit several fields of one entry at once" path.
    /// </summary>
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
        Assert.NotNull(updated);
        Assert.Contains(updated.Entries, e => e.Id == entry.Id && e.Subject == "Updated" && e.Amount == 15.5m && e.ValutaDate == newValuta && e.BookingDescription == "Updated description");
    }

    /// <summary>
    /// A batch update that sets an invalid value (a zero amount) must be rejected with a field-level error
    /// attached to the specific entry/field, letting the UI highlight exactly what is wrong rather than failing
    /// generically.
    /// </summary>
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

    /// <summary>
    /// A single batch request combining an update, a delete, and a create must apply all three operations
    /// together: the updated entry keeps its id with the new values, the deleted entry disappears, and the newly
    /// created entry appears with its given fields.
    /// </summary>
    [Fact]
    public async Task ApplyBatchEntryUpdatesAsync_ShouldApplyUpdatesDeletesAndCreates_WhenValid()
    {
        var (sut, db, owner) = Create();
        var draft = await sut.CreateEmptyDraftAsync(owner, "file.csv", CancellationToken.None);
        Assert.NotNull(draft);
        var withFirst = await sut.AddEntryAsync(draft!.DraftId, owner, DateTime.Today, 10m, "First", CancellationToken.None);
        Assert.NotNull(withFirst);
        var withSecond = await sut.AddEntryAsync(draft.DraftId, owner, DateTime.Today.AddDays(1), 20m, "Second", CancellationToken.None);
        Assert.NotNull(withSecond);
        var first = withSecond!.Entries.Single(e => e.Subject == "First");
        var second = withSecond.Entries.Single(e => e.Subject == "Second");

        var clientId = Guid.NewGuid();
        var req = new FinanceManager.Shared.Dtos.Statements.BatchUpdateRequestDto();
        req.Updates.Add(new FinanceManager.Shared.Dtos.Statements.EntryUpdateDto
        {
            EntryId = first.Id,
            Fields = new Dictionary<string, object?> { ["Subject"] = "Updated first", ["Amount"] = 15m }
        });
        req.Deletes.Add(second.Id);
        req.Creates.Add(new FinanceManager.Shared.Dtos.Statements.EntryCreateDto
        {
            ClientId = clientId,
            BookingDate = DateTime.Today.AddDays(2),
            ValutaDate = DateTime.Today.AddDays(3),
            Amount = 30m,
            Subject = "Created",
            BookingDescription = "Created description",
            RecipientName = "Recipient"
        });

        var result = await sut.ApplyBatchEntryUpdatesAsync(draft.DraftId, owner, req, CancellationToken.None);

        Assert.True(result.Success);
        var updated = await sut.GetDraftAsync(draft.DraftId, owner, CancellationToken.None);
        Assert.NotNull(updated);
        Assert.Contains(updated!.Entries, e => e.Id == first.Id && e.Subject == "Updated first" && e.Amount == 15m);
        Assert.DoesNotContain(updated.Entries, e => e.Id == second.Id);
        Assert.Contains(updated.Entries, e => e.Subject == "Created" && e.Amount == 30m && e.ValutaDate == DateTime.Today.AddDays(3));
    }

    /// <summary>
    /// If any one operation in a combined batch request is invalid (here, a create with a zero amount), the
    /// entire batch must be rejected atomically - none of the otherwise-valid update/delete/create operations may
    /// be partially persisted.
    /// </summary>
    [Fact]
    public async Task ApplyBatchEntryUpdatesAsync_ShouldNotPersistAnyChanges_WhenCreateIsInvalid()
    {
        var (sut, db, owner) = Create();
        var draft = await sut.CreateEmptyDraftAsync(owner, "file.csv", CancellationToken.None);
        Assert.NotNull(draft);
        var withFirst = await sut.AddEntryAsync(draft!.DraftId, owner, DateTime.Today, 10m, "First", CancellationToken.None);
        Assert.NotNull(withFirst);
        var withSecond = await sut.AddEntryAsync(draft.DraftId, owner, DateTime.Today.AddDays(1), 20m, "Second", CancellationToken.None);
        Assert.NotNull(withSecond);
        var first = withSecond!.Entries.Single(e => e.Subject == "First");
        var second = withSecond.Entries.Single(e => e.Subject == "Second");

        var req = new FinanceManager.Shared.Dtos.Statements.BatchUpdateRequestDto();
        req.Updates.Add(new FinanceManager.Shared.Dtos.Statements.EntryUpdateDto
        {
            EntryId = first.Id,
            Fields = new Dictionary<string, object?> { ["Subject"] = "Should not persist" }
        });
        req.Deletes.Add(second.Id);
        req.Creates.Add(new FinanceManager.Shared.Dtos.Statements.EntryCreateDto
        {
            ClientId = Guid.NewGuid(),
            BookingDate = DateTime.Today,
            Amount = 0m,
            Subject = "Invalid"
        });

        var result = await sut.ApplyBatchEntryUpdatesAsync(draft.DraftId, owner, req, CancellationToken.None);

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorResponse);
        var unchanged = await sut.GetDraftAsync(draft.DraftId, owner, CancellationToken.None);
        Assert.NotNull(unchanged);
        Assert.Contains(unchanged!.Entries, e => e.Id == first.Id && e.Subject == "First");
        Assert.Contains(unchanged.Entries, e => e.Id == second.Id && e.Subject == "Second");
        Assert.DoesNotContain(unchanged.Entries, e => e.Subject == "Should not persist");
    }

    /// <summary>
    /// An entry that is only "announced" (a bank preview, not yet a final settled movement) must still be
    /// deletable through the batch delete operation just like any other entry.
    /// </summary>
    [Fact]
    public async Task ApplyBatchEntryUpdatesAsync_ShouldDeleteAnnouncedEntries()
    {
        var (sut, db, owner) = Create();
        var draft = new FinanceManager.Domain.Statements.StatementDraft(owner, "file.csv", null, null);
        var entry = draft.AddEntry(DateTime.Today, 10m, "Announced", null, null, null, null, isAnnounced: true);
        db.StatementDrafts.Add(draft);
        db.SaveChanges();

        var req = new FinanceManager.Shared.Dtos.Statements.BatchUpdateRequestDto();
        req.Deletes.Add(entry.Id);

        var result = await sut.ApplyBatchEntryUpdatesAsync(draft.Id, owner, req, CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.SuccessResponse);
        var updated = await sut.GetDraftAsync(draft.Id, owner, CancellationToken.None);
        Assert.DoesNotContain(updated!.Entries, e => e.Id == entry.Id);
    }

    /// <summary>
    /// When a batch update touches only unrelated fields (e.g. Subject) and does not explicitly include "Status"
    /// in the update payload, the entry's existing status (Announced) and cost-neutral flag must remain
    /// untouched - status must never be reset implicitly as a side effect of updating other fields.
    /// </summary>
    [Fact]
    public async Task ApplyBatchEntryUpdatesAsync_ShouldNotApplyStatusLogic_WhenStatusFieldIsNotProvided()
    {
        var (sut, db, owner) = Create();
        var draft = new FinanceManager.Domain.Statements.StatementDraft(owner, "file.csv", null, null);
        var entry = draft.AddEntry(
            DateTime.Today,
            10m,
            "Announced",
            null,
            null,
            null,
            null,
            isAnnounced: true,
            isCostNeutral: true);
        db.StatementDrafts.Add(draft);
        db.SaveChanges();

        var req = new FinanceManager.Shared.Dtos.Statements.BatchUpdateRequestDto();
        req.Updates.Add(new FinanceManager.Shared.Dtos.Statements.EntryUpdateDto
        {
            EntryId = entry.Id,
            Fields = new Dictionary<string, object?> { ["Subject"] = "Updated announced" }
        });

        var result = await sut.ApplyBatchEntryUpdatesAsync(draft.Id, owner, req, CancellationToken.None);

        Assert.True(result.Success);
        var unchangedStatusEntry = await db.StatementDraftEntries.SingleAsync(e => e.Id == entry.Id, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(FinanceManager.Shared.Dtos.Statements.StatementDraftEntryStatus.Announced, unchangedStatusEntry.Status);
        Assert.True(unchangedStatusEntry.IsCostNeutral);
        Assert.Equal("Updated announced", unchangedStatusEntry.Subject);
    }

    /// <summary>
    /// An entry previously marked <c>AlreadyBooked</c> (flagged as a probable duplicate) can be explicitly reset
    /// back to <c>Open</c> via a batch update that includes the Status field alongside other corrected values -
    /// letting the user override a false-positive duplicate detection.
    /// </summary>
    [Fact]
    public async Task ApplyBatchEntryUpdatesAsync_ShouldAllowAlreadyBookedResetWithFieldUpdates()
    {
        var (sut, db, owner) = Create();
        var draft = new FinanceManager.Domain.Statements.StatementDraft(owner, "file.csv", null, null);
        var entry = draft.AddEntry(
            DateTime.Today,
            10m,
            "Duplicate",
            null,
            null,
            null,
            null,
            isAnnounced: false);
        entry.MarkAlreadyBooked();
        db.StatementDrafts.Add(draft);
        db.SaveChanges();

        var req = new FinanceManager.Shared.Dtos.Statements.BatchUpdateRequestDto();
        req.Updates.Add(new FinanceManager.Shared.Dtos.Statements.EntryUpdateDto
        {
            EntryId = entry.Id,
            Fields = new Dictionary<string, object?>
            {
                ["Status"] = FinanceManager.Shared.Dtos.Statements.StatementDraftEntryStatus.Open,
                ["Subject"] = "Corrected duplicate",
                ["Amount"] = 12.5m
            }
        });

        var result = await sut.ApplyBatchEntryUpdatesAsync(draft.Id, owner, req, CancellationToken.None);

        Assert.True(result.Success);
        var updatedEntry = await db.StatementDraftEntries.SingleAsync(e => e.Id == entry.Id, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(FinanceManager.Shared.Dtos.Statements.StatementDraftEntryStatus.Open, updatedEntry.Status);
        Assert.Equal("Corrected duplicate", updatedEntry.Subject);
        Assert.Equal(12.5m, updatedEntry.Amount);
    }

    /// <summary>
    /// When the owner has no accounts configured at all, importing a file must leave <c>DetectedAccountId</c>
    /// null rather than throwing or guessing, so the UI can prompt the user to pick an account manually.
    /// </summary>
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

    /// <summary>
    /// Committing a draft with multiple entries must return a non-null commit result whose
    /// <c>TotalEntries</c> count matches the number of entries that were actually committed.
    /// </summary>
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

    /// <summary>
    /// Importing a statement file must store the original uploaded bytes as an <c>Attachment</c> linked to the
    /// created draft (by <c>AttachmentEntityKind.StatementDraft</c> and the draft's id), with the correct file
    /// name, a generic binary content type, and non-empty content - preserving the source file for later
    /// reference or audit.
    /// </summary>
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
        var att = await db.Attachments.FirstOrDefaultAsync(a => a.OwnerUserId == owner && a.EntityKind == FinanceManager.Domain.Attachments.AttachmentEntityKind.StatementDraft && a.EntityId == createdDraftId, cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(att);
        Assert.Equal("original.ndjson", att!.FileName);
        Assert.Equal("application/octet-stream", att.ContentType);
        Assert.NotNull(att.Content);
        Assert.True(att.Content!.Length > 0);
    }

    private static async Task<(Account account, StatementDraftDto draft, StatementDraftEntryDto entry)> CreateDraftWithAccountRuleAsync(StatementDraftService sut, AppDbContext db, Guid owner)
    {
        var bank = new Contact(owner, "Bank", ContactType.Bank, null, null);
        db.Contacts.Add(bank);
        await db.SaveChangesAsync();
        var account = new Account(owner, AccountType.Giro, "Testkonto", "DE01", bank.Id);
        db.Accounts.Add(account);
        db.AccountPostingRules.Add(new AccountPostingRule(account.Id, PostingRuleField.Subject, "Zins", BankPostingType.Interest, 1));
        await db.SaveChangesAsync();

        var draft = await sut.CreateEmptyDraftAsync(owner, "file.csv", CancellationToken.None);
        Assert.NotNull(draft);
        var entity = await db.StatementDrafts.SingleAsync(d => d.Id == draft!.DraftId);
        entity.SetDetectedAccount(account.Id);
        await db.SaveChangesAsync();

        var created = await sut.AddEntryAsync(draft!.DraftId, owner, DateTime.Today, 10m, "Initial", CancellationToken.None);
        Assert.NotNull(created);
        return (account, draft!, created!.Entries.First());
    }

    /// <summary>
    /// Providing a posting type via <see cref="StatementDraftService.SaveEntryAllAsync"/> must persist it as a
    /// manual assignment, set the manual flag and return both values on the resulting DTO.
    /// </summary>
    [Fact]
    public async Task SaveEntryAllAsync_ShouldSetPostingTypeManually_AndReturnDto()
    {
        var (sut, db, owner) = Create();
        var draft = await sut.CreateEmptyDraftAsync(owner, "file.csv", CancellationToken.None);
        Assert.NotNull(draft);
        var created = await sut.AddEntryAsync(draft!.DraftId, owner, DateTime.Today, 10m, "Initial", CancellationToken.None);
        var entry = created!.Entries.First();

        var dto = await sut.SaveEntryAllAsync(draft!.DraftId, entry.Id, owner, new StatementDraftSaveEntryAllRequest(null, null, null, null, null, null, null, null, null, PostingType: BankPostingType.Tax), CancellationToken.None);

        Assert.NotNull(dto);
        Assert.Equal(BankPostingType.Tax, dto!.PostingType);
        Assert.True(dto.IsPostingTypeManual);

        var entity = await db.StatementDraftEntries.SingleAsync(e => e.Id == entry.Id, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(BankPostingType.Tax, entity.PostingType);
        Assert.True(entity.IsPostingTypeManual);
    }

    /// <summary>
    /// A manual posting type of <see cref="BankPostingType.Unspecified"/> is not a valid choice and must be
    /// rejected with a domain validation error.
    /// </summary>
    [Fact]
    public async Task SaveEntryAllAsync_ShouldRejectUnspecifiedPostingType()
    {
        var (sut, db, owner) = Create();
        var draft = await sut.CreateEmptyDraftAsync(owner, "file.csv", CancellationToken.None);
        Assert.NotNull(draft);
        var created = await sut.AddEntryAsync(draft!.DraftId, owner, DateTime.Today, 10m, "Initial", CancellationToken.None);
        var entry = created!.Entries.First();

        var ex = await Assert.ThrowsAsync<FinanceManager.Application.Exceptions.DomainValidationException>(() =>
            sut.SaveEntryAllAsync(draft!.DraftId, entry.Id, owner, new StatementDraftSaveEntryAllRequest(null, null, null, null, null, null, null, null, null, PostingType: BankPostingType.Unspecified), CancellationToken.None));
        Assert.Equal("POSTINGTYPE_INVALID", ex.Code);
    }

    /// <summary>
    /// Leaving the posting type parameter <c>null</c> must keep the previously set (manual) posting type.
    /// </summary>
    [Fact]
    public async Task SaveEntryAllAsync_ShouldKeepPostingType_WhenParameterIsNull()
    {
        var (sut, db, owner) = Create();
        var draft = await sut.CreateEmptyDraftAsync(owner, "file.csv", CancellationToken.None);
        Assert.NotNull(draft);
        var created = await sut.AddEntryAsync(draft!.DraftId, owner, DateTime.Today, 10m, "Initial", CancellationToken.None);
        var entry = created!.Entries.First();

        var first = await sut.SaveEntryAllAsync(draft!.DraftId, entry.Id, owner, new StatementDraftSaveEntryAllRequest(null, null, null, null, null, null, null, null, null, PostingType: BankPostingType.Tax), CancellationToken.None);
        var second = await sut.SaveEntryAllAsync(draft!.DraftId, entry.Id, owner, new StatementDraftSaveEntryAllRequest(null, null, null, null, null, null, null, null, null), CancellationToken.None);

        Assert.Equal(BankPostingType.Tax, second!.PostingType);
        Assert.True(second.IsPostingTypeManual);
    }

    /// <summary>
    /// A core-field update on a non-manual entry must re-evaluate the posting type using the account's posting
    /// rules, so changing the subject to a rule-matching text reclassifies the entry.
    /// </summary>
    [Fact]
    public async Task UpdateEntryCoreAsync_ShouldReevaluatePostingType_WhenNotManual()
    {
        var (sut, db, owner) = Create();
        var (_, draft, entry) = await CreateDraftWithAccountRuleAsync(sut, db, owner);

        var dto = await sut.UpdateEntryCoreAsync(draft.DraftId, entry.Id, owner, DateTime.Today, null, 10m, "Zinsgutschrift", null, "EUR", null, CancellationToken.None);

        Assert.NotNull(dto);
        Assert.Equal(BankPostingType.Interest, dto!.PostingType);
        Assert.False(dto.IsPostingTypeManual);
    }

    /// <summary>
    /// A core-field update must not touch the posting type of an entry whose posting type was assigned
    /// manually, even when the new subject would match a rule.
    /// </summary>
    [Fact]
    public async Task UpdateEntryCoreAsync_ShouldPreserveManualPostingType()
    {
        var (sut, db, owner) = Create();
        var (_, draft, entry) = await CreateDraftWithAccountRuleAsync(sut, db, owner);
        var entity = await db.StatementDraftEntries.SingleAsync(e => e.Id == entry.Id, cancellationToken: TestContext.Current.CancellationToken);
        entity.SetPostingType(BankPostingType.Tax, manual: true);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var dto = await sut.UpdateEntryCoreAsync(draft.DraftId, entry.Id, owner, DateTime.Today, null, 10m, "Zinsgutschrift", null, "EUR", null, CancellationToken.None);

        Assert.NotNull(dto);
        Assert.Equal(BankPostingType.Tax, dto!.PostingType);
        Assert.True(dto.IsPostingTypeManual);
    }

    /// <summary>
    /// A batch update changing core fields of a non-manual entry must re-evaluate the posting type the same
    /// way <see cref="StatementDraftService.UpdateEntryCoreAsync"/> does.
    /// </summary>
    [Fact]
    public async Task ApplyBatchEntryUpdatesAsync_ShouldReevaluatePostingType_OnCoreChange()
    {
        var (sut, db, owner) = Create();
        var (_, draft, entry) = await CreateDraftWithAccountRuleAsync(sut, db, owner);

        var req = new FinanceManager.Shared.Dtos.Statements.BatchUpdateRequestDto();
        req.Updates.Add(new FinanceManager.Shared.Dtos.Statements.EntryUpdateDto
        {
            EntryId = entry.Id,
            Fields = new Dictionary<string, object?> { ["Subject"] = "Zinsgutschrift" }
        });
        var result = await sut.ApplyBatchEntryUpdatesAsync(draft.DraftId, owner, req, CancellationToken.None);

        Assert.True(result.Success);
        var entity = await db.StatementDraftEntries.SingleAsync(e => e.Id == entry.Id, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(BankPostingType.Interest, entity.PostingType);
        Assert.False(entity.IsPostingTypeManual);
    }

    /// <summary>
    /// <see cref="StatementDraftService.GetDraftAsync"/> must project the posting type and the manual flag onto
    /// the entry DTOs so the UI can display and preserve them.
    /// </summary>
    [Fact]
    public async Task GetDraftAsync_ShouldProjectPostingType_AndManualFlag()
    {
        var (sut, db, owner) = Create();
        var draft = await sut.CreateEmptyDraftAsync(owner, "file.csv", CancellationToken.None);
        Assert.NotNull(draft);
        var created = await sut.AddEntryAsync(draft!.DraftId, owner, DateTime.Today, 10m, "Initial", CancellationToken.None);
        var entry = created!.Entries.First();
        await sut.SaveEntryAllAsync(draft!.DraftId, entry.Id, owner, new StatementDraftSaveEntryAllRequest(null, null, null, null, null, null, null, null, null, PostingType: BankPostingType.Tax), CancellationToken.None);

        var loaded = await sut.GetDraftAsync(draft!.DraftId, owner, CancellationToken.None);

        var dto = Assert.Single(loaded!.Entries);
        Assert.Equal(BankPostingType.Tax, dto.PostingType);
        Assert.True(dto.IsPostingTypeManual);
    }

    /// <summary>
    /// <see cref="StatementDraftService.GetOpenDraftsAsync"/> must also project posting type and manual flag so
    /// the draft list shows the same values as the detail view.
    /// </summary>
    [Fact]
    public async Task GetOpenDraftsAsync_ShouldProjectPostingType_AndManualFlag()
    {
        var (sut, db, owner) = Create();
        var draft = await sut.CreateEmptyDraftAsync(owner, "file.csv", CancellationToken.None);
        Assert.NotNull(draft);
        var created = await sut.AddEntryAsync(draft!.DraftId, owner, DateTime.Today, 10m, "Initial", CancellationToken.None);
        var entry = created!.Entries.First();
        await sut.SaveEntryAllAsync(draft!.DraftId, entry.Id, owner, new StatementDraftSaveEntryAllRequest(null, null, null, null, null, null, null, null, null, PostingType: BankPostingType.Interest), CancellationToken.None);

        var drafts = await sut.GetOpenDraftsAsync(owner, CancellationToken.None);

        var loaded = Assert.Single(drafts, d => d.DraftId == draft!.DraftId);
        var dto = Assert.Single(loaded.Entries);
        Assert.Equal(BankPostingType.Interest, dto.PostingType);
        Assert.True(dto.IsPostingTypeManual);
    }
}
