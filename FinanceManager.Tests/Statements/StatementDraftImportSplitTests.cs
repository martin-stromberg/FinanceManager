using Bunit; // added for Self contact
using FinanceManager.Application.Accounts;
using FinanceManager.Application.Attachments;
using FinanceManager.Domain.Contacts;
using FinanceManager.Domain.Users;
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
using System.Linq.Expressions;
using System.Text;
using FinanceManager.Tests.TestHelpers;

namespace FinanceManager.Tests.Statements;

/// <summary>
/// Tests the import split logic (CreateDraftAsync) for fixed, monthly and hybrid modes.
/// NOTE: MinEntriesPerDraft (new feature) is NOT yet applied by the implementation � tests document current behaviour (TDD baseline).
/// </summary>
public sealed class StatementDraftImportSplitTests
{
    private sealed record ImportedDraft(Guid Id, int EntryCount, string? Description);

    private static (StatementDraftService sut, AppDbContext db, SqliteConnection conn, Guid userId) Create()
    {
        var conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options;
        var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        var user = new User("importer", "hash", false);
        db.Users.Add(user);
        // ensure required Self contact exists for classification
        db.Contacts.Add(new Contact(user.Id, "Me", ContactType.Self, null));
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

        var accountService = new StubAccountService();
        var fileFactory = new StatementFileFactory(sp);
        var svc = new StatementDraftService(db, new PostingAggregateService(db), accountService, fileFactory, sp.GetServices<IStatementFileParser>(), NullLogger<StatementDraftService>.Instance, null);
        return (svc, db, conn, user.Id);
    }


