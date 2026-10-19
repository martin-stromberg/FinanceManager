using FinanceManager.Application.Reports;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text;

namespace FinanceManager.Infrastructure.Reports;

/// <summary>
/// Service responsible for exporting the entries of a statement draft.
/// Supports streaming queries, counting, CSV streaming and generating full CSV/XLSX exports.
/// </summary>
public sealed class StatementDraftExportService : IStatementDraftExportService
{
    private const string CsvHeader = "EntryNumber;BookingDate;ValutaDate;Amount;Currency;Subject;RecipientName;Description;Status;IsAnnounced;IsCostNeutral;Contact;SavingsPlan;Security;SecurityTransactionType;SecurityQuantity;SecurityFeeAmount;SecurityTaxAmount";
    private static readonly string[] XlsxHeaders = CsvHeader.Split(';');

    private readonly AppDbContext _db;
    private readonly ILogger<StatementDraftExportService> _logger;

    /// <summary>
    /// Holds the resolved display name lookups for the entity types referenced by export rows.
    /// </summary>
    private sealed record ResolvedEntityNames
    {
        /// <summary>Mapping of contact ids to display names.</summary>
        public required Dictionary<Guid, string> Contacts { get; init; }
        /// <summary>Mapping of savings plan ids to display names.</summary>
        public required Dictionary<Guid, string> SavingsPlans { get; init; }
        /// <summary>Mapping of security ids to display names.</summary>
        public required Dictionary<Guid, string> Securities { get; init; }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StatementDraftExportService"/> class.
    /// </summary>
    /// <param name="db">Database context used to read statement drafts and related data.</param>
    /// <param name="logger">Logger for informational and diagnostic messages.</param>
    public StatementDraftExportService(AppDbContext db, ILogger<StatementDraftExportService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Executes the export query and yields matching rows asynchronously. The sequence may contain at most
    /// <see cref="StatementDraftExportQuery.MaxRows"/> + 1 rows because the caller uses an extra row to detect overflow.
    /// </summary>
    /// <param name="query">The export query specifying draft, filters and limits.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>An asynchronous sequence of <see cref="StatementDraftEntryExportRow"/> matching the query.</returns>
    public async IAsyncEnumerable<StatementDraftEntryExportRow> QueryAsync(StatementDraftExportQuery query, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        var filtered = await BuildFilteredQueryAsync(query, ct);

        var ordered = filtered
            .OrderBy(e => e.EntryNumber)
            .ThenBy(e => e.Id)
            .Take(query.MaxRows + 1); // overtake to detect overflow

        await foreach (var row in ordered
            .Select(e => new StatementDraftEntryExportRow(
                e.EntryNumber,
                e.BookingDate,
                e.ValutaDate,
                e.Amount,
                e.CurrencyCode,
                e.Subject,
                e.RecipientName,
                e.BookingDescription,
                e.Status,
                e.IsAnnounced,
                e.IsCostNeutral,
                e.ContactId,
                e.SavingsPlanId,
                e.SecurityId,
                e.SecurityTransactionType,
                e.SecurityQuantity,
                e.SecurityFeeAmount,
                e.SecurityTaxAmount))
            .AsAsyncEnumerable().WithCancellation(ct))
        {
            yield return row;
        }
    }

    /// <summary>
    /// Verifies that the requested draft exists and is owned by the querying user.
    /// </summary>
    /// <param name="query">The export query containing draft and owner information.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when ownership has been verified.</returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when the draft is not owned by the querying user.</exception>
    private async Task EnsureOwnershipAsync(StatementDraftExportQuery query, CancellationToken ct)
    {
        var owned = await _db.StatementDrafts.AsNoTracking()
            .AnyAsync(d => d.Id == query.DraftId && d.OwnerUserId == query.OwnerUserId, ct);
        if (!owned)
        {
            throw new UnauthorizedAccessException("Statement draft not owned by user.");
        }
    }

    /// <summary>
    /// Builds the filtered entry query used by listing, streaming and counting methods. Applies ownership check,
    /// date and text filters.
    /// </summary>
    /// <param name="query">The export query containing filters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>An <see cref="IQueryable{T}"/> of draft entries matching the filters.</returns>
    private async Task<IQueryable<Domain.Statements.StatementDraftEntry>> BuildFilteredQueryAsync(StatementDraftExportQuery query, CancellationToken ct)
    {
        await EnsureOwnershipAsync(query, ct);

        var entries = _db.StatementDraftEntries.AsNoTracking().Where(e => e.DraftId == query.DraftId);

        if (query.From.HasValue)
        {
            var from = query.From.Value.Date;
            entries = entries.Where(e => e.BookingDate >= from);
        }
        if (query.To.HasValue)
        {
            var to = query.To.Value.Date.AddDays(1);
            entries = entries.Where(e => e.BookingDate < to);
        }
        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var term = query.Q.Trim();
            var termLower = term.ToLowerInvariant();
            entries = entries.Where(e =>
                EF.Functions.Like(e.Subject.ToLower(), "%" + termLower + "%") ||
                (e.RecipientName != null && EF.Functions.Like(e.RecipientName.ToLower(), "%" + termLower + "%")) ||
                (e.BookingDescription != null && EF.Functions.Like(e.BookingDescription.ToLower(), "%" + termLower + "%"))
            );
        }
        return entries;
    }

