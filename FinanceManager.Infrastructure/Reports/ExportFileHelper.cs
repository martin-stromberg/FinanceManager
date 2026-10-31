using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;

namespace FinanceManager.Infrastructure.Reports;

/// <summary>
/// Shared helpers for CSV/XLSX export file generation used by the export services in this namespace.
/// </summary>
internal static class ExportFileHelper
{
    /// <summary>
    /// Escapes a string value for inclusion in a CSV field by removing newlines and quoting double-quotes.
    /// Values that would be interpreted as a spreadsheet formula or command by common CSV consumers are
    /// additionally neutralized with a leading apostrophe.
    /// </summary>
    /// <param name="value">Input string value to escape.</param>
    /// <returns>CSV-safe quoted string, or empty string when input is null or empty.</returns>
    public static string CsvEscape(string? value)
    {
        if (string.IsNullOrEmpty(value)) { return string.Empty; }
        var v = value.Replace("\r", string.Empty).Replace("\n", " ").Replace("\"", "\"\"");
        if (v.Length > 0 && v[0] is '=' or '+' or '-' or '@' or '\t')
        {
            v = "'" + v;
        }
        return $"\"{v}\"";
    }

    /// <summary>
    /// Writes CSV content (a header line followed by one line per pre-built column array) to the provided
    /// stream using UTF-8 with BOM. The stream is left open.
    /// </summary>
    /// <param name="output">Output stream to write to.</param>
    /// <param name="header">Header line (columns separated by semicolons).</param>
    /// <param name="rows">Sequence of column arrays to write; each entry is joined with semicolons.</param>
    /// <param name="ct">Cancellation token.</param>
    public static async Task WriteCsvAsync(Stream output, string header, IEnumerable<string[]> rows, CancellationToken ct)
    {
        using var writer = new StreamWriter(output, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), 1024, leaveOpen: true);
        await writer.WriteLineAsync(header);
        foreach (var cols in rows)
        {
            ct.ThrowIfCancellationRequested();
            await writer.WriteLineAsync(string.Join(';', cols));
        }
        await writer.FlushAsync();
    }

    /// <summary>
    /// Writes an XLSX workbook containing a single worksheet with a header row followed by the provided rows.
    /// The stream is left open.
    /// </summary>
    /// <param name="output">Output stream where the XLSX file will be written.</param>
    /// <param name="sheetName">Name of the single worksheet inside the workbook.</param>
    /// <param name="headers">Header cell texts.</param>
    /// <param name="rows">Sequence of cell value arrays; values are converted by <see cref="CreateRow"/>.</param>
    public static void WriteXlsx(Stream output, string sheetName, IReadOnlyList<string> headers, IEnumerable<object[]> rows)
    {
        using var doc = SpreadsheetDocument.Create(output, SpreadsheetDocumentType.Workbook, autoSave: true);
        var wbPart = doc.AddWorkbookPart();
        wbPart.Workbook = new Workbook();
        var wsPart = wbPart.AddNewPart<WorksheetPart>();
        var sheetData = new SheetData();
        wsPart.Worksheet = new Worksheet(sheetData);

        var sheets = wbPart.Workbook.AppendChild(new Sheets());
        var sheet = new Sheet() { Id = wbPart.GetIdOfPart(wsPart), SheetId = 1, Name = sheetName };
        sheets.Append(sheet);

        // Header
        sheetData.AppendChild(CreateRow(headers.Select(h => (object)h).ToArray()));

        foreach (var rowValues in rows)
        {
            sheetData.AppendChild(CreateRow(rowValues));
        }

        wbPart.Workbook.Save();
    }

    /// <summary>
    /// Creates an OpenXML row for the supplied values. Dates are formatted as ISO strings and decimals as invariant numbers.
    /// </summary>
    /// <param name="values">Array of cell values. Supported types: DateTime, decimal, int, string, Enum; other types are written via ToString().</param>
    /// <returns>An OpenXML <see cref="Row"/> representing the provided values.</returns>
    public static Row CreateRow(object[] values)
    {
        var row = new Row();
        foreach (var v in values)
        {
            if (v is DateTime dt)
            {
                // Excel stores dates as serial numbers from 1899-12-30 by default
                // For simplicity write as string ISO-8601 to avoid styles
                row.Append(new Cell { DataType = CellValues.String, CellValue = new CellValue(dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)) });
            }
            else if (v is decimal dec)
            {
                row.Append(new Cell { DataType = CellValues.Number, CellValue = new CellValue(dec.ToString(CultureInfo.InvariantCulture)) });
            }
            else if (v is int i)
            {
                row.Append(new Cell { DataType = CellValues.Number, CellValue = new CellValue(i.ToString(CultureInfo.InvariantCulture)) });
            }
            else if (v is string s)
            {
                row.Append(new Cell { DataType = CellValues.String, CellValue = new CellValue(s) });
            }
            else if (v is Enum e)
            {
                row.Append(new Cell { DataType = CellValues.String, CellValue = new CellValue(e.ToString()) });
            }
            else if (v == null)
            {
                row.Append(new Cell { DataType = CellValues.String, CellValue = new CellValue(string.Empty) });
            }
            else
            {
                row.Append(new Cell { DataType = CellValues.String, CellValue = new CellValue(v.ToString() ?? string.Empty) });
            }
        }
        return row;
    }

    /// <summary>
    /// Resolves the display name for an optional entity id using the provided name mapping.
    /// </summary>
    /// <param name="id">Optional entity id to resolve.</param>
    /// <param name="names">Mapping of entity ids to display names.</param>
    /// <returns>The resolved display name, or an empty string when the id is missing or unknown.</returns>
    public static string ResolveName(Guid? id, IReadOnlyDictionary<Guid, string> names)
        => id.HasValue && names.TryGetValue(id.Value, out var name) ? name : string.Empty;

    /// <summary>
    /// Resolves the display names of the given account ids, restricted to entities owned by the user.
    /// </summary>
    /// <param name="db">Database context.</param>
    /// <param name="ownerUserId">Owner user id used to scope the lookup.</param>
    /// <param name="ids">Account ids to resolve.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Dictionary mapping account id to account name.</returns>
    public static async Task<Dictionary<Guid, string>> ResolveAccountNamesAsync(AppDbContext db, Guid ownerUserId, IReadOnlyCollection<Guid> ids, CancellationToken ct)
        => ids.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.Accounts.AsNoTracking()
                .Where(a => ids.Contains(a.Id) && a.OwnerUserId == ownerUserId)
                .ToDictionaryAsync(a => a.Id, a => a.Name, ct);

    /// <summary>
    /// Resolves the display names of the given contact ids, restricted to entities owned by the user.
    /// </summary>
    /// <param name="db">Database context.</param>
    /// <param name="ownerUserId">Owner user id used to scope the lookup.</param>
    /// <param name="ids">Contact ids to resolve.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Dictionary mapping contact id to contact name.</returns>
    public static async Task<Dictionary<Guid, string>> ResolveContactNamesAsync(AppDbContext db, Guid ownerUserId, IReadOnlyCollection<Guid> ids, CancellationToken ct)
        => ids.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.Contacts.AsNoTracking()
                .Where(c => ids.Contains(c.Id) && c.OwnerUserId == ownerUserId)
                .ToDictionaryAsync(c => c.Id, c => c.Name, ct);

    /// <summary>
    /// Resolves the display names of the given savings plan ids, restricted to entities owned by the user.
    /// </summary>
    /// <param name="db">Database context.</param>
    /// <param name="ownerUserId">Owner user id used to scope the lookup.</param>
    /// <param name="ids">Savings plan ids to resolve.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Dictionary mapping savings plan id to savings plan name.</returns>
    public static async Task<Dictionary<Guid, string>> ResolveSavingsPlanNamesAsync(AppDbContext db, Guid ownerUserId, IReadOnlyCollection<Guid> ids, CancellationToken ct)
        => ids.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.SavingsPlans.AsNoTracking()
                .Where(s => ids.Contains(s.Id) && s.OwnerUserId == ownerUserId)
                .ToDictionaryAsync(s => s.Id, s => s.Name, ct);

    /// <summary>
    /// Resolves the display names of the given security ids, restricted to entities owned by the user.
    /// </summary>
    /// <param name="db">Database context.</param>
    /// <param name="ownerUserId">Owner user id used to scope the lookup.</param>
    /// <param name="ids">Security ids to resolve.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Dictionary mapping security id to security name.</returns>
    public static async Task<Dictionary<Guid, string>> ResolveSecurityNamesAsync(AppDbContext db, Guid ownerUserId, IReadOnlyCollection<Guid> ids, CancellationToken ct)
        => ids.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.Securities.AsNoTracking()
                .Where(s => ids.Contains(s.Id) && s.OwnerUserId == ownerUserId)
                .ToDictionaryAsync(s => s.Id, s => s.Name, ct);
}
