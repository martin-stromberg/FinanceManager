using FinanceManager.Application.Reports;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text;

namespace FinanceManager.Infrastructure.Reports;

/// <summary>
/// Service responsible for exporting postings for a given context (Account/Contact/SavingsPlan/Security).
/// Supports streaming queries, counting, CSV streaming and generating full CSV/XLSX exports.
/// </summary>
public sealed class PostingExportService : IPostingExportService
{
    private const string CsvHeader = "BookingDate;ValutaDate;Amount;Kind;Subject;RecipientName;Description;Account;Contact;SavingsPlan;Security;SecuritySubType;Quantity";
    private static readonly string[] XlsxHeaders = CsvHeader.Split(';');

    private readonly AppDbContext _db;
    private readonly ILogger<PostingExportService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PostingExportService"/> class.
    /// </summary>
    /// <param name="db">Database context used to read postings and related data.</param>
    /// <param name="logger">Logger for informational and diagnostic messages.</param>
    public PostingExportService(AppDbContext db, ILogger<PostingExportService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Executes the export query and yields matching rows asynchronously. The sequence may contain at most <see cref="PostingExportQuery.MaxRows"/> + 1 rows
    /// because the caller uses an extra row to detect overflow.
    /// </summary>
    /// <param name="query">The export query specifying context, filters and limits.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>An asynchronous sequence of <see cref="PostingExportRow"/> matching the query.</returns>
    public async IAsyncEnumerable<PostingExportRow> QueryAsync(PostingExportQuery query, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        var baseQuery = BuildFilteredJoinedQuery(query);

        var ordered = baseQuery
            .OrderByDescending(x => x.P.BookingDate)
            .ThenByDescending(x => x.P.Id)
            .Take(query.MaxRows + 1); // overtake to detect overflow

        await foreach (var row in ordered
            .Select(x => new PostingExportRow(
                x.P.BookingDate,
                x.P.ValutaDate,
                x.P.Amount,
                x.P.Kind,
                x.Subject,
                x.Recipient,
                x.Description,
                x.P.AccountId,
                x.P.ContactId,
                x.P.SavingsPlanId,
                x.P.SecurityId,
                x.P.SecuritySubType,
                x.P.Quantity))
            .AsAsyncEnumerable().WithCancellation(ct))
        {
            yield return row;
        }
    }

    /// <summary>
    /// Builds the filtered joined query used by listing and counting methods. Applies date and text filters.
    /// </summary>
    /// <param name="query">The export query containing filters.</param>
    /// <returns>An <see cref="IQueryable{T}"/> of internal joined projection representing postings with resolved text fields.</returns>
    private IQueryable<Joined> BuildFilteredJoinedQuery(PostingExportQuery query)
    {
        var baseQuery = BuildBaseQuery(query);

        // Apply filters
        if (query.From.HasValue)
        {
            var from = query.From.Value.Date;
            baseQuery = baseQuery.Where(x => x.P.BookingDate >= from);
        }
        if (query.To.HasValue)
        {
            var to = query.To.Value.Date.AddDays(1);
            baseQuery = baseQuery.Where(x => x.P.BookingDate < to);
        }
        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var term = query.Q.Trim();
            var termLower = term.ToLowerInvariant();
            baseQuery = baseQuery.Where(x =>
                (x.Subject != null && EF.Functions.Like(x.Subject.ToLower(), "%" + termLower + "%")) ||
                (x.Recipient != null && EF.Functions.Like(x.Recipient.ToLower(), "%" + termLower + "%")) ||
                (x.Description != null && EF.Functions.Like(x.Description.ToLower(), "%" + termLower + "%"))
            );
        }
        return baseQuery;
    }