    /// <summary>
    /// Generates a full export file (CSV or XLSX) for the provided query and returns the content stream and metadata.
    /// </summary>
    /// <param name="query">The statement draft export query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Tuple containing the content type, suggested file name and a stream containing the file content. The returned stream is positioned at the beginning.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the export exceeds the configured maximum rows or when an unknown format is requested.</exception>
    public async Task<(string ContentType, string FileName, Stream Content)> GenerateAsync(StatementDraftExportQuery query, CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var rows = new List<StatementDraftEntryExportRow>(capacity: Math.Min(query.MaxRows, 10000));
        var count = 0;
        await foreach (var r in QueryAsync(query, ct))
        {
            rows.Add(r);
            count++;
            if (count > query.MaxRows)
            {
                throw new InvalidOperationException("MaxRowsExceeded");
            }
        }

        // Resolve entity names for referenced IDs to make export human-readable
        var names = await ResolveEntityNamesAsync(rows, query.OwnerUserId, ct);

        Stream stream;
        string contentType;
        string ext;
        if (query.Format == PostingExportFormat.Csv)
        {
            stream = new MemoryStream();
            await ExportFileHelper.WriteCsvAsync(stream, CsvHeader,
                rows.Select(r => CreateCsvColumns(r,
                    ResolveName(r.ContactId, names.Contacts),
                    ResolveName(r.SavingsPlanId, names.SavingsPlans),
                    ResolveName(r.SecurityId, names.Securities))), ct);
            stream.Position = 0;
            contentType = "text/csv";
            ext = "csv";
        }
        else if (query.Format == PostingExportFormat.Xlsx)
        {
            stream = new MemoryStream();
            ExportFileHelper.WriteXlsx(stream, "Entries", XlsxHeaders,
                rows.Select(r => CreateXlsxValues(r, names.Contacts, names.SavingsPlans, names.Securities)));
            stream.Position = 0;
            contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
            ext = "xlsx";
        }
        else
        {
            throw new ArgumentOutOfRangeException(nameof(query.Format));
        }

        sw.Stop();
        _logger.LogInformation("Statement draft export generated: DraftId={DraftId}, Rows={Rows}, Format={Format}, DurationMs={Ms}", query.DraftId, rows.Count, query.Format, sw.ElapsedMilliseconds);

        // Filename responsibility of caller (needs draft name). Use generic fallback here
        var fileName = $"StatementDraft_{DateTime.UtcNow:yyyyMMddHHmm}.{ext}";
        return (contentType, fileName, stream);
    }

    /// <summary>
    /// Resolves the display names of the contacts, savings plans and securities referenced by the provided rows,
    /// restricted to entities owned by the exporting user.
    /// </summary>
    /// <param name="rows">Export rows whose referenced ids are resolved.</param>
    /// <param name="ownerUserId">Owner user id used to scope the lookups.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="ResolvedEntityNames"/> containing dictionaries that map entity ids to display names.</returns>
    private async Task<ResolvedEntityNames> ResolveEntityNamesAsync(
        IReadOnlyList<StatementDraftEntryExportRow> rows, Guid ownerUserId, CancellationToken ct)
    {
        var contactIds = rows.Where(r => r.ContactId.HasValue).Select(r => r.ContactId!.Value).Distinct().ToList();
        var savingsIds = rows.Where(r => r.SavingsPlanId.HasValue).Select(r => r.SavingsPlanId!.Value).Distinct().ToList();
        var securityIds = rows.Where(r => r.SecurityId.HasValue).Select(r => r.SecurityId!.Value).Distinct().ToList();

        var contactNames = await ExportFileHelper.ResolveContactNamesAsync(_db, ownerUserId, contactIds, ct);
        var savingsNames = await ExportFileHelper.ResolveSavingsPlanNamesAsync(_db, ownerUserId, savingsIds, ct);
        var securityNames = await ExportFileHelper.ResolveSecurityNamesAsync(_db, ownerUserId, securityIds, ct);
        return new ResolvedEntityNames { Contacts = contactNames, SavingsPlans = savingsNames, Securities = securityNames };
    }