    private static string BuildBackupPayload(IEnumerable<(DateTime date, decimal amount, string subject)> lines, string? iban = "DE00", string? description = "Test")
    {
        var sb = new StringBuilder();
        sb.AppendLine("{\"Type\":\"Backup\",\"Version\":2}");
        sb.Append("{ \"BankAccounts\": [ { \"IBAN\": \"").Append(iban).Append("\"} ], \"BankAccountLedgerEntries\": [], \"BankAccountJournalLines\": [");
        bool first = true; int id = 1;
        foreach (var l in lines)
        {
            if (!first) sb.Append(',');
            first = false;
            sb.Append("{\"Id\": ").Append(id++).Append(",")
              .Append("\"PostingDate\": \"").Append(l.date.ToString("yyyy-MM-ddTHH:mm:ss")).Append("\",")
              .Append("\"ValutaDate\": \"").Append(l.date.ToString("yyyy-MM-ddTHH:mm:ss")).Append("\",")
              .Append("\"PostingDescription\": \"Lastschrift\",")
              .Append("\"SourceName\": \"SRC\",")
              .Append("\"Description\": \"").Append(l.subject).Append("\",")
              .Append("\"CurrencyCode\": \"EUR\",")
              .Append("\"Amount\": ").Append(l.amount.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(',')
              .Append("\"CreatedAt\": \"").Append(l.date.ToString("yyyy-MM-ddTHH:mm:ss")).Append("\"}");
        }
        sb.Append("], \"Description\": \"").Append(description).Append("\" }");
        return sb.ToString();
    }

    private static async Task<List<ImportedDraft>> ImportAsync(StatementDraftService sut, AppDbContext db, Guid userId, string payload)
    {
        var drafts = new List<ImportedDraft>();
        var bytes = Encoding.UTF8.GetBytes(payload);
        await foreach (var d in sut.CreateDraftAsync(userId, "import.csv", bytes, CancellationToken.None))
        {
            drafts.Add(new ImportedDraft(d.DraftId, d.Entries.Count, d.Description));
        }
        return drafts;
    }

    /// <summary>
    /// FixedSize split mode must chunk the imported entries into successive drafts of at most
    /// <c>maxEntriesPerDraft</c> each (3, 3, 1 for 7 entries with a max of 3), label each chunk's description with
    /// a "(Teil N)" part suffix, and report <c>EffectiveMonthly = false</c> in <c>LastImportSplitInfo</c>.
    /// </summary>
    [Fact]
    public async Task FixedSizeMode_ShouldChunk_ByMaxEntries()
    {
        var (sut, db, conn, user) = Create();
        var lines = Enumerable.Range(0, 7).Select(i => (new DateTime(2024, 3, 10).AddDays(i), 10m + i, $"L{i}"));
        var payload = BuildBackupPayload(lines);
        var u = await db.Users.SingleAsync(cancellationToken: Xunit.TestContext.Current.CancellationToken);
        u.SetImportSplitSettings(ImportSplitMode.FixedSize, 3, null, 1);
        await db.SaveChangesAsync(Xunit.TestContext.Current.CancellationToken);
        var drafts = await ImportAsync(sut, db, user, payload);
        Assert.Equal(3, drafts.Count);
        var counts = drafts.Select(d => d.EntryCount).ToArray();
        Assert.Equal(new[] { 3, 3, 1 }, counts);
        Assert.Contains("(Teil 1)", drafts[0].Description);
        Assert.False(sut.LastImportSplitInfo!.EffectiveMonthly);
        conn.Dispose();
    }

    /// <summary>
    /// Monthly split mode must group imported entries into exactly one draft per calendar month, with each
    /// draft's description ending in the month's "yyyy-MM" identifier, and report <c>EffectiveMonthly = true</c>.
    /// </summary>
    [Fact]
    public async Task MonthlyMode_ShouldProduceOneDraftPerMonth()
    {
        var (sut, db, conn, user) = Create();
        var lines = new List<(DateTime, decimal, string)>
        {
            (new DateTime(2024,1,5), 10m, "A"),
            (new DateTime(2024,1,6), 11m, "B"),
            (new DateTime(2024,2,1), 12m, "C"),
            (new DateTime(2024,2,2), 13m, "D")
        };
        var payload = BuildBackupPayload(lines);
        var u = await db.Users.SingleAsync(cancellationToken: Xunit.TestContext.Current.CancellationToken);
        u.SetImportSplitSettings(ImportSplitMode.Monthly, 100, null, 1);
        await db.SaveChangesAsync(Xunit.TestContext.Current.CancellationToken);
        var drafts = await ImportAsync(sut, db, user, payload);
        Assert.Equal(2, drafts.Count);
        Assert.True(drafts.All(d => d.Description!.EndsWith("2024-01") || d.Description!.EndsWith("2024-02")));
        Assert.True(sut.LastImportSplitInfo!.EffectiveMonthly);
        conn.Dispose();
    }

    /// <summary>
    /// When a single month's entry count exceeds <c>maxEntriesPerDraft</c>, Monthly mode must further chunk that
    /// month into multiple "(Teil N)"-labeled parts (2, 2, 1 for 5 entries with a max of 2), combining the
    /// monthly grouping with the fixed-size safety cap.
    /// </summary>
    [Fact]
    public async Task MonthlyMode_ShouldSplitMonth_WhenExceedsMax()
    {
        var (sut, db, conn, user) = Create();
        var lines = Enumerable.Range(0, 5).Select(i => (new DateTime(2024, 4, 1).AddDays(i), 1m + i, $"X{i}"));
        var payload = BuildBackupPayload(lines);
        var u = await db.Users.SingleAsync(cancellationToken: Xunit.TestContext.Current.CancellationToken);
        u.SetImportSplitSettings(ImportSplitMode.Monthly, 2, null, 1);
        await db.SaveChangesAsync(Xunit.TestContext.Current.CancellationToken);
        var drafts = await ImportAsync(sut, db, user, payload);
        Assert.Equal(3, drafts.Count);
        var counts = drafts.Select(d => d.EntryCount).ToArray();
        Assert.Equal(new[] { 2, 2, 1 }, counts);
        Assert.Contains("(Teil 1)", drafts[0].Description);
        Assert.True(sut.LastImportSplitInfo!.EffectiveMonthly);
        conn.Dispose();
    }

    /// <summary>
    /// In <see cref="ImportSplitMode.MonthlyOrFixed"/> (hybrid) mode, when the total imported entry count exceeds
    /// the configured <c>monthlySplitThreshold</c>, the current implementation switches to monthly grouping (one
    /// draft per month) rather than fixed-size chunking - documents the current threshold-crossing behavior as a
    /// TDD baseline (see the class-level note about <c>MinEntriesPerDraft</c> not yet being applied).
    /// </summary>
    [Fact]
    public async Task HybridMode_UsesMonthly_WhenTotalGreaterThanThreshold_CurrentImplementation()
    {
        var (sut, db, conn, user) = Create();
        var lines = Enumerable.Range(0, 6).Select(i => (new DateTime(2024, 1, 1).AddDays(i), 1m, $"J{i}"))
            .Concat(Enumerable.Range(0, 6).Select(i => (new DateTime(2024, 2, 1).AddDays(i), 1m, $"K{i}")));
        var payload = BuildBackupPayload(lines);
        var u = await db.Users.SingleAsync(cancellationToken: Xunit.TestContext.Current.CancellationToken);
        u.SetImportSplitSettings(ImportSplitMode.MonthlyOrFixed, 8, 8, 1);
        await db.SaveChangesAsync(Xunit.TestContext.Current.CancellationToken);
        var drafts = await ImportAsync(sut, db, user, payload);
        Assert.Equal(2, drafts.Count);
        Assert.True(sut.LastImportSplitInfo!.EffectiveMonthly);
        conn.Dispose();
    }

    /// <summary>
    /// Conversely, when the total entry count does not exceed the configured threshold, hybrid mode stays in
    /// fixed-size mode and produces a single draft containing all entries rather than splitting by month.
    /// </summary>
    [Fact]
    public async Task HybridMode_UsesFixed_WhenTotalNotGreaterThanThreshold_CurrentImplementation()
    {
        var (sut, db, conn, user) = Create();
        var lines = Enumerable.Range(0, 6).Select(i => (new DateTime(2024, 3, 1).AddDays(i), 1m, $"H{i}"));
        var payload = BuildBackupPayload(lines);
        var u = await db.Users.SingleAsync(cancellationToken: Xunit.TestContext.Current.CancellationToken);
        u.SetImportSplitSettings(ImportSplitMode.MonthlyOrFixed, 10, 10, 1);
        await db.SaveChangesAsync(Xunit.TestContext.Current.CancellationToken);
        var drafts = await ImportAsync(sut, db, user, payload);
        Assert.Equal(1, drafts.Count);
        Assert.Equal(6, drafts[0].EntryCount);
        Assert.False(sut.LastImportSplitInfo!.EffectiveMonthly);
        conn.Dispose();
    }

    /// <summary>
    /// Documents the current (pre-<c>minEntriesPerDraft</c>-enforcement) baseline: a month with very few entries
    /// (just one) is not yet merged into a neighboring month and remains its own standalone draft, even though it
    /// would fall below what later becomes the minimum-entries-per-draft threshold.
    /// </summary>
    [Fact]
    public async Task MonthlyMode_SmallMonth_RemainsStandalone_BeforeMinEntriesLogic()
    {
        var (sut, db, conn, user) = Create();
        var lines = new List<(DateTime, decimal, string)>();
        lines.Add((new DateTime(2024, 5, 15), 5m, "Solo"));
        lines.AddRange(Enumerable.Range(0, 9).Select(i => (new DateTime(2024, 6, 1).AddDays(i), 1m + i, $"M{i}")));
        var payload = BuildBackupPayload(lines);
        var u = await db.Users.SingleAsync(cancellationToken: Xunit.TestContext.Current.CancellationToken);
        u.SetImportSplitSettings(ImportSplitMode.Monthly, 50, null, 1);
        await db.SaveChangesAsync(Xunit.TestContext.Current.CancellationToken);
        var drafts = await ImportAsync(sut, db, user, payload);
        Assert.Equal(2, drafts.Count);
        var ordered = drafts.Select(d => d.EntryCount).OrderBy(x => x).ToArray();
        Assert.Equal(new[] { 1, 9 }, ordered);
        conn.Dispose();
    }

    /// <summary>
    /// Test case matrix for <see cref="MonthlyMode_ShouldMergeSmallMonths_WhenBelowMinEntries"/>: describes how
    /// consecutive small months should be merged with their neighbors once a minimum entries-per-draft threshold
    /// is configured - covering merges at the start, middle, end, and across multiple consecutive undersized months.
    /// </summary>
    public static IEnumerable<object[]> MonthlyMinEntriesMergeCases =>
        new[]
        {
            new object[] { new[] { 3, 20 }, 5, new[] { 23 } },          // kleiner Monat zuerst
            new object[] { new[] { 20, 3 }, 5, new[] { 23 } },          // kleiner Monat zuletzt
            new object[] { new[] { 3, 20, 20 }, 5, new[] { 23, 20 } },      // kleiner Monat am Anfang (3 von 3)
            new object[] { new[] { 20, 3, 20 }, 5, new[] { 20, 23 } },      // kleiner Monat in der Mitte
            new object[] { new[] { 20, 20, 3 }, 5, new[] { 20, 23 } },      // kleiner Monat am Ende,
            new object[] { new[] { 1, 1, 1, 1, 1, 1, 20 }, 5, new[] { 5, 21 } },
            new object[] { new[] { 19, 1, 1, 19 }, 5, new[] { 20, 20 } },
        };

    /// <summary>
    /// When <c>minEntriesPerDraft</c> is configured, Monthly mode must merge a month whose entry count falls below
    /// the minimum into an adjacent month's draft - regardless of whether the undersized month occurs first,
    /// last, in the middle, or spans several consecutive small months - producing the draft counts prescribed by
    /// <see cref="MonthlyMinEntriesMergeCases"/>.
    /// </summary>
    /// <param name="monthEntryCounts">Number of entries to generate for each consecutive month, in order.</param>
    /// <param name="minEntriesPerDraft">The minimum-entries-per-draft threshold under which a month gets merged.</param>
    /// <param name="expectedDrafts">Expected entry count of each resulting draft, in order, after merging.</param>
    [Theory]
    [MemberData(nameof(MonthlyMinEntriesMergeCases))]
    public async Task MonthlyMode_ShouldMergeSmallMonths_WhenBelowMinEntries(int[] monthEntryCounts, int minEntriesPerDraft, int[] expectedDrafts)
    {
        var (sut, db, conn, user) = Create();

        // Arrange: Monate ab 2024-01
        var all = new List<(DateTime date, decimal amount, string subject)>();
        var start = new DateTime(2024, 1, 1);
        for (int m = 0; m < monthEntryCounts.Length; m++)
        {
            var monthStart = start.AddMonths(m);
            for (int i = 0; i < monthEntryCounts[m]; i++)
            {
                all.Add((monthStart.AddDays(i), 1m, $"M{m:D2}_{i:D2}"));
            }
        }

        var payload = BuildBackupPayload(all);
        var u = await db.Users.SingleAsync(cancellationToken: Xunit.TestContext.Current.CancellationToken);

        // Max gro� genug w�hlen, damit kein Split wegen Max greift
        u.SetImportSplitSettings(ImportSplitMode.Monthly, maxEntriesPerDraft: 500, monthlySplitThreshold: null, minEntriesPerDraft: minEntriesPerDraft);
        await db.SaveChangesAsync(Xunit.TestContext.Current.CancellationToken);

        // Act
        var drafts = await ImportAsync(sut, db, user, payload);

        // Assert
        Assert.Equal(expectedDrafts.Length, drafts.Count);

        var actualCounts = drafts.Select(d => d.EntryCount).ToArray();
        Assert.Equal(expectedDrafts, actualCounts);

        Assert.True(sut.LastImportSplitInfo!.EffectiveMonthly);
        conn.Dispose();
    }
}
