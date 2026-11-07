using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using FinanceManager.Application.Reports;
using FinanceManager.Domain.Accounts;
using FinanceManager.Domain.Postings;
using FinanceManager.Domain.Savings;
using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Reports;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text;

namespace FinanceManager.Tests.Reports;

/// <summary>
/// Covers the derived <c>Informal</c> flag and the <c>SavingsPlanSubType</c> marker in the savings
/// plan postings export (<see cref="PostingExportService"/>): the CSV/XLSX schema carries both
/// trailing columns and a plan posting is exported as informal only when its plan has at least one
/// linked account and its posting group holds a bank posting on a non-linked account.
/// </summary>
public sealed class PostingExportServiceInformalTests
{
    private static (PostingExportService service, AppDbContext db, SqliteConnection conn) Create()
    {
        var conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options;
        var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        var service = new PostingExportService(db, NullLogger<PostingExportService>.Instance);
        return (service, db, conn);
    }

    private static async Task<(SavingsPlan plan, Account giro, Account contract)> SeedAsync(AppDbContext db, Guid owner)
    {
        var plan = new SavingsPlan(owner, "Bausparen", SavingsPlanType.Open, null, null, null);
        var giro = new Account(owner, AccountType.Giro, "Giro", null, Guid.NewGuid());
        var contract = new Account(owner, AccountType.Savings, "Vertragskonto", null, Guid.NewGuid());
        db.SavingsPlans.Add(plan);
        db.Accounts.AddRange(giro, contract);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return (plan, giro, contract);
    }

    private static void AddPair(AppDbContext db, Guid planId, Guid bankAccountId, DateTime date, Guid groupId)
    {
        db.Postings.Add(new Posting(Guid.NewGuid(), PostingKind.Bank, bankAccountId, null, null, null, date, -100m, null, null, null, null).SetGroup(groupId));
        db.Postings.Add(new Posting(Guid.NewGuid(), PostingKind.SavingsPlan, null, null, planId, null, date, 100m, null, null, null, null).SetGroup(groupId));
    }

    private static PostingExportQuery Query(Guid owner, Guid planId, PostingExportFormat format)
        => new(owner, PostingKind.SavingsPlan, planId, format, 100);

    private static async Task<string> StreamCsvAsync(PostingExportService service, PostingExportQuery query)
    {
        var ms = new MemoryStream();
        await service.StreamCsvAsync(query, ms, TestContext.Current.CancellationToken);
        ms.Position = 0;
        using var reader = new StreamReader(ms, Encoding.UTF8);
        return await reader.ReadToEndAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Verifies that the CSV export schema ends with the <c>Informal</c> and
    /// <c>SavingsPlanSubType</c> columns and that an informal funding posting is exported with
    /// <c>True</c> while an actual-balance report carries its subtype marker.
    /// </summary>
    [Fact]
    public async Task StreamCsvAsync_SavingsPlanExport_WritesInformalAndSubTypeColumns()
    {
        var (service, db, conn) = Create();
        var owner = Guid.NewGuid();
        var (plan, giro, contract) = await SeedAsync(db, owner);
        db.SavingsPlanAccountLinks.Add(new SavingsPlanAccountLink(plan.Id, contract.Id, owner));
        var g = Guid.NewGuid();
        AddPair(db, plan.Id, giro.Id, DateTime.Today.AddDays(-5), g); // bank counterpart on non-linked account -> informal
        db.Postings.Add(new Posting(Guid.NewGuid(), PostingKind.SavingsPlan, null, null, plan.Id, null, DateTime.Today.AddDays(-3), 75.30m, "Istmeldung", null, null, null)
            .SetSavingsPlanSubType(SavingsPlanPostingSubType.ActualReport));
        db.Postings.Add(new Posting(Guid.NewGuid(), PostingKind.SavingsPlan, null, null, plan.Id, null, DateTime.Today.AddDays(-1), -50m, null, null, null, null));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var csv = await StreamCsvAsync(service, Query(owner, plan.Id, PostingExportFormat.Csv));
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal("BookingDate;ValutaDate;Amount;Kind;Subject;RecipientName;Description;Account;Contact;SavingsPlan;Security;SecuritySubType;Quantity;Informal;SavingsPlanSubType", lines[0].TrimEnd('\r'));
        Assert.Equal(4, lines.Length);

        // Ordered by BookingDate descending: disbursement, actual report, informal funding posting.
        var disbursement = lines[1].TrimEnd('\r').Split(';');
        Assert.Equal("False", disbursement[13]);
        Assert.Equal(string.Empty, disbursement[14]);

        var actualReport = lines[2].TrimEnd('\r').Split(';');
        Assert.Equal("False", actualReport[13]);
        Assert.Equal("ActualReport", actualReport[14]);

        var informal = lines[3].TrimEnd('\r').Split(';');
        Assert.Equal("True", informal[13]);
        Assert.Equal(string.Empty, informal[14]);

        conn.Dispose();
    }

    /// <summary>
    /// Verifies that the XLSX export carries the same trailing <c>Informal</c> /
    /// <c>SavingsPlanSubType</c> header cells and the derived informal value in the data row.
    /// </summary>
    [Fact]
    public async Task GenerateAsync_Xlsx_IncludesInformalAndSubTypeCells()
    {
        var (service, db, conn) = Create();
        var owner = Guid.NewGuid();
        var (plan, giro, contract) = await SeedAsync(db, owner);
        db.SavingsPlanAccountLinks.Add(new SavingsPlanAccountLink(plan.Id, contract.Id, owner));
        var g = Guid.NewGuid();
        AddPair(db, plan.Id, giro.Id, DateTime.Today.AddDays(-5), g);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var (contentType, _, stream) = await service.GenerateAsync(Query(owner, plan.Id, PostingExportFormat.Xlsx), TestContext.Current.CancellationToken);

        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", contentType);
        using var doc = SpreadsheetDocument.Open(stream, false);
        var sheetData = doc.WorkbookPart?.WorksheetParts.FirstOrDefault()?.Worksheet.Elements<SheetData>().Single();
        Assert.NotNull(sheetData);
        var rows = sheetData.Elements<Row>().ToList();
        Assert.Equal(2, rows.Count);

        var headerCells = rows[0].Elements<Cell>().Select(c => c.InnerText).ToList();
        Assert.Equal("Informal", headerCells[13]);
        Assert.Equal("SavingsPlanSubType", headerCells[14]);

        var dataCells = rows[1].Elements<Cell>().Select(c => c.InnerText).ToList();
        Assert.Equal("True", dataCells[13]);
        Assert.Equal(string.Empty, dataCells[14]);

        conn.Dispose();
    }
}