    /// <summary>
    /// Counts the number of rows matching the provided export query.
    /// </summary>
    /// <param name="query">The export query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The number of matching rows.</returns>
    public async Task<int> CountAsync(StatementDraftExportQuery query, CancellationToken ct)
    {
        var filtered = await BuildFilteredQueryAsync(query, ct);
        return await filtered.CountAsync(ct);
    }

    /// <summary>
    /// Streams CSV formatted draft entries directly to the provided output stream. This method performs a streaming
    /// DB query and writes rows sequentially to the output to avoid loading the entire result set into memory.
    /// </summary>
    /// <param name="query">The export query.</param>
    /// <param name="output">The output stream where CSV data will be written. Stream is left open.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task StreamCsvAsync(StatementDraftExportQuery query, Stream output, CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        var filtered = await BuildFilteredQueryAsync(query, ct);

        var contacts = _db.Contacts.AsNoTracking().Where(c => c.OwnerUserId == query.OwnerUserId);
        var savings = _db.SavingsPlans.AsNoTracking().Where(s => s.OwnerUserId == query.OwnerUserId);
        var securities = _db.Securities.AsNoTracking().Where(s => s.OwnerUserId == query.OwnerUserId);

        var q = (from e in filtered
                 join co in contacts on e.ContactId equals co.Id into coJoin
                 from coOpt in coJoin.DefaultIfEmpty()
                 join sp in savings on e.SavingsPlanId equals sp.Id into spJoin
                 from spOpt in spJoin.DefaultIfEmpty()
                 join sc in securities on e.SecurityId equals sc.Id into scJoin
                 from scOpt in scJoin.DefaultIfEmpty()
                 select new
                 {
                     e.Id,
                     e.EntryNumber,
                     e.BookingDate,
                     e.ValutaDate,
                     e.Amount,
                     e.CurrencyCode,
                     e.Subject,
                     e.RecipientName,
                     e.BookingDescription,
                     e.Status,
                     e.IsAnnounced,
                     e.IsCostNeutral,
                     e.ContactId,
                     e.SavingsPlanId,
                     e.SecurityId,
                     ContactName = coOpt != null ? coOpt.Name : null,
                     SavingsName = spOpt != null ? spOpt.Name : null,
                     SecurityName = scOpt != null ? scOpt.Name : null,
                     e.SecurityTransactionType,
                     e.SecurityQuantity,
                     e.SecurityFeeAmount,
                     e.SecurityTaxAmount
                 })
                .OrderBy(x => x.EntryNumber)
                .ThenBy(x => x.Id)
                .Take(query.MaxRows);

        // Write header + rows directly to output (async only)
        var writer = new StreamWriter(output, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), 16 * 1024, leaveOpen: true)
        {
            AutoFlush = false
        };
        await writer.WriteLineAsync(CsvHeader);

        await foreach (var r in q.AsAsyncEnumerable().WithCancellation(ct))
        {
            ct.ThrowIfCancellationRequested();
            var row = new StatementDraftEntryExportRow(
                r.EntryNumber, r.BookingDate, r.ValutaDate, r.Amount, r.CurrencyCode,
                r.Subject, r.RecipientName, r.BookingDescription, r.Status,
                r.IsAnnounced, r.IsCostNeutral, r.ContactId, r.SavingsPlanId, r.SecurityId,
                r.SecurityTransactionType, r.SecurityQuantity, r.SecurityFeeAmount, r.SecurityTaxAmount);
            var cols = CreateCsvColumns(row, r.ContactName, r.SavingsName, r.SecurityName);
            await writer.WriteLineAsync(string.Join(';', cols));
        }
        await writer.FlushAsync();
        await output.FlushAsync(ct);

