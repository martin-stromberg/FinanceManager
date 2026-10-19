using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using FinanceManager.Application.Reports;
using FinanceManager.Domain.Contacts;
using FinanceManager.Domain.Savings;
using FinanceManager.Domain.Securities;
using FinanceManager.Domain.Statements;
using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Reports;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using System.Globalization;
using System.Text;

namespace FinanceManager.Tests.Reports;

/// <summary>
/// Tests for <see cref="StatementDraftExportService"/> against a real SQLite-backed <see cref="AppDbContext"/>:
/// CSV streaming in entry-number order, name resolution for assigned contacts/savings plans/securities,
/// date and search filters, ownership enforcement, XLSX generation and row counting.
/// </summary>
public sealed class StatementDraftExportServiceTests
{
    private static (StatementDraftExportService service, AppDbContext db, Guid userId) Create()
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
        var service = new StatementDraftExportService(db, NullLogger<StatementDraftExportService>.Instance);
        return (service, db, owner.Id);
    }

    private static StatementDraftExportQuery Query(Guid userId, Guid draftId, PostingExportFormat format = PostingExportFormat.Csv, int maxRows = 1000, DateTime? from = null, DateTime? to = null, string? q = null)
        => new(userId, draftId, format, maxRows, from, to, q);

    private static async Task<string> StreamCsvAsync(StatementDraftExportService service, StatementDraftExportQuery query)
    {
        var ms = new MemoryStream();
        await service.StreamCsvAsync(query, ms, TestContext.Current.CancellationToken);
        ms.Position = 0;
        using var reader = new StreamReader(ms, Encoding.UTF8);
        return await reader.ReadToEndAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Verifies that a CSV export contains the header line followed by all draft entries ordered by
    /// <c>EntryNumber</c> (the original statement order), so external analysis reproduces the statement layout.
    /// </summary>
    [Fact]
    public async Task StreamCsvAsync_ShouldWriteAllEntriesInEntryNumberOrder()
    {
        var (service, db, user) = Create();
        var draft = new StatementDraft(user, "export.csv", null, "My Draft");
        draft.AddEntry(new DateTime(2026, 1, 5), -10m, "Alpha", "RecA", new DateTime(2026, 1, 6), "EUR", "Desc A", false);
        draft.AddEntry(new DateTime(2026, 1, 3), 20m, "Beta", null, null, null, null, false);
        draft.AddEntry(new DateTime(2026, 1, 8), -30m, "Gamma", null, null, null, null, false);
        db.StatementDrafts.Add(draft);
        db.StatementDraftEntries.AddRange(draft.Entries);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var csv = await StreamCsvAsync(service, Query(user, draft.Id));
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal("EntryNumber;BookingDate;ValutaDate;Amount;Currency;Subject;RecipientName;Description;Status;IsAnnounced;IsCostNeutral;Contact;SavingsPlan;Security;SecurityTransactionType;SecurityQuantity;SecurityFeeAmount;SecurityTaxAmount", lines[0].TrimEnd('\r'));
        Assert.Equal(4, lines.Length);
        Assert.StartsWith("1;", lines[1]);
        Assert.StartsWith("2;", lines[2]);
        Assert.StartsWith("3;", lines[3]);
        Assert.Contains("\"Alpha\"", lines[1]);
        Assert.Contains("\"Beta\"", lines[2]);
        Assert.Contains("\"Gamma\"", lines[3]);
    }

    /// <summary>
    /// Verifies that assigned contact, savings plan and security ids are exported as human-readable names
    /// (resolved via owner-scoped joins), not as raw GUIDs.
    /// </summary>
    [Fact]
    public async Task StreamCsvAsync_ShouldResolveAssignmentNames()
    {
        var (service, db, user) = Create();
        var contact = new Contact(user, "Alice", ContactType.Person, null, null);
        var plan = new SavingsPlan(user, "MyPlan", SavingsPlanType.Recurring, null, null, SavingsPlanInterval.Monthly);
        var security = new Security(user, "MySec", "DE000TEST", null, null, "EUR", null);
        db.Contacts.Add(contact);
        db.SavingsPlans.Add(plan);
        db.Securities.Add(security);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var draft = new StatementDraft(user, "export.csv", null, null);
        var entry = draft.AddEntry(new DateTime(2026, 1, 5), -10m, "Alpha", null, null, null, null, false);
        entry.MarkAccounted(contact.Id);
        entry.AssignSavingsPlan(plan.Id);
        entry.SetSecurity(security.Id, SecurityTransactionType.Buy, 1.5m, 2m, 0.5m);
        db.StatementDrafts.Add(draft);
        db.StatementDraftEntries.Add(entry);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var csv = await StreamCsvAsync(service, Query(user, draft.Id));
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var cols = lines[1].TrimEnd('\r').Split(';');

        Assert.Equal("\"Alice\"", cols[11]);
        Assert.Equal("\"MyPlan\"", cols[12]);
        Assert.Equal("\"MySec\"", cols[13]);
        Assert.Equal("Buy", cols[14]);
        Assert.Equal(1.5m, decimal.Parse(cols[15], CultureInfo.InvariantCulture));
        Assert.Equal(2m, decimal.Parse(cols[16], CultureInfo.InvariantCulture));
        Assert.Equal(0.5m, decimal.Parse(cols[17], CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Verifies that the optional <c>From</c>/<c>To</c> date filters restrict the exported rows to entries
    /// whose booking date lies inside the requested range.
    /// </summary>
    [Fact]
    public async Task StreamCsvAsync_ShouldRespectFromToFilter()
    {
        var (service, db, user) = Create();
        var draft = new StatementDraft(user, "export.csv", null, null);
        draft.AddEntry(new DateTime(2026, 1, 5), -10m, "Alpha", null, null, null, null, false);
        draft.AddEntry(new DateTime(2026, 1, 10), 20m, "Beta", null, null, null, null, false);
        draft.AddEntry(new DateTime(2026, 1, 20), -30m, "Gamma", null, null, null, null, false);
        db.StatementDrafts.Add(draft);
        db.StatementDraftEntries.AddRange(draft.Entries);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var byDate = await StreamCsvAsync(service, Query(user, draft.Id, from: new DateTime(2026, 1, 6), to: new DateTime(2026, 1, 15)));
        var dateLines = byDate.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, dateLines.Length);
        Assert.Contains("\"Beta\"", dateLines[1]);
    }

    /// <summary>
    /// Verifies that the <c>Q</c> search term restricts the exported rows to entries whose subject, recipient
    /// or description contains the term (case-insensitive).
    /// </summary>
    [Fact]
    public async Task StreamCsvAsync_ShouldRespectSearchQuery()
    {
        var (service, db, user) = Create();
        var draft = new StatementDraft(user, "export.csv", null, null);
        draft.AddEntry(new DateTime(2026, 1, 5), -10m, "Alpha", null, null, null, null, false);
        draft.AddEntry(new DateTime(2026, 1, 10), 20m, "Beta", null, null, null, null, false);
        draft.AddEntry(new DateTime(2026, 1, 20), -30m, "Gamma", null, null, null, null, false);
        db.StatementDrafts.Add(draft);
        db.StatementDraftEntries.AddRange(draft.Entries);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var bySearch = await StreamCsvAsync(service, Query(user, draft.Id, q: "gamma"));
        var searchLines = bySearch.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, searchLines.Length);
        Assert.Contains("\"Gamma\"", searchLines[1]);
    }

    /// <summary>
    /// Verifies that streaming a draft that belongs to a different user throws <see cref="UnauthorizedAccessException"/>
    /// so ownership cannot be bypassed via the export service.
    /// </summary>
    [Fact]
    public async Task QueryAsync_ShouldThrowForForeignDraft()
    {
        var (service, db, user) = Create();
        var other = new FinanceManager.Domain.Users.User("other", "hash", true);
        db.Users.Add(other);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var draft = new StatementDraft(other.Id, "foreign.csv", null, null);
        draft.AddEntry(new DateTime(2026, 1, 5), -10m, "Alpha", null, null, null, null, false);
        db.StatementDrafts.Add(draft);
        db.StatementDraftEntries.AddRange(draft.Entries);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
        {
            await foreach (var _ in service.QueryAsync(Query(user, draft.Id), TestContext.Current.CancellationToken))
            {
            }
        });
    }

    /// <summary>
    /// Verifies that generating an XLSX export returns the spreadsheet content type and a workbook containing
    /// the header row plus one row per exported entry on the "Entries" sheet.
    /// </summary>
    [Fact]
    public async Task GenerateAsync_Xlsx_ShouldReturnSpreadsheetWithRows()
    {
        var (service, db, user) = Create();
        var draft = new StatementDraft(user, "export.csv", null, null);
        draft.AddEntry(new DateTime(2026, 1, 5), -10m, "Alpha", null, null, null, null, false);
        draft.AddEntry(new DateTime(2026, 1, 10), 20m, "Beta", null, null, null, null, false);
        db.StatementDrafts.Add(draft);
        db.StatementDraftEntries.AddRange(draft.Entries);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var (contentType, _, stream) = await service.GenerateAsync(Query(user, draft.Id, PostingExportFormat.Xlsx), TestContext.Current.CancellationToken);

        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", contentType);
        using var doc = SpreadsheetDocument.Open(stream, false);
        var worksheet = doc.WorkbookPart?.WorksheetParts.FirstOrDefault()?.Worksheet;
        Assert.NotNull(worksheet);
        var sheetData = worksheet.Elements<SheetData>().Single();
        Assert.Equal(3, sheetData.Elements<Row>().Count());
    }

    /// <summary>
    /// Verifies that <see cref="StatementDraftExportService.CountAsync"/> returns the number of matching entries,
    /// honoring the same filters as the export itself.
    /// </summary>
    [Fact]
    public async Task CountAsync_ShouldReturnEntryCount()
    {
        var (service, db, user) = Create();
        var draft = new StatementDraft(user, "export.csv", null, null);
        draft.AddEntry(new DateTime(2026, 1, 5), -10m, "Alpha", null, null, null, null, false);
        draft.AddEntry(new DateTime(2026, 1, 10), 20m, "Beta", null, null, null, null, false);
        draft.AddEntry(new DateTime(2026, 1, 20), -30m, "Gamma", null, null, null, null, false);
        db.StatementDrafts.Add(draft);
        db.StatementDraftEntries.AddRange(draft.Entries);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(3, await service.CountAsync(Query(user, draft.Id), TestContext.Current.CancellationToken));
        Assert.Equal(1, await service.CountAsync(Query(user, draft.Id, q: "beta"), TestContext.Current.CancellationToken));
    }
}