    /// <summary>
    /// Builds the base join and applies ownership validation for the requested context.
    /// </summary>
    /// <param name="query">The export query containing context and owner information.</param>
    /// <returns>An <see cref="IQueryable{Joined}"/> representing postings joined with optional statement entries.</returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when the requested context entity is not owned by the querying user.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the context kind is not supported.</exception>
    private IQueryable<Joined> BuildBaseQuery(PostingExportQuery query)
    {
        // Ownership per context
        switch (query.ContextKind)
        {
            case PostingKind.Bank:
                if (!_db.Accounts.AsNoTracking().Any(a => a.Id == query.ContextId && a.OwnerUserId == query.OwnerUserId))
                {
                    throw new UnauthorizedAccessException("Account not owned by user.");
                }
                break;
            case PostingKind.Contact:
                if (!_db.Contacts.AsNoTracking().Any(c => c.Id == query.ContextId && c.OwnerUserId == query.OwnerUserId))
                {
                    throw new UnauthorizedAccessException("Contact not owned by user.");
                }
                break;
            case PostingKind.SavingsPlan:
                if (!_db.SavingsPlans.AsNoTracking().Any(s => s.Id == query.ContextId && s.OwnerUserId == query.OwnerUserId))
                {
                    throw new UnauthorizedAccessException("Savings plan not owned by user.");
                }
                break;
            case PostingKind.Security:
                if (!_db.Securities.AsNoTracking().Any(s => s.Id == query.ContextId && s.OwnerUserId == query.OwnerUserId))
                {
                    throw new UnauthorizedAccessException("Security not owned by user.");
                }
                break;
            default:
                throw new InvalidOperationException("Unsupported context kind for export.");
        }

        var postings = _db.Postings.AsNoTracking().Where(p => p.Kind == query.ContextKind);
        postings = query.ContextKind switch
        {
            PostingKind.Bank => postings.Where(p => p.AccountId == query.ContextId),
            PostingKind.Contact => postings.Where(p => p.ContactId == query.ContextId),
            PostingKind.SavingsPlan => postings.Where(p => p.SavingsPlanId == query.ContextId),
            PostingKind.Security => postings.Where(p => p.SecurityId == query.ContextId),
            _ => throw new InvalidOperationException()
        };

        var joined = from p in postings
                     join se in _db.StatementEntries.AsNoTracking() on p.SourceId equals se.Id into seJoin
                     from seOpt in seJoin.DefaultIfEmpty()
                     select new Joined
                     {
                         P = p,
                         Subject = p.Subject ?? seOpt.Subject,
                         Recipient = p.RecipientName ?? seOpt.RecipientName,
                         Description = p.Description ?? seOpt.BookingDescription
                     };
        return joined;
    }

    /// <summary>
    /// Holds the resolved display name lookups for the entity types referenced by export rows.
    /// </summary>
    private sealed record ResolvedEntityNames
    {
        /// <summary>Mapping of account ids to display names.</summary>
        public required Dictionary<Guid, string> Accounts { get; init; }
        /// <summary>Mapping of contact ids to display names.</summary>
        public required Dictionary<Guid, string> Contacts { get; init; }
        /// <summary>Mapping of savings plan ids to display names.</summary>
        public required Dictionary<Guid, string> SavingsPlans { get; init; }
        /// <summary>Mapping of security ids to display names.</summary>
        public required Dictionary<Guid, string> Securities { get; init; }
    }

    /// <summary>
    /// Internal projection type used by the export queries to combine postings with optional statement entry fields.
    /// </summary>
    private sealed class Joined
    {
        public Domain.Postings.Posting P { get; set; } = null!;
        public string? Subject { get; set; }
        public string? Recipient { get; set; }
        public string? Description { get; set; }
    }