        sw.Stop();
        _logger.LogInformation("Statement draft export streamed: DraftId={DraftId}, Format=CSV, DurationMs={Ms}", query.DraftId, sw.ElapsedMilliseconds);
    }

    /// <summary>
    /// Resolves the display name for an optional entity id using the provided name mapping.
    /// </summary>
    /// <param name="id">Optional entity id to resolve.</param>
    /// <param name="names">Mapping of entity ids to display names.</param>
    /// <returns>The resolved display name, or an empty string when the id is missing or unknown.</returns>
    private static string ResolveName(Guid? id, IReadOnlyDictionary<Guid, string> names)
        => id.HasValue && names.TryGetValue(id.Value, out var name) ? name : string.Empty;

    /// <summary>
    /// Builds the CSV column values for a single export row using already resolved display names.
    /// </summary>
    /// <param name="r">The export row.</param>
    /// <param name="contactName">Resolved contact display name, or null when none.</param>
    /// <param name="savingsName">Resolved savings plan display name, or null when none.</param>
    /// <param name="securityName">Resolved security display name, or null when none.</param>
    /// <returns>The escaped CSV column values in header order.</returns>
    private static string[] CreateCsvColumns(StatementDraftEntryExportRow r,
        string? contactName,
        string? savingsName,
        string? securityName)
    {
        return new[]
        {
            r.EntryNumber.ToString(CultureInfo.InvariantCulture),
            r.BookingDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            r.ValutaDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty,
            r.Amount.ToString(CultureInfo.InvariantCulture),
            ExportFileHelper.CsvEscape(r.CurrencyCode),
            ExportFileHelper.CsvEscape(r.Subject),
            ExportFileHelper.CsvEscape(r.RecipientName),
            ExportFileHelper.CsvEscape(r.BookingDescription),
            r.Status.ToString(),
            r.IsAnnounced.ToString(),
            r.IsCostNeutral.ToString(),
            ExportFileHelper.CsvEscape(contactName),
            ExportFileHelper.CsvEscape(savingsName),
            ExportFileHelper.CsvEscape(securityName),
            r.SecurityTransactionType?.ToString() ?? string.Empty,
            r.SecurityQuantity?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            r.SecurityFeeAmount?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            r.SecurityTaxAmount?.ToString(CultureInfo.InvariantCulture) ?? string.Empty
        };
    }

    /// <summary>
    /// Builds the XLSX cell values for a single export row, resolving referenced ids to display names.
    /// </summary>
    /// <param name="r">The export row.</param>
    /// <param name="contactNames">Mapping of contact ids to names.</param>
    /// <param name="savingsNames">Mapping of savings plan ids to names.</param>
    /// <param name="securityNames">Mapping of security ids to names.</param>
    /// <returns>The cell values in header order.</returns>
    private static object[] CreateXlsxValues(StatementDraftEntryExportRow r,
        IReadOnlyDictionary<Guid, string> contactNames,
        IReadOnlyDictionary<Guid, string> savingsNames,
        IReadOnlyDictionary<Guid, string> securityNames)
    {
        var con = r.ContactId.HasValue && contactNames.TryGetValue(r.ContactId.Value, out var cn) ? cn : string.Empty;
        var sav = r.SavingsPlanId.HasValue && savingsNames.TryGetValue(r.SavingsPlanId.Value, out var sn) ? sn : string.Empty;
        var sec = r.SecurityId.HasValue && securityNames.TryGetValue(r.SecurityId.Value, out var scn) ? scn : string.Empty;
        return new object[]
        {
            r.EntryNumber,
            r.BookingDate,
            r.ValutaDate ?? (object)string.Empty,
            r.Amount,
            r.CurrencyCode,
            r.Subject ?? string.Empty,
            r.RecipientName ?? string.Empty,
            r.BookingDescription ?? string.Empty,
            r.Status.ToString(),
            r.IsAnnounced.ToString(),
            r.IsCostNeutral.ToString(),
            con,
            sav,
            sec,
            r.SecurityTransactionType?.ToString() ?? string.Empty,
            r.SecurityQuantity ?? (object)string.Empty,
            r.SecurityFeeAmount ?? (object)string.Empty,
            r.SecurityTaxAmount ?? (object)string.Empty
        };
    }
}
