using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using FinanceManager.Application.Budget;
using FinanceManager.Domain.Postings;
using FinanceManager.Shared.Dtos.Budget;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Infrastructure.Budget;

/// <summary>
/// Generates an XLSX export containing all postings in a budget report range, grouped into worksheets by posting kind,
/// and enriched with the budget purpose assignment.
/// </summary>
public sealed class BudgetReportExportService : IBudgetReportExportService
{
    private const int MaxRows = 250_000;

    private readonly AppDbContext _db;
    private readonly ILogger<BudgetReportExportService> _logger;

    /// <summary>
    /// Creates a new instance.
    /// </summary>
    public BudgetReportExportService(AppDbContext db, ILogger<BudgetReportExportService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<(string ContentType, string FileName, Stream Content)> GenerateXlsxAsync(Guid ownerUserId, BudgetReportExportRequest request, CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        var months = Math.Clamp(request.Months, 1, 60);
        var asOf = request.AsOfDate;
        var rangeTo = EndOfMonth(asOf);
        var rangeFrom = StartOfMonth(asOf.AddMonths(-(months - 1)));

        var fromDt = rangeFrom.ToDateTime(TimeOnly.MinValue);
        var toDt = rangeTo.ToDateTime(TimeOnly.MaxValue);

        // Ownership scope: now we only export postings that belong to the current user, by joining to owned context entities.
        // This also ensures we do not accidentally export data from other users.
        var ownedAccounts = _db.Accounts.AsNoTracking().Where(a => a.OwnerUserId == ownerUserId).Select(a => a.Id);
        var ownedContacts = _db.Contacts.AsNoTracking().Where(c => c.OwnerUserId == ownerUserId).Select(c => c.Id);
        var ownedPlans = _db.SavingsPlans.AsNoTracking().Where(s => s.OwnerUserId == ownerUserId).Select(s => s.Id);
        var ownedSecurities = _db.Securities.AsNoTracking().Where(s => s.OwnerUserId == ownerUserId).Select(s => s.Id);

        var baseQuery = _db.Postings.AsNoTracking()
            .Where(p =>
                (p.AccountId != null && ownedAccounts.Contains(p.AccountId.Value)) ||
                (p.ContactId != null && ownedContacts.Contains(p.ContactId.Value)) ||
                (p.SavingsPlanId != null && ownedPlans.Contains(p.SavingsPlanId.Value)) ||
                (p.SecurityId != null && ownedSecurities.Contains(p.SecurityId.Value)));

        baseQuery = request.DateBasis == BudgetReportDateBasis.ValutaDate
            ? baseQuery.Where(p => p.ValutaDate >= fromDt && p.ValutaDate <= toDt)
            : baseQuery.Where(p => p.BookingDate >= fromDt && p.BookingDate <= toDt);

        // Resolve statement entry texts when missing.
        var stmt = _db.StatementEntries.AsNoTracking();

        // Load purposes and map coverage (purpose assignment per posting).
        // Budget report purposes are defined by source (Contact/ContactGroup/SavingsPlan) and use date basis.
        var purposes = await _db.BudgetPurposes.AsNoTracking()
            .Where(p => p.OwnerUserId == ownerUserId)
            .Select(p => new PurposeInfo(p.Id, p.Name, p.SourceType, p.SourceId, p.BudgetCategoryId))
            .ToListAsync(ct);

        var categoryNames = await _db.BudgetCategories.AsNoTracking()
            .Where(c => c.OwnerUserId == ownerUserId)
            .ToDictionaryAsync(c => c.Id, c => c.Name, ct);

        var groupPurposeIds = purposes.Where(p => p.SourceType == BudgetSourceType.ContactGroup).Select(p => p.SourceId).Distinct().ToList();
        var groupContactIds = new Dictionary<Guid, HashSet<Guid>>();
        if (groupPurposeIds.Count > 0)
        {
            var pairs = await _db.Contacts.AsNoTracking()
                .Where(c => c.OwnerUserId == ownerUserId && c.CategoryId != null && groupPurposeIds.Contains(c.CategoryId.Value))
                .Select(c => new { GroupId = c.CategoryId!.Value, ContactId = c.Id })
                .ToListAsync(ct);

            foreach (var p in pairs)
            {
                if (!groupContactIds.TryGetValue(p.GroupId, out var set))
                {
                    set = new HashSet<Guid>();
                    groupContactIds[p.GroupId] = set;
                }
                set.Add(p.ContactId);
            }
        }

        var contactPurposeByContactId = purposes
            .Where(p => p.SourceType == BudgetSourceType.Contact)
            .GroupBy(p => p.SourceId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var planPurposeByPlanId = purposes
            .Where(p => p.SourceType == BudgetSourceType.SavingsPlan)
            .GroupBy(p => p.SourceId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Names for referenced entities (for readability)
        var accountNames = await _db.Accounts.AsNoTracking().Where(a => a.OwnerUserId == ownerUserId).ToDictionaryAsync(a => a.Id, a => a.Name, ct);
        var contactNames = await _db.Contacts.AsNoTracking().Where(a => a.OwnerUserId == ownerUserId).ToDictionaryAsync(a => a.Id, a => a.Name, ct);
        var planNames = await _db.SavingsPlans.AsNoTracking().Where(a => a.OwnerUserId == ownerUserId).ToDictionaryAsync(a => a.Id, a => a.Name, ct);
        var securityNames = await _db.Securities.AsNoTracking().Where(a => a.OwnerUserId == ownerUserId).ToDictionaryAsync(a => a.Id, a => a.Name, ct);

        var rows = await (from p in baseQuery
                          join se in stmt on p.SourceId equals se.Id into seJoin
                          from seOpt in seJoin.DefaultIfEmpty()
                          select new
                          {
                              Posting = p,
                              Subject = p.Subject ?? seOpt.Subject,
                              Recipient = p.RecipientName ?? seOpt.RecipientName,
                              Description = p.Description ?? seOpt.BookingDescription
                          })
            .OrderBy(p => p.Posting.Kind)
            .ThenByDescending(p => request.DateBasis == BudgetReportDateBasis.ValutaDate ? p.Posting.ValutaDate : p.Posting.BookingDate)
            .ThenByDescending(p => p.Posting.Id)
            .Take(MaxRows)
            .ToListAsync(ct);

        // Cross-link contact <-> savings plan postings within the same GroupId (mirror)
        var groups = rows
            .Select(x => x.Posting.GroupId)
            .Where(g => g != Guid.Empty)
            .Distinct()
            .ToList();

        var groupLinks = groups.Count == 0
            ? new Dictionary<Guid, (Guid? ContactId, Guid? SavingsPlanId)>()
            : await _db.Postings.AsNoTracking()
                .Where(p => p.GroupId != Guid.Empty && groups.Contains(p.GroupId))
                .GroupBy(p => p.GroupId)
                .Select(g => new
                {
                    GroupId = g.Key,
                    ContactId = g.Where(p => p.Kind == PostingKind.Contact && p.ContactId != null).Select(p => p.ContactId).FirstOrDefault(),
                    SavingsPlanId = g.Where(p => p.Kind == PostingKind.SavingsPlan && p.SavingsPlanId != null).Select(p => p.SavingsPlanId).FirstOrDefault()
                })
                .ToDictionaryAsync(x => x.GroupId, x => (x.ContactId, x.SavingsPlanId), ct);

        var byKind = rows.GroupBy(r => r.Posting.Kind).ToDictionary(g => g.Key, g => g.ToList());

        Stream stream = new MemoryStream();
        using (var doc = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook))
        {
            var wbPart = doc.AddWorkbookPart();
            wbPart.Workbook = new Workbook();

            var sheets = wbPart.Workbook.AppendChild(new Sheets());

            uint sheetId = 1;
            foreach (var kind in new[] { PostingKind.Bank, PostingKind.Contact, PostingKind.SavingsPlan, PostingKind.Security })
            {
                if (!byKind.TryGetValue(kind, out var list) || list.Count == 0)
                {
                    continue;
                }

                var wsPart = wbPart.AddNewPart<WorksheetPart>();
                var sheetData = new SheetData();
                wsPart.Worksheet = new Worksheet(sheetData);

                var sheetName = kind.ToString();
                var sheet = new Sheet { Id = wbPart.GetIdOfPart(wsPart), SheetId = sheetId++, Name = sheetName };
                sheets.Append(sheet);

                // Header
                sheetData.AppendChild(new Row(new[]
                {
                    Str("PostingId"),
                    Str("SourceId"),
                    Str("GroupId"),
                    Str("ParentId"),
                    Str("LinkedPostingId"),
                    Str("BookingDate"),
                    Str("ValutaDate"),
                    Str("Amount"),
                    Str("Kind"),
                    Str("Account"),
                    Str("Contact"),
                    Str("SavingsPlan"),
                    Str("RelatedContact"),
                    Str("RelatedSavingsPlan"),
                    Str("Security"),
                    Str("Subject"),
                    Str("RecipientName"),
                    Str("Description"),
                    Str("SecuritySubType"),
                    Str("Quantity"),
                    Str("BudgetPurposeId"),
                    Str("BudgetPurposeName"),
                    Str("BudgetCategory"),
                    Str("BudgetPurposeSourceType"),
                    Str("BudgetPurposeSourceId")
                }));

                foreach (var r in list)
                {
                    var p = r.Posting;
                    var purpose = ResolvePurpose(p, contactPurposeByContactId, planPurposeByPlanId, groupContactIds, purposes);

                    var categoryName = string.Empty;
                    if (purpose != null && purpose.Value.CategoryId.HasValue && categoryNames.TryGetValue(purpose.Value.CategoryId.Value, out var catName))
                    {
                        categoryName = catName;
                    }

                    var acc = string.Empty;
                    if (p.AccountId.HasValue && accountNames.TryGetValue(p.AccountId.Value, out var an))
                    {
                        acc = an;
                    }

                    var con = string.Empty;
                    if (p.ContactId.HasValue && contactNames.TryGetValue(p.ContactId.Value, out var cn))
                    {
                        con = cn;
                    }

                    var sp = string.Empty;
                    if (p.SavingsPlanId.HasValue && planNames.TryGetValue(p.SavingsPlanId.Value, out var sn))
                    {
                        sp = sn;
                    }

                    var sec = string.Empty;
                    if (p.SecurityId.HasValue && securityNames.TryGetValue(p.SecurityId.Value, out var scn))
                    {
                        sec = scn;
                    }

                    string relatedContact = string.Empty;
                    string relatedSavings = string.Empty;
                    if (p.GroupId != Guid.Empty && groupLinks.TryGetValue(p.GroupId, out var link))
                    {
                        if (p.Kind == PostingKind.Contact && link.SavingsPlanId.HasValue && planNames.TryGetValue(link.SavingsPlanId.Value, out var relatedSavingsName))
                        {
                            relatedSavings = relatedSavingsName;
                        }
                        if (p.Kind == PostingKind.SavingsPlan && link.ContactId.HasValue && contactNames.TryGetValue(link.ContactId.Value, out var relatedContactName))
                        {
                            relatedContact = relatedContactName;
                        }
                    }

                    sheetData.AppendChild(new Row(new[]
                    {
                        Str(p.Id.ToString()),
                        Str(p.SourceId.ToString()),
                        Str(p.GroupId == Guid.Empty ? string.Empty : p.GroupId.ToString()),
                        Str(p.ParentId?.ToString() ?? string.Empty),
                        Str(p.LinkedPostingId?.ToString() ?? string.Empty),
                        Str(p.BookingDate.ToString("O")),
                        Str(p.ValutaDate.ToString("O")),
                        Num(p.Amount),
                        Str(p.Kind.ToString()),
                        Str(acc),
                        Str(con),
                        Str(sp),
                        Str(relatedContact),
                        Str(relatedSavings),
                        Str(sec),
                        Str(r.Subject ?? string.Empty),
                        Str(r.Recipient ?? string.Empty),
                        Str(r.Description ?? string.Empty),
                        Str(p.SecuritySubType?.ToString() ?? string.Empty),
                        Num(p.Quantity),
                        Str(purpose?.Id.ToString() ?? string.Empty),
                        Str(purpose?.Name ?? string.Empty),
                        Str(categoryName),
                        Str(purpose?.SourceType.ToString() ?? string.Empty),
                        Str(purpose?.SourceId.ToString() ?? string.Empty)
                    }));
                }
            }

            wbPart.Workbook.Save();
        }

        stream.Position = 0;
        sw.Stop();

        _logger.LogInformation("Budget report export generated: From={From}, To={To}, DateBasis={Basis}, DurationMs={Ms}", rangeFrom, rangeTo, request.DateBasis, sw.ElapsedMilliseconds);

        var fileName = $"BudgetReport_{rangeFrom:yyyyMMdd}_{rangeTo:yyyyMMdd}.xlsx";
        return ("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName, stream);

        static Cell Str(string value) => new(new CellValue(value ?? string.Empty)) { DataType = CellValues.String };
        static Cell Num(decimal? value) => new(new CellValue(value?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty)) { DataType = CellValues.Number };

        static (Guid Id, string Name, Guid? CategoryId, BudgetSourceType SourceType, Guid SourceId)? ResolvePurpose(
            Posting p,
            IReadOnlyDictionary<Guid, List<PurposeInfo>> contactPurposeByContactId,
            IReadOnlyDictionary<Guid, List<PurposeInfo>> planPurposeByPlanId,
            IReadOnlyDictionary<Guid, HashSet<Guid>> groupContactIds,
            IReadOnlyList<PurposeInfo> allPurposes)
        {
            // Match order: explicit contact purpose -> contact group purpose -> plan purpose
            if (p.ContactId.HasValue && contactPurposeByContactId.TryGetValue(p.ContactId.Value, out var cp) && cp.Count > 0)
            {
                var x = cp[0];
                return (x.Id, x.Name, x.BudgetCategoryId, x.SourceType, x.SourceId);
            }

            if (p.ContactId.HasValue && groupContactIds.Count > 0)
            {
                foreach (var kvp in groupContactIds)
                {
                    if (kvp.Value.Contains(p.ContactId.Value))
                    {
                        var gp = allPurposes.FirstOrDefault(pp => pp.SourceType == BudgetSourceType.ContactGroup && pp.SourceId == kvp.Key);
                        if (gp != null)
                        {
                            return (gp.Id, gp.Name, gp.BudgetCategoryId, gp.SourceType, gp.SourceId);
                        }
                    }
                }
            }

            if (p.SavingsPlanId.HasValue && planPurposeByPlanId.TryGetValue(p.SavingsPlanId.Value, out var sp) && sp.Count > 0)
            {
                var x = sp[0];
                return (x.Id, x.Name, x.BudgetCategoryId, x.SourceType, x.SourceId);
            }

            return null;
        }
    }

    private sealed record PurposeInfo(Guid Id, string Name, BudgetSourceType SourceType, Guid SourceId, Guid? BudgetCategoryId);

    private static DateOnly StartOfMonth(DateOnly d) => new(d.Year, d.Month, 1);

    private static DateOnly EndOfMonth(DateOnly d)
        => new(d.Year, d.Month, DateTime.DaysInMonth(d.Year, d.Month));
}