    /// <summary>
    /// Generates a full export file (CSV or XLSX) for the provided query and returns the content stream and metadata.
    /// </summary>
    /// <param name="query">The posting export query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Tuple containing the content type, suggested file name and a stream containing the file content. The returned stream is positioned at the beginning.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the export exceeds the configured maximum rows or when an unknown format is requested.</exception>
    public async Task<(string ContentType, string FileName, Stream Content)> GenerateAsync(PostingExportQuery query, CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var rows = new List<PostingExportRow>(capacity: Math.Min(query.MaxRows, 10000));
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
                    ExportFileHelper.ResolveName(r.AccountId, names.Accounts),
                    ExportFileHelper.ResolveName(r.ContactId, names.Contacts),
                    ExportFileHelper.ResolveName(r.SavingsPlanId, names.SavingsPlans),
                    ExportFileHelper.ResolveName(r.SecurityId, names.Securities))), ct);
            stream.Position = 0;
            contentType = "text/csv";
            ext = "csv";
        }
        else if (query.Format == PostingExportFormat.Xlsx)
        {
            stream = new MemoryStream();
            ExportFileHelper.WriteXlsx(stream, "Postings", XlsxHeaders,
                rows.Select(r => CreateXlsxValues(r, names.Accounts, names.Contacts, names.SavingsPlans, names.Securities)));
            stream.Position = 0;
            contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
            ext = "xlsx";
        }
        else
        {
            throw new ArgumentOutOfRangeException(nameof(query.Format));
        }

        sw.Stop();
        _logger.LogInformation("Posting export generated: Kind={Kind}, Id={Id}, Rows={Rows}, Format={Format}, DurationMs={Ms}", query.ContextKind, query.ContextId, rows.Count, query.Format, sw.ElapsedMilliseconds);

        // Filename responsibility of caller (needs entity name). Use generic fallback here
        var fileName = $"Postings_{query.ContextKind}_{DateTime.UtcNow:yyyyMMddHHmm}.{ext}";
        return (contentType, fileName, stream);
    }

    /// <summary>
    /// Resolves the display names of the accounts, contacts, savings plans and securities referenced by the
    /// provided rows, restricted to entities owned by the exporting user.
    /// </summary>
    /// <param name="rows">Export rows whose referenced ids are resolved.</param>
    /// <param name="ownerUserId">Owner user id used to scope the lookups.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="ResolvedEntityNames"/> containing dictionaries that map entity ids to display names.</returns>
    private async Task<ResolvedEntityNames> ResolveEntityNamesAsync(
        IReadOnlyList<PostingExportRow> rows, Guid ownerUserId, CancellationToken ct)
    {
        var accountIds = rows.Where(r => r.AccountId.HasValue).Select(r => r.AccountId!.Value).Distinct().ToList();
        var contactIds = rows.Where(r => r.ContactId.HasValue).Select(r => r.ContactId!.Value).Distinct().ToList();
        var savingsIds = rows.Where(r => r.SavingsPlanId.HasValue).Select(r => r.SavingsPlanId!.Value).Distinct().ToList();
        var securityIds = rows.Where(r => r.SecurityId.HasValue).Select(r => r.SecurityId!.Value).Distinct().ToList();

        var accountNames = await ExportFileHelper.ResolveAccountNamesAsync(_db, ownerUserId, accountIds, ct);
        var contactNames = await ExportFileHelper.ResolveContactNamesAsync(_db, ownerUserId, contactIds, ct);
        var savingsNames = await ExportFileHelper.ResolveSavingsPlanNamesAsync(_db, ownerUserId, savingsIds, ct);
        var securityNames = await ExportFileHelper.ResolveSecurityNamesAsync(_db, ownerUserId, securityIds, ct);
        return new ResolvedEntityNames { Accounts = accountNames, Contacts = contactNames, SavingsPlans = savingsNames, Securities = securityNames };
    }

    /// <summary>
    /// Counts the number of rows matching the provided export query.
    /// </summary>
    /// <param name="query">The export query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The number of matching rows.</returns>
    public async Task<int> CountAsync(PostingExportQuery query, CancellationToken ct)
    {
        var filtered = BuildFilteredJoinedQuery(query);
        return await filtered.CountAsync(ct);
    }

    /// <summary>
    /// Streams CSV formatted postings directly to the provided output stream. This method performs a streaming DB query
    /// and writes rows sequentially to the output to avoid loading the entire result set into memory.
    /// </summary>
    /// <param name="query">The export query.</param>
    /// <param name="output">The output stream where CSV data will be written. Stream is left open.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task StreamCsvAsync(PostingExportQuery query, Stream output, CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        // Build a streaming query that also resolves names via left joins filtered by owner
        var postings = _db.Postings.AsNoTracking().Where(p => p.Kind == query.ContextKind);
        postings = query.ContextKind switch
        {
            PostingKind.Bank => postings.Where(p => p.AccountId == query.ContextId),
            PostingKind.Contact => postings.Where(p => p.ContactId == query.ContextId),
            PostingKind.SavingsPlan => postings.Where(p => p.SavingsPlanId == query.ContextId),
            PostingKind.Security => postings.Where(p => p.SecurityId == query.ContextId),
            _ => postings.Where(_ => false)
        };

        // Ownership checks already enforced in BuildBaseQuery in regular paths; do it again for safety
        _ = BuildBaseQuery(query);

        var accounts = _db.Accounts.AsNoTracking().Where(a => a.OwnerUserId == query.OwnerUserId);
        var contacts = _db.Contacts.AsNoTracking().Where(c => c.OwnerUserId == query.OwnerUserId);
        var savings = _db.SavingsPlans.AsNoTracking().Where(s => s.OwnerUserId == query.OwnerUserId);
        var securities = _db.Securities.AsNoTracking().Where(s => s.OwnerUserId == query.OwnerUserId);
        var stmt = _db.StatementEntries.AsNoTracking();

        var q = from p in postings
                join se in stmt on p.SourceId equals se.Id into seJoin
                from seOpt in seJoin.DefaultIfEmpty()
                join ac in accounts on p.AccountId equals ac.Id into acJoin
                from acOpt in acJoin.DefaultIfEmpty()
                join co in contacts on p.ContactId equals co.Id into coJoin
                from coOpt in coJoin.DefaultIfEmpty()
                join sp in savings on p.SavingsPlanId equals sp.Id into spJoin
                from spOpt in spJoin.DefaultIfEmpty()
                join sc in securities on p.SecurityId equals sc.Id into scJoin
                from scOpt in scJoin.DefaultIfEmpty()
                select new
                {
                    p.Id,
                    p.BookingDate,
                    p.ValutaDate,
                    p.Amount,
                    p.Kind,
                    Subject = p.Subject ?? seOpt.Subject,
                    Recipient = p.RecipientName ?? seOpt.RecipientName,
                    Description = p.Description ?? seOpt.BookingDescription,
                    p.AccountId,
                    p.ContactId,
                    p.SavingsPlanId,
                    p.SecurityId,
                    AccountName = acOpt != null ? acOpt.Name : null,
                    ContactName = coOpt != null ? coOpt.Name : null,
                    SavingsName = spOpt != null ? spOpt.Name : null,
                    SecurityName = scOpt != null ? scOpt.Name : null,
                    p.SecuritySubType,
                    p.Quantity
                };

        // Filters
        if (query.From.HasValue)
        {
            var from = query.From.Value.Date;
            q = q.Where(x => x.BookingDate >= from);
        }
        if (query.To.HasValue)
        {
            var to = query.To.Value.Date.AddDays(1);
            q = q.Where(x => x.BookingDate < to);
        }
        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var term = query.Q.Trim();
            var termLower = term.ToLowerInvariant();
            q = q.Where(x =>
                (x.Subject != null && EF.Functions.Like(x.Subject.ToLower(), "%" + termLower + "%")) ||
                (x.Recipient != null && EF.Functions.Like(x.Recipient.ToLower(), "%" + termLower + "%")) ||
                (x.Description != null && EF.Functions.Like(x.Description.ToLower(), "%" + termLower + "%"))
            );
        }

        q = q.OrderByDescending(x => x.BookingDate).ThenByDescending(x => x.Id).Take(query.MaxRows);

        // Write header + rows directly to output (async only)
        var writer = new StreamWriter(output, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), 16 * 1024, leaveOpen: true)
        {
            AutoFlush = false
        };
        await writer.WriteLineAsync(CsvHeader);

        await foreach (var r in q.AsAsyncEnumerable().WithCancellation(ct))
        {
            ct.ThrowIfCancellationRequested();
            var row = new PostingExportRow(
                r.BookingDate, r.ValutaDate, r.Amount, r.Kind, r.Subject, r.Recipient, r.Description,
                r.AccountId, r.ContactId, r.SavingsPlanId, r.SecurityId, r.SecuritySubType, r.Quantity);
            var cols = CreateCsvColumns(row, r.AccountName, r.ContactName, r.SavingsName, r.SecurityName);
            await writer.WriteLineAsync(string.Join(';', cols));
        }
        await writer.FlushAsync();
        await output.FlushAsync(ct);

        sw.Stop();
        _logger.LogInformation("Posting export streamed: Kind={Kind}, Id={Id}, Format=CSV, DurationMs={Ms}", query.ContextKind, query.ContextId, sw.ElapsedMilliseconds);
    }

    /// <summary>
    /// Builds the CSV column values for a single export row using already resolved display names.
    /// </summary>
    /// <param name="r">The export row.</param>
    /// <param name="accountName">Resolved account display name, or null when none.</param>
    /// <param name="contactName">Resolved contact display name, or null when none.</param>
    /// <param name="savingsName">Resolved savings plan display name, or null when none.</param>
    /// <param name="securityName">Resolved security display name, or null when none.</param>
    /// <returns>The escaped CSV column values in header order.</returns>
    private static string[] CreateCsvColumns(PostingExportRow r,
        string? accountName,
        string? contactName,
        string? savingsName,
        string? securityName)
    {
        return new[]
        {
            r.BookingDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            r.ValutaDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            r.Amount.ToString(CultureInfo.InvariantCulture),
            r.Kind.ToString(),
            ExportFileHelper.CsvEscape(r.Subject),
            ExportFileHelper.CsvEscape(r.RecipientName),
            ExportFileHelper.CsvEscape(r.Description),
            ExportFileHelper.CsvEscape(accountName),
            ExportFileHelper.CsvEscape(contactName),
            ExportFileHelper.CsvEscape(savingsName),
            ExportFileHelper.CsvEscape(securityName),
            r.SecuritySubType?.ToString() ?? string.Empty,
            r.Quantity?.ToString(CultureInfo.InvariantCulture) ?? string.Empty
        };
    }

    /// <summary>
    /// Builds the XLSX cell values for a single export row, resolving referenced ids to display names.
    /// </summary>
    /// <param name="r">The export row.</param>
    /// <param name="accountNames">Mapping of account ids to names.</param>
    /// <param name="contactNames">Mapping of contact ids to names.</param>
    /// <param name="savingsNames">Mapping of savings plan ids to names.</param>
    /// <param name="securityNames">Mapping of security ids to names.</param>
    /// <returns>The cell values in header order.</returns>
    private static object[] CreateXlsxValues(PostingExportRow r,
        IReadOnlyDictionary<Guid, string> accountNames,
        IReadOnlyDictionary<Guid, string> contactNames,
        IReadOnlyDictionary<Guid, string> savingsNames,
        IReadOnlyDictionary<Guid, string> securityNames)
    {
        var acc = ExportFileHelper.ResolveName(r.AccountId, accountNames);
        var con = ExportFileHelper.ResolveName(r.ContactId, contactNames);
        var sav = ExportFileHelper.ResolveName(r.SavingsPlanId, savingsNames);
        var sec = ExportFileHelper.ResolveName(r.SecurityId, securityNames);
        return new object[]
        {
            r.BookingDate,
            r.ValutaDate,
            r.Amount,
            r.Kind.ToString(),
            r.Subject ?? string.Empty,
            r.RecipientName ?? string.Empty,
            r.Description ?? string.Empty,
            acc,
            con,
            sav,
            sec,
            r.SecuritySubType?.ToString() ?? string.Empty,
            r.Quantity ?? (object)string.Empty
        };
    }
}
