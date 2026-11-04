using FinanceManager.Shared.Dtos.Mcp;
using System.Text;

namespace FinanceManager.Infrastructure.Mcp;

/// <summary>
/// CSV serialization of the MCP audit export (F-0601, audit-konzept.md §6.3: CSV is the
/// compact "Kurzfassung" — one row per audit entry, field deltas stay in the JSON export).
/// Values are invariant-culture; fields carrying free text are quoted.
/// </summary>
public static class McpAuditCsvExport
{
    /// <summary>
    /// Writes the CSV document (UTF-8 BOM for spreadsheet compatibility, semicolon
    /// separator following the UI culture convention of the finance exports).
    /// </summary>
    /// <param name="entries">The export rows (already owner-scoped and filtered).</param>
    /// <returns>The CSV document text.</returns>
    public static string Write(IEnumerable<McpAuditEntryDetailDto> entries)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Utc;SequenceNo;Category;EventType;ResultStatus;AccessId;AccessName;Tool;CorrelationId;Context;DurationMs;AffectedCount;Changes");
        foreach (var e in entries)
        {
            sb.Append(e.Utc.ToString("O", System.Globalization.CultureInfo.InvariantCulture)).Append(';')
                .Append(e.SequenceNo.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(';')
                .Append(e.Category.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(';')
                .Append(Escape(e.EventType)).Append(';')
                .Append(Escape(e.ResultStatus)).Append(';')
                .Append(e.McpAccessId?.ToString() ?? string.Empty).Append(';')
                .Append(Escape(e.McpAccessName)).Append(';')
                .Append(Escape(e.ToolName)).Append(';')
                .Append(e.CorrelationId?.ToString() ?? string.Empty).Append(';')
                .Append(Escape(e.Context)).Append(';')
                .Append(e.DurationMs?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty).Append(';')
                .Append(e.AffectedCount?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty).Append(';')
                .Append(e.Changes.Count.ToString(System.Globalization.CultureInfo.InvariantCulture))
                .AppendLine();
        }
        return sb.ToString();
    }

    /// <summary>Quotes a free-text field when it contains separator/quote/newline characters.</summary>
    private static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }
        return value.IndexOfAny(new[] { ';', '"', '\n', '\r' }) >= 0
            ? "\"" + value.Replace("\"", "\"\"") + "\""
            : value;
    }
}
